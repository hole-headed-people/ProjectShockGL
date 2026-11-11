using System.Numerics;
using System.Reflection;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public static class Inspector
{
    private static Object _frameObject = null!;
    
    private static string _before = "";
    private static string _textureSearch = "";
    private static string _targetSearch = "";

    public static void Draw()
    {
        if (!ImGui.Begin("Inspector", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            
            return;
        }

        var obj = Selection.Object;
        
        if (Selection.Objects.Length > 1)
        {
            ImGui.TextUnformatted($"Selected objects: {Selection.Objects.Length}");
            ImGui.TextWrapped("Multiple-object editing is not available. Use the scene gizmo to move, rotate or scale the selection.");
            PrefabTools.DrawSave();
            ImGui.End();
            
            return;
        }
        
        if (obj == null || obj.IsDestroyed)
        {
            ImGui.TextWrapped("Select an object in the scene or the Scene list.");
            ImGui.End();
            
            return;
        }
        
        _frameObject = obj;
        _before = InspectorState.Capture(obj);
        
        ImGui.PushID(obj.GetHashCode());
        ImGui.TextUnformatted("Transform");
        
        // Position
        var pos = obj.Transform.Position;
        
        if (ImGui.DragFloat3("Position", ref pos, 0.1f))
        {
            obj.Transform.Position = pos;
            Changed("Position");
        }

        Finish("Position");
        
        // Rotation
        var euler = QuaternionHelper.ToEulerDegrees(obj.Transform.Rotation);
        
        if (ImGui.DragFloat3("Rotation", ref euler, 0.1f))
        {
            obj.Transform.Rotation = QuaternionHelper.FromEulerDegrees(euler);
            Changed("Rotation");
        }

        Finish("Rotation");
        
        // Scale
        var scale = obj.Transform.Scale;
        
        if (ImGui.DragFloat3("Scale", ref scale, 0.1f))
        {
            obj.Transform.Scale = Vector3.Max(new Vector3(0.01f), scale);
            Changed("Scale");
        }
        
        Finish("Scale");
        
        var isActive = obj.IsActive;
        
        if (ImGui.Checkbox("IsActive", ref isActive))
        {
            obj.SetActive(isActive);
            Changed("IsActive", true);
        }


        if (obj is BrushBox brush)
        {
            var movable = brush.Movable;
            if (ImGui.Checkbox("Movable", ref movable))
            {
                brush.Movable = movable;
                Changed("Movable", true);
            }

            var simulatePhysics = brush.SimulatePhysics;
            if (ImGui.Checkbox("Simulate physics", ref simulatePhysics))
            {
                brush.SimulatePhysics = simulatePhysics;
                Changed("SimulatePhysics", true);
            }

            if (brush.GetComponent<Rigidbody>() is { IsDestroyed: false } brushBody)
            {
                DrawMass(brushBody);
            }

            ImGui.Separator();
            ImGui.TextUnformatted("Properties");
            
            var brushName = brush.Name;

            if (ImGui.InputText("Name", ref brushName, 128))
            {
                brush.Name = brushName;
                Changed("Name");
            }
            
            Finish("Name");
            
            var size = brush.Size;

            if (ImGui.DragFloat3("Size", ref size, 0.1f))
            {
                brush.Size = Vector3.Max(new Vector3(0.01f), size);
                Changed("Size");
            }
            
            Finish("Size");
            
            var density = brush.TextureScale;

            if (ImGui.DragFloat("Texture scale", ref density, 0.05f, 0.01f, 1000))
            {
                brush.TextureScale = Math.Clamp(density, 0.01f, 1000);
                Changed("TextureScale");
            }
            
            Finish("TextureScale");
            
            ImGui.TextUnformatted("Texture scale = repeats per world unit");
            
            if (ImGui.BeginCombo("Texture", $"{brush.TextureName} ({brush.TextureFormat})"))
            {
                ImGui.SetNextItemWidth(-1);
                
                ImGui.InputTextWithHint("##textureSearch", "Search textures...", ref _textureSearch, 256);
                
                if ("WhitePixel".Contains(_textureSearch.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    ImGui.Selectable("WhitePixel", brush.TextureName == "WhitePixel"))
                {
                    brush.TextureName = "WhitePixel";
                    Changed("Texture", true);
                }
                
                var directory = Path.Combine(Content.Path, "Textures");
                
                var textures = Directory.Exists(directory)
                    ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                        .Where(file => Path.GetExtension(file).ToLowerInvariant() is ".png" or ".jpg")
                        .Select(file => Path.GetRelativePath(directory, file).Replace('\\', '/')).ToArray()
                    : [];
                
                AssetFolders.Draw(textures, _textureSearch,
                    path => brush.TextureName.Replace('\\', '/') == path[..^Path.GetExtension(path).Length] &&
                        brush.TextureFormat == (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? TextureFormat.Png : TextureFormat.Jpeg),
                    path => Editor.Execute(() =>
                    {
                        var extension = Path.GetExtension(path);
                        var name = path[..^extension.Length];
                        var format = extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? TextureFormat.Png : TextureFormat.Jpeg;
                        _ = Content.LoadTexture(name, format) ?? throw new FileNotFoundException(path);
                        brush.TextureName = name;
                        brush.TextureFormat = format;
                        Changed("Texture", true);
                    }));
                
                ImGui.EndCombo();
            }
            
            var color = new Vector4(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
            
            if (ImGui.ColorEdit4("Color", ref color))
            {
                brush.Color = new Color(color.X, color.Y, color.Z, color.W);
                
                Changed("Color");
            }
            
            Finish("Color");
            
            var lit = brush.Lit;

            if (ImGui.Checkbox("Lit", ref lit))
            {
                brush.Lit = lit;
                Changed("Lit", true);
            }
        }

        if (obj is Entity entity)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("Properties");

            // Name
            var name = entity.Name;
            
            if (ImGui.InputText("Name", ref name, 64))
            {
                entity.Name = name;
                Changed("Name");
            }
            
            Finish("Name");
            
            //Properties
            if (entity.Rigidbody is { } body)
            {
                var simulatePhysics = body.IsEnabled;
                if (ImGui.Checkbox("Simulate physics", ref simulatePhysics))
                {
                    entity.SetSimulatePhysics(simulatePhysics);
                    Changed("SimulatePhysics", true);
                }

                DrawMass(body);
            }
            
            foreach (var prop in EditableProperties.Get(entity.GetType()))
            {
                if (entity is Light && prop.Name is nameof(Light.InnerAngle) or nameof(Light.OuterAngle))
                {
                    continue;
                }
                
                var value = prop.GetValue(entity)!;
                var type = prop.PropertyType;
                
                ImGui.PushID("Property/" + prop.Name);

                if (type == typeof(float))
                {
                    var f = (float)value;
                    
                    if (ImGui.DragFloat(prop.Name, ref f, 0.001f))
                    {
                        prop.SetValue(entity, f);
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type == typeof(int))
                {
                    var i = (int)value;
                    
                    if (ImGui.DragInt(prop.Name, ref i))
                    {
                        prop.SetValue(entity, i);
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type == typeof(bool))
                {
                    var b = (bool)value;
                    
                    if (ImGui.Checkbox(prop.Name, ref b))
                    {
                        prop.SetValue(entity, b);
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type == typeof(string))
                {
                    var s = (string)value;
                    
                    if (ImGui.InputText(prop.Name, ref s, 256))
                    {
                        prop.SetValue(entity, s);
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type == typeof(string[]))
                {
                    DrawStringArray(entity, prop, value as string[] ?? []);
                }
                else if (type == typeof(Vector3))
                {
                    var v = (Vector3)value;

                    if (ImGui.DragFloat3(prop.Name, ref v, 0.001f))
                    {
                        prop.SetValue(entity, v);
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type == typeof(Color))
                {
                    var c = (Color)value;
                    var vec4 = new Vector4(c.R, c.G, c.B, c.A);

                    if (ImGui.ColorEdit4(prop.Name, ref vec4))
                    {
                        prop.SetValue(entity, new Color(vec4.X, vec4.Y, vec4.Z, vec4.W));
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                else if (type.IsEnum)
                {
                    var names = Enum.GetNames(type);
                    var current = Array.IndexOf(names, value.ToString());

                    if (ImGui.Combo(prop.Name, ref current, names, names.Length))
                    {
                        prop.SetValue(entity, Enum.Parse(type, names[current]));
                        
                        Changed("Property/" + prop.Name, type == typeof(bool) || type.IsEnum);
                    }
                }
                
                Finish("Property/" + prop.Name);
                
                ImGui.PopID();
            }
            
            if (entity is Light { Type: LightType.Spot } spot)
            {
                ImGui.Separator();
                
                ImGui.TextUnformatted("Spot cone (full angles, degrees)");
                
                var innerAngle = spot.InnerAngle;

                if (ImGui.SliderFloat("Inner angle", ref innerAngle, 0, 179))
                {
                    spot.InnerAngle = innerAngle;
                    
                    Changed("InnerAngle");
                }
                
                Finish("InnerAngle");
                
                var outerAngle = spot.OuterAngle;

                if (ImGui.SliderFloat("Outer angle", ref outerAngle, 0.1f, 179))
                {
                    spot.OuterAngle = outerAngle;
                    Changed("OuterAngle");
                }
                
                Finish("OuterAngle");
                
                ImGui.TextUnformatted("Yellow: outer | Cyan: inner | White: direction");
            }
            
            //Outputs

            ImGui.Separator();
            ImGui.Text("Outputs");
            var eventNames = GetOutputEventNames(entity.GetType());
            var world = ProjectShockGL.Core.Game.Instance.World;
            var targetNames = entity.Outputs.Count > 0 ? GetOutputTargetNames(world) : [];

            for (var i = 0; i < entity.Outputs.Count; i++)
            {
                var output = entity.Outputs[i];

                ImGui.PushID(i);

                var eventName = output.Event;
                
                if (ImGui.InputText("Event", ref eventName, 64))
                {
                    output.Event = eventName;
                    Changed($"Output/{i}/Event");
                }

                Finish($"Output/{i}/Event");

                if (ChooseOutputValue("##EventChoices", output.Event, eventNames) is { } eventChoice)
                {
                    output.Event = eventChoice;
                    Changed($"Output/{i}/Event", true);
                }

                var targetName = output.TargetName;
                
                if (ImGui.InputText("Target", ref targetName, 64))
                {
                    output.TargetName = targetName;
                    Changed($"Output/{i}/Target");
                }

                Finish($"Output/{i}/Target");

                if (ChooseOutputValue("##TargetChoices", output.TargetName, targetNames, searchable: true) is { } targetChoice)
                {
                    output.TargetName = targetChoice;
                    Changed($"Output/{i}/Target", true);
                }

                var target = world.Find(output.TargetName);

                if (target != null)
                {
                    var inputNames = InputInvoker.GetInputNames(target.GetType());

                    if (inputNames.Length > 0)
                    {
                        var preview = string.IsNullOrEmpty(output.Input) ? "<None>" : output.Input;
                        
                        if (ImGui.BeginCombo("Input", preview))
                        {
                            if (ImGui.Selectable("<None>", string.IsNullOrEmpty(output.Input)))
                            {
                                output.Input = string.Empty;
                                Changed($"Output/{i}/Input", true);
                            }

                            foreach (var inputName in inputNames)
                            {
                                if (inputName == nameof(Entity.SetSimulatePhysics) && target is Entity { Rigidbody: null })
                                {
                                    continue;
                                }

                                var selected = output.Input == inputName;
                                
                                if (ImGui.Selectable(inputName, selected))
                                {
                                    output.Input = inputName;
                                    Changed($"Output/{i}/Input", true);
                                }

                                if (selected)
                                {
                                    ImGui.SetItemDefaultFocus();
                                }
                            }
                            
                            ImGui.EndCombo();
                        }
                    }
                    else
                    {
                        var input = output.Input;

                        if (ImGui.InputText("Input", ref input, 64))
                        {
                            output.Input = input;
                            Changed($"Output/{i}/Input");
                        }
                        Finish($"Output/{i}/Input");
                    }
                }
                else
                {
                    var input = output.Input;

                    if (ImGui.InputText("Input", ref input, 64))
                    {
                        output.Input = input;
                        Changed($"Output/{i}/Input");
                    }
                    Finish($"Output/{i}/Input");
                }

                var parameter = output.Parameter;
                
                if (ImGui.InputText("Parameter", ref parameter, 64))
                {
                    output.Parameter = parameter;
                    Changed($"Output/{i}/Parameter");
                }

                Finish($"Output/{i}/Parameter");
                if (ImGui.Button("Remove"))
                {
                    entity.Outputs.RemoveAt(i);
                    Changed("Outputs", true);
                    i--;
                }

                ImGui.Separator();
                ImGui.PopID();
            }

            if (ImGui.Button("Add Output"))
            {
                entity.Outputs.Add(new OutputData());
                Changed("Outputs", true);
            }
        }

        DrawCollisionLayers(obj);

        PrefabTools.DrawSave();

        ImGui.Separator();

        if (ImGui.Button("Focus (F)"))
        {
            SceneCamera.Focus(obj);
        }
        
        ImGui.SameLine();

        ImGui.BeginDisabled(!Editor.CanDuplicateSelection || ObjectManipulator.IsDragging);
        
        var duplicate = ImGui.Button("Duplicate");
        
        ImGui.EndDisabled();
        
        if (duplicate)
        {
            Editor.DuplicateSelection();
            ImGui.PopID();
            ImGui.End();
            
            return;
        }

        ImGui.SameLine();
        
        if (ImGui.Button("Delete"))
        {
            Editor.DeleteSelection();
            ImGui.PopID();
            ImGui.End();
            
            return;
        }
        
        ImGui.PopID();
        ImGui.End();
    }
    
    private static string[] GetOutputEventNames(Type type)
    {
        return type.GetCustomAttributes<OutputEventAttribute>(inherit: true)
            .Select(attribute => attribute.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetOutputTargetNames(World world)
    {
        return world.GetAllObjects(includePending: true)
            .Select(obj => obj.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ChooseOutputValue(string id, string current, string[] choices, bool searchable = false)
    {
        if (choices.Length == 0)
        {
            return null;
        }

        ImGui.SameLine();

        if (!ImGui.BeginCombo(id, "", ImGuiComboFlags.NoPreview))
        {
            return null;
        }

        if (searchable)
        {
            if (ImGui.IsWindowAppearing())
            {
                _targetSearch = "";
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.SetNextItemWidth(220);
            ImGui.InputTextWithHint("##TargetSearch", "Search targets...", ref _targetSearch, 256);
        }

        var search = searchable ? _targetSearch.Trim() : "";
        var hasMatches = false;
        string? choice = null;

        foreach (var value in choices)
        {
            if (!value.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasMatches = true;
            var selected = value == current;

            if (ImGui.Selectable(value, selected) && !selected)
            {
                choice = value;
            }

            if (selected && !searchable)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        if (!hasMatches)
        {
            ImGui.TextDisabled("No matching targets");
        }

        ImGui.EndCombo();
        return choice;
    }

    private static void DrawCollisionLayers(Object obj)
    {
        var layers = Layers.GetAll();
        var layerName = layers.FirstOrDefault(layer => layer.Value == obj.Layer).Key;
        var layerPreview = layerName ?? (obj.Layer == Layers.None ? "None" : $"0x{obj.Layer:X8}");

        if (ImGui.BeginCombo("Layer", layerPreview))
        {
            if (ImGui.Selectable("None##layer", obj.Layer == Layers.None))
            {
                obj.Layer = Layers.None;
                Changed(nameof(obj.Layer), true);
            }

            foreach (var (name, bit) in layers)
            {
                if (ImGui.Selectable(name, obj.Layer == bit))
                {
                    obj.Layer = bit;
                    Changed(nameof(obj.Layer), true);
                }
            }

            ImGui.EndCombo();
        }

        var maskPreview = obj.CollidesWith switch
        {
            Layers.None => "None",
            Layers.Everything => "Everything",
            _ => string.Join(", ", layers.Where(layer => (obj.CollidesWith & layer.Value) != 0)
                .Select(layer => layer.Key))
        };

        if (maskPreview.Length == 0)
        {
            maskPreview = $"0x{obj.CollidesWith:X8}";
        }

        if (!ImGui.BeginCombo("CollidesWith", maskPreview))
        {
            return;
        }

        if (ImGui.Button("Everything"))
        {
            obj.CollidesWith = Layers.Everything;
            Changed(nameof(obj.CollidesWith), true);
        }

        ImGui.SameLine();

        if (ImGui.Button("None"))
        {
            obj.CollidesWith = Layers.None;
            Changed(nameof(obj.CollidesWith), true);
        }

        foreach (var (name, bit) in layers)
        {
            var enabled = (obj.CollidesWith & bit) != 0;
            if (ImGui.Checkbox(name, ref enabled))
            {
                obj.CollidesWith = enabled ? obj.CollidesWith | bit : obj.CollidesWith & ~bit;
                Changed(nameof(obj.CollidesWith), true);
            }
        }

        ImGui.EndCombo();
    }

    private static void DrawMass(Rigidbody body)
    {
        var mass = body.Mass;
        if (ImGui.DragFloat("Mass", ref mass, 0.1f) && float.IsFinite(mass))
        {
            body.Mass = MathF.Max(0.001f, mass);
            Changed("RigidbodyMass");
        }

        Finish("RigidbodyMass");
    }

    private static void DrawStringArray(Entity entity, PropertyInfo property, string[] entries)
    {
        var options = property.GetCustomAttribute<EditableAttribute>()!;
        var field = "Property/" + property.Name;
        ImGui.TextUnformatted(property.Name);

        for (var index = 0; index < entries.Length; index++)
        {
            ImGui.PushID(index);
            var text = entries[index] ?? "";
            var rowField = $"{field}/{index}";
            var reservedWidth = string.IsNullOrWhiteSpace(options.ContentDirectory) ? 28f : 62f;
            ImGui.SetNextItemWidth(MathF.Max(30f, ImGui.GetContentRegionAvail().X - reservedWidth));
            if (ImGui.InputText("##Entry", ref text, 256))
            {
                SetEntry(text);
                Changed(rowField);
            }

            Finish(rowField);

            if (options.ContentDirectory is { Length: > 0 } directory)
            {
                ImGui.SameLine();
                if (ImGui.BeginCombo("##Asset", "", ImGuiComboFlags.NoPreview))
                {
                    var root = Path.Combine(Content.Path, directory);
                    if (Directory.Exists(root))
                    {
                        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                                     .Order(StringComparer.OrdinalIgnoreCase))
                        {
                            var name = Path.ChangeExtension(Path.GetRelativePath(root, file), null)!.Replace('\\', '/');
                            if (ImGui.Selectable(name, name == text))
                            {
                                SetEntry(name);
                                Changed(field, true);
                            }
                        }
                    }

                    ImGui.EndCombo();
                }
            }

            ImGui.SameLine();
            var remove = ImGui.SmallButton("X");
            ImGui.PopID();
            if (remove)
            {
                property.SetValue(entity, entries.Where((_, entryIndex) => entryIndex != index).ToArray());
                Changed(field, true);
                return;
            }

            void SetEntry(string value)
            {
                entries = (string[])entries.Clone();
                entries[index] = value;
                property.SetValue(entity, entries);
            }
        }

        ImGui.BeginDisabled(entries.Length >= options.MaxEntries);
        if (ImGui.SmallButton("Add"))
        {
            property.SetValue(entity, entries.Append("").ToArray());
            Changed(field, true);
        }

        ImGui.EndDisabled();
    }

    private static void Changed(string field, bool discrete = false)
    {
        var obj = _frameObject;
        var after = InspectorState.Capture(obj);
        
        UndoRedo.RecordEdit(obj, field, _before, after, state => InspectorState.Apply(obj, state),
            editing: !discrete && ImGui.IsItemActive());
        
        _before = after;
    }

    private static void Finish(string field)
    {
        var obj = _frameObject;
        
        if (ImGui.IsItemDeactivated())
        {
            UndoRedo.RecordEdit(obj, field, _before, _before,
                state => InspectorState.Apply(obj, state), editing: false);
        }
    }
}




