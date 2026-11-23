using System.Numerics;
using ImGuiNET;
using NativeFileDialogNET;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using Silk.NET.OpenGL;

namespace ProjectShockGLEditor;

public static class ParticleEditor
{
    private static ParticleAssetDocument? _document;
    private static ParticleAssetDocument Document => _document ??= new();
    
    private static World? _world;
    
    private static ParticleSystemComponent? _particles;
    
    private static Camera? _camera;
    private static RenderTexture? _target;
    
    private static int _revision = -1;
    
    private static bool _paused;
    private static bool _askUnsaved;
    
    private static Action? _pending;
    
    private static string? _error;

    private static float _yaw = .5f;
    private static float _pitch = -.25f;
    private static float _distance = 6;
    public static bool Open { get; set; }

    public static void Toggle()
    {
        if (Open)
        {
            Request(Close);
        }
        else
        {
            Open = true;
        }
    }

    public static bool AllowClose()
    {
        if (_document?.IsDirty != true)
        {
            return true;
        }
        
        Open = true;
        Request(Application.Quit);
        
        return false;
    }

    private static void Request(Action action)
    {
        if (Document.IsDirty)
        {
            _pending = action; 
            _askUnsaved = true;
        }
        else
        {
            Execute(action);
        }
    }

    private static void Execute(Action action)
    {
        try
        {
            action();
            _error = null;
        }
        catch (Exception error)
        {
            _error = error.GetBaseException().Message;
        }
    }

    private static void Close()
    {
        Open = false;
    }

    public static void Dispose()
    {
        ReleasePreview();
        
        _document = null;
        _pending = null;
        
        _askUnsaved = false;
        
        Open = false;
    }

    private static void ReleasePreview()
    {
        try
        {
            _world?.Clear();
        }
        finally
        {
            _world = null;
            _particles = null;
            
            _camera?.Dispose();
            
            _camera = null;
            
            _target?.Dispose();
            _target = null;
            
            _revision = -1;
        }
    }

    private static void Load()
    {
        Directory.CreateDirectory(Document.DirectoryPath);
        
        using var dialog = new NativeFileDialog().AddFilter("Particle JSON", "json");

        if (dialog.Open(out string? path, Document.DirectoryPath, "") == DialogResult.Okay &&
            !string.IsNullOrWhiteSpace(path))
        {
            Document.Open(path);
        }
    }

