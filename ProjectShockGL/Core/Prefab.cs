using System.Numerics;
using Newtonsoft.Json;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public static class Prefab
{
    internal static Placeable WithTransform(Placeable source, Vector3 position, Quaternion rotation)
    {
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        
        var json = Newtonsoft.Json.Linq.JObject.FromObject(source, serializer);
        
        json["Position"] = Newtonsoft.Json.Linq.JToken.FromObject(position, serializer);
        json["Rotation"] = Newtonsoft.Json.Linq.JToken.FromObject(rotation, serializer);
        
        return json.ToObject<Placeable>(serializer)!;
    }

    public static Object[] InstantiateAll(string name, World? world = null, Vector3? position = null, Quaternion? rotation = null)
    {
        return InstantiateAll(
            Content.LoadPrefabData(name) ?? throw new FileNotFoundException($"Prefab '{name}' not found."),
            world ?? Game.Instance.World, position, rotation);
    }

    public static Object[] InstantiateAll(PrefabData template, World world, Vector3? position = null, Quaternion? rotation = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(world);

        if (template.Placeables == null || template.Placeables.Length == 0 || template.Placeables.Any(p => p == null))
        {
            throw new InvalidDataException("Prefab needs at least one placement.");
        }
        
        var origin = position ?? template.Pivot;
        var orientation = rotation ?? Quaternion.Identity;

        if (!float.IsFinite(orientation.LengthSquared()) || orientation.LengthSquared() < 1e-8f)
        {
            throw new ArgumentOutOfRangeException(nameof(rotation));
        }
        
        orientation = Quaternion.Normalize(orientation);
        
        var existing = world.GetAllObjects(true).ToHashSet();
        
        try
        {
            return template.Placeables.Select(source => Instantiate(source, world,
                origin + Vector3.Transform(source.Position, orientation),
                Quaternion.Normalize(orientation * source.Rotation))).ToArray();
        }
        catch (Exception error)
        {
            var errors = new List<Exception> { error };

            foreach (var created in world.GetAllObjects(true).Where(o => !existing.Contains(o)))
            {
                try
                {
                    created.Release();
                }
                catch (Exception cleanup)
                {
                    errors.Add(cleanup);
                }
            }

            if (errors.Count > 1)
            {
                throw new AggregateException("Prefab set creation failed.", errors);
            }
            
            throw;
        }
    }
    public static Object Instantiate(string name, World? world = null, Vector3? position = null, Quaternion? rotation = null)
    {
        world ??= Game.Instance.World;
        
        var source = Content.LoadPrefab(name) ?? throw new FileNotFoundException($"Prefab '{name}' not found.");
        
        return Instantiate(source, world, position, rotation);
    }

    public static Object Instantiate(Placeable template, World world, Vector3? position = null, Quaternion? rotation = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(world);
        
        var settings = JsonHelper.GetSettings();
        var copy = JsonConvert.DeserializeObject<Placeable>(JsonConvert.SerializeObject(template, settings), settings)!;
        var stem = Path.GetFileName(copy.Name.Replace('\\', '/'));

        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = copy.Type.ToString();
        }
        
        var names = world.GetAllObjects(true).Where(o => !o.IsDestroyed).Select(o => o.Name).ToHashSet();
        var suffix = 1;

        while (names.Contains($"{stem}_{suffix}"))
        {
            suffix++;
        }
        
        var placement = new Placeable
        {
            Name = $"{stem}_{suffix}", Type = copy.Type, Path = copy.Path,
            Position = position ?? copy.Position, Rotation = rotation ?? copy.Rotation, Scale = copy.Scale,
            IsActive = copy.IsActive, Brush = copy.Brush, Properties = copy.Properties, Outputs = copy.Outputs
        };
        
        var existing = world.GetAllObjects(true).ToHashSet();
        
        try
        {
            return LevelManager.CreateObject(world, placement);
        }
        catch (Exception creationError)
        {
            var errors = new List<Exception> { creationError };
            
            foreach (var created in world.GetAllObjects(true).Where(o => !existing.Contains(o)))
            {
                try
                {
                    created.Release();
                }
                catch (Exception cleanupError)
                {
                    errors.Add(cleanupError);
                }
                
            }

            if (errors.Count > 1)
            {
                throw new AggregateException("Prefab creation and cleanup failed.", errors);
            }
            
            throw;
        }
    }
}
