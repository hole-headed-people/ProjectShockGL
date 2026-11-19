using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGL;

public static class LevelManager
{
    public static event Action<Level>? Loading;
    public static event Action<Level>? Loaded;
    public static event Action<Level>? Unloading;
    public static event Action<Level>? Unloaded;

    public static Level? ActiveLevel { get; private set; }
    public static string? ActiveLevelName { get; private set; }
    public static int EntryIndex { get; private set; } = -1;
    public static bool IsLoading { get; private set; }
    
    private static bool _isChangingLevel;
    
    public static void Load(Level level)
    {
        Load(level, -1);
    }

    public static void Load(Level level, int entryIndex)
    {
        Load(level, null, null, null, entryIndex);
    }

    private static void Load(Level level, string? name, IReadOnlySet<Guid>? removedObjects, Action<World>? restore, int entryIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (entryIndex < -1) throw new ArgumentOutOfRangeException(nameof(entryIndex));
        
        EnsureIdle();
        
        _isChangingLevel = true;
        
        IsLoading = true;
        
        var staged = new World();
        
        try
        {
            Loading?.Invoke(level);

            if (level.Placeables == null)
            {
                throw new InvalidDataException("Map has no Placeables array.");
            }
            
            var environment = level.Environment ?? new EnvironmentData();
            level.Validate();

            foreach (var placeable in level.Placeables)
            {
                if (placeable?.Guid is { } guid && removedObjects?.Contains(guid) == true)
                {
                    continue;
                }

                CreatePlacement(staged, placeable, placeable?.Guid);
            }

            foreach (var detail in level.Details)
            {
                _ = Content.LoadDetail(detail.AssetPath)
                    ?? throw new FileNotFoundException($"Detail asset '{detail.AssetPath}' was not found.");
                staged.Details.Add(detail);
            }
            staged.Details.Seal();

            if (removedObjects != null)
            {
                staged.RemovedSaveableObjects.UnionWith(removedObjects);
            }

            restore?.Invoke(staged);
            
            var previous = ActiveLevel;

            if (previous != null)
            {
                Unloading?.Invoke(previous);
            }
            
            Game.Instance.World.ReplaceWith(staged);
            Game.Instance.World.KillY = level.KillY;
            Game.Instance.World.SaveLevelState = level.SaveLevelState;
            Environment.Apply(environment);
            
            ActiveLevel = level;
            ActiveLevelName = name;
            EntryIndex = entryIndex;
            RefreshLights();

            if (previous != null)
            {
                Unloaded?.Invoke(previous);
            }
            
            IsLoading = false;
            Loaded?.Invoke(level);
        }
        catch (Exception error)
        {
            Log.PrintError($"Cannot load level '{name ?? "(in memory)"}'.", error);
            throw;
        }
        finally
        {
            try
            {
                try { staged.Clear(); }
                finally { RefreshLights(); }
            }
            finally
            {
                IsLoading = false;
                _isChangingLevel = false;
            }
        }
    }

    private static void RefreshLights()
    {
        foreach (var light in Game.Instance.World.GetAllObjects(true).OfType<Light>())
        {
            light.OnTypeChanged();
        }
    }

    private static void EnsureIdle()
    {
        if (_isChangingLevel)
        {
            throw new InvalidOperationException("Cannot change level from within another level transition.");
        }
    }

    public static void Unload()
    {
        Unload(Game.Instance.World);
    }

    internal static void Unload(World world)
    {
        EnsureIdle();
        
        _isChangingLevel = true;
        
        try
        {
            var previous = ActiveLevel;
            
            if (previous != null)
            {
                Unloading?.Invoke(previous);
            }
            
            try { world.Clear(); }
            finally
            {
                ActiveLevel = null;
                ActiveLevelName = null;
                EntryIndex = -1;
                world.KillY = new Level().KillY;
                world.SaveLevelState = true;
                Environment.Reset();
            }
            
            if (previous != null)
            {
                Unloaded?.Invoke(previous);
            }
        }
        finally
        {
            _isChangingLevel = false;
        }
    }

    public static Object CreateObject(World world, Placeable placeable)
    {
        return CreatePlacement(world, placeable, null);
    }

    public static Object RestoreObject(World world, Placeable placeable)
    {
        if (placeable?.Guid is { } guid && guid != Guid.Empty &&
            world.GetAllObjects(true).Any(obj => obj.Guid == guid))
        {
            throw new InvalidDataException($"An object with GUID '{guid}' already exists in this world.");
        }

        return CreatePlacement(world, placeable, placeable?.Guid);
    }