    private static bool Save(bool saveAs = false)
    {
        try
        {
            var path = saveAs ? null : Document.FilePath;

            if (path == null)
            {
                Directory.CreateDirectory(Document.DirectoryPath);

                using var dialog = new NativeFileDialog().SaveFile().AddFilter("Particle JSON", "json");

                var name = Document.FilePath == null
                    ? "Particle.json"
                    : Path.GetFileNameWithoutExtension(Document.FilePath) + " Copy.json";

                if (dialog.Open(out path, Document.DirectoryPath, name) != DialogResult.Okay ||
                    string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                if (!Path.HasExtension(path))
                {
                    path += ".json";
                }
            }

            Document.Save(path);

            _error = null;

            return true;
        }
        catch (Exception error)
        {
            _error = error.GetBaseException().Message; 
            return false;
        }
    }

    internal static void PlayPreview()
    {
        var resume=_paused;
        _paused=false;
        if (!resume) _particles?.Play();
    }
    internal static void PausePreview()
    {
        _paused = true;
    }

    internal static void RestartPreview()
    {
        _particles?.Clear(); 
        _particles?.Play(); 
        
        _paused = false;
    }
    
    public static void Draw()
    {
        if (!Open)
        {
            ReleasePreview(); 
            return;
        }
        
        ImGui.SetNextWindowSize(new Vector2(1100, 720), ImGuiCond.FirstUseEver);
        
        var open = true;
        var name = Document.FilePath == null ? "New" : Path.GetFileNameWithoutExtension(Document.FilePath);
        
        var visible = ImGui.Begin($"Particle Editor — {name}{(Document.IsDirty ? " *" : "")}###Particle Editor", ref open);

        if (!open)
        {
            Request(Close);
        }
        
        if (visible && Open)
        {
            if (ImGui.Button("New"))
            {
                Request(Document.New);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Open"))
            {
                Request(Load);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Save"))
            {
                Save();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Save As"))
            {
                Save(true);
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Play"))
            {
                PlayPreview();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Pause"))
            {
                PausePreview();
            }
            
            ImGui.SameLine();

            if (ImGui.Button("Restart"))
            {
                RestartPreview();
            }
            
            ImGui.SameLine();
            
            ImGui.TextUnformatted($"Alive: {_particles?.ParticleCount ?? 0}{(_paused ? " / Paused" : "")}");
            ImGui.TextWrapped(Document.FilePath ?? "Unsaved particle asset");

            if (_error != null)
            {
                ImGui.TextWrapped(_error);
            }
            
            ImGui.BeginChild("Parameters", new Vector2(330, 0), ImGuiChildFlags.Border);
            
            DrawParameters();
            
            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginChild("Preview", Vector2.Zero, ImGuiChildFlags.Border);

            try
            {
                DrawPreview();
            }
            catch (Exception error)
            {
                _error = error.GetBaseException().Message;
            }
            
            ImGui.EndChild();
        }
        
        DrawUnsaved();
        
        ImGui.End();
    }

    private static void DrawParameters()
    {
        DrawAppearance();
        DrawMotion();
        
        foreach (var property in typeof(ParticleAsset).GetProperties())
        {
            var asset = Document.Asset;
            if(property.Name is "VelocityOverLifetime" or "VelocityCurve" or "Noise" or "NoiseStrength" or "NoiseFrequency" or "NoiseScrollSpeed") continue;
            if (property.Name == "Rate" && asset.EmissionMode != ParticleEmissionMode.Continuous ||
                property.Name == "BurstCount" && asset.EmissionMode != ParticleEmissionMode.Burst) continue;
            
            if (property.Name is "StartSize" or "StartLifetime" or "StartRotation" or "RotationSpeed" or "Size" or "Lifetime" or "SizeCurve" or "ColorGradient" or "SizeOverLifetime" or "ColorOverLifetime") continue;

            if (property.Name == nameof(ParticleAsset.Version) ||
                property.Name == nameof(ParticleAsset.BoxSize) && asset.Shape != ParticleShape.Box ||
                property.Name == nameof(ParticleAsset.Radius) &&
                asset.Shape is not (ParticleShape.Sphere or ParticleShape.Cone) ||
                property.Name == nameof(ParticleAsset.ConeAngle) && asset.Shape != ParticleShape.Cone ||
                property.Name == nameof(ParticleAsset.SphereSurface) && asset.Shape != ParticleShape.Sphere)
            {
                continue;
            }
            
            var value = property.GetValue(asset);
            var changed = false;
            
            object? next = value;
            
            ImGui.PushID(property.Name);
            
            if (property.PropertyType == typeof(string))
            {
                var text = (string?)value ?? "";
                
                changed = ImGui.InputText(property.Name, ref text, 1024);
                next = string.IsNullOrWhiteSpace(text) ? null : text;
            }
            else if (value is bool boolean)
            {
                changed = ImGui.Checkbox(property.Name, ref boolean); next = boolean;
            }
            else if (value is int integer)
            {
                changed = ImGui.InputInt(property.Name, ref integer); next = integer;
            }
            else if (value is float number)
            {
                changed = ImGui.DragFloat(property.Name, ref number, .05f); next = number;
            }
            else if (value is Vector3 vector)
            {
                changed = ImGui.DragFloat3(property.Name, ref vector, .05f); next = vector;
            }
            else if (value is Color color)
            {
                var rgba = new Vector4(color.R,color.G,color.B,color.A);
                
                changed = ImGui.ColorEdit4(property.Name, ref rgba);
                next = new Color(rgba.X,rgba.Y,rgba.Z,rgba.W);
            }
            else if (property.PropertyType.IsEnum)
            {
                if (ImGui.BeginCombo(property.Name, value?.ToString()))
                {
                    foreach (var item in Enum.GetValues(property.PropertyType))
                    {
                        if (ImGui.Selectable(item.ToString(), Equals(value, item)))
                        {
                            next = item;
                            changed = true;
                        }
                    }
                    
                    ImGui.EndCombo();
                }
            }

            if (changed)
            {
                Execute(() => Document.Set(property.Name, next));
            }
            
            ImGui.PopID();
        }
        
        ImGui.Separator();
        ImGui.TextWrapped("Cone: local -Z, half-angle in degrees. Velocity length is speed. Shapes do not confine living particles. Parameter changes restart the preview.");
    }

    private static void DrawAppearance()
    {
        ImGui.TextUnformatted("Initial ranges (rotation in degrees)");

        void Range(string name, ParticleRange range, bool optional = false, string? legacy = null)
        {
            if (optional)
            {
                var enabled = typeof(ParticleAsset).GetProperty(name)!.GetValue(Document.Asset) != null;

                if (ImGui.Checkbox("Random " + name, ref enabled))
                {
                    Execute(() => Document.Set(name, enabled ? range : null));
                }

                if (!enabled)
                {
                    var number = name == "StartSize" ? Document.Asset.Size : Document.Asset.Lifetime;

                    if (ImGui.InputFloat(legacy!, ref number))
                    {
                        Execute(() => Document.Set(legacy!, number));
                    }

                    return;
                }
            }

            var vector = new Vector2(range.Min, range.Max);

            ImGui.TextUnformatted(name + " Min / Max");
            ImGui.SetNextItemWidth(-1);

            if (ImGui.DragFloat2("##" + name, ref vector, .05f))
            {
                Execute(() => Document.Set(name, new ParticleRange(vector.X, vector.Y)));
            }
        }

        var asset = Document.Asset;

        Range("StartLifetime", asset.StartLifetime ?? new ParticleRange(asset.Lifetime, asset.Lifetime), true,
            "Lifetime");
        Range("StartSize", asset.StartSize ?? new ParticleRange(asset.Size, asset.Size), true, "Size");
        Range("StartRotation", Document.Asset.StartRotation);
        Range("RotationSpeed", Document.Asset.RotationSpeed);
        
        var sizeEnabled = Document.Asset.SizeOverLifetime;

        if (ImGui.Checkbox("Size over lifetime", ref sizeEnabled))
        {
            Execute(() => Document.Set("SizeOverLifetime", sizeEnabled));
        }

        if (sizeEnabled)
        {
            DrawSizeCurve();
        }
        
        var colorEnabled = Document.Asset.ColorOverLifetime;

        if (ImGui.Checkbox("Color over lifetime", ref colorEnabled))
        {
            Execute(() => Document.Set("ColorOverLifetime", colorEnabled));
        }

        if (colorEnabled)
        {
            DrawColorGradient();
        }
        
        ImGui.Separator();
    }

    private static float NewKeyTime(IEnumerable<float> keys)
    {
        var times=keys.Append(0).Append(1).Distinct().Order().ToArray();
        var largest=Enumerable.Range(1,times.Length-1).OrderByDescending(i=>times[i]-times[i-1]).First();
        
        return (times[largest]+times[largest-1])*.5f;
    }

    private static void DrawSizeCurve()
    {
        var keys = Document.Asset.SizeCurve;
        
        ImGui.TextUnformatted("Normalized age / size multiplier");
        
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(20, ImGui.GetContentRegionAvail().X);
        
        ImGui.Dummy(new Vector2(width, 65));
        
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + new Vector2(width, 65), 0xff222222);
        
        var max = Math.Max(1, keys.Max(key => key.Value));

        Vector2 Point(float time)
        {
            return origin + new Vector2(time * width, 62 - ParticleAppearance.EvaluateSize(keys, time) / max * 59);
        }

        for (var i = 0; i < 63; i++)
        {
            draw.AddLine(Point(i / 63f), Point((i + 1) / 63f), 0xff66ddff, 1.5f);
        }
        
        for (var i = 0; i < keys.Length; i++)
        {
            ImGui.PushID("SizeKey" + i);
            
            var value = new Vector2(keys[i].Time, keys[i].Value);
            
            ImGui.SetNextItemWidth(Math.Max(30, width - 35));
            
            if (ImGui.DragFloat2("##TimeValue", ref value, .01f))
            {
                var next = keys.ToArray();
                next[i] = new ParticleCurveKey(value.X, value.Y);
                
                Execute(() => Document.Set("SizeCurve", next.OrderBy(key => key.Time).ToArray()));
            }

            ImGui.SameLine();

            if (keys.Length > 1 && ImGui.SmallButton("X"))
            {
                Execute(() => Document.Set("SizeCurve", keys.Where((_, index) => index != i).ToArray()));
            }
            
            ImGui.PopID();
        }

        if (keys.Length < 128 && ImGui.Button("Add size key"))
        {
            var time = NewKeyTime(keys.Select(key => key.Time));
            
            Execute(() => Document.Set("SizeCurve",
                keys.Append(new ParticleCurveKey(time, ParticleAppearance.EvaluateSize(keys, time)))
                    .OrderBy(key => key.Time).ToArray()));
        }
    }

    private static void DrawColorGradient()
    {
        var keys = Document.Asset.ColorGradient;
        
        ImGui.TextUnformatted("Normalized age / color and alpha");
        
        var origin = ImGui.GetCursorScreenPos();
        var width = Math.Max(20, ImGui.GetContentRegionAvail().X);
        
        ImGui.Dummy(new Vector2(width, 24));
        
        var draw = ImGui.GetWindowDrawList();
        
        for (var i = 0; i < 64; i++)
        {
            var color = ParticleAppearance.EvaluateColor(keys, i / 63f);
            // Checkerboard makes fading alpha visible in the preview strip.
            
            for (var row = 0; row < 2; row++)
            {
                var bg = ((i / 2 + row) % 2 == 0) ? .2f : .4f;
                var alpha = Math.Clamp(color.A, 0, 1);
                var rgb = new Vector4(color.R * alpha + bg * (1 - alpha), color.G * alpha + bg * (1 - alpha),
                    color.B * alpha + bg * (1 - alpha), 1);
                
                draw.AddRectFilled(origin + new Vector2(i * width / 64, row * 12),
                    origin + new Vector2((i + 1) * width / 64, (row + 1) * 12), ImGui.ColorConvertFloat4ToU32(rgb));
            }
        }

        for (var i = 0; i < keys.Length; i++)
        {
            ImGui.PushID("ColorKey" + i);
            
            var time = keys[i].Time;
            var color = keys[i].Color;
            var rgba = new Vector4(color.R, color.G, color.B, color.A);
            
            ImGui.SetNextItemWidth(65);
            
            var changed = ImGui.DragFloat("##Time", ref time, .01f);
            
            ImGui.SameLine();
            ImGui.SetNextItemWidth(Math.Max(30, width - 110));
            
            changed |= ImGui.ColorEdit4("##Color", ref rgba,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar);
            
            if (changed)
            {
                var next = keys.ToArray();
                
                next[i] = new ParticleColorKey(time, new Color(rgba.X, rgba.Y, rgba.Z, rgba.W));
                Execute(() => Document.Set("ColorGradient", next.OrderBy(key => key.Time).ToArray()));
            }

            ImGui.SameLine();

            if (keys.Length > 1 && ImGui.SmallButton("X"))
            {
                Execute(() => Document.Set("ColorGradient", keys.Where((_, index) => index != i).ToArray()));
            }
            
            ImGui.PopID();
        }

        if (keys.Length < 128 && ImGui.Button("Add color key"))
        {
            var time = NewKeyTime(keys.Select(key => key.Time));
            
            Execute(() => Document.Set("ColorGradient",
                keys.Append(new ParticleColorKey(time, ParticleAppearance.EvaluateColor(keys, time)))
                    .OrderBy(key => key.Time).ToArray()));
        }
    }

    private static void DrawMotion()
    {
        var enabled=Document.Asset.VelocityOverLifetime;
        if(ImGui.Checkbox("Velocity by lifetime",ref enabled)) Execute(()=>Document.Set("VelocityOverLifetime",enabled));
        if(enabled)
        {
            var keys=Document.Asset.VelocityCurve;
            ImGui.TextWrapped("Added local velocity: normalized age / X Y Z");
            var width=Math.Max(20,ImGui.GetContentRegionAvail().X);
            var origin=ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(width,65));
            var draw=ImGui.GetWindowDrawList();
            draw.AddRectFilled(origin,origin+new Vector2(width,65),0xff222222);
            var scale=Math.Max(1,keys.Max(key=>Math.Max(Math.Abs(key.Velocity.X),Math.Max(Math.Abs(key.Velocity.Y),Math.Abs(key.Velocity.Z)))));
            draw.AddLine(origin+new Vector2(0,32),origin+new Vector2(width,32),0xff555555);
            for(var axis=0;axis<3;axis++)
            {
                Vector2 Point(float time)
                {
                    var v=ParticleMotion.EvaluateVelocity(keys,time);
                    var value=axis==0?v.X:axis==1?v.Y:v.Z;
                    return origin+new Vector2(time*width,32-value/scale*30);
                }
                for(var i=0;i<63;i++) draw.AddLine(Point(i/63f),Point((i+1)/63f),axis==0?0xff5555ff:axis==1?0xff55ff55:0xffffaa55,1.5f);
            }
            for(var i=0;i<keys.Length;i++)
            {
                ImGui.PushID("VelocityKey"+i);
                var time=keys[i].Time;var velocity=keys[i].Velocity;
                ImGui.SetNextItemWidth(55);
                var changed=ImGui.DragFloat("##Age",ref time,.01f);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(Math.Max(30,width-100));
                changed|=ImGui.DragFloat3("##Velocity",ref velocity,.05f);
                if(changed)
                {
                    var next=keys.ToArray();next[i]=new ParticleVelocityKey(time,velocity);
                    Execute(()=>Document.Set("VelocityCurve",next.OrderBy(key=>key.Time).ToArray()));
                }
                ImGui.SameLine();
                if(keys.Length>1 && ImGui.SmallButton("X")) Execute(()=>Document.Set("VelocityCurve",keys.Where((_,index)=>index!=i).ToArray()));
                ImGui.PopID();
            }
            if(keys.Length<128 && ImGui.Button("Add velocity key"))
            {
                var time=NewKeyTime(keys.Select(key=>key.Time));
                Execute(()=>Document.Set("VelocityCurve",keys.Append(new ParticleVelocityKey(time,ParticleMotion.EvaluateVelocity(keys,time))).OrderBy(key=>key.Time).ToArray()));
            }
        }
        var noise=Document.Asset.Noise;
        if(ImGui.Checkbox("Noise",ref noise)) Execute(()=>Document.Set("Noise",noise));
        if(noise)
        {
            var strength=Document.Asset.NoiseStrength;
            if(ImGui.DragFloat3("Noise strength",ref strength,.05f)) Execute(()=>Document.Set("NoiseStrength",strength));
            var frequency=Document.Asset.NoiseFrequency;
            if(ImGui.DragFloat("Noise frequency",ref frequency,.05f)) Execute(()=>Document.Set("NoiseFrequency",frequency));
            var scroll=Document.Asset.NoiseScrollSpeed;
            if(ImGui.DragFloat("Noise scroll speed",ref scroll,.05f)) Execute(()=>Document.Set("NoiseScrollSpeed",scroll));
            ImGui.TextWrapped("Smooth world-space velocity noise, not accumulated acceleration.");
        }
        ImGui.Separator();
    }
    private static void DrawPreview()
    {
        if (_world == null)
        {
            _world = new World();
            
            var owner = _world.Spawn<ProjectShockGL.Core.Object>();
            
            _particles = owner.AddComponent<ParticleSystemComponent>();
            
            _world.Update(0, simulate: false);
            
            _camera = Camera.Create();
            _camera.ClearColor = new Color(.045f,.05f,.06f);
        }
        if (_revision != Document.Revision)
        {
            // Mark failed revisions too: show the error without repeated resource loads every frame.
            _revision = Document.Revision;
            
            _particles!.Stop();
            _particles.Clear();
            _particles.Apply(Document.Asset);
            
            _error = null;
            
            _particles.Play();
        }

        if (!_paused)
        {
            _particles!.Update(Math.Clamp(ImGui.GetIO().DeltaTime, 0, .25f));
        }
        
        ImGui.TextUnformatted("Right drag: orbit / Wheel: zoom");
        
        var available = ImGui.GetContentRegionAvail();
        
        var width = Math.Clamp((int)available.X, 32, 2048);
        var height = Math.Clamp((int)available.Y, 32, 2048);

        if (_target == null)
        {
            _target = Content.CreateRenderTexture(width,height,RenderTextureFilter.Linear);
        }
        else if (_target.Width != width || _target.Height != height)
        {
            _target.Resize(width,height);
        }
        
        var origin = ImGui.GetCursorScreenPos();
        
        ImGui.InvisibleButton("Viewport", new Vector2(width,height));
        
        if (ImGui.IsItemHovered())
        {
            var io = ImGui.GetIO();
            
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                _yaw -= io.MouseDelta.X * .01f;
                _pitch = Math.Clamp(_pitch - io.MouseDelta.Y * .01f, -1.45f, 1.45f);
            }
            
            _distance = Math.Clamp(_distance * MathF.Exp(-io.MouseWheel * .15f), .2f, 1000);
        }
        
        _camera!.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(_yaw,_pitch,0);
        _camera.Transform.Position = -_camera.Transform.Forward * _distance;
        // Capture restores AspectRatio: keep the overlay on the same projection as the image.
        _camera.AspectRatio = (float)width / height;
        
        var environment = ProjectShockGL.Environment.Capture();

        try
        {
            ProjectShockGL.Environment.Apply(new EnvironmentData { IsFogEnabled = false, SkyColorEnabled = false });

            _target.Capture(_world, _camera);
        }
        finally
        {
            ProjectShockGL.Environment.Apply(environment);
        }
        
        var draw = ImGui.GetWindowDrawList();
        draw.AddImage((nint)_target.Texture.Handle, origin, origin + new Vector2(width,height));
        
        DrawShape(draw,origin,new Vector2(width,height));
    }

    private static void DrawShape(ImDrawListPtr draw, Vector2 origin, Vector2 size)
    {
        var asset = Document.Asset;
        var matrix = _camera!.GetViewMatrix() * _camera.GetProjectionMatrix();
        
        void Line(Vector3 a, Vector3 b, uint color = 0xff66ddff)
        {
            var start=Vector4.Transform(new Vector4(a+asset.Offset,1),matrix);
            var end=Vector4.Transform(new Vector4(b+asset.Offset,1),matrix);
            
            if (start.W <= .001f || end.W <= .001f)
            {
                return;
            }
            
            Vector2 Screen(Vector4 p) => origin + new Vector2((p.X/p.W+1)*.5f,(1-p.Y/p.W)*.5f)*size;
            draw.AddLine(Screen(start),Screen(end),color,1);
        }
        
        draw.PushClipRect(origin,origin+size,true);
        // The fixed emitter origin remains visible when the shape is offset.
        Line(-asset.Offset-new Vector3(.15f,0,0),-asset.Offset+new Vector3(.15f,0,0),0xffaaaaaa);
        Line(-asset.Offset-new Vector3(0,.15f,0),-asset.Offset+new Vector3(0,.15f,0),0xffaaaaaa);
        Line(-asset.Offset-new Vector3(0,0,.15f),-asset.Offset+new Vector3(0,0,.15f),0xffaaaaaa);
        
        if (asset.Shape == ParticleShape.Box)
        {
            var half=asset.BoxSize*.5f;
            var corners=Enumerable.Range(0,8).Select(i=>new Vector3((i&1)==0?-half.X:half.X,(i&2)==0?-half.Y:half.Y,(i&4)==0?-half.Z:half.Z)).ToArray();
            
            for (var i = 0; i < 8; i++)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    if ((i & (1 << axis)) == 0)
                    {
                        Line(corners[i], corners[i | (1 << axis)]);
                    }
                }
            }
        }
        else if (asset.Shape == ParticleShape.Sphere)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                for (var i = 0; i < 64; i++)
                {
                    Vector3 Circle(float angle) => axis switch
                    {
                        0 => new Vector3(0, MathF.Cos(angle), MathF.Sin(angle)) * asset.Radius,
                        1 => new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * asset.Radius,
                        _ => new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * asset.Radius
                    };

                    Line(Circle(i * MathF.Tau / 64), Circle((i + 1) * MathF.Tau / 64));
                }
            }
        }
        else if (asset.Shape == ParticleShape.Cone)
        {
            var length=Math.Max(.5f,Math.Min(5,asset.Velocity.Length()*asset.Lifetime));
            var spread=length*MathF.Tan(asset.ConeAngle*MathF.PI/180);
            
            Vector3 Ring(float angle,float radius,float z)=>new(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius,z);

            for (var i = 0; i < 64; i++)
            {
                var a = i * MathF.Tau / 64;
                var b = (i + 1) * MathF.Tau / 64;
                
                Line(Ring(a, asset.Radius, 0), Ring(b, asset.Radius, 0));
                Line(Ring(a, asset.Radius + spread, -length), Ring(b, asset.Radius + spread, -length));
                
                if (i % 16 == 0)
                {
                    Line(Ring(a, asset.Radius, 0), Ring(a, asset.Radius + spread, -length));
                }
            }

            Line(Vector3.Zero,new Vector3(0,0,-length));
        }
        else
        {
            Line(new Vector3(-.1f,0,0),new Vector3(.1f,0,0));
            Line(new Vector3(0,-.1f,0),new Vector3(0,.1f,0));
            Line(new Vector3(0,0,-.1f),new Vector3(0,0,.1f));
        }
        
        draw.PopClipRect();
    }

    private static void DrawUnsaved()
    {
        if (_askUnsaved)
        {
            ImGui.OpenPopup("Unsaved particle asset"); 
            
            _askUnsaved=false;
        }

        if (!ImGui.BeginPopupModal("Unsaved particle asset", ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }
        
        ImGui.TextUnformatted("Save changes to this particle asset?");
        
        void Complete()
        {
            var action=_pending;
            _pending = null;
            
            ImGui.CloseCurrentPopup();
            
            if (action != null)
            {
                Execute(action);
            }
        }

        if (ImGui.Button("Save") && Save())
        {
            Complete();
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
            Document.New(); 
            Complete();
        }
        
        ImGui.SameLine();

        if (ImGui.Button("Cancel"))
        {
            _pending = null;
            
            ImGui.CloseCurrentPopup();
        }

        if (_error != null)
        {
            ImGui.TextWrapped(_error);
        }
        
        ImGui.EndPopup();
    }
}