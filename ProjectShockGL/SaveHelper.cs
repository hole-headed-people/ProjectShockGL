using ProjectShockGL.Core;

namespace ProjectShockGL;

public static class SaveHelper
{
    public static void CaptureLevel()
    {
        var world = Game.Instance.World;

        if (!world.SaveLevelState)
        {
            return;
        }

        var name = GetActiveLevelName();
        var level = LevelManager.ActiveLevel!;

        if (level.Placeables.Any(placement => placement.Guid.GetValueOrDefault() == Guid.Empty))
        {
            throw new InvalidOperationException("Resave this map in the editor to assign persistent object GUIDs.");
        }

        var states = new Dictionary<Guid, SavedObjectState>();

        foreach (var obj in world.GetAllObjects(includePending: true))
        {
            if (!obj.Saveable || obj.IsDestroyed)
            {
                continue;
            }

            var state = new SavedObjectState
            {
                Position = obj.Transform.Position,
                Rotation = obj.Transform.Rotation,
                Scale = obj.Transform.Scale,
                IsActive = obj.IsActive,
                SpawnSource = obj.SpawnSource,
                Name = obj.SpawnSource == null ? null : obj.Name
            };

            obj.OnCaptureState(state);

            if (!state.IsValid())
            {
                throw new InvalidDataException($"Cannot capture an invalid state for object '{obj.Guid}'.");
            }

            states.Add(obj.Guid, state);
        }

        foreach (var guid in world.RemovedSaveableObjects)
        {
            states[guid] = new SavedObjectState { IsDestroyed = true };
        }

        SaveManager.GetData().Levels[name] = states;
    }

    public static void RestoreLevel()
    {
        var world = Game.Instance.World;

        if (!world.SaveLevelState)
        {
            return;
        }

        var name = GetActiveLevelName();

        if (!SaveManager.GetData().Levels.TryGetValue(name, out var states))
        {
            return;
        }

        var existingIds = world.GetAllObjects(includePending: true).Select(obj => obj.Guid).ToHashSet();

        foreach (var (guid, state) in states)
        {
            if (!state.IsDestroyed && state.SpawnSource is { } source && !existingIds.Contains(guid))
            {
                var instance = source switch
                {
                    { ClassName: { } className } => RestoreClass(className, world, guid, source),
                    { Path: { } path, PrefabMemberIndex: { } index } => Prefab.RestoreMember(path, index, world, guid, state),
                    { Path: { } path } => RestoreEntity(path, world, guid, state),
                    _ => throw new InvalidDataException($"Object '{guid}' has no valid spawn source.")
                };

                instance.SpawnSource = source;
                instance.Saveable = true;
                existingIds.Add(guid);
            }
        }

        world.RemovedSaveableObjects.UnionWith(states.Where(pair => pair.Value.IsDestroyed).Select(pair => pair.Key));

        foreach (var obj in world.GetAllObjects(includePending: true))
        {
            if (obj.IsDestroyed || !states.TryGetValue(obj.Guid, out var state))
            {
                continue;
            }

            if (!obj.Saveable)
            {
                world.RemovedSaveableObjects.Remove(obj.Guid);
                continue;
            }

            if (state.IsDestroyed)
            {
                obj.Release();
                continue;
            }

            obj.Transform.Position = state.Position;
            obj.Transform.Rotation = state.Rotation;
            obj.Transform.Scale = state.Scale;

            if (state.SpawnSource != null && state.Name != null)
            {
                obj.Name = state.Name;
            }
            
            obj.SetActive(state.IsActive);
            obj.OnRestoreState(state);
        }
    }

    private static Core.Object RestoreClass(string className, World world, Guid guid, ObjectSpawnSource source)
    {
        var type = Type.GetType(className);

        if (type == null || !typeof(Core.Object).IsAssignableFrom(type) ||
            type.IsAbstract || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidDataException($"Cannot restore '{guid}': '{className}' is not a spawnable object class.");
        }

        return world.Spawn<Core.Object>(type, guid, source)!;
    }

    private static Entity RestoreEntity(string path, World world, Guid guid, SavedObjectState state)
    {
        var root = Path.GetFullPath(Path.Combine(Content.Path, "Entities")) + Path.DirectorySeparatorChar;
        var assetPath = Path.GetFullPath(path + ".json", root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (Path.IsPathRooted(path) || !assetPath.StartsWith(root, comparison))
        {
            throw new InvalidDataException($"Entity asset '{path}' must remain inside Content/Entities.");
        }

        return (Entity)LevelManager.RestoreObject(world, new Placeable
        {
            Guid = guid, Type = PlaceableType.Entity, Path = path, Name = state.Name ?? "",
            Position = state.Position, Rotation = state.Rotation, Scale = state.Scale, IsActive = state.IsActive
        });
    }

    private static string GetActiveLevelName()
    {
        return LevelManager.ActiveLevelName
            ?? throw new InvalidOperationException("A named game level must be active to capture or restore object states.");
    }
}
