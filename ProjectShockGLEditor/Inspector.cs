using System.Numerics;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

public static class Inspector
{
    private static Vector3 _dragStartPosition;
    private static Quaternion _dragStartRotation;
    private static Vector3 _dragStartScale;
    private static Vector3 _brushSizeStart;
    
    public static void Draw()
    {
        if (!ImGui.Begin("Inspector", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            
            return;
        }

        var obj = Selection.Object;
        
        if (obj == null || obj.IsDestroyed)
        {
            ImGui.TextWrapped("Select an object in the scene or the Scene list.");
            ImGui.End();
            
            return;
        }
        
        ImGui.TextUnformatted(obj.GetType().Name);
        var isActive = obj.IsActive;
        if (ImGui.Checkbox("IsActive", ref isActive))
        {
            obj.SetActive(isActive);
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
            ImGui.End();
            return;
        }

        ImGui.SameLine();
        
        if (ImGui.Button("Delete"))
        {
            Editor.DeleteSelection();
            ImGui.End();
            return;
        }
        
        ImGui.Separator();

        // Position
        var pos = obj.Transform.Position;
        var beforePosition = pos;
        
        if (ImGui.DragFloat3("Position", ref pos, 0.1f))
        {
            obj.Transform.Position = pos;
        }

        if (ImGui.IsItemActivated())
        {
            _dragStartPosition = beforePosition;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _dragStartPosition != obj.Transform.Position)
        {
            UndoRedo.Register(new PropertyChangeAction<Vector3>(
                v => obj.Transform.Position = v,
                _dragStartPosition,
                obj.Transform.Position));
        }

        // Rotation
        var euler = QuaternionHelper.ToEulerDegrees(obj.Transform.Rotation);
        var beforeRotation = obj.Transform.Rotation;
        
        if (ImGui.DragFloat3("Rotation", ref euler, 0.1f))
        {
            obj.Transform.Rotation = QuaternionHelper.FromEulerDegrees(euler);
        }

        if (ImGui.IsItemActivated())
        {
            _dragStartRotation = beforeRotation;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _dragStartRotation != obj.Transform.Rotation)
        {
            UndoRedo.Register(new PropertyChangeAction<Quaternion>(
                q => obj.Transform.Rotation = q,
                _dragStartRotation,
                obj.Transform.Rotation));
        }

        // Scale
        var scale = obj.Transform.Scale;
        var beforeScale = scale;
        
        if (ImGui.DragFloat3("Scale", ref scale, 0.1f))
        {
            obj.Transform.Scale = Vector3.Max(new Vector3(0.01f), scale);
        }

        if (ImGui.IsItemActivated())
        {
            _dragStartScale = beforeScale;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _dragStartScale != obj.Transform.Scale)
        {
            UndoRedo.Register(new PropertyChangeAction<Vector3>(
                s => obj.Transform.Scale = s,
                _dragStartScale,
                obj.Transform.Scale));
        }
        
        if (obj is BrushBox brush)
        {
            ImGui.Separator();
            ImGui.TextUnformatted("BrushBox");
            
            var brushName = brush.Name;

            if (ImGui.InputText("Name", ref brushName, 128))
            {
                brush.Name = brushName;
            }
            
            var size = brush.Size;
            var beforeSize = size;

            if (ImGui.DragFloat3("Size", ref size, 0.1f))
            {
                brush.Size = Vector3.Max(new Vector3(0.01f), size);
            }

            if (ImGui.IsItemActivated())
            {
                _brushSizeStart = beforeSize;
            }

            if (ImGui.IsItemDeactivatedAfterEdit() && _brushSizeStart != brush.Size)
            {
                UndoRedo.Register(new PropertyChangeAction<Vector3>(v => brush.Size = v, _brushSizeStart, brush.Size));
            }
            
            var density = brush.TextureScale;

            if (ImGui.DragFloat("Texture scale", ref density, 0.05f, 0.01f, 1000))
            {
                brush.TextureScale = Math.Clamp(density, 0.01f, 1000);
            }
            
            ImGui.TextUnformatted("Texture scale = repeats per world unit");
            
            if (ImGui.BeginCombo("Texture", $"{brush.TextureName} ({brush.TextureFormat})"))
            {
                if (ImGui.Selectable("WhitePixel", brush.TextureName == "WhitePixel"))
                {
                    brush.TextureName = "WhitePixel";
                }
                
                var directory = System.IO.Path.Combine(Content.Path, "Textures");
                
                if (Directory.Exists(directory))
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order())
                    {
                        var extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                        
                        if (extension is not (".jpg" or ".png"))
                        {
                            continue;
                        }
                        
                        var relative = System.IO.Path.GetRelativePath(directory, file);
                        var textureName = relative[..^extension.Length];
                        var format = extension == ".png" ? TextureFormat.Png : TextureFormat.Jpeg;
                        
                        if (ImGui.Selectable(relative, brush.TextureName == textureName && brush.TextureFormat == format))
                            Editor.Execute(() =>
                            {
                                _ = Content.LoadTexture(textureName, format) ?? throw new FileNotFoundException(relative);
                                brush.TextureName = textureName;
                                brush.TextureFormat = format;
                            });
                    }
                
                ImGui.EndCombo();
            }
            
            var color = new Vector4(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
            
            if (ImGui.ColorEdit4("Color", ref color))
            {
                brush.Color = new Color(color.X, color.Y, color.Z, color.W);
            }
            
            var lit = brush.Lit;

            if (ImGui.Checkbox("Lit", ref lit))
            {
                brush.Lit = lit;
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
            }
            
            //Properties
            
            foreach (var prop in EditableProperties.Get(entity.GetType()))
            {
                var value = prop.GetValue(entity)!;
                var type = prop.PropertyType;

                if (type == typeof(float))
                {
                    var f = (float)value;
                    
                    if (ImGui.DragFloat(prop.Name, ref f))
                    {
                        prop.SetValue(entity, f);
                    }
                }
                else if (type == typeof(int))
                {
                    var i = (int)value;
                    
                    if (ImGui.DragInt(prop.Name, ref i))
                    {
                        prop.SetValue(entity, i);
                    }
                }
                else if (type == typeof(bool))
                {
                    var b = (bool)value;
                    
                    if (ImGui.Checkbox(prop.Name, ref b))
                    {
                        prop.SetValue(entity, b);
                    }
                }
                else if (type == typeof(string))
                {
                    var s = (string)value;
                    
                    if (ImGui.InputText(prop.Name, ref s, 256))
                    {
                        prop.SetValue(entity, s);
                    }
                }
                else if (type == typeof(Vector3))
                {
                    var v = (Vector3)value;

                    if (ImGui.DragFloat3(prop.Name, ref v))
                    {
                        prop.SetValue(entity, v);
                    }
                }
                else if (type == typeof(Color))
                {
                    var c = (Color)value;
                    var vec4 = new Vector4(c.R, c.G, c.B, c.A);

                    if (ImGui.ColorEdit4(prop.Name, ref vec4))
                    {
                        prop.SetValue(entity, new Color(vec4.X, vec4.Y, vec4.Z, vec4.W));
                    }
                }
                else if (type.IsEnum)
                {
                    var names = Enum.GetNames(type);
                    var current = Array.IndexOf(names, value.ToString());

                    if (ImGui.Combo(prop.Name, ref current, names, names.Length))
                    {
                        prop.SetValue(entity, Enum.Parse(type, names[current]));
                    }
                }
            }
            
            // Outputs
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
                }

                var targetName = output.TargetName;
                
                if (ImGui.InputText("Target", ref targetName, 64))
                {
                    output.TargetName = targetName;
                }

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
                            }

                            foreach (var inputName in inputNames)
                            {
                                var selected = output.Input == inputName;
                                
                                if (ImGui.Selectable(inputName, selected))
                                {
                                    output.Input = inputName;
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
                        }
                    }
                }
                else
                {
                    var input = output.Input;

                    if (ImGui.InputText("Input", ref input, 64))
                    {
                        output.Input = input;
                    }
                }

                var parameter = output.Parameter;
                
                if (ImGui.InputText("Parameter", ref parameter, 64))
                {
                    output.Parameter = parameter;
                }

                if (ImGui.Button("Remove"))
                {
                    entity.Outputs.RemoveAt(i);
                    i--;
                }

                ImGui.Separator();
                ImGui.PopID();
            }

            if (ImGui.Button("Add Output"))
            {
                entity.Outputs.Add(new OutputData());
            }
        }

        ImGui.End();
    }
}
