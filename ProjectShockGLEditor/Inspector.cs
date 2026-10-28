using System.Numerics;
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
        ImGui.TextUnformatted(obj.GetType().Name);
        
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
            ImGui.Separator();
            ImGui.TextUnformatted("BrushBox");
            
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
            ImGui.Text("Entity");

            // Name
            var name = entity.Name;
            
            if (ImGui.InputText("Name", ref name, 64))
            {
                entity.Name = name;
                Changed("Name");
            }
            
            Finish("Name");
            
            //Properties
            
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
                var targetName = output.TargetName;
                
                if (ImGui.InputText("Target", ref targetName, 64))
                {
                    output.TargetName = targetName;
                    Changed($"Output/{i}/Target");
                }

                Finish($"Output/{i}/Target");
                var target = ProjectShockGL.Core.Game.Instance.World.Find(output.TargetName);

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
        
        ImGui.Separator();

        PrefabTools.DrawSave();
        ImGui.PopID();
        ImGui.End();
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




