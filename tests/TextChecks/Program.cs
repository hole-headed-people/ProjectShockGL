using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.UI;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Texture = ProjectShockGL.Rendering.Texture;
using Shader = ProjectShockGL.Rendering.Shader;

var root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".");
var fontPath = Path.Combine(root, "ProjectDungeon/Content/Fonts/NotoSans-Regular.ttf");
var atlas = FontAtlas.Load(fontPath, 24);
Run("Atlas contains Latin and every Russian letter including Yo", () =>
{
    foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789№!?")
        Check(atlas.Glyphs.ContainsKey(c));
    Check(atlas.Glyphs['Ё'].Size.Y > 0 && atlas.Glyphs['ё'].Size.Y > 0);
});
Run("Atlas pixels and UV rectangles agree and preserve antialiasing", () =>
{
    Check(atlas.Pixels.Length == atlas.Width * atlas.Height * 4);
    var pixels = atlas.Pixels.Span;
    var covered = 0;
    var smooth = 0;
    for (var i = 3; i < pixels.Length; i += 4)
    {
        if (pixels[i] > 0) covered++;
        if (pixels[i] > 0 && pixels[i] < 255) smooth++;
    }
    Check(covered > 1000 && smooth > 1000);
    foreach (var glyph in atlas.Glyphs.Values)
    {
        var uv = glyph.UvRect;
        Check(uv.X >= 0 && uv.Y >= 0 && uv.X + uv.Z <= 1 && uv.Y + uv.W <= 1);
        Near(uv.Z * atlas.Width, glyph.Size.X);
        Near(uv.W * atlas.Height, glyph.Size.Y);
    }
});
Run("Empty strings, newlines, CRLF, tabs and spaces have predictable metrics", () =>
{
    Check(atlas.MeasureText("") == Vector2.Zero);
    Near(atlas.MeasureText("\n").Y, atlas.LineHeight * 2);
    Check(atlas.MeasureText("a\r\nb") == atlas.MeasureText("a\nb"));
    Check(atlas.MeasureText("a\rb") == atlas.MeasureText("a\nb"));
    Near(atlas.MeasureText("\t").X, atlas.GetGlyph(' ').Advance * 4);
    Check(atlas.LayoutText("   ").Glyphs.Count == 0 && atlas.MeasureText("   ").X > 0);
});
Run("Kerning uses font metrics and resets at line breaks", () =>
{
    var a = atlas.GetGlyph('A');
    var v = atlas.GetGlyph('V');
    Check(atlas.GetKerning('A', 'V') < 0);
    Near(atlas.MeasureText("AV").X, a.Advance + v.Advance + atlas.GetKerning('A', 'V'));
    Near(atlas.MeasureText("A\nV").X, Math.Max(a.Advance, v.Advance));
});
Run("Mixed text wraps whole words and long words without dropping glyphs", () =>
{
    var text = "Привет world Ёжик";
    var width = atlas.MeasureText("Привет ").X + 1;
    var wrapped = atlas.LayoutText(text, width);
    Check(wrapped.LineWidths.Count >= 3 && wrapped.LineWidths.All(w => w <= width + 0.01f));
    Check(wrapped.Glyphs.Count == atlas.LayoutText(text).Glyphs.Count);
    var longWord = atlas.LayoutText("абвгдеёжзийклмн", 25);
    Check(longWord.LineWidths.Count > 1 && longWord.Glyphs.Count == "абвгдеёжзийклмн".Length);
    Check(atlas.LayoutText("ABC", 0).Glyphs.Count == 3);
});
Run("Unsupported Unicode codepoints use one fallback each", () =>
{
    var layout = atlas.LayoutText("A😀Б");
    Check(layout.Glyphs.Count == 3 && layout.Glyphs[1].Glyph.Codepoint == '?');
    Check(atlas.LayoutText("\0\x01").Glyphs.Count == 0);
});
Run("Raster size changes metrics and malformed fonts are rejected", () =>
{
    var larger = FontAtlas.Load(fontPath, 48);
    Near(larger.MeasureText("Русский English").X, atlas.MeasureText("Русский English").X * 2);
    Near(larger.LineHeight, atlas.LineHeight * 2);
    Throws<ArgumentOutOfRangeException>(() => FontAtlas.Load(fontPath, 0));
    var invalid = Path.Combine(AppContext.BaseDirectory, "invalid.ttf");
    File.WriteAllBytes(invalid, new byte[32]);
    Throws<InvalidDataException>(() => FontAtlas.Load(invalid));
    Throws<ArgumentOutOfRangeException>(() => atlas.LayoutText("text", -1));
});
Run("Text widget starts as pointer-transparent and tolerates an unset font", () =>
{
    var canvas = new Canvas();
    var text = canvas.Add<Text>();
    Check(!text.HitTestVisible && text.MeasureText() == Vector2.Zero);
    text.Content = "Русский / English";
    Check(text.MeasureText() == Vector2.Zero);
    canvas.Destroy();
});
if (args.Contains("--render")) Render();
Console.WriteLine("All text checks passed.");

