using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public static class ObjectLinks
{
    public static void ValidateNames(IEnumerable<Placeable> placements)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        
        foreach (var placement in placements)
        {
            if (placement == null)
            {
                throw new InvalidDataException("A placement is missing.");
            }

            if (placement.Name == null)
            {
                throw new InvalidDataException("An object name cannot be null.");
            }

            if (!names.Add(placement.Name))
            {
                throw new InvalidDataException($"Duplicate object name '{placement.Name}' inside the set.");
            }
        }
    }

    public static Placeable[] Prepare(IReadOnlyList<Placeable> placements, World world, bool duplicate = false)
    {
        ValidateNames(placements);
        
        var used = world.GetAllObjects(true).Select(obj => obj.Name).ToHashSet(StringComparer.Ordinal);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var assigned = new string[placements.Count];
        
        for (var index = 0; index < placements.Count; index++)
        {
            var source = placements[index];
            var stem = duplicate ? source.Name : Path.GetFileName(source.Name.Replace('\\', '/'));

            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = duplicate ? Path.GetFileNameWithoutExtension(source.Path) : source.Type.ToString();
            }
            
            if (duplicate)
            {
                var separator = stem.LastIndexOf('_');
                
                if (separator > 0 && int.TryParse(stem.AsSpan(separator + 1), out _))
                {
                    stem = stem[..separator];
                }
            }
            
            var suffix = 1;

            while (!used.Add($"{stem}_{suffix}"))
            {
                suffix++;
            }
            
            assigned[index] = $"{stem}_{suffix}";

            if (!string.IsNullOrWhiteSpace(source.Name))
            {
                names.Add(source.Name, assigned[index]);
            }
        }
        
        return placements.Select((source, index) =>
        {
            var json = JObject.FromObject(Remap(source, names), JsonSerializer.Create(JsonHelper.GetSettings()));
            
            json.Remove(nameof(Placeable.Guid));
            json[nameof(Placeable.Name)] = assigned[index];
            
            return json.ToObject<Placeable>(JsonSerializer.Create(JsonHelper.GetSettings()))!;
            
        }).ToArray();
    }

    public static Placeable Remap(Placeable source, IReadOnlyDictionary<string, string> names)
    {
        var serializer = JsonSerializer.Create(JsonHelper.GetSettings());
        var copy = JObject.FromObject(source, serializer).ToObject<Placeable>(serializer)!;
        
        foreach (var output in copy.Outputs)
        {
            if (names.TryGetValue(output.TargetName, out var target))
            {
                output.TargetName = target;
            }
        }
        
        if (copy.Type == PlaceableType.Entity)
        {
            var asset = Content.LoadEntity(copy.Path, copy.AssetId) ?? throw new FileNotFoundException($"Entity asset '{copy.Path}' was not found.");
            var type = Type.GetType(asset.Class) ?? throw new InvalidDataException($"Entity class '{asset.Class}' was not found.");
            
            foreach (var property in EditableProperties.Get(type).Where(property => property.GetCustomAttribute<ObjectNameAttribute>() != null))
            {
                if (property.PropertyType != typeof(string))
                {
                    throw new InvalidDataException($"ObjectName requires a string property: {type.Name}.{property.Name}.");
                }

                if (!copy.Properties.TryGetValue(property.Name, out var value))
                {
                    continue;
                }
                
                var text = value is JToken token && token.Type == JTokenType.String ? token.Value<string>() : value as string;

                if (text != null && names.TryGetValue(text, out var target))
                {
                    copy.Properties[property.Name] = target;
                }
            }
        }
        
        var json = JObject.FromObject(copy, serializer);

        if (names.TryGetValue(copy.Name, out var name))
        {
            json[nameof(Placeable.Name)] = name;
        }
        
        return json.ToObject<Placeable>(serializer)!;
    }
}