    private static Object CreatePlacement(World world, Placeable? placeable, Guid? guid)
    {
        if (placeable == null || (placeable.Type != PlaceableType.BrushBox && string.IsNullOrWhiteSpace(placeable.Path)))
        {
            throw new InvalidDataException("A map object has no asset path.");
        }

        if (!Finite(placeable.Position) || !Finite(placeable.Scale) ||
            !float.IsFinite(placeable.Rotation.LengthSquared()) || placeable.Rotation.LengthSquared() < 1e-8f)
        {
            throw new InvalidDataException($"Object '{placeable.Path}' has an invalid transform.");
        }

        if (placeable.Outputs == null || placeable.Properties == null)
        {
            throw new InvalidDataException($"Object '{placeable.Path}' has invalid properties or outputs.");
        }

        Object? obj = placeable.Type switch
        {
            PlaceableType.Entity => Entity.Create(placeable.Path, world, guid),
            PlaceableType.Geometry => Geometry.Create(placeable.Path, world, guid),
            PlaceableType.BrushBox => CreateBrush(world, placeable, guid),
            _ => throw new InvalidDataException($"Unknown object type: {placeable.Type}")
        };

        if (obj == null)
        {
            throw new InvalidDataException($"Cannot create asset '{placeable.Path}' ({placeable.Type}).");
        }

        obj.Name = placeable.Name;
        obj.EditorGroups.AddRange(placeable.EditorGroups ?? []);
        
        if (obj is Entity entity)
        {
            foreach (var output in placeable.Outputs)
            {
                entity.Outputs.Add(new OutputData
                {
                    Event = output.Event, TargetName = output.TargetName,
                    Input = output.Input, Parameter = output.Parameter
                });
            }
            
            foreach (var property in EditableProperties.Get(obj.GetType()))
            {
                if (!placeable.Properties.TryGetValue(property.Name, out var value))
                {
                    continue;
                }
                
                property.SetValue(obj, ConvertProperty(value, property.PropertyType));
            }

            if (placeable.SimulatePhysics is { } enabled)
            {
                entity.SetSimulatePhysics(enabled);
            }
        }

        if (placeable.RigidbodyMass is { } mass && obj.GetComponent<Rigidbody>() is { IsDestroyed: false } body)
        {
            body.Mass = mass;
        }

        if (placeable.BackfaceCulling is { } backfaceCulling && obj.GetComponent<MeshComponent>() is { IsDestroyed: false } mesh)
        {
            mesh.BackfaceCulling = backfaceCulling;
        }

        RenderSurfaceData.Apply(obj, placeable.RenderSurfaces);
        obj.Transform.Position = placeable.Position;
        obj.Transform.Rotation = Quaternion.Normalize(placeable.Rotation);
        obj.Transform.Scale = placeable.Scale;
        obj.Layer = placeable.Layer ?? obj.Layer;
        obj.CollidesWith = placeable.CollidesWith ?? obj.CollidesWith;
        obj.SetActive(placeable.IsActive);
        obj.CompletePlacement();
        
        return obj;
    }

    private static BrushBox CreateBrush(World world, Placeable placement, Guid? guid)
    {
        var data = placement.Brush ?? new BrushBoxData();
        
        if (!Enum.IsDefined(data.SurfaceMode) || !float.IsFinite(data.AlphaCutoff) || data.AlphaCutoff < 0 || data.AlphaCutoff > 1)
            throw new InvalidDataException("Invalid BrushBox surface settings.");
        if (data.Faces == null) throw new InvalidDataException("BrushBox Faces cannot be null.");
        BrushBoxMesh.Build(data.Size, placement.Scale, data.TextureScale, data.Faces);

        if (!Enum.IsDefined(data.TextureFormat) || !float.IsFinite(data.Color.R) || !float.IsFinite(data.Color.G) ||
            !float.IsFinite(data.Color.B) || !float.IsFinite(data.Color.A))
        {
            throw new InvalidDataException("Invalid BrushBox material.");
        }
        
        var textures = Enumerable.Range(0, 6).Select(index =>
        {
            data.Faces.TryGetValue((BrushFace)index, out var face);
            return BrushFaceData.GetTexture(face, data.Texture, data.TextureFormat);
        }).Distinct();
        foreach (var texture in textures)
        {
            if (texture.Name == "WhitePixel") continue;
            var extension = texture.Format == TextureFormat.Png ? ".png" : ".jpg";
            var path = Path.Combine(Content.Path, "Textures", texture.Name + extension);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("BrushBox face texture was not found.", path);
            }
            using var stream = File.OpenRead(path);
            StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        }
        var brush = world.Spawn<BrushBox>(guid);
        
        brush.Size = data.Size;
        brush.Movable = data.Movable;
        brush.SimulatePhysics = data.SimulatePhysics;
        brush.TextureName = data.Texture;
        brush.TextureFormat = data.TextureFormat;
        brush.TextureScale = data.TextureScale;
        brush.SetFaces(data.Faces);
        brush.SurfaceMode = data.SurfaceMode;
        brush.AlphaCutoff = data.AlphaCutoff;
        brush.Color = data.Color;
        brush.Lit = data.Lit;
        
        return brush;
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static object? ConvertProperty(object? value, Type type)
    {
        if (value is JToken token)
        {
            return token.ToObject(type, JsonSerializer.Create(JsonHelper.GetSettings()));
        }

        if (value == null || type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type.IsEnum)
        {
            return value is string text ? Enum.Parse(type, text) : Enum.ToObject(type, Convert.ToInt64(value));
        }
        
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    [UI.UIAction("Level/Load")]
    public static void Load(string name)
    {
        Load(name, null, null);
    }

    public static void Load(string name, int entryIndex)
    {
        Load(name, null, null, entryIndex);
    }

    internal static void Load(string name, IReadOnlySet<Guid>? removedObjects, Action<World>? restore, int entryIndex = -1)
    {
        EnsureIdle();
        if (entryIndex < -1) throw new ArgumentOutOfRangeException(nameof(entryIndex));

        var level = Content.LoadLevel(name);
        if (level == null)
        {
            var error = new FileNotFoundException($"Cannot load level '{name}'.");
            Log.PrintError(error.Message);
            throw error;
        }
        Load(level, name, removedObjects, restore, entryIndex);
    }
}