void Render()
{
    var options = WindowOptions.Default;
    options.Size = new Vector2D<int>(640, 240);
    options.IsVisible = false;
    options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
    using var window = Window.Create(options);
    window.Initialize();
    using var gl = window.CreateOpenGL();
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Settings.ini"),
        "[Content]\nCustomPath=" + Path.Combine(root, "ProjectDungeon/Content") + "\n");
    Settings.Initialize();
    Content.Initialize(gl);
    var corruptTexture = Path.Combine(AppContext.BaseDirectory, "CorruptTexture.png");
    File.WriteAllText(corruptTexture, "not an image");
    try
    {
        var rejected = false;
        try { Texture.Load(gl, corruptTexture); }
        catch { rejected = true; }
        Check(rejected && gl.GetInteger(GetPName.TextureBinding2D) == 0);
        using var shader = new Shader(gl, ShaderType.VertexShader, "#version 330 core\nvoid main(){gl_Position=vec4(0);}");
        var shaderHandle = shader.Handle;
        Check(gl.IsShader(shaderHandle));
        shader.Dispose();
        shader.Dispose();
        Check(!gl.IsShader(shaderHandle));
        var invalidRejected = false;
        try { using var invalid = new Shader(gl, ShaderType.VertexShader, "invalid shader source"); }
        catch { invalidRejected = true; }
        Check(invalidRejected && gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Corrupt texture releases its GL handle and shaders clean up safely");
    }
    finally { File.Delete(corruptTexture); }
    var preview = new RenderPreviewSettings();
    using var renderer = new Renderer(gl, 640, 240, preview);
    var graphics = new Graphics(renderer);
    Camera.Initialize(window);
    graphics.BeginDraw(Camera.Active);
    graphics.DrawLine2D(new Vector3(-0.5f, 0, -2), new Vector3(0.5f, 0, -2),
        ProjectShockGL.Rendering.Plane.Xy, Content.GetWhitePixel(), Color.White, ignoreDepth: true);
    var unlitProgram = (uint)typeof(Renderer).GetField("_unlitProgram",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(renderer)!;
    Check((uint)gl.GetInteger(GetPName.CurrentProgram) == unlitProgram && gl.IsEnabled(EnableCap.DepthTest));
    graphics.EndDraw();
    Console.WriteLine("PASS: DrawLine2D depth override keeps the line unlit and restores depth testing");
    var deformation = Content.LoadShaderProgram("Deform", "Unlit");
    Check(ReferenceEquals(deformation, Content.LoadShaderProgram("Deform", "Unlit")));
    Throws<FileNotFoundException>(() => Content.LoadShaderProgram("MissingVertex", "Unlit"));
    var settings = new ShaderParameters();
    Throws<ArgumentException>(() => settings.Set("uModel", Matrix4x4.Identity));
    settings.Set("uAmplitude", 0f);
    settings.Set("uSpeed", 0f);
    settings.Set("uFrequency", 3f);
    settings.Set("unusedOptionalParameter", 5f);
    var effectWorld = new World();
    var effectObject = effectWorld.Spawn<ProjectShockGL.Core.Object>();
    effectObject.Transform.Position = new Vector3(0, 0, -3);
    var effectMesh = effectObject.AddComponent<ProjectShockGL.Core.MeshComponent>();
    effectMesh.Mesh = Content.LoadMesh("Cube");
    effectMesh.Texture = Content.GetWhitePixel();
    effectMesh.Shader = deformation;
    effectMesh.ShaderParameters.Set("uAmplitude", 0f);
    effectMesh.ShaderParameters.Set("uSpeed", 0f);
    effectMesh.ShaderParameters.Set("uFrequency", 3f);
    effectWorld.Update(0, simulate: false);
    Camera.Active.Orthographic = true;
    Camera.Active.OrthoSize = 4;
    float ReadFloat(string name)
    {
        unsafe
        {
            float result;
            gl.GetUniform(deformation.Handle, gl.GetUniformLocation(deformation.Handle, name), &result);
            return result;
        }
    }
    byte[] DrawEffect(float amplitude)
    {
        effectMesh.ShaderParameters.Set("uAmplitude", amplitude);
        var queue = new RenderQueue();
        graphics.BeginDraw(Camera.Active);
        effectWorld.Render(queue);
        // A queued draw owns a snapshot, even if the component changes before Flush.
        effectMesh.ShaderParameters.Set("uAmplitude", 9f);
        queue.Flush(graphics);
        Near(ReadFloat("uAmplitude"), amplitude);
        gl.Finish();
        var image = new byte[640 * 240 * 4];
        unsafe { fixed (byte* pixels = image) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, pixels); }
        Check(gl.GetError() == GLEnum.NoError);
        return image;
    }
    var straightImage = DrawEffect(0f);
    var deformedImage = DrawEffect(0.6f);
    var changedPixels = 0;
    for (var i = 0; i < straightImage.Length; i += 4)
        if (straightImage[i] != deformedImage[i]) changedPixels++;
    Check(changedPixels > 200);
    // Draw a second object with independent values, then omit the amplitude entirely.
    graphics.DrawMesh(effectMesh.Mesh!, effectObject.Transform.Position, Quaternion.Identity, Vector3.One,
        effectMesh.Texture!, Color.White, shader: deformation, shaderParameters: settings);
    Near(ReadFloat("uAmplitude"), 0);
    graphics.DrawMesh(effectMesh.Mesh!, effectObject.Transform.Position, Quaternion.Identity, Vector3.One,
        effectMesh.Texture!, Color.White, shader: deformation);
    Near(ReadFloat("uAmplitude"), 0.2f); // GLSL initializer, not the previous object's value.
    var wrongType = new ShaderParameters();
    wrongType.Set("uAmplitude", new Vector4(1));
    Throws<ArgumentException>(() => graphics.DrawMesh(effectMesh.Mesh!, effectObject.Transform.Position,
        Quaternion.Identity, Vector3.One, effectMesh.Texture!, Color.White,
        shader: deformation, shaderParameters: wrongType));
    // Both vertex/fragment combinations must link and draw, including the standard lighting uniforms.
    var litDeformation = Content.LoadShaderProgram("Deform", "Lit");
    graphics.DrawMesh(effectMesh.Mesh!, effectObject.Transform.Position, Quaternion.Identity, Vector3.One,
        effectMesh.Texture!, Color.White, shader: litDeformation, shaderParameters: settings);
    Check(gl.GetError() == GLEnum.NoError);
    var shaderEnvironment = ProjectShockGL.Environment.Capture();
    try
    {
        ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData { AmbientColor = Color.Black, IsFogEnabled = false });
        byte[] CustomLitPixel()
        {
            graphics.BeginDraw(Camera.Active);
            graphics.DrawMesh(effectMesh.Mesh!, effectObject.Transform.Position, Quaternion.Identity, Vector3.One,
                effectMesh.Texture!, Color.White, shader: litDeformation, shaderParameters: settings);
            gl.Finish();
            var pixel = new byte[4];
            unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            return pixel;
        }
        Check(CustomLitPixel()[0] < 10);
        preview.Unlit = true;
        Check(CustomLitPixel()[0] > 240);
        preview.Unlit = false;
        preview.EnvironmentEnabled = false;
        Check(CustomLitPixel()[0] > 240);
        Check(gl.GetError() == GLEnum.NoError);
    }
    finally
    {
        preview.Unlit = false;
        preview.EnvironmentEnabled = true;
        ProjectShockGL.Environment.Apply(shaderEnvironment);
    }
    Console.WriteLine("PASS: Custom Lit shader respects editor Unlit and Environment overrides");
    effectWorld.Clear();
    Check(deformation.Handle != 0 && gl.IsProgram(deformation.Handle));
    Camera.Active.Orthographic = false;
    Console.WriteLine("PASS: Custom vertex deformation changes pixels, isolates parameters, snapshots queued draws and supports Lit/Unlit");
    var demoWorld = new World();
    var demo = (ProjectDungeon.Placeables.DeformationExample)ProjectShockGL.Core.Entity.Create("DeformationExample", demoWorld)!;
    demo.Amplitude = 0.45f;
    demo.Speed = 1.25f;
    demo.Frequency = 4;
    var placement = new ProjectShockGL.Core.Placeable
    {
        Type = ProjectShockGL.Core.PlaceableType.Entity, Path = demo.Path,
        Properties = EditableProperties.Get(demo.GetType()).ToDictionary(p => p.Name, p => p.GetValue(demo)!)
    };
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(placement, ProjectShockGL.Tools.JsonHelper.GetSettings());
    var loadedPlacement = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectShockGL.Core.Placeable>(json,
        ProjectShockGL.Tools.JsonHelper.GetSettings())!;
    var loadedDemo = (ProjectDungeon.Placeables.DeformationExample)LevelManager.CreateObject(demoWorld, loadedPlacement);
    Check(loadedDemo.Amplitude == 0.45f && loadedDemo.Speed == 1.25f && loadedDemo.Frequency == 4);
    Check(ReferenceEquals(demo.GetComponent<ProjectShockGL.Core.MeshComponent>()!.Shader,
        loadedDemo.GetComponent<ProjectShockGL.Core.MeshComponent>()!.Shader));
    loadedDemo.Amplitude = 0;
    Check(demo.Amplitude == 0.45f);
    demoWorld.Clear();
    Console.WriteLine("PASS: DeformationExample asset creates, serializes editable parameters and restores independent instances");
    using (var vertex = new Shader(gl, ShaderType.VertexShader, """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        uniform mat4 uProjection, uView, uModel;
        uniform mat4 effectTransform = mat4(1.0);
        uniform vec2 effectOffset;
        uniform vec3 effectBias;
        void main() { gl_Position=uProjection*uView*uModel*effectTransform*vec4(aPosition+effectBias+vec3(effectOffset,0),1); }
        """))
    using (var fragment = new Shader(gl, ShaderType.FragmentShader, """
        #version 330 core
        uniform vec4 effectTint;
        uniform bool effectEnabled;
        uniform int effectCount;
        uniform float effectGain;
        out vec4 FragColor;
        void main() { FragColor=effectEnabled ? effectTint*effectGain*float(effectCount) : vec4(0); }
        """))
    using (var parameterProgram = (ShaderProgram)Activator.CreateInstance(typeof(ShaderProgram),
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
        new object[] { gl, vertex, fragment }, null)!)
    {
        var values = new ShaderParameters();
        values.Set("effectTransform", Matrix4x4.CreateScale(0.5f));
        values.Set("effectOffset", new Vector2(0.1f, 0.2f));
        values.Set("effectBias", new Vector3(0.3f, 0.4f, 0.5f));
        values.Set("effectTint", new Color(0.2f, 0.4f, 0.6f, 1));
        values.Set("effectEnabled", true);
        values.Set("effectCount", 2);
        values.Set("effectGain", 0.5f);
        graphics.BeginDraw(Camera.Active);
        graphics.DrawQuad(new Vector3(0, 0, -3), Quaternion.Identity, Vector2.One,
            Content.GetWhitePixel(), Color.White, shader: parameterProgram, shaderParameters: values);
        float[] ReadValues(string name)
        {
            var result = new float[16];
            unsafe { fixed (float* data = result) gl.GetUniform(parameterProgram.Handle, gl.GetUniformLocation(parameterProgram.Handle, name), data); }
            return result;
        }
        Near(ReadValues("effectOffset")[1], 0.2f);
        Near(ReadValues("effectBias")[2], 0.5f);
        Near(ReadValues("effectTint")[2], 0.6f);
        Near(ReadValues("effectCount")[0], 2);
        Near(ReadValues("effectEnabled")[0], 1);
        Near(ReadValues("effectTransform")[0], 0.5f);
        values.Remove("effectTransform");
        values.Clear();
        graphics.DrawBillboard(new Vector3(0, 0, -3), Vector2.One, Content.GetWhitePixel(), Color.White,
            shader: parameterProgram, shaderParameters: values);
        Near(ReadValues("effectTransform")[0], 1); // GLSL mat4 initializer restored.
        Near(ReadValues("effectEnabled")[0], 0);
        Near(ReadValues("effectTint")[2], 0);
        Check(gl.GetError() == GLEnum.NoError);
    }
    Console.WriteLine("PASS: Typed uniforms support vectors, Color, matrix, bool and int, and Clear restores GLSL defaults");
    var lightWorld = new World();
    for (var i = 0; i < 17; i++) lightWorld.Spawn<ProjectShockGL.Core.Light>().SetActive(false);
    var point = lightWorld.Spawn<ProjectShockGL.Core.Light>();
    var directional = lightWorld.Spawn<ProjectShockGL.Core.Light>();
    directional.Type = ProjectShockGL.Core.LightType.Directional;
    var program = (uint)typeof(Renderer).GetField("_litProgram",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(renderer)!;
    int UniformInt(string name)
    {
        unsafe
        {
            var value = 0;
            gl.GetUniform(program, gl.GetUniformLocation(program, name), &value);
            return value;
        }
    }
    graphics.BeginDraw(Camera.Active);
    Check(UniformInt("uPointLightCount") == 1 && UniformInt("uMainLightEnabled") == 1);
    point.SetActive(false);
    directional.SetActive(false);
    graphics.BeginDraw(Camera.Active);
    Check(UniformInt("uPointLightCount") == 0 && UniformInt("uMainLightEnabled") == 0);
    point.SetActive(true);
    directional.SetActive(true);
    graphics.BeginDraw(Camera.Active);
    Check(UniformInt("uPointLightCount") == 1 && UniformInt("uMainLightEnabled") == 1);
    lightWorld.Clear();
    Console.WriteLine("PASS: OpenGL excludes inactive lights before the 16-light limit and restores reenabled lights");
    var savedEnvironment = ProjectShockGL.Environment.Capture();
    try
    {
        ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData
        {
            AmbientColor = Color.Black, IsFogEnabled = true, FogColor = Color.Red, FogNear = 0, FogFar = 1
        });
        byte[] DrawSample(bool uiRoundtrip = false)
        {
            graphics.BeginDraw(Camera.Active);
            if (uiRoundtrip)
            {
                graphics.BeginUI(new Vector2(640, 240));
                graphics.EndUI();
            }
            graphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
                Content.GetWhitePixel(), Color.White, lit: true);
            gl.Finish();
            var pixel = new byte[4];
            unsafe { fixed (byte* p = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, p); }
            Check(gl.GetError() == GLEnum.NoError);
            return pixel;
        }
        var fogged = DrawSample();
        Check(fogged[0] > 240 && fogged[1] < 10 && fogged[2] < 10);
        preview.EnvironmentEnabled = false;
        var neutral = DrawSample(true);
        Check(neutral.Take(3).All(c => c > 240));
        Check(ProjectShockGL.Environment.IsFogEnabled && ProjectShockGL.Environment.AmbientColor.Equals(Color.Black));
        preview.Unlit = true;
        preview.EnvironmentEnabled = true;
        var unlitFog = DrawSample();
        Check(unlitFog[0] > 240 && unlitFog[1] < 10);
        ProjectShockGL.Environment.IsFogEnabled = false;
        var unlit = DrawSample();
        Check(unlit.Take(3).All(c => c > 240));
        preview.Unlit = false;
        var litPixel = DrawSample();
        Check(litPixel.Take(3).All(c => c < 10));
        // A renderer without overrides uses the level even when preview settings have been changed.
        preview.Unlit = true;
        preview.EnvironmentEnabled = false;
        using var gameRenderer = new Renderer(gl, 640, 240);
        var gameGraphics = new Graphics(gameRenderer);
        gameGraphics.BeginDraw(Camera.Active);
        gameGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
            Content.GetWhitePixel(), Color.White, lit: true);
        var gamePixel = new byte[4];
        unsafe { fixed (byte* p = gamePixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, p); }
        Check(gamePixel.Take(3).All(c => c < 10) && gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: OpenGL preview overrides lighting/fog independently, preserves Environment and leaves game rendering unchanged");
    }
    finally
    {
        ProjectShockGL.Environment.Apply(savedEnvironment);
        preview.Unlit = false;
        preview.EnvironmentEnabled = true;
    }
    var spotEnvironment = ProjectShockGL.Environment.Capture();
    var spotWorld = new World();
    try
    {
        ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData { AmbientColor = Color.Black });
        var spot = spotWorld.Spawn<ProjectShockGL.Core.Light>();
        spot.Type = ProjectShockGL.Core.LightType.Spot;
        spot.InnerAngle = 20;
        spot.OuterAngle = 50;
        spot.Radius = 10;
        byte[] SpotPixels()
        {
            graphics.BeginDraw(Camera.Active);
            graphics.DrawQuad(new Vector3(0, 0, -3), Quaternion.Identity, new Vector2(20),
                Content.GetWhitePixel(), Color.White, lit: true);
            gl.Finish();
            var values = new byte[3];
            for (var sample = 0; sample < 3; sample++)
            {
                var pixel = new byte[4];
                unsafe { fixed (byte* p = pixel) gl.ReadPixels(320 + sample * 60, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, p); }
                values[sample] = pixel[0];
            }
            Check(gl.GetError() == GLEnum.NoError);
            return values;
        }
        var cone = SpotPixels();
        Check(cone[0] > cone[1] + 20 && cone[1] > 10 && cone[2] < 3);
        spot.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Check(SpotPixels().All(value => value < 3));
        spot.Transform.Rotation = Quaternion.Identity;
        spot.SetActive(false);
        Check(SpotPixels().All(value => value < 3));
        spot.SetActive(true);
        spot.InnerAngle = spot.OuterAngle;
        Check(SpotPixels()[0] > 100);
        spot.Type = ProjectShockGL.Core.LightType.Point;
        Check(SpotPixels()[2] > 30);
        Console.WriteLine("PASS: Spotlight renders a soft cone, follows rotation/activity and preserves ordinary point lighting");
    }
    finally { spotWorld.Clear(); ProjectShockGL.Environment.Apply(spotEnvironment); }

    // Use a tiny striped BMP (decoded by content, regardless of its .jpg extension).
    var checkerName = Path.Combine(AppContext.BaseDirectory, "brush-checker");
    using (var writer = new BinaryWriter(File.Create(checkerName + ".jpg")))
    {
        writer.Write((ushort)0x4d42); writer.Write(102); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(4); writer.Write(4); writer.Write((ushort)1); writer.Write((ushort)24);
        writer.Write(0); writer.Write(48); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                for (var channel = 0; channel < 3; channel++) writer.Write((byte)(x < 2 ? 255 : 0));
    }
    var brushWorld = new World();
    var brush = brushWorld.Spawn<ProjectShockGL.Core.BrushBox>();
    brush.Size = new Vector3(2, 2, 0.5f);
    brush.Transform.Position = new Vector3(0, 0, -3);
    brush.TextureName = checkerName;
    brush.Lit = false;
    brushWorld.Update(0);
    Camera.Active.Orthographic = true;
    Camera.Active.OrthoSize = 4;
    void CheckBrushPattern()
    {
        graphics.BeginDraw(Camera.Active);
        var queue = new RenderQueue();
        brushWorld.Render(queue);
        queue.Flush(graphics);
        gl.Finish();
        for (var sample = 0; sample < 4; sample++)
        {
            var pixel = new byte[4];
            unsafe { fixed (byte* p = pixel) gl.ReadPixels(275 + sample * 30, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, p); }
            Check(sample % 2 == 0 ? pixel[0] > 240 : pixel[0] < 10);
        }
        Check(gl.GetError() == GLEnum.NoError);
    }
    CheckBrushPattern();
    brush.Transform.Scale = new Vector3(2, 1, 1);
    CheckBrushPattern();
    brush.Transform.Scale = Vector3.One;
    brush.Size = new Vector3(4, 2, 0.5f);
    CheckBrushPattern();
    var brushMesh = (Mesh)typeof(ProjectShockGL.Core.BrushBox).GetField("_mesh",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(brush)!;
    var brushVao = (uint)typeof(Mesh).GetField("_vao",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(brushMesh)!;
    Check(gl.IsVertexArray(brushVao));
    brushWorld.Clear();
    Check(!gl.IsVertexArray(brushVao) && gl.GetError() == GLEnum.NoError);
    Camera.Active.Orthographic = false;
    Console.WriteLine("PASS: BrushBox renders tiled texture after size/scale edits and releases its private GPU mesh");
    var editorProperty = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    var wasEditor = Application.IsEditor;
    var previewWorld = new World();
    editorProperty.SetValue(null, true);
    try
    {
        var missingBrush = previewWorld.Spawn<ProjectShockGL.Core.BrushBox>();
        missingBrush.TextureName = "MissingPreviewTexture";
        previewWorld.Update(0, simulate: false);
        var queue = new RenderQueue();
        graphics.BeginDraw(Camera.Active);
        previewWorld.Render(queue);
        queue.Flush(graphics);
        graphics.EndDraw();
        Check(missingBrush.TextureName == "MissingPreviewTexture" && gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Missing BrushBox texture uses editor fallback without changing its saved material");
    }
    finally
    {
        previewWorld.Clear();
        editorProperty.SetValue(null, wasEditor);
    }
    var canvas = new Canvas();
    canvas.Resize(new Vector2(640, 240));
    var font = Content.LoadFont("NotoSans-Regular", 24)!;
    Check(ReferenceEquals(font, Content.LoadFont("NotoSans-Regular", 24)));
    Check(Content.LoadFont("missing-font") == null);
    var text = canvas.Add<Text>();
    text.Font = font;
    text.Content = "Русский текст: Ёжик, ёлка.\nEnglish text: AV To 0123456789";
    text.Rect.Position = new Vector2(20, 10);
    text.Rect.Size = new Vector2(600, 70);
    text.Color = new Color(0.4f, 1, 0.6f);
    var clipped = canvas.Add<Text>();
    clipped.Font = font;
    clipped.Content = "Обрезка длинной строки / clipped text";
    clipped.WordWrap = false;
    clipped.Rect.Position = new Vector2(20, 100);
    clipped.Rect.Size = new Vector2(180, 18);
    var aligned = canvas.Add<Text>();
    aligned.Font = font;
    aligned.Content = "По центру / Center";
    aligned.Rect.Position = new Vector2(280, 100);
    aligned.Rect.Size = new Vector2(340, 100);
    aligned.HorizontalAlignment = HorizontalAlignment.Center;
    aligned.VerticalAlignment = VerticalAlignment.Center;
    graphics.BeginDraw(Camera.Active);
    graphics.BeginUI(canvas.Size);
    canvas.Draw(graphics);
    // A normal quad after glyphs must reset UVs back to the full texture.
    graphics.DrawUIRect(new Vector2(20, 200), new Vector2(50, 20), Color.Red);
    graphics.EndUI();
    gl.Finish();
    var rgba = new byte[640 * 240 * 4];
    unsafe { fixed (byte* pixels = rgba) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, pixels); }
    Check(gl.GetError() == GLEnum.NoError);
    var lit = 0;
    for (var y = 0; y < 240; y++)
        for (var x = 0; x < 640; x++)
        {
            var index = ((239 - y) * 640 + x) * 4;
            if (y < 80 && rgba[index + 1] > 30) lit++;
            if (x >= 200 && x < 260 && y >= 100 && y < 130)
                Check(rgba[index] == 0 && rgba[index + 1] == 0 && rgba[index + 2] == 0);
        }
    Check(lit > 1000);
    var imagePath = Path.Combine(root, ".build/Text/text-preview.ppm");
    using (var stream = File.Create(imagePath))
    {
        var header = System.Text.Encoding.ASCII.GetBytes("P6\n640 240\n255\n");
        stream.Write(header);
        for (var y = 239; y >= 0; y--)
            for (var x = 0; x < 640; x++) stream.Write(rgba, (y * 640 + x) * 4, 3);
    }
    text.Content = "Другая строка";
    Check(text.MeasureText().X > 0);
    canvas.Destroy();
    Check(!font.IsDisposed);
    Content.UnloadAll();
    Check(font.IsDisposed && deformation.Handle == 0 && litDeformation.Handle == 0);
    Check(gl.GetError() == GLEnum.NoError);
    Console.WriteLine("PASS: Hidden OpenGL rendering, clipping, font cache and lifetime");
}
static void Run(string name, Action test) { test(); Console.WriteLine("PASS: " + name); }
static void Check(bool condition) { if (!condition) throw new Exception("Text check failed"); }
static void Near(float a, float b) => Check(Math.Abs(a - b) < 0.002f);
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
