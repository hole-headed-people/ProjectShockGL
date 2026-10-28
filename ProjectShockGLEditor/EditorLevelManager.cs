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
            ProjectShockGL.Core.Game.Instance.World.GetAllObjects(includePending: true));
    }

    public static Level CaptureLevel(IEnumerable<Object> objects)
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
                Name = obj.Name, Path = path,
                
                IsActive = obj.IsActive,
                EditorGroups = obj.EditorGroups.ToArray(),
                
                Type = obj is BrushBox ? PlaceableType.BrushBox : obj is Geometry ? PlaceableType.Geometry : PlaceableType.Entity,
                
                Brush = obj is BrushBox brush ? new BrushBoxData
                {
                    Movable = brush.Movable,
                    Size = brush.Size, Texture = brush.TextureName, TextureFormat = brush.TextureFormat,
                    TextureScale = brush.TextureScale, Color = brush.Color, Lit = brush.Lit
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
            Groups = SceneGroups.Capture(),
            Placeables = result.ToArray()
        };
    }

    private static string Serialize(Level level)
    {
        return JsonConvert.SerializeObject(level, JsonHelper.GetSettings());
    }

    public static Object DuplicateObject(Object source)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(includePending: true).ToHashSet();

        if (source.IsDestroyed || !existing.Contains(source))
        {
            throw new InvalidOperationException("The source object is no longer in the scene.");
        }

        var placement = CaptureLevel([source]).Placeables.SingleOrDefault()
            ?? throw new InvalidOperationException("Only placed geometry and entities can be duplicated.");
        
        var settings = JsonHelper.GetSettings();
        var snapshot = JsonConvert.DeserializeObject<Placeable>(
            JsonConvert.SerializeObject(placement, settings), settings)!;
        
        var stem = string.IsNullOrWhiteSpace(source.Name) ? Path.GetFileNameWithoutExtension(snapshot.Path) : source.Name;
        var separator = stem.LastIndexOf('_');
        
        if (separator > 0 && int.TryParse(stem.AsSpan(separator + 1), out _)) stem = stem[..separator];
        
        var names = existing.Select(obj => obj.Name).ToHashSet(StringComparer.Ordinal);
        var suffix = 1;

        while (names.Contains($"{stem}_{suffix}"))
        {
            suffix++;
        }
        
        var name = $"{stem}_{suffix}";
        
        if (!string.IsNullOrEmpty(source.Name))
        {
            foreach (var output in snapshot.Outputs)
            {
                if (output.TargetName == source.Name) output.TargetName = name;
            }
        }

        var duplicate = new Placeable
        {
            Name = name, Path = snapshot.Path, Type = snapshot.Type,
            IsActive = snapshot.IsActive,
            EditorGroups = snapshot.EditorGroups,
            Brush = snapshot.Brush,
            Position = snapshot.Position, Rotation = snapshot.Rotation, Scale = snapshot.Scale,
            Properties = snapshot.Properties, Outputs = snapshot.Outputs
        };
        
        return CreatePlacement(duplicate);
    }

    public static Object CreatePlacement(Placeable placement)
    {
        var world = ProjectShockGL.Core.Game.Instance.World;
        var existing = world.GetAllObjects(true).ToHashSet();

        try
        {
            return LevelManager.CreateObject(world, placement);
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
        
        (level.Environment ?? new EnvironmentData()).Validate();
        
        var directory = Path.GetDirectoryName(fullPath)!;
        var json = Serialize(level);
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
        
        _savedState = Serialize(level);
        
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
        
        Selection.Object = null;
        UndoRedo.Clear();
        
        CurrentPath = null;
        Initialize();
    }
}

