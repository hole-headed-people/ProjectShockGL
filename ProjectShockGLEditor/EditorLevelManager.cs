using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class EditorLevelManager
{
    private static string _savedState = "";
    public static string? CurrentPath { get; private set; }
    public static bool IsDirty { get; private set; }
    public static string DisplayName => CurrentPath == null ? "Untitled.map" : Path.GetFileName(CurrentPath);

    public static void Initialize()
    {
        _savedState = Serialize(CaptureCurrentLevel());
        
        IsDirty = false;
    }

    public static void RefreshDirty()
    {
        IsDirty = Serialize(CaptureCurrentLevel()) != _savedState;
    }

    public static Level CaptureCurrentLevel()
    {
        return CaptureLevel(
            ProjectShockGL.Core.Game.Instance.World.GetAllObjects(includePending: true),
            ProjectShockGL.Core.Game.Instance.World.Details);
    }

    public static Level CaptureLevel(IEnumerable<Object> objects, IEnumerable<DetailPlacement>? details = null)
    {
        var result = new List<Placeable>();
        
        foreach (var obj in objects)
        {
            if (obj.IsDestroyed)
            {
                continue;
            }
            
            var path = obj switch
            {
                Geometry geometry => geometry.Path, 
                Entity entity => entity.Path, 
                BrushBox => "BrushBox",
                _ => null
            };
            
            if (path == null) 
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException($"Object '{obj.Name}' has no asset path and cannot be saved.");
            }

            var placeable = new Placeable
            {
                Guid = obj.Guid,
                AssetId = obj is Entity placedEntity
                    ? placedEntity.AssetId ?? Content.LoadEntity(path)?.AssetId : null,
                Name = obj.Name, Path = path,
                
                IsActive = obj.IsActive,
                Layer = obj.Layer,
                CollidesWith = obj.CollidesWith,
                SimulatePhysics = (obj as Entity)?.Rigidbody?.IsEnabled,
                RigidbodyMass = obj.GetComponent<Rigidbody>() is { IsDestroyed: false } body ? body.Mass : null,
                RenderSurfaces = RenderSurfaceData.Capture(obj),
                BackfaceCulling = obj.GetComponent<MeshComponent>() is { IsDestroyed: false } mesh ? mesh.BackfaceCulling : null,
                EditorGroups = obj.EditorGroups.ToArray(),
                
                Type = obj switch
                {
                    BrushBox => PlaceableType.BrushBox,
                    Geometry => PlaceableType.Geometry,
                    _ => PlaceableType.Entity
                },
                
                Brush = obj is BrushBox brush ? new BrushBoxData
                {
                    Movable = brush.Movable,
                    SimulatePhysics = brush.SimulatePhysics,
                    Size = brush.Size, Texture = brush.TextureName, TextureFormat = brush.TextureFormat,
                    TextureScale = brush.TextureScale, Color = brush.Color, Lit = brush.Lit, Faces = brush.CaptureFaces(),
                    SurfaceMode = brush.SurfaceMode, AlphaCutoff = brush.AlphaCutoff
                } : null,
                
                Position = obj.Transform.Position, Rotation = obj.Transform.Rotation, Scale = obj.Transform.Scale,
                
                Outputs = obj is Entity source ? source.Outputs.Select(output => new OutputData
                {
                    Event = output.Event, 
                    TargetName = output.TargetName,
                    Input = output.Input, 
                    Parameter = output.Parameter
                }).ToList() : []
            };
            
            if (obj is Entity)
            {
                foreach (var property in EditableProperties.Get(obj.GetType()))
                {
                    placeable.Properties[property.Name] = property.GetValue(obj)!;
                }
            }
            
            result.Add(placeable);
        }
        
        return new Level
        {
            Environment = ProjectShockGL.Environment.Capture(),
            KillY = ProjectShockGL.Core.Game.Instance.World.KillY,
            SaveLevelState = ProjectShockGL.Core.Game.Instance.World.SaveLevelState,
            Lightmap = details == null ? null : ProjectShockGL.Core.Game.Instance.World.LightmapAsset,
            LightmapSettings = details == null ? new() : ProjectShockGL.Core.Game.Instance.World.LightmapSettings,
            Groups = SceneGroups.Capture(),
            Folders = details == null ? [] : SceneFolders.Capture(),
            Details = details?.ToArray() ?? [],
            Placeables = result.ToArray()
        };
    }

    private static string Serialize(Level level)
    {
        return JsonConvert.SerializeObject(level, JsonHelper.GetSettings());
    }

    public static Object DuplicateObject(Object source) => DuplicateObjects([source]).Single();

    public static Object[] DuplicateObjects(IEnumerable<Object> sources)
    {
        var selected = sources.Distinct().ToArray();
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();

        if (selected.Any(obj => obj.IsDestroyed || !existing.Contains(obj)))
        {
            throw new InvalidOperationException("The source object is no longer in the scene.");
        }
        
        var placements = CaptureLevel(selected).Placeables;

        if (placements.Length != selected.Length)
        {
            throw new InvalidOperationException("Only placed objects can be duplicated.");
        }
        
        var prepared = ObjectLinks.Prepare(placements, world, duplicate: true);
        
        try
        {
            var copies = prepared.Select(CreatePlacement).ToArray();

            for (var index = 0; index < selected.Length; index++)
            {
                SceneFolders.Copy(selected[index].Guid, copies[index].Guid, detail: false);
            }
            
            return copies;
        }
        catch (Exception error)
        {
            RollbackCreated(existing, error);
            
            throw;
        }
    }
    public static Object CreatePlacement(Placeable placement)
    {
        return CreatePlacement(placement, restoreGuid: false);
    }

    internal static Object CreatePlacement(Placeable placement, bool restoreGuid)
    {
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();

        try
        {
            return restoreGuid ? LevelManager.RestoreObject(world, placement) : LevelManager.CreateObject(world, placement);
        }
        catch (Exception error)
        {
            RollbackCreated(existing, error);
            throw;
        }
    }

    internal static void RollbackCreated(HashSet<Object> existing, Exception creationError)
    {
        List<Exception> errors = [creationError];
        
        foreach (var created in ProjectShockGL.Core.Game.Instance.World.GetAllObjects(true).Where(obj => !existing.Contains(obj)))
        {
            try
            {
                ProjectShockGL.Core.Game.Instance.World.Destroy(created);
                
                if (!created.IsDestroyed)
                {
                    created.Destroy();
                }
            }
            catch (Exception cleanupError)
            {
                errors.Add(cleanupError);
            }
        }

        if (errors.Count > 1)
        {
            throw new AggregateException("Object creation and cleanup failed.", errors);
        }
    }

    public static void SaveLevel(Level level, string path)
    {
        var fullPath = Path.GetFullPath(path);
        
        level.Validate();
        
        var directory = Path.GetDirectoryName(fullPath)!;
        
        EntityAssetReferences.Refresh();
        
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        var document = Newtonsoft.Json.Linq.JObject.FromObject(level, serializer);
        document[nameof(Level.Placeables)] = Newtonsoft.Json.Linq.JArray.FromObject(
            level.Placeables.Select(EntityAssetReferences.Upgrade).ToArray(), serializer);
        
        var json = document.ToString(Formatting.Indented);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
       
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static void SaveCurrentLevel(string path)
    {
        ObjectManipulator.Commit();
        
        var level = CaptureCurrentLevel();
        
        SaveLevel(level, path);
        CurrentPath = Path.GetFullPath(path);
        
        _savedState = Serialize(CaptureCurrentLevel());
        
        IsDirty = false;
    }

    public static Level ReadLevel(string path)
    {
        return
            JsonConvert.DeserializeObject<Level>(File.ReadAllText(path), JsonHelper.GetSettings())
            ?? throw new InvalidDataException("Map file is empty or contains null.");
    }

    public static void OpenLevel(string path)
    {
        var level = ReadLevel(path);
        
        ObjectManipulator.Cancel();
        LevelManager.Load(level);
        SceneGroups.Load(level.Groups);
        SceneFolders.Load(level.Folders);
        Selection.Object = null;
        UndoRedo.Clear();
        
        CurrentPath = Path.GetFullPath(path);
        
        _savedState = Serialize(CaptureCurrentLevel());
        
        IsDirty = false;
    }

    public static void NewLevel()
    {
        ObjectManipulator.Cancel();
        LevelManager.Load(new Level());
        SceneGroups.Load([]);
        SceneFolders.Load([]);
        
        Selection.Object = null;
        UndoRedo.Clear();
        
        CurrentPath = null;
        Initialize();
    }
}

