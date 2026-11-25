using System.Numerics;
using Newtonsoft.Json.Linq;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.UI;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Texture = ProjectShockGL.Rendering.Texture;
using Shader = ProjectShockGL.Rendering.Shader;

var root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".");
var fontPath = Path.Combine(root, "StarterContent/Fonts/NotoSans-Regular.ttf");
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
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "GameSettings.ini"),
        "[Content]\nCustomPath=" + Path.Combine(root, "StarterContent") + "\n");
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
    var skyEnvironment = ProjectShockGL.Environment.Capture();
    var skyCamera = Camera.Active;
    var skyClear = skyCamera.ClearColor;
    using (var otherSkyCamera = Camera.Create())
    {
        try
        {
            void ClearIs(Color expected)
            {
                graphics.BeginDraw(Camera.Active);
                var pixel = new byte[4];
                unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                graphics.EndDraw();
                Check(Math.Abs(pixel[0] - expected.R * 255) < 2 && Math.Abs(pixel[1] - expected.G * 255) < 2 &&
                    Math.Abs(pixel[2] - expected.B * 255) < 2);
            }
            skyCamera.ClearColor = Color.Red;
            otherSkyCamera.ClearColor = Color.Blue;
            ProjectShockGL.Environment.SkyColor = Color.Green;
            ProjectShockGL.Environment.SkyColorEnabled = true;
            ClearIs(Color.Green);
            Camera.ToggleTo(otherSkyCamera);
            ClearIs(Color.Green);
            Check(skyCamera.ClearColor.Equals(Color.Red) && otherSkyCamera.ClearColor.Equals(Color.Blue));
            ProjectShockGL.Environment.SkyColorEnabled = false;
            ClearIs(Color.Blue);
            Camera.ToggleTo(skyCamera);
            ClearIs(Color.Red);
            ProjectShockGL.Environment.SkyColorEnabled = true;
            preview.EnvironmentEnabled = false;
            ClearIs(Color.Red);
            Check(ProjectShockGL.Environment.SkyColorEnabled);
            preview.EnvironmentEnabled = true;
            ClearIs(Color.Green);
            Check(gl.GetError() == GLEnum.NoError);
            Console.WriteLine("PASS: Environment sky color overrides frame clear without mutating cameras; disabled sky/preview uses active camera");
        }
        finally
        {
            Camera.ToggleTo(skyCamera);
            skyCamera.ClearColor = skyClear;
            ProjectShockGL.Environment.Apply(skyEnvironment);
            preview.EnvironmentEnabled = true;
        }
    }
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
    var demo = (ProjectOxygenGL.Placeables.DeformationExample)ProjectShockGL.Core.Entity.Create("Placeables/DeformationExample", demoWorld)!;
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
    var loadedDemo = (ProjectOxygenGL.Placeables.DeformationExample)LevelManager.CreateObject(demoWorld, loadedPlacement);
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
    {
        var layout = LightmapUvMesh.GenerateBrush(Vector3.One, Vector3.One, 2);
        using var mesh = layout.CreateMesh(gl);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var uvBuffer = (uint)typeof(Mesh).GetField("_lightmapVbo", flags)!.GetValue(mesh)!;
        Check(gl.IsBuffer(uvBuffer));
        var gpuUv = new Vector2[layout.LightmapUv.Length];
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, uvBuffer);
        unsafe { fixed (Vector2* data = gpuUv) gl.GetBufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(gpuUv.Length * sizeof(Vector2)), data); }
        Check(gpuUv.SequenceEqual(layout.LightmapUv.ToArray()));
        var regions = new[] { new Vector4(.5f, .5f, 0, 0), new Vector4(.5f, .5f, .5f, 0) };
        using (var instances = mesh.CreateInstances(new[] { Matrix4x4.Identity, Matrix4x4.CreateTranslation(2, 0, 0) }, regions))
        {
            var buffer = (uint)instances.GetType().GetField("_lightmapRegions", flags)!.GetValue(instances)!;
            var copy = new Vector4[2];
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            unsafe { fixed (Vector4* data = copy) gl.GetBufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(2 * sizeof(Vector4)), data); }
            Check(copy.SequenceEqual(regions) && instances.Count == 2);
            instances.Draw();
            Check(gl.GetError() == GLEnum.NoError);
            instances.Dispose();
            Check(!gl.IsBuffer(buffer));
        }
        var batcher = new StaticBatcher();
        try
        {
            MeshCommand Command(int page, Vector4 region) => new()
            {
                Mesh = mesh, Texture = Content.GetWhitePixel(), Color = Color.White, Lit = true,
                Rotation = Quaternion.Identity, Scale = Vector3.One, StaticBatchEligible = true,
                LightmapPage = page, LightmapScaleOffset = region
            };
            var a = Command(0, regions[0]);
            var b = Command(0, regions[1]); b.Position = Vector3.UnitX * 3;
            var queue = new RenderQueue();
            batcher.Submit(new[] { a, b }, queue);
            var combined = (MeshCommand)queue.Commands.Single();
            Check(combined.LightmapPage == 0 && combined.Mesh != mesh && combined.Mesh!.LightmapUv.Length == gpuUv.Length * 2);
            for (var i = 0; i < gpuUv.Length; i++)
            {
                Check(Vector2.Distance(combined.Mesh!.LightmapUv.Span[i], gpuUv[i] * .5f) < .000001f);
                Check(Vector2.Distance(combined.Mesh.LightmapUv.Span[i + gpuUv.Length], gpuUv[i] * .5f + new Vector2(.5f, 0)) < .000001f);
            }
            b.LightmapPage = 1;
            var separate = new RenderQueue();
            batcher.Submit(new[] { a, b }, separate);
            Check(separate.Commands.Count == 2 && combined.Mesh!.IsDisposed);
        }
        finally { batcher.Clear(); }
        mesh.Dispose();
        Check(!gl.IsBuffer(uvBuffer));
        Console.WriteLine("PASS: Independent lightmap UV buffers survive batching and instancing, separate atlas pages and release GPU resources");
    }
    {
        var world = new World();
        var source = Content.LoadMesh("Cube")!;
        var originalVertices = source.Vertices.ToArray();
        var originalIndices = source.Indices.ToArray();
        try
        {
            var geometry = world.Spawn<ProjectShockGL.Core.Geometry>();
            var component = geometry.GetComponent<ProjectShockGL.Core.MeshComponent>()!;
            component.Mesh = source;
            component.Texture = Content.GetWhitePixel();
            component.Lit = true;
            component.Offset = new Vector3(0, 2, 0);
            component.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .2f);
            geometry.Transform.Scale = new Vector3(2, 1, -1);
            for (var i = 0; i < 3; i++)
                world.Details.Add(new ProjectShockGL.Core.DetailPlacement
                {
                    AssetPath = "ExampleCube", Position = new Vector3(i * 3, 0, 0),
                    Scale = new Vector3(1 + i, 1, 1)
                });
            var snapshot = world.PrepareLightmapGeometry(new LightmapPreparationOptions { AtlasSize = 256, SurfaceSize = 128 });
            Check(snapshot.Surfaces.Count == 4 && snapshot.PageCount == 1);
            var group = snapshot.DetailGroups().Single();
            Check(group.Instances.Count == 3 && group.Instances.Select(surface => surface.ScaleOffset).Distinct().Count() == 3);
            Check(snapshot.Surfaces.All(surface => ReferenceEquals(surface.Mesh, group.Mesh)));
            var triangles = snapshot.WorldTriangles();
            Check(triangles.Length == originalIndices.Length / 3 * 4);
            Check(triangles.All(triangle => float.IsFinite(triangle.A.Position.X) && MathF.Abs(triangle.A.Normal.Length() - 1) < .0001f));
            Check(triangles.All(triangle => triangle.A.LightmapUV.X >= 0 && triangle.A.LightmapUV.X <= 1 &&
                triangle.A.LightmapUV.Y >= 0 && triangle.A.LightmapUV.Y <= 1));
            Check(source.Vertices.Span.SequenceEqual(originalVertices) && source.Indices.Span.SequenceEqual(originalIndices));
            var separate = world.PrepareLightmapGeometry(new LightmapPreparationOptions { AtlasSize = 128, SurfaceSize = 128 });
            Check(separate.PageCount == 4 && separate.DetailGroups().Length == 3);
            Check(world.Details.Count == 3 && !source.IsDisposed);
        }
        finally { world.Clear(); }
        Check(!source.IsDisposed);
        Console.WriteLine("PASS: Geometry and Details share an independent UV template, retain stable identities and allocate per-placement atlas regions without mutating cached meshes");
    }
    {
        var ambient = ProjectShockGL.Environment.AmbientColor;
        var world = new World();
        using var alphaTexture = Texture.FromRgba(gl, 2, 1, new byte[] { 255, 255, 255, 0, 255, 255, 255, 255 });
        float[] vertices =
        [
            -2, 0, -2, 0, 1, 0, 0, 0,
             2, 0, -2, 0, 1, 0, 1, 0,
             2, 0,  2, 0, 1, 0, 1, 1,
            -2, 0,  2, 0, 1, 0, 0, 1
        ];
        using var plane = new Mesh(gl, vertices, [0, 2, 1, 0, 3, 2]);
        using var backPlane = new Mesh(gl, vertices, [0, 1, 2, 0, 2, 3]);
        try
        {
            ProjectShockGL.Environment.AmbientColor = Color.Black;
            var geometry = world.Spawn<ProjectShockGL.Core.Geometry>();
            geometry.Transform.Position = new Vector3(0, 2, 0);
            var mesh = geometry.GetComponent<ProjectShockGL.Core.MeshComponent>()!;
            mesh.Mesh = plane; mesh.Texture = alphaTexture; mesh.Lit = true; mesh.SurfaceMode = SurfaceMode.Cutout;
            var bakePoint = world.Spawn<ProjectShockGL.Core.Light>(); bakePoint.Mode = ProjectShockGL.Core.LightMode.Baked;
            bakePoint.Transform.Position = new Vector3(0, 5, 0); bakePoint.Intensity = 2; bakePoint.Radius = 10;
            DirectLightBakeInput Capture() => DirectLightBaker.Capture(world, world.PrepareLightmapGeometry(
                new LightmapPreparationOptions { AtlasSize = 64, SurfaceSize = 32, Padding = 1 }));
            var cutout = Capture();
            Check(DirectLightBaker.Sample(cutout, new Vector3(-2, 0, 0), Vector3.UnitY).X > .5f);
            Check(DirectLightBaker.Sample(cutout, new Vector3(2, 0, 0), Vector3.UnitY).X == 0);
            mesh.SurfaceMode = SurfaceMode.Opaque;
            Check(DirectLightBaker.Sample(Capture(), new Vector3(-2, 0, 0), Vector3.UnitY).X == 0);
            mesh.Mesh = backPlane;
            Check(DirectLightBaker.Sample(Capture(), Vector3.Zero, Vector3.UnitY).X > 0);
            mesh.BackfaceCulling = false;
            Check(DirectLightBaker.Sample(Capture(), Vector3.Zero, Vector3.UnitY).X == 0);
            world.Clear();
            alphaTexture.Dispose(); plane.Dispose(); backPlane.Dispose();
            var capturedResult = Task.Run(() => DirectLightBaker.Bake(cutout)).GetAwaiter().GetResult();
            Check(capturedResult.Pages.Count == 1 && capturedResult.Pages[0].Coverage.Span.Contains((byte)1));
            Check(DirectLightBaker.Sample(cutout, new Vector3(2, 0, 0), Vector3.UnitY).X == 0);
            Check(DirectLightBaker.Sample(cutout, new Vector3(-2, 0, 0), Vector3.UnitY).X > .5f);

            world.Details.Add(new ProjectShockGL.Core.DetailPlacement
            {
                AssetPath = "ExampleCube", Position = new Vector3(0, 2, 0), Scale = new Vector3(3, .2f, 3)
            });
            var light = world.Spawn<ProjectShockGL.Core.Light>(); light.Mode = ProjectShockGL.Core.LightMode.Baked;
            light.Transform.Position = new Vector3(0, 5, 0); light.Radius = 10;
            var details = Capture();
            Check(DirectLightBaker.Sample(details, Vector3.Zero, Vector3.UnitY).X == 0);
            world.Details.Clear();
            Check(DirectLightBaker.Sample(Capture(), Vector3.Zero, Vector3.UnitY).X > 0);
            Check(gl.GetError() == GLEnum.NoError);
            Console.WriteLine("PASS: Baked shadows include Geometry and Details, respect Cutout holes and sidedness, and bake on a worker after GPU resources are disposed");
        }
        finally { world.Clear(); ProjectShockGL.Environment.AmbientColor = ambient; }
    }
    {
        var giWorld=new World();
        var giEnvironment=ProjectShockGL.Environment.Capture();
        using var giTexture=Texture.FromRgba(gl,1,1,new byte[]{255,0,0,255});
        using var giHole=Texture.FromRgba(gl,1,1,new byte[]{255,255,255,0});
        float[] giVertices=
        [
            -4,0,-4,0,1,0,0,0,
             4,0,-4,0,1,0,1,0,
             4,0, 4,0,1,0,1,1,
            -4,0, 4,0,1,0,0,1
        ];
        using var giMesh=new Mesh(gl,giVertices,[0,2,1,0,3,2]);
        string? giAsset=null;
        try
        {
            ProjectShockGL.Environment.AmbientColor=Color.Black;
            var giFloor=giWorld.Spawn<ProjectShockGL.Core.Geometry>();
            var giComponent=giFloor.GetComponent<ProjectShockGL.Core.MeshComponent>()!;
            giComponent.Mesh=giMesh;giComponent.Texture=giTexture;giComponent.Lit=true;
            giComponent.SurfaceMode=SurfaceMode.Opaque;
            var giLamp=giWorld.Spawn<ProjectShockGL.Core.Light>();
            giLamp.Mode=ProjectShockGL.Core.LightMode.Baked;giLamp.Type=ProjectShockGL.Core.LightType.Spot;
            giLamp.Transform.Position=new Vector3(0,1.5f,0);
            giLamp.Transform.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-MathF.PI/2);
            giLamp.InnerAngle=90;giLamp.OuterAngle=120;giLamp.Intensity=2;giLamp.Radius=10;
            var giDynamic=giWorld.Spawn<ProjectShockGL.Core.Entity>();
            var giDynamicMesh=giDynamic.AddComponent<ProjectShockGL.Core.MeshComponent>();
            giDynamicMesh.Mesh=giMesh;giDynamicMesh.Texture=Content.GetWhitePixel();giDynamicMesh.Lit=true;
            DirectLightBakeInput CaptureGI()=>DirectLightBaker.Capture(giWorld,giWorld.PrepareLightmapGeometry(
                new LightmapPreparationOptions{AtlasSize=64,SurfaceSize=32,Padding=1}));
            DirectLightBakeInput giInput;
            var giPack=gl.GetInteger(GetPName.PackAlignment);
            var giRow=gl.GetInteger(GetPName.PackRowLength);
            var giSkipPixels=gl.GetInteger(GetPName.PackSkipPixels);
            var giSkipRows=gl.GetInteger(GetPName.PackSkipRows);
            var giBoundTexture=gl.GetInteger(GetPName.TextureBinding2D);
            var giOldBuffer=gl.GetInteger(GetPName.PixelPackBufferBinding);
            var giBuffer=gl.GenBuffer();
            try
            {
                gl.BindBuffer(BufferTargetARB.PixelPackBuffer,giBuffer);
                gl.BufferData<byte>(BufferTargetARB.PixelPackBuffer,16,null,BufferUsageARB.StaticRead);
                gl.PixelStore(PixelStoreParameter.PackAlignment,8);
                gl.PixelStore(PixelStoreParameter.PackRowLength,11);
                gl.PixelStore(PixelStoreParameter.PackSkipPixels,3);
                gl.PixelStore(PixelStoreParameter.PackSkipRows,2);
                giInput=CaptureGI();
                Check(gl.GetInteger(GetPName.PixelPackBufferBinding)==giBuffer);
                Check(gl.GetInteger(GetPName.PackAlignment)==8 && gl.GetInteger(GetPName.PackRowLength)==11);
                Check(gl.GetInteger(GetPName.PackSkipPixels)==3 && gl.GetInteger(GetPName.PackSkipRows)==2);
                Check(gl.GetInteger(GetPName.TextureBinding2D)==giBoundTexture);
            }
            finally
            {
                gl.BindBuffer(BufferTargetARB.PixelPackBuffer,(uint)giOldBuffer);gl.DeleteBuffer(giBuffer);
                gl.PixelStore(PixelStoreParameter.PackAlignment,giPack);
                gl.PixelStore(PixelStoreParameter.PackRowLength,giRow);
                gl.PixelStore(PixelStoreParameter.PackSkipPixels,giSkipPixels);
                gl.PixelStore(PixelStoreParameter.PackSkipRows,giSkipRows);
                gl.BindTexture(TextureTarget.Texture2D,(uint)giBoundTexture);
            }
            var giOptions=new DirectLightBakeOptions{IndirectSamples=128};
            var giReceiver=new Vector3(0,2,0);
            var giRed=DirectLightBaker.SampleWithIndirect(giInput,giReceiver,-Vector3.UnitY,giOptions);
            Check(giRed.X>.1f && giRed.Y<.0001f && giRed.Z<.0001f);
            giComponent.Color=Color.Green;
            Check(DirectLightBaker.SampleWithIndirect(CaptureGI(),giReceiver,-Vector3.UnitY,giOptions).Length()<.0001f);
            giComponent.Color=Color.White;
            var giBarrier=giWorld.Spawn<ProjectShockGL.Core.Geometry>();
            giBarrier.Transform.Position=new Vector3(0,1,0);
            var giBarrierMesh=giBarrier.GetComponent<ProjectShockGL.Core.MeshComponent>()!;
            giBarrierMesh.Mesh=giMesh;giBarrierMesh.Texture=giHole;giBarrierMesh.SurfaceMode=SurfaceMode.Cutout;
            Check(DirectLightBaker.SampleWithIndirect(CaptureGI(),giReceiver,-Vector3.UnitY,giOptions).X>.1f);
            giBarrierMesh.SurfaceMode=SurfaceMode.Opaque;
            Check(DirectLightBaker.SampleWithIndirect(CaptureGI(),giReceiver,-Vector3.UnitY,giOptions).Length()<.0001f);
            giWorld.Destroy(giBarrier);
            var giBake=DirectLightBaker.Bake(giInput,new DirectLightBakeOptions{IndirectSamples=32});
            giAsset=Content.SaveLightmap("Checks/GITexture",giBake);
            giWorld.ApplyLightmap(giAsset);
            giWorld.Update(0,simulate:false);
            var giQueue=new RenderQueue();giWorld.Render(giQueue,false);
            var giCommands=giQueue.Commands.OfType<MeshCommand>().ToArray();
            Check(giCommands.Single(command=>command.LightmapSurface.ObjectId==giFloor.Guid).LightmapTexture!=null);
            Check(giCommands.Single(command=>command.LightmapSurface.ObjectId==giDynamic.Guid).LightmapTexture==null);
            giWorld.Clear();giTexture.Dispose();giHole.Dispose();giMesh.Dispose();
            var giAfter=Task.Run(()=>DirectLightBaker.SampleWithIndirect(giInput,giReceiver,-Vector3.UnitY,giOptions)).GetAwaiter().GetResult();
            Check(giAfter==giRed && gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: GI samples texture RGB and tint, respects Cutout, restores GL pack state, survives disposal, and leaves dynamic objects without lightmap");
        }
        finally
        {
            giWorld.Clear();ProjectShockGL.Environment.Apply(giEnvironment);
            if(giAsset!=null)File.Delete(LightmapStorage.GetPath(giAsset));
        }
    }
    {
        var dynamicEnvironment=ProjectShockGL.Environment.Capture();
        var dynamicWorld=new World();
        using var ambientCamera=Camera.Create();
        ambientCamera.Orthographic=true;ambientCamera.OrthoSize=3;ambientCamera.AspectRatio=640f/240;
        try
        {
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData
            {
                AmbientColor=new Color(.2f,0,0),DynamicAmbientColorEnabled=true,
                DynamicAmbientColor=new Color(0,.4f,0),IsFogEnabled=false
            });
            graphics.BeginDraw(ambientCamera);
            graphics.DrawQuad(new Vector3(0,0,-2),Quaternion.Identity,Vector2.One,Content.GetWhitePixel(),Color.White,lit:true,dynamicAmbient:true);
            byte[] AmbientPixel()
            {
                var pixel=new byte[4];
                unsafe {fixed(byte* data=pixel)gl.ReadPixels(320,120,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,data);}
                return pixel;
            }
            var dynamicPixel=AmbientPixel();
            Check(dynamicPixel[0]<3 && Math.Abs(dynamicPixel[1]-102)<=2 && dynamicPixel[2]<3);
            graphics.DrawQuad(new Vector3(0,0,-1),Quaternion.Identity,Vector2.One,Content.GetWhitePixel(),Color.White,lit:true);
            var staticPixel=AmbientPixel();
            Check(Math.Abs(staticPixel[0]-51)<=2 && staticPixel[1]<3);
            ProjectShockGL.Environment.DynamicAmbientColorEnabled=false;
            graphics.BeginDraw(ambientCamera);
            graphics.DrawQuad(new Vector3(0,0,-2),Quaternion.Identity,Vector2.One,Content.GetWhitePixel(),Color.White,lit:true,dynamicAmbient:true);
            Check(Math.Abs(AmbientPixel()[0]-51)<=2 && AmbientPixel()[1]<3);
            ProjectShockGL.Environment.DynamicAmbientColorEnabled=true;
            var pbr=Content.LoadShaderProgram("Lit","SimplePBR");
            graphics.BeginDraw(ambientCamera);
            graphics.DrawQuad(new Vector3(0,0,-2),Quaternion.Identity,Vector2.One,Content.GetWhitePixel(),Color.White,lit:true,shader:pbr,dynamicAmbient:true);
            var ambientUniform=new float[3];
            unsafe {fixed(float* data=ambientUniform)gl.GetUniform(pbr.Handle,gl.GetUniformLocation(pbr.Handle,"uAmbientColor"),data);}
            Check(MathF.Abs(ambientUniform[0])<.0001f && MathF.Abs(ambientUniform[1]-.4f)<.0001f);
            var dynamicActor=dynamicWorld.Spawn<ProjectShockGL.Core.Entity>();
            var mesh=dynamicActor.AddComponent<ProjectShockGL.Core.MeshComponent>();
            mesh.Mesh=Content.LoadMesh("Cube");mesh.Texture=Content.GetWhitePixel();mesh.Lit=true;
            dynamicActor.AddComponent<ProjectShockGL.Core.QuadComponent>();
            dynamicActor.AddComponent<ProjectShockGL.Core.BillboardComponent>();
            var actorCommands=new RenderQueue();dynamicActor.Render(actorCommands);
            Check(actorCommands.Commands.Count==3 && actorCommands.Commands.All(command=>command.UseDynamicAmbient));
            var staticGeometry=dynamicWorld.Spawn<ProjectShockGL.Core.Geometry>();
            staticGeometry.GetComponent<ProjectShockGL.Core.MeshComponent>()!.Mesh=mesh.Mesh;
            var staticCommands=new RenderQueue();staticGeometry.Render(staticCommands);
            Check(staticCommands.Commands.Count==1 && !staticCommands.Commands[0].UseDynamicAmbient);
            var movingBrush=dynamicWorld.Spawn<ProjectShockGL.Core.BrushBox>();movingBrush.Movable=true;
            var movingCommands=new RenderQueue();movingBrush.Render(movingCommands);
            Check(movingCommands.Commands.Count>0 && movingCommands.Commands.All(command=>command.UseDynamicAmbient));
            movingBrush.Movable=false;
            var fixedCommands=new RenderQueue();movingBrush.Render(fixedCommands);
            Check(fixedCommands.Commands.All(command=>!command.UseDynamicAmbient));
            Check(gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Dynamic Ambient affects only movable owners, resets per draw, supports custom Lit/PBR and preserves old-level fallback");
        }
        finally {dynamicWorld.Clear();ProjectShockGL.Environment.Apply(dynamicEnvironment);}
    }
    {
        var world=new World();using var camera=Camera.Create();
        var zone=world.Spawn<ProjectShockGL.Placeables.AudioReverbZone>();
        zone.Transform.Position=new Vector3(0,0,-4);zone.Transform.Scale=new Vector3(2,1,1);
        zone.Transform.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f);zone.BoxSize=new Vector3(2);
        try
        {
            graphics.BeginDraw(camera);zone.DebugRenderSelected(new DebugDraw(graphics));
            Check(graphics.Statistics.DrawCalls==12);graphics.EndDraw();
            zone.Shape=ProjectShockGL.Placeables.ReverbZoneShape.Sphere;zone.Radius=1;
            graphics.BeginDraw(camera);zone.DebugRenderSelected(new DebugDraw(graphics));
            Check(graphics.Statistics.DrawCalls==96);graphics.EndDraw();
            Check(gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Selected reverb zones draw rotated/scaled Box and Sphere wire shapes without GPU errors");
        }
        finally {world.Clear();}
    }
    {
        var particleEnvironment = ProjectShockGL.Environment.Capture();
        var particleWorld = new World();
        var emitter = particleWorld.Spawn<ProjectShockGL.Core.Object>();
        emitter.Transform.Position = new Vector3(0, 0, -3);
        var particles = emitter.AddComponent<ProjectShockGL.Core.ParticleSystemComponent>();
        var particleAssetName = "Checks/GpuParticles-" + Guid.NewGuid().ToString("N");
        Content.SaveParticle(particleAssetName, new ProjectShockGL.Core.ParticleAsset
        {
            Texture = "WhitePixel", Shape = ProjectShockGL.Core.ParticleShape.Box,
            BoxSize = Vector3.Zero, Velocity = Vector3.Zero, Size = 1, Rate = 10
        });
        particles.Apply(Content.LoadParticle(particleAssetName)!);
        var capturedParticleAsset = ProjectShockGL.Core.ParticleAsset.Capture(particles);
        Check(capturedParticleAsset.Texture == "WhitePixel" && capturedParticleAsset.Shape == ProjectShockGL.Core.ParticleShape.Box);
        var previousParticleTexture = particles.Texture;
        try
        {
            particles.Apply(new ProjectShockGL.Core.ParticleAsset { Texture = "Checks/MissingParticleTexture", Rate = 123 });
            throw new Exception("Missing particle texture was accepted.");
        }
        catch (FileNotFoundException) { }
        Check(particles.Rate == 10 && particles.Texture == previousParticleTexture);
        particleWorld.Update(1);
        var particleCamera = Camera.Create();
        var queue = new RenderQueue();
        try
        {
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData { AmbientColor = Color.Black, IsFogEnabled = false });
            byte[] ParticlePixel(bool occluder = false)
            {
                graphics.BeginDraw(particleCamera);
                if (occluder) graphics.DrawQuad(new Vector3(0,0,-2), Quaternion.Identity, new Vector2(3), Content.GetWhitePixel(), Color.Red, surfaceMode: SurfaceMode.Opaque);
                particleWorld.Render(queue);
                queue.Flush(graphics);
                gl.Finish();
                var pixel = new byte[4];
                unsafe { fixed (byte* ptr = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, ptr); }
                graphics.EndDraw();
                return pixel;
            }
            var plain = ParticlePixel();
            Check(plain[0] > 240 && plain[1] > 240 && graphics.Statistics.DrawCalls == 1);
            Check(particles.ParticleCount == 10);
            var covered = ParticlePixel(occluder: true);
            Check(covered[0] > 240 && covered[1] < 10);
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData { IsFogEnabled = true, FogNear = 0, FogFar = 1, FogColor = Color.Blue });
            var fogged = ParticlePixel();
            Check(fogged[0] < 10 && fogged[2] > 240);
            ProjectShockGL.Environment.IsFogEnabled = false;
            particleCamera.Transform.Position = new Vector3(3, 0, -3);
            particleCamera.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
            var sideways = ParticlePixel();
            Check(sideways[0] > 240 && sideways[1] > 240);
            var particleMesh = (Mesh)typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_mesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(particles)!;
            particles.Clear();
            ParticlePixel();
            Check(graphics.Statistics.DrawCalls == 0);
            particleWorld.Clear();
            Check(particleMesh.IsDisposed && particles.IsDestroyed);
            Check(gl.GetError() == GLEnum.NoError);
            Console.WriteLine("PASS: Particle billboards batch into one Unlit draw, receive fog, respect scene depth, face each camera and release GPU meshes");
        }
        finally
        {
            particleWorld.Clear();
            particleCamera.Dispose();
            File.Delete(Path.Combine(Content.Path, "Particles", particleAssetName + ".json"));
            ProjectShockGL.Environment.Apply(particleEnvironment);
        }
    }
    {
        var environment=ProjectShockGL.Environment.Capture();
        var world=new World();
        using var front=Camera.Create();
        using var back=Camera.Create();
        using var side=Camera.Create();
        front.AspectRatio=1;front.Near=.1f;front.Far=20;
        back.AspectRatio=1;back.Transform.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);
        side.Orthographic=true;side.OrthoSize=4;side.AspectRatio=1;
        side.Transform.Position=new Vector3(100,0,0);
        var particles=world.Spawn<ProjectShockGL.Core.Object>().AddComponent<ProjectShockGL.Core.ParticleSystemComponent>();
        particles.Texture=Content.GetWhitePixel();particles.Rate=3;particles.Lifetime=100;
        particles.Velocity=Vector3.Zero;particles.Size=.5f;
        world.Update(1);particles.Stop();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var list=(System.Collections.IList)typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_particles",flags)!.GetValue(particles)!;
        void Set(int index,string field,object value)
        {
            var particle=list[index]!;particle.GetType().GetField(field)!.SetValue(particle,value);list[index]=particle;
        }
        float Age(int index) {var particle=list[index]!;return (float)particle.GetType().GetField("Age")!.GetValue(particle)!;}
        Mesh Mesh()=>(Mesh)typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_mesh",flags)!.GetValue(particles)!;
        var queue=new RenderQueue();
        void Frame(Camera camera,int expected)
        {
            graphics.BeginDraw(camera);world.Render(queue);queue.Flush(graphics);gl.Finish();
            Check(graphics.Statistics.DrawCalls==(expected==0?0:1));
            Check(Mesh().DrawIndexCount==(uint)(expected*6));graphics.EndDraw();
        }
        try
        {
            ProjectShockGL.Environment.IsFogEnabled=false;
            Set(0,"Position",new Vector3(0,0,-3));
            Set(1,"Position",new Vector3(100,0,-3));
            Set(2,"Position",new Vector3(0,0,3));
            Frame(front,1);Frame(back,1);Frame(side,1);Frame(front,1);
            front.Transform.Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);
            Frame(front,0);
            var age=Age(0);Set(0,"Velocity",new Vector3(.2f,0,0));world.Update(1);
            Check(particles.ParticleCount==3 && Age(0)>age+.99f);
            front.Transform.Rotation=Quaternion.Identity;Frame(front,1);
            // Center lies outside the orthographic view, but a rotated corner remains visible.
            side.Transform.Position=Vector3.Zero;
            Set(1,"Position",new Vector3(1000,0,-3));Set(2,"Position",new Vector3(1000,0,3));
            Set(0,"Position",new Vector3(2.8f,0,-3));Set(0,"StartSize",2f);Set(0,"Rotation",45f);
            Frame(side,1);
            Set(0,"Position",new Vector3(4,0,-3));Frame(side,0);
            // Lifetime scaling must be included in the bound used for visibility.
            particles.SizeOverLifetime=true;particles.SizeCurve=[new(0,3),new(1,3)];Frame(side,1);
            particles.SizeOverLifetime=false;
            Set(0,"Position",new Vector3(0,0,-30));Frame(front,0);
            Set(0,"Position",new Vector3(0,0,-3));Frame(front,1);
            Check(particles.ParticleCount==3 && gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Particle frustum culling filters per perspective/orthographic camera, retains rotated edge quads and lifetime sizes, skips empty draws and keeps offscreen simulation alive");
        }
        finally {world.Clear();ProjectShockGL.Environment.Apply(environment);}
    }
    {
        var appearanceEnvironment=ProjectShockGL.Environment.Capture();
        var appearanceWorld=new World();
        using var appearanceCamera=Camera.Create();
        appearanceCamera.Orthographic=true;
        appearanceCamera.OrthoSize=4;
        var appearanceOwner=appearanceWorld.Spawn<ProjectShockGL.Core.Object>();
        var appearanceParticles=appearanceOwner.AddComponent<ProjectShockGL.Core.ParticleSystemComponent>();
        appearanceParticles.Texture=Content.GetWhitePixel();
        appearanceParticles.Rate=2;
        appearanceParticles.Velocity=Vector3.Zero;
        appearanceParticles.StartSize=new ProjectShockGL.Core.ParticleRange(.5f,.5f);
        appearanceParticles.StartLifetime=new ProjectShockGL.Core.ParticleRange(4,4);
        appearanceParticles.StartRotation=new ProjectShockGL.Core.ParticleRange(0,0);
        appearanceParticles.RotationSpeed=new ProjectShockGL.Core.ParticleRange(90,90);
        appearanceParticles.SizeOverLifetime=true;
        appearanceParticles.SizeCurve=[new(0,1),new(1,2)];
        appearanceParticles.ColorOverLifetime=true;
        appearanceParticles.ColorGradient=[new(0,Color.Red),new(1,Color.Blue)];
        appearanceWorld.Update(1);
        appearanceParticles.Stop();
        var instanceFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var particleList=(System.Collections.IList)typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_particles",instanceFlags)!.GetValue(appearanceParticles)!;
        for(var index=0;index<2;index++)
        {
            var particle=particleList[index]!;
            particle.GetType().GetField("Age")!.SetValue(particle,index==0?1f:3f);
            particle.GetType().GetField("Position")!.SetValue(particle,new Vector3(index==0?-1:1,0,-3));
            particleList[index]=particle;
        }
        try
        {
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData {IsFogEnabled=false});
            graphics.BeginDraw(appearanceCamera);
            var appearanceQueue=new RenderQueue();
            appearanceWorld.Render(appearanceQueue);
            appearanceQueue.Flush(graphics);
            gl.Finish();
            byte[] Pixel(int x)
            {
                var pixel=new byte[4];
                unsafe {fixed(byte* data=pixel)gl.ReadPixels(x,120,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,data);}
                return pixel;
            }
            var red=Pixel(260);var blue=Pixel(380);
            Check(Math.Abs(red[0]-191)<4 && Math.Abs(red[2]-64)<4);
            Check(Math.Abs(blue[0]-64)<4 && Math.Abs(blue[2]-191)<4);
            Check(graphics.Statistics.DrawCalls==1);
            var vertices=(float[])typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_vertices",instanceFlags)!.GetValue(appearanceParticles)!;
            // Age=1: size=.5*1.25=.625, rotation=90 degrees.
            Check(Math.Abs(vertices[0]-(-1+.3125f))<.001f && Math.Abs(vertices[1]+.3125f)<.001f);
            graphics.EndDraw();
            Check(gl.GetError()==GLEnum.NoError);
            var mesh=(Mesh)typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_mesh",instanceFlags)!.GetValue(appearanceParticles)!;
            var sortBuffer=typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_sortBuffer",instanceFlags)!.GetValue(appearanceParticles);
            var colorBuffer=typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_colors",instanceFlags)!.GetValue(appearanceParticles);
            var revision=mesh.Revision;
            var vbo=(uint)typeof(Mesh).GetField("_vbo",instanceFlags)!.GetValue(mesh)!;
            particleList.RemoveAt(1);
            graphics.BeginDraw(appearanceCamera);
            appearanceWorld.Render(appearanceQueue);appearanceQueue.Flush(graphics);gl.Finish();
            Check(mesh.DrawIndexCount==6 && mesh.TriangleCount==2 && graphics.Statistics.DrawCalls==1);
            Check(ReferenceEquals(sortBuffer,typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_sortBuffer",instanceFlags)!.GetValue(appearanceParticles)));
            Check(ReferenceEquals(vertices,typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_vertices",instanceFlags)!.GetValue(appearanceParticles)));
            Check(ReferenceEquals(colorBuffer,typeof(ProjectShockGL.Core.ParticleSystemComponent).GetField("_colors",instanceFlags)!.GetValue(appearanceParticles)));
            Check(mesh.Revision==revision);
            var uploaded=new float[32];gl.BindBuffer(BufferTargetARB.ArrayBuffer,vbo);
            unsafe {fixed(float* data=uploaded)gl.GetBufferSubData(BufferTargetARB.ArrayBuffer,0,(nuint)(32*sizeof(float)),data);}
            Check(uploaded.AsSpan().SequenceEqual(vertices.AsSpan(0,32)));
            Check(mesh.Vertices.Span[0]==0); // Transient uploads leave the unused CPU mesh copy untouched.
            graphics.EndDraw();Check(gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Particle gradients and rotation remain correct; live-prefix GPU uploads reuse buffers and shrink draw counts without CPU mesh rebuilds");
        }
        finally
        {
            appearanceWorld.Clear();
            ProjectShockGL.Environment.Apply(appearanceEnvironment);
        }
    }
    {
        var blendEnvironment=ProjectShockGL.Environment.Capture();
        var blendWorld=new World();
        using var blendCamera=Camera.Create();
        blendCamera.ClearColor=new Color(.4f,.5f,.6f,1);
        var blendOwner=blendWorld.Spawn<ProjectShockGL.Core.Object>();
        blendOwner.Transform.Position=new Vector3(0,0,-3);
        var blendParticles=blendOwner.AddComponent<ProjectShockGL.Core.ParticleSystemComponent>();
        blendParticles.Texture=Content.GetWhitePixel();
        blendParticles.Velocity=Vector3.Zero;
        blendParticles.Rate=1;
        blendParticles.Size=1;
        byte[] BlendPixel(ParticleBlendMode mode,Color color,bool fog=false)
        {
            blendParticles.Clear();blendParticles.Play();blendParticles.Color=color;blendParticles.BlendMode=mode;
            blendWorld.Update(1);
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData{IsFogEnabled=fog,FogNear=0,FogFar=1,FogColor=Color.Red});
            gl.DepthMask(true);
            graphics.BeginDraw(blendCamera);
            gl.Enable(EnableCap.Blend);
            gl.BlendFuncSeparate(BlendingFactor.One,BlendingFactor.Zero,BlendingFactor.One,BlendingFactor.Zero);
            gl.DepthMask(false);
            var queue=new RenderQueue();blendWorld.Render(queue);queue.Flush(graphics);
            Check(gl.IsEnabled(EnableCap.Blend) && !gl.GetBoolean(GetPName.DepthWritemask));
            Check(gl.GetInteger(GetPName.BlendSrcRgb)==(int)BlendingFactor.One && gl.GetInteger(GetPName.BlendDstRgb)==(int)BlendingFactor.Zero);
            Check(graphics.Statistics.DrawCalls==1);
            var pixel=new byte[4];
            unsafe{fixed(byte* data=pixel)gl.ReadPixels(320,120,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,data);}
            graphics.EndDraw();gl.DepthMask(true);
            return pixel;
        }
        void Matches(byte[] pixel,Vector3 expected)
        {
            Check(Math.Abs(pixel[0]-expected.X*255)<4 && Math.Abs(pixel[1]-expected.Y*255)<4 && Math.Abs(pixel[2]-expected.Z*255)<4);
        }
        try
        {
            var color=new Color(.2f,.4f,.6f,.5f);
            Matches(BlendPixel(ParticleBlendMode.Alpha,color),new Vector3(.3f,.45f,.6f));
            Matches(BlendPixel(ParticleBlendMode.Additive,color),new Vector3(.5f,.7f,.9f));
            Matches(BlendPixel(ParticleBlendMode.Multiply,color),new Vector3(.24f,.35f,.48f));
            Matches(BlendPixel(ParticleBlendMode.Additive,Color.Black),new Vector3(.4f,.5f,.6f));
            Matches(BlendPixel(ParticleBlendMode.Multiply,Color.White),new Vector3(.4f,.5f,.6f));
            Matches(BlendPixel(ParticleBlendMode.Multiply,new Color(0,0,0,0)),new Vector3(.4f,.5f,.6f));
            Matches(BlendPixel(ParticleBlendMode.Multiply,Color.Black),Vector3.Zero);
            Matches(BlendPixel(ParticleBlendMode.Additive,Color.White,true),new Vector3(.4f,.5f,.6f));
            Matches(BlendPixel(ParticleBlendMode.Multiply,Color.Black,true),new Vector3(.4f,.5f,.6f));
            foreach(var mode in Enum.GetValues<ParticleBlendMode>())
            {
                Matches(BlendPixel(mode,new Color(.8f,.6f,.4f,0)),new Vector3(.4f,.5f,.6f));
                Matches(BlendPixel(mode,new Color(.8f,.6f,.4f,0),true),new Vector3(.4f,.5f,.6f));
            }
            // Returning to Alpha must not retain Multiply's shader parameter or GL blend factors.
            Matches(BlendPixel(ParticleBlendMode.Alpha,color),new Vector3(.3f,.45f,.6f));
            Check(gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Particle Alpha/Additive/Multiply match pixel formulas, respect opacity, fade neutrally in fog and restore GL blending/depth state");
        }
        finally {blendWorld.Clear();gl.DepthMask(true);ProjectShockGL.Environment.Apply(blendEnvironment);}
    }
    {
        var particleContext = ImGuiNET.ImGui.CreateContext();
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(ProjectShockGLEditor.ParticleEditor);
        var savedParticleEnvironment = ProjectShockGL.Environment.Capture();
        var activeBefore = Camera.Active;
        var gameTimeScale = Time.TimeScale;
        try
        {
            Time.TimeScale = 0;
            var io = ImGuiNET.ImGui.GetIO();
            unsafe { io.NativePtr->IniFilename = null; io.NativePtr->LogFilename = null; }
            io.DisplaySize = new Vector2(1280,800);
            io.DeltaTime = .1f;
            unsafe { io.Fonts.GetTexDataAsRGBA32(out byte* pixels,out int width,out int height); }
            ProjectShockGLEditor.ParticleEditor.Open = true;
            var particleFrames=0;
            void Frame()
            {
                ImGuiNET.ImGui.NewFrame();
                ProjectShockGLEditor.ParticleEditor.Draw();
                ImGuiNET.ImGui.Render();
                if (++particleFrames > 1) Check(ImGuiNET.ImGui.GetDrawData().TotalVtxCount>0);
            }
            Frame(); Frame();
            var document=(ProjectShockGLEditor.ParticleAssetDocument)type.GetField("_document",flags)!.GetValue(null)!;
            var particles=(ProjectShockGL.Core.ParticleSystemComponent)type.GetField("_particles",flags)!.GetValue(null)!;
            var target=(RenderTexture)type.GetField("_target",flags)!.GetValue(null)!;
            Check(particles.ParticleCount>0 && gl.IsTexture(target.Texture.Handle));
            Check(ReferenceEquals(activeBefore,Camera.Active));
            Check(ProjectShockGL.Environment.Capture().FogColor.Equals(savedParticleEnvironment.FogColor));
            type.GetMethod("PausePreview",flags)!.Invoke(null,null);
            var count=particles.ParticleCount;
            Frame(); Frame();
            Check(particles.ParticleCount==count);
            type.GetMethod("RestartPreview",flags)!.Invoke(null,null);
            Check(particles.ParticleCount==0);
            Frame(); Frame();
            Check(particles.ParticleCount>0);
            document.Set("StartSize", new ProjectShockGL.Core.ParticleRange(.5f,1));
            document.Set("StartLifetime", new ProjectShockGL.Core.ParticleRange(2,4));
            document.Set("SizeOverLifetime",true);
            document.Set("ColorOverLifetime",true);
            Frame();
            var editorCamera=(Camera)type.GetField("_camera",flags)!.GetValue(null)!;
            type.GetField("_yaw",flags)!.SetValue(null,0f);
            type.GetField("_pitch",flags)!.SetValue(null,0f);
            Frame();
            Check(Math.Abs(editorCamera.AspectRatio-(float)target.Width/target.Height)<.00001f);
            Vector2 ProjectUnit(Vector3 point)
            {
                var clip=Vector4.Transform(new Vector4(point,1),editorCamera.GetViewMatrix()*editorCamera.GetProjectionMatrix());
                return new Vector2((clip.X/clip.W+1)*.5f*target.Width,(1-clip.Y/clip.W)*.5f*target.Height);
            }
            var center=ProjectUnit(Vector3.Zero);
            var horizontal=Vector2.Distance(center,ProjectUnit(Vector3.UnitX));
            var vertical=Vector2.Distance(center,ProjectUnit(Vector3.UnitY));
            Check(Math.Abs(horizontal-vertical)<.001f);
            document.Set("VelocityOverLifetime",true);
            document.Set("VelocityCurve",new[]{new ProjectShockGL.Core.ParticleVelocityKey(0,Vector3.Zero),new ProjectShockGL.Core.ParticleVelocityKey(1,Vector3.UnitX)});
            document.Set("Noise",true);
            document.Set("NoiseStrength",new Vector3(.2f));
            Frame();
            Check(particles.Noise && particles.VelocityOverLifetime);
            var cameraPosition=editorCamera.Transform.Position;
            document.Set("Offset",new Vector3(2,0,0));
            Frame();
            Check(Vector3.Distance(editorCamera.Transform.Position,cameraPosition)<.00001f);
            document.Set("EmissionMode",ProjectShockGL.Core.ParticleEmissionMode.Burst);
            document.Set("BurstCount",7);
            Frame();
            Check(particles.ParticleCount==7);
            type.GetMethod("PausePreview",flags)!.Invoke(null,null);
            Frame();Frame();
            Check(particles.ParticleCount==7);
            type.GetMethod("PlayPreview",flags)!.Invoke(null,null);
            Check(particles.ParticleCount==7);
            Frame();
            Check(particles.ParticleCount==7);
            type.GetMethod("RestartPreview",flags)!.Invoke(null,null);
            Check(particles.ParticleCount==7);
            document.Set("EmissionMode",ProjectShockGL.Core.ParticleEmissionMode.Continuous);
            document.Set("Offset",Vector3.Zero);
            Frame();
            foreach(var blend in Enum.GetValues<ParticleBlendMode>()) { document.Set("BlendMode",blend); Frame(); }
            foreach(var shape in Enum.GetValues<ProjectShockGL.Core.ParticleShape>())
            {
                document.Set("Shape",shape);
                Frame();
                Check(particles.Shape==shape);
            }
            document.Set("Texture","Missing/ParticlePreview");
            Frame();
            Check(type.GetField("_error",flags)!.GetValue(null)!=null);
            document.Set("Texture","WhitePixel");
            Frame();
            Check(type.GetField("_error",flags)!.GetValue(null)==null);
            Check(!ProjectShockGLEditor.ParticleEditor.AllowClose());
            Frame();
            Check(ImGuiNET.ImGui.IsPopupOpen("Unsaved particle asset",ImGuiNET.ImGuiPopupFlags.AnyPopupId));
            var particlePreviewTexture=target.Texture.Handle;
            ProjectShockGLEditor.ParticleEditor.Dispose();
            Check(particles.IsDestroyed && target.IsDisposed && !gl.IsTexture(particlePreviewTexture));
            Check(ReferenceEquals(activeBefore,Camera.Active));
            Check(gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Particle Editor renders live isolated previews and shape wires, pauses/restarts at TimeScale zero, recovers resource errors, confirms unsaved changes and frees GPU resources");
        }
        finally
        {
            ProjectShockGLEditor.ParticleEditor.Dispose();
            Time.TimeScale=gameTimeScale;
            ProjectShockGL.Environment.Apply(savedParticleEnvironment);
            ImGuiNET.ImGui.DestroyContext(particleContext);
        }
    }
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
    point.Mode = ProjectShockGL.Core.LightMode.Baked;
    directional.Mode = ProjectShockGL.Core.LightMode.Baked;
    graphics.BeginDraw(Camera.Active);
    Check(UniformInt("uPointLightCount") == 0 && UniformInt("uMainLightEnabled") == 0);
    point.Mode = ProjectShockGL.Core.LightMode.Realtime;
    directional.Mode = ProjectShockGL.Core.LightMode.Realtime;
    graphics.BeginDraw(Camera.Active);
    Check(UniformInt("uPointLightCount") == 1 && UniformInt("uMainLightEnabled") == 1);
    lightWorld.Clear();
    Console.WriteLine("PASS: OpenGL excludes inactive and Baked lights and restores Realtime lights");
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
    void CheckBrushPattern(bool inverted = false)
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
            Check((sample % 2 == 0) != inverted ? pixel[0] > 240 : pixel[0] < 10);
        }
        Check(gl.GetError() == GLEnum.NoError);
    }
    CheckBrushPattern();
    brush.Transform.Scale = new Vector3(2, 1, 1);
    CheckBrushPattern();
    brush.Transform.Scale = Vector3.One;
    brush.Size = new Vector3(4, 2, 0.5f);
    CheckBrushPattern();
    brush.SetFace(ProjectShockGL.Core.BrushFace.PositiveZ,
        new ProjectShockGL.Core.BrushFaceData { Texture = "WhitePixel" });
    graphics.BeginDraw(Camera.Active);
    var faceQueue = new RenderQueue();
    brushWorld.Render(faceQueue);
    faceQueue.Flush(graphics);
    gl.Finish();
    var facePixel = new byte[4];
    unsafe { fixed (byte* p = facePixel) gl.ReadPixels(300, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, p); }
    Check(facePixel[0] > 240 && facePixel[1] > 240 && facePixel[2] > 240);
    var faceBatches = (Array)typeof(ProjectShockGL.Core.BrushBox).GetField("_faceBatches",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(brush)!;
    Check(faceBatches.Length == 2);
    var oldFaceMeshes = faceBatches.Cast<object>().Select(batch => (Mesh)batch.GetType().GetProperty("Mesh")!.GetValue(batch)!).ToArray();
    brush.SetFace(ProjectShockGL.Core.BrushFace.PositiveZ,
        new ProjectShockGL.Core.BrushFaceData { TextureOffset = new Vector2(0.5f, 0) });
    CheckBrushPattern(inverted: true);
    Check(oldFaceMeshes.All(mesh => mesh.IsDisposed));
    brush.SetFace(ProjectShockGL.Core.BrushFace.PositiveZ, null);
    CheckBrushPattern();
    Console.WriteLine("PASS: Brush face textures and UV offsets render independently and replaced face meshes are released");
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
    var surfaceEnvironment = ProjectShockGL.Environment.Capture();
    using (var surfaceRenderer = new Renderer(gl, 640, 240))
    using (var surfaceCamera = Camera.Create())
    using (var redGlass = Texture.CreateSolid(gl, 255, 0, 0, 64))
    using (var blueGlass = Texture.CreateSolid(gl, 0, 0, 255, 128))
    using (var invisibleRed = Texture.CreateSolid(gl, 255, 0, 0, 0))
    {
        try
        {
            ProjectShockGL.Environment.IsFogEnabled = false;
            ProjectShockGL.Environment.SkyColorEnabled = false;
            ProjectShockGL.Environment.AmbientColor = Color.White;
            surfaceCamera.AspectRatio = 640f / 240;
            var surfaceGraphics = new Graphics(surfaceRenderer);
            byte[] SurfacePixel()
            {
                gl.Finish();
                var pixel = new byte[4];
                unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                return pixel;
            }
            foreach (var surfaceLit in new[] { false, true })
            {
                surfaceGraphics.BeginDraw(surfaceCamera);
                surfaceGraphics.DrawQuad(new Vector3(0, 0, -4), Quaternion.Identity, new Vector2(4),
                    Content.GetWhitePixel(), Color.Green, lit: surfaceLit, surfaceMode: SurfaceMode.Opaque);
                surfaceGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, new Vector2(2),
                    invisibleRed, Color.White, lit: surfaceLit, surfaceMode: SurfaceMode.Cutout);
                Check(SurfacePixel()[1] > 240);
                surfaceGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, new Vector2(2),
                    invisibleRed, Color.White, lit: surfaceLit, surfaceMode: SurfaceMode.Opaque);
                Check(SurfacePixel()[0] > 240 && SurfacePixel()[1] < 3);
                surfaceGraphics.EndDraw();
            }
            surfaceGraphics.BeginDraw(surfaceCamera);
            var surfaceQueue = new RenderQueue();
            // Deliberately submit near glass first and the solid background last.
            surfaceQueue.Add(new BillboardCommand { Position = new Vector3(0, 0, -2), Size = new Vector2(2),
                Texture = redGlass, Color = Color.White, SurfaceMode = SurfaceMode.Transparent });
            surfaceQueue.Add(new MeshCommand { Position = new Vector3(0, 0, -4), Rotation = Quaternion.Identity, Scale = Vector3.One,
                Mesh = Content.LoadMesh("Cube"), Texture = blueGlass, Color = Color.White, SurfaceMode = SurfaceMode.Transparent });
            surfaceQueue.Add(new QuadCommand { Position = new Vector3(0, 0, -6), Rotation = Quaternion.Identity, Size = new Vector2(6),
                Texture = Content.GetWhitePixel(), Color = new Color(0, 1, 0), SurfaceMode = SurfaceMode.Opaque });
            surfaceQueue.Flush(surfaceGraphics);
            var blended = SurfacePixel();
            Check(Math.Abs(blended[0] - 64) < 3 && Math.Abs(blended[1] - 95) < 3 && Math.Abs(blended[2] - 96) < 3);
            Check(gl.GetBoolean(GetPName.DepthWritemask));
            surfaceGraphics.DrawQuad(new Vector3(0, 0, -3), Quaternion.Identity, new Vector2(3),
                Content.GetWhitePixel(), Color.Yellow, surfaceMode: SurfaceMode.Opaque);
            Check(SurfacePixel()[0] > 240 && SurfacePixel()[1] > 240 && SurfacePixel()[2] < 3);
            surfaceGraphics.EndDraw();
            surfaceGraphics.BeginDraw(surfaceCamera);
            surfaceGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, new Vector2(2),
                blueGlass, Color.White, surfaceMode: SurfaceMode.Cutout, alphaCutoff: 0.7f);
            Check(SurfacePixel().Take(3).All(channel => channel < 3));
            surfaceGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, new Vector2(2),
                blueGlass, Color.White, surfaceMode: SurfaceMode.Cutout, alphaCutoff: 0.3f);
            Check(SurfacePixel()[2] > 120);
            surfaceGraphics.EndDraw();
            gl.Disable(EnableCap.Blend);
            gl.Disable(EnableCap.DepthTest);
            gl.DepthMask(false);
            gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.DstAlpha, BlendingFactor.One);
            gl.BlendEquationSeparate(BlendEquationModeEXT.Max, BlendEquationModeEXT.Min);
            surfaceGraphics.DrawBillboard(new Vector3(0, 0, -2), Vector2.One, blueGlass, Color.White, surfaceMode: SurfaceMode.Transparent);
            Check(!gl.IsEnabled(EnableCap.Blend) && !gl.IsEnabled(EnableCap.DepthTest) && !gl.GetBoolean(GetPName.DepthWritemask));
            Check(gl.GetInteger(GetPName.BlendSrcAlpha) == (int)BlendingFactor.DstAlpha &&
                gl.GetInteger(GetPName.BlendEquationRgb) == (int)BlendEquationModeEXT.Max);
            Check(gl.GetError() == GLEnum.NoError);
            var surfaceBatchWorld = new World();
            try
            {
                var brushes = Enumerable.Range(0, 4).Select(index =>
                {
                    var item = surfaceBatchWorld.Spawn<ProjectShockGL.Core.BrushBox>();
                    item.Transform.Position = new Vector3(index * 2 - 3, 0, -6);
                    item.Lit = false;
                    item.SurfaceMode = index < 2 ? SurfaceMode.Opaque : SurfaceMode.Cutout;
                    return item;
                }).ToArray();
                surfaceBatchWorld.Update(0, simulate: false);
                surfaceGraphics.BeginDraw(surfaceCamera);
                var batches = new RenderQueue();
                surfaceBatchWorld.Render(batches, debugOnly: false);
                batches.Flush(surfaceGraphics);
                Check(surfaceGraphics.Statistics.DrawCalls == 2);
                surfaceGraphics.EndDraw();
                brushes[0].SurfaceMode = SurfaceMode.Transparent;
                brushes[0].Color = new Color(1, 1, 1, 0.5f);
                surfaceGraphics.BeginDraw(surfaceCamera);
                surfaceBatchWorld.Render(batches, debugOnly: false);
                batches.Flush(surfaceGraphics);
                Check(surfaceGraphics.Statistics.DrawCalls == 8);
                surfaceGraphics.EndDraw();
                var ownedMeshes = brushes.SelectMany(item =>
                    ((Array)typeof(ProjectShockGL.Core.BrushBox).GetField("_faceBatches",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(item)!)
                    .Cast<object>().Select(batch => (Mesh)batch.GetType().GetProperty("Mesh")!.GetValue(batch)!)).ToArray();
                surfaceBatchWorld.Clear();
                Check(ownedMeshes.All(mesh => mesh.IsDisposed));
                Console.WriteLine("PASS: Static batches separate Opaque/Cutout while transparent BrushBox faces sort separately and release GPU resources");
            }
            finally { surfaceBatchWorld.Clear(); }
            Console.WriteLine("PASS: Surface modes preserve opaque/cutout pixels, sort transparent meshes and billboards, avoid transparent depth writes and restore GL state");
        }
        finally
        {
            ProjectShockGL.Environment.Apply(surfaceEnvironment);
            gl.Enable(EnableCap.DepthTest); gl.Enable(EnableCap.Blend); gl.DepthMask(true);
            gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
    }
    var canvas = new Canvas();
    canvas.Resize(new Vector2(640, 240));
    var font = Content.LoadFont("NotoSans-Regular", 24)!;
    Check(ReferenceEquals(font, Content.LoadFont("NotoSans-Regular", 24)));
    Check(Content.LoadFont("missing-font") == null);
    var notePanel = canvas.Add<Widget>();
    notePanel.Name = "Note";
    var button = notePanel.Add<ProjectShockGL.UI.Button>();
    button.Name = "Close";
    button.Text.Content = "Закрыть";
    button.Text.Font = font;
    button.HoverColor = Color.Red;
    var noteScroll = notePanel.Add<TextScrollView>();
    noteScroll.Name = "Scroll";
    noteScroll.Body.Name = "Body";
    noteScroll.Body.Font = font;
    noteScroll.Body.Content = "Записка / Note";
    var picture = notePanel.Add<ProjectShockGL.UI.Image>();
    picture.Name = "Picture";
    picture.Texture = Content.LoadTexture("Default");
    picture.Rect.Position = new Vector2(17, 29);
    picture.FillCanvas = true;
    var calls = 0;
    button.Click.AddListener(() => calls++);
    const string uiName = "__UIRoundtripCheck";
    try
    {
        Content.SaveUI(uiName, notePanel);
        var asset = Content.LoadUI(uiName);
        Content.SaveUI(uiName, asset);
        asset = Content.LoadUI(uiName);
        var copy = asset.Instantiate(canvas);
        var second = asset.Instantiate(canvas);
        Check(copy.Name == "Note" && copy.Children.Count == 3);
        Check(copy.Find<ProjectShockGL.UI.Button>("Close")!.Children.Count == 2);
        Check(copy.Find<Text>("Body")!.Content == "Записка / Note");
        Check(ReferenceEquals(copy.Find<Text>("Body")!.Font, font));
        Check(copy.Find<TextScrollView>("Scroll")!.Children.Count == 1);
        var restoredPicture = copy.Find<ProjectShockGL.UI.Image>("Picture")!;
        Check(restoredPicture.FillCanvas && restoredPicture.Rect.Size == canvas.Size);
        Check(restoredPicture.Rect.AbsolutePosition == Vector2.Zero);
        restoredPicture.FillCanvas = false;
        Check(restoredPicture.Rect.Position == new Vector2(17, 29));
        restoredPicture.FillCanvas = true;
        Check(ReferenceEquals(copy.Find<ProjectShockGL.UI.Image>("Picture")!.Texture, picture.Texture));
        copy.Find<Text>("Body")!.Content = "Changed";
        Check(second.Find<Text>("Body")!.Content == "Записка / Note");
        Check(notePanel.Find<Text>("Body")!.Content == "Записка / Note");
        Check(canvas.Find<Widget>("Note") == notePanel && calls == 0);
        copy.Rect.Position = new Vector2(100);
        copy.Rect.Anchor = new Vector2(0.5f);
        copy.Rect.Pivot = Vector2.One;
        copy.Rect.Size = Vector2.One;
        Check(copy.Rect.Position == Vector2.Zero && copy.Rect.Anchor == Vector2.Zero && copy.Rect.Pivot == Vector2.Zero);
        Check(copy.Rect.AbsolutePosition == Vector2.Zero && copy.Rect.Size == canvas.Size);
        var oldCanvasSize = canvas.Size;
        canvas.Resize(new Vector2(1280, 720));
        Check(copy.Rect.Size == canvas.Size && second.Rect.Size == canvas.Size);
        Check(restoredPicture.Rect.Size == canvas.Size && restoredPicture.Rect.Position == Vector2.Zero);
        canvas.Resize(oldCanvasSize);
        var attached = asset.Instantiate(canvas, copy);
        Check(attached.Parent == copy);
        attached.Destroy();
        Check(copy.Find<Widget>("Note") == null);
        var before = canvas.Children.Count;
        asset.Root.Children.Add(new WidgetData { Type = "Unknown" });
        Throws<InvalidDataException>(() => asset.Instantiate(canvas));
        Check(canvas.Children.Count == before);
        copy.Destroy();
        second.Destroy();
        Console.WriteLine("PASS: UI JSON roundtrip preserves composition, resources, scoped names and independent instances; failed loading cleans up");
    }
    finally
    {
        notePanel.Destroy();
        File.Delete(Path.Combine(Content.Path, "UI", uiName + ".json"));
    }
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
    var scroll = canvas.Add<TextScrollView>();
    scroll.Rect.Position = new Vector2(200, 140);
    scroll.Rect.Size = new Vector2(65, 38);
    scroll.Body.Font = font;
    scroll.Body.Content = "First second third fourth fifth sixth";
    var pointer = new Pointer(canvas);
    pointer.Update(new Vector2(210, 150), canvas.Size, [], scrollY: -1);
    Check(scroll.ScrollY > 0 && scroll.Body.Rect.Size.Y > scroll.Rect.Size.Y);
    Check(scroll.Body.Rect.AbsolutePosition.Y == 140 - scroll.ScrollY);
    var offset = scroll.ScrollY;
    pointer.Update(new Vector2(199, 150), canvas.Size, [], scrollY: -1);
    Check(scroll.ScrollY == offset);
    var panel = scroll.Add<ProjectShockGL.UI.Image>();
    panel.Rect.Position = new Vector2(-5, -5);
    panel.Rect.Size = new Vector2(75, 48);
    panel.Color = Color.Red;
    ProjectShockGLEditor.UIEditor.Initialize(gl);
    ProjectShockGLEditor.UIEditor.Open = true;
    var uiType = typeof(ProjectShockGLEditor.UIEditor);
    var privateStatic = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    var uiAsset = new UIAsset
    {
        Root = new WidgetData
        {
            Name = "Panel", Size = new Vector2(800, 600),
            Children = [new WidgetData { Type = "Image", Name = "Background", Size = new Vector2(800, 600) }]
        }
    };
    uiType.GetField("_asset", privateStatic)!.SetValue(null, uiAsset);
    gl.GetInteger(GetPName.DrawFramebufferBinding, out int framebufferBefore);
    ProjectShockGLEditor.UIEditor.Render(graphics);
    gl.GetInteger(GetPName.DrawFramebufferBinding, out int framebufferAfter);
    Check(framebufferBefore == framebufferAfter);
    var previewTexture = (uint)uiType.GetField("_texture", privateStatic)!.GetValue(null)!;
    Check(previewTexture != 0 && gl.IsTexture(previewTexture));
    Check(gl.GetError() == GLEnum.NoError);
    var context = ImGuiNET.ImGui.CreateContext();
    try
    {
        var io = ImGuiNET.ImGui.GetIO();
        io.DisplaySize = new Vector2(1280, 800);
        io.DeltaTime = 1f / 60;
        unsafe { io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height); }
        ImGuiNET.ImGui.NewFrame();
        ProjectShockGLEditor.UIEditor.Draw();
        ImGuiNET.ImGui.Render();
        ImGuiNET.ImGui.NewFrame();
        ImGuiNET.ImGui.SetNextWindowPos(new Vector2(0, 0));
        ProjectShockGLEditor.UIEditor.Draw();
        ImGuiNET.ImGui.Render();
        Check(ImGuiNET.ImGui.GetDrawData().TotalVtxCount > 0);
        var child = uiAsset.Root.Children[0];
        child.Position = new Vector2(-100, -100);
        child.Size = new Vector2(120, 60);
        child.Anchor = new Vector2(0.5f);
        child.Pivot = new Vector2(0.5f);
        uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
        ProjectShockGLEditor.UIEditor.Render(graphics);
        void UIFrame(Vector2 mouse, bool down)
        {
            io.MousePos = mouse;
            io.MouseDown[0] = down;
            ImGuiNET.ImGui.NewFrame();
            ProjectShockGLEditor.UIEditor.Draw();
            ImGuiNET.ImGui.Render();
        }
        var origin = (Vector2)uiType.GetField("_previewOrigin", privateStatic)!.GetValue(null)!;
        var display = (Vector2)uiType.GetField("_previewDisplay", privateStatic)!.GetValue(null)!;
        var start = origin + new Vector2(300, 200) / new Vector2(800, 600) * display;
        UIFrame(start, false);
        UIFrame(start, true);
        Check(ReferenceEquals(uiType.GetField("_selected", privateStatic)!.GetValue(null), child));
        var moved = start + new Vector2(40, 30);
        UIFrame(moved, true);
        var expectedPosition = new Vector2(-100, -100) + new Vector2(40, 30) / display * new Vector2(800, 600);
        Check(Vector2.Distance(child.Position, expectedPosition) < 0.01f);
        UIFrame(moved, false);
        Check(ProjectShockGLEditor.UIEditor.IsDirty);
        Check(child.Anchor == new Vector2(0.5f) && child.Pivot == new Vector2(0.5f));
        uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
        ProjectShockGLEditor.UIEditor.Render(graphics);
        var beforeCancel = child.Position;
        UIFrame(moved, false);
        UIFrame(moved, true);
        UIFrame(moved + new Vector2(15, 10), true);
        Check(child.Position != beforeCancel);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, true);
        UIFrame(moved + new Vector2(15, 10), true);
        Check(child.Position == beforeCancel);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, false);
        UIFrame(moved, false);
        Console.WriteLine("PASS: Real UI preview selection, scaled dragging and Escape cancellation preserve anchors and pivot");
        ProjectShockGLEditor.UIEditor.Undo();
        UIAsset CurrentUI() => (UIAsset)uiType.GetField("_asset", privateStatic)!.GetValue(null)!;
        Check(CurrentUI().Root.Children[0].Position == new Vector2(-100, -100));
        ProjectShockGLEditor.UIEditor.Redo();
        Check(Vector2.Distance(CurrentUI().Root.Children[0].Position, expectedPosition) < 0.01f);
        void EditUI(Action action, uint id)
        {
            var before = uiType.GetMethod("Capture", privateStatic)!.Invoke(null, null);
            action();
            uiType.GetField("_editItem", privateStatic)!.SetValue(null, id);
            ImGuiNET.ImGui.NewFrame();
            uiType.GetMethod("RecordFrame", privateStatic)!.Invoke(null, [before]);
            ImGuiNET.ImGui.Render();
        }
        EditUI(() => CurrentUI().Root.Children.Add(new WidgetData { Name = "Created", Type = "Button" }), 1);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children.Count == 1);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children.Count == 2);
        EditUI(() => CurrentUI().Root.Children[1].Name = "Renamed", 2);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[1].Name == "Created");
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[1].Name == "Renamed");
        EditUI(() => CurrentUI().Root.Children.RemoveAt(1), 3);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[1].Name == "Renamed");
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children.Count == 1);
        ProjectShockGLEditor.UIEditor.Undo();
        EditUI(() => CurrentUI().Root.Name = "Branch", 4);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children.Count == 2 && CurrentUI().Root.Name == "Branch");
        Console.WriteLine("PASS: UI history restores moves, properties, creation and deletion, ignores cancelled moves and branches independently");
        foreach (var direction in new[] { new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, 0), new Vector2(0, -1) })
        {
            var resized = CurrentUI().Root.Children[0];
            uiType.GetField("_selected", privateStatic)!.SetValue(null, resized);
            uiType.GetMethod("ResetHistory", privateStatic)!.Invoke(null, null);
            uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
            ProjectShockGLEditor.UIEditor.Render(graphics);
            UIFrame(new Vector2(-100), false);
            origin = (Vector2)uiType.GetField("_previewOrigin", privateStatic)!.GetValue(null)!;
            display = (Vector2)uiType.GetField("_previewDisplay", privateStatic)!.GetValue(null)!;
            var startSize = resized.Size;
            var startPosition = resized.Position;
            var canvasSize = new Vector2(800, 600);
            var worldMin = canvasSize * resized.Anchor + startPosition - startSize * resized.Pivot;
            var handle = origin + (worldMin + startSize * (direction + Vector2.One) * 0.5f) / canvasSize * display;
            UIFrame(handle, false);
            UIFrame(handle, true);
            var delta = direction * 20;
            UIFrame(handle + delta / canvasSize * display, true);
            UIFrame(handle + delta / canvasSize * display, false);
            var expectedSize = startSize + Vector2.Abs(direction) * 20;
            Check(Vector2.Distance(resized.Size, expectedSize) < 2f * Math.Max(canvasSize.X / display.X, canvasSize.Y / display.Y));
            var finalSize = resized.Size;
            var newMin = canvasSize * resized.Anchor + resized.Position - resized.Size * resized.Pivot;
            if (direction.X > 0) Near(newMin.X, worldMin.X);
            if (direction.X < 0) Near(newMin.X + resized.Size.X, worldMin.X + startSize.X);
            if (direction.Y > 0) Near(newMin.Y, worldMin.Y);
            if (direction.Y < 0) Near(newMin.Y + resized.Size.Y, worldMin.Y + startSize.Y);
            ProjectShockGLEditor.UIEditor.Undo();
            Check(CurrentUI().Root.Children[0].Size == startSize);
            Check(CurrentUI().Root.Children[0].Position == startPosition);
            ProjectShockGLEditor.UIEditor.Redo();
            Check(CurrentUI().Root.Children[0].Size == finalSize);
        }
        var cancelWidget = CurrentUI().Root.Children[0];
        uiType.GetField("_selected", privateStatic)!.SetValue(null, cancelWidget);
        uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
        ProjectShockGLEditor.UIEditor.Render(graphics);
        var cancelSize = cancelWidget.Size;
        var cancelPosition = cancelWidget.Position;
        var corner = origin + (new Vector2(800, 600) * cancelWidget.Anchor + cancelWidget.Position
            + cancelWidget.Size * (Vector2.One - cancelWidget.Pivot)) / new Vector2(800, 600) * display;
        UIFrame(corner, false);
        UIFrame(corner, true);
        UIFrame(corner + new Vector2(20), true);
        Check(cancelWidget.Size != cancelSize);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, true);
        UIFrame(corner + new Vector2(20), true);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, false);
        UIFrame(corner, false);
        Check(cancelWidget.Size == cancelSize && cancelWidget.Position == cancelPosition);
        Console.WriteLine("PASS: UI resize handles preserve opposite edges with pivot, undo size and position together and cancel with Escape");
        uiType.GetField("_selected", privateStatic)!.SetValue(null, CurrentUI().Root);
        uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
        ProjectShockGLEditor.UIEditor.Render(graphics);
        UIFrame(new Vector2(-100), false);
        var rootPoint = origin + new Vector2(10);
        UIFrame(rootPoint, false);
        UIFrame(rootPoint, true);
        UIFrame(rootPoint + new Vector2(20), true);
        UIFrame(rootPoint + new Vector2(20), false);
        Check(CurrentUI().Root.Position == Vector2.Zero);
        Check(CurrentUI().Root.Anchor == Vector2.Zero && CurrentUI().Root.Pivot == Vector2.Zero);
        Check(CurrentUI().Root.Size == new Vector2(800, 600));
        Check(uiType.GetField("_dragged", privateStatic)!.GetValue(null) == null);
        Console.WriteLine("PASS: UI asset root follows Canvas resize and remains fixed during preview mouse dragging");
        uiType.GetField("_selected", privateStatic)!.SetValue(null, CurrentUI().Root.Children[0]);
        var originalCount = CurrentUI().Root.Children.Count;
        var originalWidget = CurrentUI().Root.Children[0];
        io.AddKeyEvent(ImGuiNET.ImGuiKey.ModCtrl, true);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.D, true);
        UIFrame(rootPoint, false);
        Check(CurrentUI().Root.Children.Count == originalCount + 1);
        var duplicated = CurrentUI().Root.Children[1];
        Check(duplicated.Name != originalWidget.Name && duplicated.Size == originalWidget.Size);
        Check(!ReferenceEquals(duplicated.Properties, originalWidget.Properties));
        io.AddKeyEvent(ImGuiNET.ImGuiKey.D, false);
        UIFrame(rootPoint, false);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Z, true);
        UIFrame(rootPoint, false);
        Check(CurrentUI().Root.Children.Count == originalCount);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Z, false);
        UIFrame(rootPoint, false);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Y, true);
        UIFrame(rootPoint, false);
        Check(CurrentUI().Root.Children.Count == originalCount + 1);
        Check(CurrentUI().Root.Children[1].Name == duplicated.Name);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Y, false);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.ModCtrl, false);
        UIFrame(rootPoint, false);
        Console.WriteLine("PASS: Real Ctrl+D duplicates UI widgets independently and Ctrl+Z/Ctrl+Y undo and redo the action");
        var inputType = typeof(Input);
        var inputInitialized = inputType.GetField("_isInitialized", privateStatic)!;
        var wasInputInitialized = inputInitialized.GetValue(null);
        var keys = (HashSet<Silk.NET.Input.Key>)inputType.GetProperty("CurrentKeys", privateStatic)!.GetValue(null)!;
        var previousKeys = (HashSet<Silk.NET.Input.Key>)inputType.GetProperty("PreviousKeys", privateStatic)!.GetValue(null)!;
        var originalKeys = keys.ToArray();
        var originalPreviousKeys = previousKeys.ToArray();
        try
        {
            inputInitialized.SetValue(null, true);
            void EngineKey(Silk.NET.Input.Key key)
            {
                keys.Clear();
                previousKeys.Clear();
                keys.Add(Silk.NET.Input.Key.ControlLeft);
                keys.Add(key);
                uiType.GetMethod("PollShortcuts", privateStatic)!.Invoke(null, null);
                Input.Update();
                UIFrame(rootPoint, false);
            }
            var countBeforeEngineKey = CurrentUI().Root.Children.Count;
            uiType.GetField("_selected", privateStatic)!.SetValue(null, CurrentUI().Root.Children[0]);
            EngineKey(Silk.NET.Input.Key.D);
            Check(CurrentUI().Root.Children.Count == countBeforeEngineKey + 1);
            EngineKey(Silk.NET.Input.Key.Z);
            Check(CurrentUI().Root.Children.Count == countBeforeEngineKey);
            EngineKey(Silk.NET.Input.Key.Y);
            Check(CurrentUI().Root.Children.Count == countBeforeEngineKey + 1);
        }
        finally
        {
            keys.Clear();
            keys.UnionWith(originalKeys);
            previousKeys.Clear();
            previousKeys.UnionWith(originalPreviousKeys);
            inputInitialized.SetValue(null, wasInputInitialized);
        }
        Console.WriteLine("PASS: UI shortcuts travel through engine Input before Input.Update clears key transitions");
        var leftPanel = new WidgetData { Name = "Left", Position = new Vector2(80, 70), Size = new Vector2(200) };
        var rightPanel = new WidgetData { Name = "Right", Position = new Vector2(300, 50), Size = new Vector2(250, 180) };
        var nestedWidget = new WidgetData
        {
            Name = "Nested", Position = new Vector2(10, 20), Size = new Vector2(50, 40),
            Anchor = new Vector2(0.5f), Pivot = new Vector2(0.5f)
        };
        leftPanel.Children.Add(nestedWidget);
        var treeAsset = new UIAsset { Root = new WidgetData { Name = "Root", Children = [leftPanel, rightPanel] } };
        uiType.GetField("_asset", privateStatic)!.SetValue(null, treeAsset);
        uiType.GetField("_selected", privateStatic)!.SetValue(null, nestedWidget);
        uiType.GetMethod("ResetHistory", privateStatic)!.Invoke(null, null);
        bool MoveNode(WidgetData source, WidgetData target, int placement) =>
            (bool)uiType.GetMethod("MoveWidget", privateStatic)!.Invoke(null, [source, target, placement])!;
        Vector2 NodePosition(string name)
        {
            var temporaryCanvas = new Canvas();
            temporaryCanvas.Resize(new Vector2(800, 600));
            try
            {
                var tree = CurrentUI().Instantiate(temporaryCanvas);
                temporaryCanvas.Update(0);
                return tree.Find<Widget>(name)!.Rect.AbsolutePosition;
            }
            finally { temporaryCanvas.Destroy(); }
        }
        var nestedPosition = NodePosition("Nested");
        Check(MoveNode(nestedWidget, rightPanel, 0));
        Check(leftPanel.Children.Count == 0 && rightPanel.Children.Single() == nestedWidget);
        Check(Vector2.Distance(NodePosition("Nested"), nestedPosition) < 0.001f);
        Check(!MoveNode(rightPanel, nestedWidget, 0));
        Check(!MoveNode(treeAsset.Root, rightPanel, 0));
        Check(!MoveNode(nestedWidget, nestedWidget, 0));
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].Children.Single().Name == "Nested");
        Check(Vector2.Distance(NodePosition("Nested"), nestedPosition) < 0.001f);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[1].Children.Single().Name == "Nested");
        var firstSibling = CurrentUI().Root.Children[0];
        var secondSibling = CurrentUI().Root.Children[1];
        var firstPosition = firstSibling.Position;
        Check(MoveNode(firstSibling, secondSibling, 1));
        Check(CurrentUI().Root.Children[1] == firstSibling && firstSibling.Position == firstPosition);
        Check(!MoveNode(firstSibling, secondSibling, 1));
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].Name == "Left");
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[0].Name == "Right");
        Console.WriteLine("PASS: UI hierarchy moves preserve screen position, reorder siblings, reject cycles and restore through Undo/Redo");

        var actionButtonData = new WidgetData { Type = "Button", Name = "ActionButton" };
        var actionAsset = new UIAsset { Root = new WidgetData { Name = "ActionScreen", Children = [actionButtonData] } };
        uiType.GetField("_asset", privateStatic)!.SetValue(null, actionAsset);
        uiType.GetField("_selected", privateStatic)!.SetValue(null, actionButtonData);
        uiType.GetMethod("ResetHistory", privateStatic)!.Invoke(null, null);
        var drawActions = uiType.GetMethod("DrawClickActions", privateStatic)!;
        void ActionFrame(Vector2 mouse, bool down)
        {
            var before = uiType.GetMethod("Capture", privateStatic)!.Invoke(null, null);
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, down);
            ImGuiNET.ImGui.NewFrame();
            ImGuiNET.ImGui.SetNextWindowPos(Vector2.Zero);
            ImGuiNET.ImGui.SetNextWindowSize(new Vector2(520, 700));
            ImGuiNET.ImGui.Begin("Actions Inspector Test");
            drawActions.Invoke(null, [CurrentUI().Root.Children[0]]);
            ImGuiNET.ImGui.End();
            uiType.GetMethod("RecordFrame", privateStatic)!.Invoke(null, [before]);
            ImGuiNET.ImGui.Render();
        }
        ActionFrame(new Vector2(-100), false);
        ActionFrame(new Vector2(-100), false);
        for (var y = 38; y < 78 && CurrentUI().Root.Children[0].ClickActions.Count == 0; y += 2)
        {
            ActionFrame(new Vector2(30, y), true);
            ActionFrame(new Vector2(30, y), false);
        }
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 1);
        var authoredButtonId = CurrentUI().Root.Children[0].Id;
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 0);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 1 && CurrentUI().Root.Children[0].Id == authoredButtonId);
        var visibleMethod = UIActionCatalog.GetWidgetMethods(typeof(Widget)).Single(method => method.Name == "Widget/Set visible");
        EditUI(() =>
        {
            var action = CurrentUI().Root.Children[0].ClickActions[0];
            action.TargetId = CurrentUI().Root.Id;
            action.Method = visibleMethod.Id;
            action.Parameter = new Newtonsoft.Json.Linq.JValue(true);
        }, 2201);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].ClickActions[0].Method == "");
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[0].ClickActions[0].TargetId == CurrentUI().Root.Id);
        ActionFrame(new Vector2(-100), false);
        for (var y = 136; y < 190 && CurrentUI().Root.Children[0].ClickActions[0].Parameter!.Value<bool>(); y += 2)
        {
            ActionFrame(new Vector2(15, y), true);
            ActionFrame(new Vector2(15, y), false);
        }
        Check(!CurrentUI().Root.Children[0].ClickActions[0].Parameter!.Value<bool>());
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].ClickActions[0].Parameter!.Value<bool>());
        ProjectShockGLEditor.UIEditor.Redo();
        Check(!CurrentUI().Root.Children[0].ClickActions[0].Parameter!.Value<bool>());

        var globalAction = UIActionCatalog.GetGlobalMethods().Single(method => method.Name == "PreviewTest/Increment");
        EditUI(() => CurrentUI().Root.Children[0].ClickActions.Add(new UIActionCall
        {
            Target = UIActionTarget.Global, Method = globalAction.Id
        }), 2202);
        EditUI(() => CurrentUI().Root.Children[0].ClickActions.Reverse(), 2203);
        Check(CurrentUI().Root.Children[0].ClickActions[0].Method == globalAction.Id);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].ClickActions[0].Method == visibleMethod.Id);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[0].ClickActions[0].Method == globalAction.Id);

        uiType.GetField("_selected", privateStatic)!.SetValue(null, CurrentUI().Root.Children[0]);
        ProjectShockGLEditor.UIEditor.DuplicateSelected();
        var duplicatedActionButton = CurrentUI().Root.Children[1];
        Check(duplicatedActionButton.Id != authoredButtonId &&
            duplicatedActionButton.ClickActions[1].TargetId == CurrentUI().Root.Id);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children.Count == 1);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[1].Id == duplicatedActionButton.Id);

        PreviewActionFixture.Calls = 0;
        uiType.GetField("_rebuild", privateStatic)!.SetValue(null, true);
        ProjectShockGLEditor.UIEditor.Render(graphics);
        var actionsPreview = (Canvas)uiType.GetField("_preview", privateStatic)!.GetValue(null)!;
        var previewRoot = actionsPreview.Children[0];
        previewRoot.Find<ProjectShockGL.UI.Button>("ActionButton")!.Click.Invoke();
        Check(PreviewActionFixture.Calls == 0 && previewRoot.Visible);

        // Exercise the actual remove button and its history.
        ActionFrame(new Vector2(-100), false);
        for (var y = 70; y < 100 && CurrentUI().Root.Children[0].ClickActions.Count == 2; y += 2)
        {
            ActionFrame(new Vector2(95, y), true);
            ActionFrame(new Vector2(95, y), false);
        }
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 1);
        ProjectShockGLEditor.UIEditor.Undo();
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 2);
        ProjectShockGLEditor.UIEditor.Redo();
        Check(CurrentUI().Root.Children[0].ClickActions.Count == 1);
        Console.WriteLine("PASS: Real Click Actions Inspector add/parameter/remove controls, target/method/order history, duplicate IDs and disabled preview execution");
        Check(ProjectShockGLEditor.UIEditor.IsDirty);
        Check(!ProjectShockGLEditor.UIEditor.AllowClose());
        var discardClose = (Action)uiType.GetField("_pending", privateStatic)!.GetValue(null)!;
        uiType.GetField("_pending", privateStatic)!.SetValue(null, null);
        var quitField = typeof(Application).GetField("_quitRequested", privateStatic)!;
        var oldQuit = quitField.GetValue(null);
        try
        {
            discardClose();
            UIFrame(rootPoint, false);
            UIFrame(rootPoint, false);
            Check(ProjectShockGLEditor.UIEditor.AllowClose());
            Check(!ProjectShockGLEditor.UIEditor.IsDirty);
            Check(uiType.GetField("_pending", privateStatic)!.GetValue(null) == null);
            Check((bool)quitField.GetValue(null)!);
        }
        finally { quitField.SetValue(null, oldQuit); }
        Console.WriteLine("PASS: Discard on editor exit stays acknowledged across later frames and requests application shutdown");
    }
    finally
    {
        ImGuiNET.ImGui.DestroyContext(context);
        ProjectShockGLEditor.UIEditor.Open = false;
        ProjectShockGLEditor.UIEditor.Dispose();
    }
    Console.WriteLine("PASS: UI Editor renders its preview into a texture, restores the framebuffer and draws real ImGui panels");
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
    Check(rgba[((239 - 140) * 640 + 200) * 4] > 200);
    Check(rgba[((239 - 139) * 640 + 200) * 4] == 0);
    Check(rgba[((239 - 140) * 640 + 199) * 4] == 0);
    Check(rgba[((239 - 178) * 640 + 200) * 4] == 0);
    Check(rgba[((239 - 140) * 640 + 265) * 4] == 0);
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
    ProjectShockGL.Core.Game.Initialize();
    {
        var lightmapEnvironment = ProjectShockGL.Environment.Capture();
        var savedEditor = Application.IsEditor;
        var savedGameWorld = ProjectShockGL.Core.Game.Instance.World;
        var bakedWorld = new World();
        var gameWorldProperty = typeof(ProjectShockGL.Core.Game).GetProperty(nameof(ProjectShockGL.Core.Game.World))!;
        gameWorldProperty.SetValue(ProjectShockGL.Core.Game.Instance,bakedWorld);
        using var bakedCamera = Camera.Create();
        bakedCamera.Orthographic = true; bakedCamera.OrthoSize = 3; bakedCamera.AspectRatio = 640f / 240;
        var mapFile = Path.Combine(AppContext.BaseDirectory,"LightmapRoundtrip.map");
        var assets = new List<string>();
        try
        {
            editorProperty.SetValue(null,false);
            preview.Unlit = false; preview.EnvironmentEnabled = true;
            ProjectShockGL.Environment.Apply(new ProjectShockGL.Core.EnvironmentData { AmbientColor = new Color(.2f,0,0), IsFogEnabled = false });
            for (var i = 0; i < 2; i++)
            {
                var box = bakedWorld.Spawn<ProjectShockGL.Core.BrushBox>();
                box.Name = "BakedBox_" + i;
                box.Transform.Position = new Vector3(i == 0 ? -.8f : .8f,0,-3);
            }
            bakedWorld.Update(0,simulate:false);
            var geometry = bakedWorld.PrepareLightmapGeometry(new LightmapPreparationOptions { AtlasSize = 128, SurfaceSize = 64, Padding = 1 });
            var result = DirectLightBaker.Bake(DirectLightBaker.Capture(bakedWorld,geometry));
            var asset = Content.SaveLightmap("Checks/Runtime",result); assets.Add(asset);
            bakedWorld.ApplyLightmap(asset);
            ProjectShockGL.Environment.AmbientColor = new Color(0,.5f,0);
            byte[] FrameWorld(World world)
            {
                graphics.BeginDraw(bakedCamera);
                var queue = new RenderQueue(); world.Render(queue,false);
                queue.Flush(graphics); graphics.EndDraw();
                var pixels = new byte[640*240*4];
                unsafe { fixed (byte* p = pixels) gl.ReadPixels(0,0,640,240,PixelFormat.Rgba,PixelType.UnsignedByte,p); }
                return pixels;
            }
            void AssertColor(byte[] pixels, byte red, byte green, byte blue)
            {
                var count = 0;
                for (var at = 0; at < pixels.Length; at += 4)
                {
                    if (pixels[at] + pixels[at+1] + pixels[at+2] < 10) continue;
                    Check(Math.Abs(pixels[at]-red)<=2 && Math.Abs(pixels[at+1]-green)<=2 && Math.Abs(pixels[at+2]-blue)<=2);
                    count++;
                }
                Check(count > 100);
            }
            AssertColor(FrameWorld(bakedWorld),51,0,0); // Current green Ambient is not added a second time.
            bakedWorld.StaticBatchingEnabled = false;
            AssertColor(FrameWorld(bakedWorld),51,0,0);
            bakedWorld.StaticBatchingEnabled = true;
            ProjectShockGLEditor.EditorLevelManager.SaveCurrentLevel(mapFile);
            Check(ProjectShockGLEditor.EditorLevelManager.ReadLevel(mapFile).Lightmap == asset);
            ProjectShockGLEditor.EditorLevelManager.OpenLevel(mapFile);
            bakedWorld.Update(0,simulate:false);
            Check(bakedWorld.LightmapAsset == asset);
            AssertColor(FrameWorld(bakedWorld),51,0,0);
            var realtime = bakedWorld.Spawn<ProjectShockGL.Core.Light>();
            realtime.Type = ProjectShockGL.Core.LightType.Directional; realtime.Color = Color.Green; realtime.Intensity = .25f;
            AssertColor(FrameWorld(bakedWorld),51,64,0);
            realtime.SetActive(false);
            graphics.BeginDraw(bakedCamera);
            graphics.DrawQuad(new Vector3(0,0,-2),Quaternion.Identity,Vector2.One,Content.GetWhitePixel(),Color.White,lit:true);
            var fallbackPixel = new byte[4];
            unsafe { fixed(byte* p=fallbackPixel) gl.ReadPixels(320,120,1,1,PixelFormat.Rgba,PixelType.UnsignedByte,p); }
            Check(fallbackPixel[0]<3 && Math.Abs(fallbackPixel[1]-128)<=2); // No texture/uniform leakage to an ordinary draw.
            var queueForHandles = new RenderQueue(); bakedWorld.Render(queueForHandles,false);
            var textureHandles = queueForHandles.Commands.OfType<MeshCommand>().Select(command=>command.LightmapTexture!.Handle).Distinct().ToArray();
            bakedWorld.ApplyLightmap(null);
            Check(textureHandles.All(handle=>!gl.IsTexture(handle)));
            bakedWorld.Clear();
            ProjectShockGL.Environment.AmbientColor = new Color(0,0,.3f);
            for(var i=0;i<2;i++) bakedWorld.Details.Add(new ProjectShockGL.Core.DetailPlacement
            {
                AssetPath="ExampleCube",Position=new Vector3(i==0?-.8f:.8f,0,-3)
            });
            var detailGeometry = bakedWorld.PrepareLightmapGeometry(new LightmapPreparationOptions {AtlasSize=128,SurfaceSize=64,Padding=1});
            var detailResult = DirectLightBaker.Bake(DirectLightBaker.Capture(bakedWorld,detailGeometry));
            var detailAsset = Content.SaveLightmap("Checks/Details",detailResult); assets.Add(detailAsset);
            bakedWorld.ApplyLightmap(detailAsset);
            ProjectShockGL.Environment.AmbientColor=Color.Green;
            var instanceQueue=new RenderQueue(); bakedWorld.Render(instanceQueue,false);
            var instance=instanceQueue.Commands.OfType<InstancedMeshCommand>().Single();
            Check(instance.Instances.Count==2 && instance.Lightmap!=null);
            var instanceHandle=instance.Lightmap!.Handle;
            // The asset's base texture modulates the baked blue illumination.
            var detailPixels=FrameWorld(bakedWorld);
            var colored=0;
            for(var at=0;at<detailPixels.Length;at+=4)
                if(detailPixels[at+2]>5) {Check(detailPixels[at]<3 && detailPixels[at+1]<3);colored++;}
            Check(colored>100);
            bakedWorld.Clear();
            Check(!gl.IsTexture(instanceHandle) && gl.GetError()==GLEnum.NoError);
            Console.WriteLine("PASS: Saved lightmaps reload through map references, replace Ambient, retain Realtime light, support batches and instanced Details, and release level-owned GPU textures");
        }
        finally
        {
            bakedWorld.Clear();
            gameWorldProperty.SetValue(ProjectShockGL.Core.Game.Instance,savedGameWorld);
            editorProperty.SetValue(null,savedEditor);
            ProjectShockGL.Environment.Apply(lightmapEnvironment);
            foreach(var asset in assets)
            {
                var path=LightmapStorage.GetPath(asset);
                if(File.Exists(path))File.Delete(path);
            }
            if(File.Exists(mapFile))File.Delete(mapFile);
        }
    }
    {
        var sessionWorld=new World();
        var sessionEnvironment=ProjectShockGL.Environment.Capture();
        var sessionAssets=new List<string>();
        var sessionOptions=new LightmapBakeSettings{AtlasSize=64,SurfaceSize=32,Padding=1,IndirectEnabled=false};
        var sessionBlockRoot="Blocked-"+Guid.NewGuid().ToString("N");
        var sessionBlockName=sessionBlockRoot+"/Map";
        var sessionBlockFile=Path.Combine(Content.Path,"Lightmaps",sessionBlockRoot);
        void FinishBake(ProjectShockGLEditor.LightmapBakeSession session)
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while(session.IsBusy && clock.Elapsed<TimeSpan.FromSeconds(10)){session.Poll();Thread.Yield();}
            Check(!session.IsBusy);
        }
        uint SessionTexture()
        {
            var queue=new RenderQueue();sessionWorld.Render(queue,false);
            return queue.Commands.OfType<MeshCommand>().First(command=>command.LightmapTexture!=null).LightmapTexture!.Handle;
        }
        try
        {
            ProjectShockGL.Environment.AmbientColor=new Color(.2f,.1f,.05f);
            sessionWorld.Spawn<ProjectShockGL.Core.BrushBox>();
            sessionWorld.Update(0,simulate:false);
            sessionWorld.LightmapSettings=sessionOptions;
            var input=DirectLightBaker.Capture(sessionWorld,sessionWorld.PrepareLightmapGeometry(sessionOptions.CreatePreparationOptions()));
            var oldAsset=Content.SaveLightmap("Checks/OldSession",DirectLightBaker.Bake(input));sessionAssets.Add(oldAsset);
            sessionWorld.ApplyLightmap(oldAsset);
            var oldTexture=SessionTexture();
            using var session=new ProjectShockGLEditor.LightmapBakeSession();
            var signature="unchanged";
            session.Start(sessionWorld,sessionOptions,"Checks/Session",signature:()=>signature);
            Check(sessionWorld.LightmapAsset==oldAsset && gl.IsTexture(oldTexture) && session.IsBusy);
            FinishBake(session);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Completed && session.Summary!.Pages==1);
            var published=session.PublishedAsset!;sessionAssets.Add(published);
            Check(published!=oldAsset && sessionWorld.LightmapAsset==published && !gl.IsTexture(oldTexture));
            var publishedTexture=SessionTexture();
            session.Start(sessionWorld,sessionOptions,"Checks/CancelledSession");
            var clearRejected=false;
            try{session.Clear(sessionWorld);}catch(InvalidOperationException){clearRejected=true;}
            Check(clearRejected);
            session.Cancel();FinishBake(session);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Cancelled && sessionWorld.LightmapAsset==published && gl.IsTexture(publishedTexture));
            session.Start(sessionWorld,sessionOptions,"Checks/ChangedSession",signature:()=>signature);
            signature="edited";FinishBake(session);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Discarded && sessionWorld.LightmapAsset==published);
            session.Start(sessionWorld,sessionOptions,"Checks/ReplacedSession");
            sessionWorld.ApplyLightmap(oldAsset);
            FinishBake(session);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Discarded && sessionWorld.LightmapAsset==oldAsset);
            sessionWorld.ApplyLightmap(published);
            Directory.CreateDirectory(Path.GetDirectoryName(sessionBlockFile)!);
            File.WriteAllText(sessionBlockFile,"blocks creation of the output directory");
            session.Start(sessionWorld,sessionOptions,sessionBlockName);
            FinishBake(session);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Failed && sessionWorld.LightmapAsset==published);
            var remainingTexture=SessionTexture();
            session.Clear(sessionWorld);
            Check(session.State==ProjectShockGLEditor.LightmapBakeState.Idle && sessionWorld.LightmapAsset==null && !gl.IsTexture(remainingTexture));
            Check(File.Exists(LightmapStorage.GetPath(oldAsset)) && File.Exists(LightmapStorage.GetPath(published)));
            Console.WriteLine("PASS: Editor bake sessions retain the previous GPU result until success, preserve it on cancel/edit/save failure, and Clear removes only the reference");
        }
        finally
        {
            sessionWorld.Clear();ProjectShockGL.Environment.Apply(sessionEnvironment);
            if(File.Exists(sessionBlockFile))File.Delete(sessionBlockFile);
            foreach(var asset in sessionAssets)if(File.Exists(LightmapStorage.GetPath(asset)))File.Delete(LightmapStorage.GetPath(asset));
        }
    }
    var detailWorld = new World();
    typeof(ProjectShockGL.Core.Game).GetProperty(nameof(ProjectShockGL.Core.Game.World))!
        .SetValue(ProjectShockGL.Core.Game.Instance, detailWorld);
    var detailEditorBefore = Application.IsEditor;
    var detailCamera = ProjectShockGLEditor.TransformState.Capture(Camera.Active.Transform);
    var detailAspect = Camera.Active.AspectRatio;
    var detailOrtho = Camera.Active.Orthographic;
    var detailContext = ImGuiNET.ImGui.CreateContext();
    editorProperty.SetValue(null, true);
    try
    {
        ProjectShockGLEditor.EditorLevelManager.NewLevel();
        Camera.Active.Transform.Position = new Vector3(0, 0, 6);
        Camera.Active.Transform.Rotation = Quaternion.Identity;
        Camera.Active.AspectRatio = 1280f / 800;
        Camera.Active.Orthographic = false;
        var io = ImGuiNET.ImGui.GetIO();
        io.DisplaySize = new Vector2(1280, 800);
        io.DeltaTime = 1f / 60;
        unsafe { io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height); }
        var paletteType = typeof(ProjectShockGLEditor.Palette);
        paletteType.GetField("_tab", privateStatic)!.SetValue(null, "Details");
        paletteType.GetField("_currentType", privateStatic)!.SetValue(null, "");
        void DetailFrame(Vector2 mouse, bool down)
        {
            io.MousePos = mouse;
            io.MouseDown[0] = down;
            ImGuiNET.ImGui.NewFrame();
            ImGuiNET.ImGui.SetNextWindowPos(Vector2.Zero);
            ImGuiNET.ImGui.SetNextWindowSize(new Vector2(230, 800));
            ProjectShockGLEditor.Palette.Draw();
            ImGuiNET.ImGui.SetNextWindowPos(new Vector2(960, 0));
            ImGuiNET.ImGui.SetNextWindowSize(new Vector2(320, 800));
            ProjectShockGLEditor.Inspector.Draw();
            ProjectShockGLEditor.ObjectManipulator.Draw(io.DisplaySize, new Vector2(230, 82), new Vector2(960, 772), true);
            ImGuiNET.ImGui.Render();
        }
        DetailFrame(new Vector2(-1), false);
        paletteType.GetField("_selected", privateStatic)!.SetValue(null, "ExampleCube");
        for (var y = 680; y <= 795 && detailWorld.Details.Count == 0; y += 3)
        {
            DetailFrame(new Vector2(20, y), true);
            DetailFrame(new Vector2(20, y), false);
        }
        Check(detailWorld.Details.Count == 1 && ProjectShockGLEditor.Selection.Detail != null &&
              detailWorld.GetAllObjects(true).Length == 0);
        var id = detailWorld.Details[0].Id;
        ProjectShockGLEditor.UndoRedo.Undo(); Check(detailWorld.Details.Count == 0);
        ProjectShockGLEditor.UndoRedo.Redo(); Check(detailWorld.Details[0].Id == id);
        for (var y = 115; y <= 350 && detailWorld.Details.Count == 1; y += 3)
        {
            DetailFrame(new Vector2(1060, y), true);
            DetailFrame(new Vector2(1060, y), false);
        }
        Check(detailWorld.Details.Count == 2 && ProjectShockGLEditor.Selection.Detail!.Id != id);
        var copyId = ProjectShockGLEditor.Selection.Detail!.Id;
        ProjectShockGLEditor.UndoRedo.Undo();
        Check(detailWorld.Details.Count == 1 && ProjectShockGLEditor.Selection.Detail!.Id == id);
        ProjectShockGLEditor.UndoRedo.Redo();
        Check(ProjectShockGLEditor.Selection.Detail!.Id == copyId);
        for (var y = 115; y <= 350 && detailWorld.Details.Count == 2; y += 3)
        {
            DetailFrame(new Vector2(1150, y), true);
            DetailFrame(new Vector2(1150, y), false);
        }
        Check(detailWorld.Details.Count == 1 && detailWorld.Details[0].Id == id);
        ProjectShockGLEditor.UndoRedo.Undo();
        Check(detailWorld.Details.Count == 2 && ProjectShockGLEditor.Selection.Detail!.Id == copyId);
        ProjectShockGLEditor.UndoRedo.Undo();
        Check(detailWorld.Details.Count == 1 && ProjectShockGLEditor.Selection.Detail!.Id == id);
        Camera.Active.Transform.Position = new Vector3(0, 0, 6);
        Camera.Active.Transform.Rotation = Quaternion.Identity;
        Console.WriteLine("PASS: Real Detail Inspector Duplicate/Delete buttons use atomic Undo/Redo with stable restored IDs");
        ProjectShockGLEditor.UndoRedo.Clear();
        ProjectShockGLEditor.ObjectManipulator.Mode = ProjectShockGLEditor.ManipulationMode.Move;
        ProjectShockGLEditor.ObjectManipulator.Local = false;
        ProjectShockGLEditor.ObjectManipulator.SnapEnabled = false;
        var size = io.DisplaySize;
        var vp = Camera.Active.GetViewMatrix() * Camera.Active.GetProjectionMatrix();
        var length = 2 * 6 * MathF.Tan(Camera.Active.Fov * .5f) * 90 / size.Y;
        ProjectShockGLEditor.GizmoMath.Project(Vector3.UnitX * length * .65f, vp, size, out var start);
        ProjectShockGLEditor.GizmoMath.Project(Vector3.UnitX * (length * .65f + .5f), vp, size, out var end);
        DetailFrame(start, false);
        DetailFrame(start, true);
        Check(ProjectShockGLEditor.ObjectManipulator.IsDragging);
        DetailFrame(end, true);
        DetailFrame(end, false);
        Check(MathF.Abs(detailWorld.Details[0].Position.X - .5f) < .015f); // ImGui quantizes mouse positions to pixels.
        ProjectShockGLEditor.UndoRedo.Undo();
        Check(detailWorld.Details[0].Position == Vector3.Zero && !ProjectShockGLEditor.UndoRedo.CanUndo);
        ProjectShockGLEditor.UndoRedo.Redo();
        ProjectShockGLEditor.UndoRedo.Undo();
        DetailFrame(start, false); DetailFrame(start, true); DetailFrame(end, true);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, true);
        DetailFrame(end, true);
        io.AddKeyEvent(ImGuiNET.ImGuiKey.Escape, false);
        DetailFrame(end, false);
        Check(!ProjectShockGLEditor.ObjectManipulator.IsDragging && detailWorld.Details[0].Position == Vector3.Zero);
        Check(ImGuiNET.ImGui.GetDrawData().TotalVtxCount > 0);
        detailWorld.Update(0, simulate: false);
        gl.Viewport(0, 0, 640, 240);
        graphics.BeginDraw(Camera.Active);
        var detailQueue = new RenderQueue();
        detailWorld.Render(detailQueue, false);
        detailQueue.Flush(graphics);
        graphics.EndDraw();
        Check(graphics.Statistics.DrawCalls == 1 && gl.GetError() == GLEnum.NoError);
        var map = Path.Combine(AppContext.BaseDirectory, "DetailEditorRoundtrip.map");
        ProjectShockGLEditor.EditorLevelManager.SaveCurrentLevel(map);
        ProjectShockGLEditor.EditorLevelManager.OpenLevel(map);
        Check(detailWorld.Details.Single().Id == id);
        File.Delete(map);
        Console.WriteLine("PASS: Real Details Palette creates a GPU-backed placement, Inspector draws and mouse gizmo supports undo, redo and Escape");
    }
    finally
    {
        ProjectShockGLEditor.ObjectManipulator.Cancel();
        ProjectShockGLEditor.Selection.Object = null;
        ProjectShockGLEditor.UndoRedo.Clear();
        LevelManager.Unload();
        editorProperty.SetValue(null, detailEditorBefore);
        detailCamera.Apply(Camera.Active.Transform);
        Camera.Active.AspectRatio = detailAspect;
        Camera.Active.Orthographic = detailOrtho;
        ImGuiNET.ImGui.DestroyContext(detailContext);
    }
    var slicePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bmp");
    var sliceClear = Camera.Active.ClearColor;
    var sliceCanvas = new Canvas();
    sliceCanvas.Resize(new Vector2(640, 240));
    try
    {
        // 12x12 BMP with a unique color per texel, region (2,2,8,8).
        using (var writer = new BinaryWriter(File.Create(slicePath)))
        {
            writer.Write((ushort)0x4d42); writer.Write(54 + 12 * 12 * 3);
            writer.Write(0); writer.Write(54); writer.Write(40); writer.Write(12); writer.Write(12);
            writer.Write((ushort)1); writer.Write((ushort)24); writer.Write(0);
            writer.Write(12 * 12 * 3); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (var y = 11; y >= 0; y--)
            for (var x = 0; x < 12; x++)
            {
                writer.Write((byte)100); writer.Write((byte)(30 + y * 15)); writer.Write((byte)(30 + x * 15));
            }
        }
        using var sliceTexture = Texture.Load(gl, slicePath);
        var image = sliceCanvas.Add<Image>();
        image.Region = new TextureRegion(sliceTexture, new Vector4(2f / 12, 2f / 12, 8f / 12, 8f / 12));
        image.Rect.Position = new Vector2(10);
        image.Rect.Size = new Vector2(37, 33);
        image.DrawMode = ImageDrawMode.Sliced;
        image.Border = new Vector4(2);
        image.BorderScale = 2;
        image.EdgeMode = image.CenterMode = ImageSliceMode.Tile;
        Camera.Active.ClearColor = Color.Black;
        gl.Viewport(0, 0, 640, 240);
        byte[] SlicePixel(int x, int y)
        {
            var pixel = new byte[4];
            unsafe { fixed (byte* data = pixel) gl.ReadPixels(10 + x, 239 - 10 - y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            return pixel;
        }
        void DrawSlice(bool clip = false)
        {
            graphics.BeginDraw(Camera.Active);
            graphics.BeginUI(sliceCanvas.Size);
            if (clip)
            {
                typeof(Graphics).GetMethod("ClipUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(graphics, [new Vector2(10), new Vector2(20, 33)]);
            }
            sliceCanvas.Draw(graphics);
            typeof(Graphics).GetProperty("UIClip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(graphics, null);
            graphics.EndUI();
            graphics.EndDraw();
            gl.Finish();
        }
        int Texel(int pixel, int length) => pixel < 4 ? pixel / 2 :
            pixel >= length - 4 ? 6 + (pixel - length + 4) / 2 : 2 + (pixel - 4) % 8 / 2;
        DrawSlice();
        foreach (var y in new[] { 0, 3, 4, 11, 12, 27, 28, 29, 32 })
        foreach (var x in new[] { 0, 3, 4, 11, 12, 27, 28, 32, 33, 36 })
        {
            var pixel = SlicePixel(x, y);
            Check(Math.Abs(pixel[0] - (30 + (2 + Texel(x, 37)) * 15)) < 3 &&
                Math.Abs(pixel[1] - (30 + (2 + Texel(y, 33)) * 15)) < 3);
        }
        image.FillCenter = false;
        DrawSlice();
        Check(SlicePixel(15, 15).Take(3).All(c => c < 3));
        image.FillCenter = true;
        DrawSlice(clip: true);
        Check(SlicePixel(25, 1).Take(3).All(c => c < 3) && SlicePixel(15, 1)[0] > 30);
        Check(gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Nine-slice atlas renders corners, repeated edges/center, partial tiles, empty center and parent clipping");
    }
    finally
    {
        sliceCanvas.Destroy();
        Camera.Active.ClearColor = sliceClear;
        File.Delete(slicePath);
    }
    var captureWorld = new World();
    var captureEnvironment = ProjectShockGL.Environment.Capture();
    var captureActive = Camera.Active;
    var captureEditor = Application.IsEditor;
    var capturePostEnabled = PostProcessSettings.Enabled;
    using var captureQuad = new Mesh(gl,
    [
        -.5f, -.5f, 0, 0, 0, 1, 0, 1,
         .5f, -.5f, 0, 0, 0, 1, 1, 1,
         .5f,  .5f, 0, 0, 0, 1, 1, 0,
        -.5f,  .5f, 0, 0, 0, 1, 0, 0
    ], [0, 1, 2, 0, 2, 3]);
    try
    {
        editorProperty.SetValue(null, false);
        ProjectShockGL.Environment.IsFogEnabled = false;
        ProjectShockGL.Environment.SkyColorEnabled = false;
        PostProcessSettings.Enabled = true; // Capture bypasses global postprocessing.
        var cameraOwner = captureWorld.Spawn<CaptureProbe>();
        var captureEntity = (ProjectShockGL.Placeables.CameraCaptureEntity)ProjectShockGL.Core.Entity.Create("Placeables/CameraCapture", captureWorld)!;
        var capture = captureEntity.Capture;
        captureEntity.Width = 64;
        captureEntity.Height = 32;
        capture.Camera.Orthographic = true;
        capture.Camera.OrthoSize = 4;
        capture.Camera.AspectRatio = 7;
        capture.Camera.ClearColor = Color.Black;
        var target = capture.Target;
        var targetTexture = capture.Texture;
        var targetHandle = targetTexture.Handle;
        Check(ReferenceEquals(target.Texture, targetTexture) && target.Width == 64 && target.Height == 32);
        ProjectShockGL.Core.Object ColoredQuad(Vector3 position, Color color, Texture? texture = null)
        {
            var obj = captureWorld.Spawn<ProjectShockGL.Core.Object>();
            obj.Transform.Position = position;
            var mesh = obj.AddComponent<ProjectShockGL.Core.MeshComponent>();
            mesh.Mesh = captureQuad;
            mesh.Texture = texture ?? Content.GetWhitePixel();
            mesh.Color = color;
            mesh.Scale = 1.5f;
            return obj;
        }
        var upper = ColoredQuad(new Vector3(-1, 1, -3), Color.Red);
        ColoredQuad(new Vector3(-1, -1, -3), Color.Blue);
        ColoredQuad(new Vector3(-1, 1, -6), Color.Green); // Depth behind red.
        ColoredQuad(new Vector3(-1, 1, -1), Color.White, targetTexture); // Excluded self-monitor.
        var debug = ColoredQuad(new Vector3(-1, 1, -2), Color.Green);
        debug.GetComponent<ProjectShockGL.Core.MeshComponent>()!.IsDebug = true;
        captureWorld.Update(0, simulate: false);
        var renderCaptures = typeof(World).GetMethod("RenderCameraCaptures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        void CaptureWorld() => renderCaptures.Invoke(captureWorld, null);
        byte[] CapturedPixel(int x, int y)
        {
            graphics.BeginDraw(captureActive);
            graphics.BeginUI(new Vector2(640, 240));
            graphics.DrawUIRect(Vector2.Zero, new Vector2(256, 128), Color.White, targetTexture);
            graphics.EndUI();
            var result = new byte[4];
            unsafe { fixed (byte* data = result) gl.ReadPixels(x, 239 - y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            graphics.EndDraw();
            return result;
        }
        bool Red(byte[] p) => p[0] > 230 && p[1] < 10 && p[2] < 10;
        bool Blue(byte[] p) => p[2] > 230 && p[0] < 10 && p[1] < 10;

        // Separate read/draw FBOs and an unusual viewport must survive capture.
        using var destination = new RenderTexture(gl, 40, 30);
        var fbField = typeof(RenderTexture).GetField("_framebuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var outputFbField = typeof(RenderTexture).GetField("_outputFramebuffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var destinationDraw = (uint)fbField.GetValue(destination)!;
        var destinationRead = (uint)outputFbField.GetValue(destination)!;
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destinationDraw);
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, destinationRead);
        gl.Viewport(3, 4, 20, 18);
        gl.Disable(EnableCap.DepthTest); gl.Enable(EnableCap.ScissorTest);
        gl.DepthMask(false); gl.ColorMask(true, false, true, false);
        CaptureWorld();
        Check(gl.GetInteger(GetPName.DrawFramebufferBinding) == destinationDraw &&
            gl.GetInteger(GetPName.ReadFramebufferBinding) == destinationRead);
        var viewport = new int[4]; gl.GetInteger(GetPName.Viewport, viewport);
        Check(viewport.SequenceEqual(new[] { 3, 4, 20, 18 }) && gl.IsEnabled(EnableCap.ScissorTest) &&
            !gl.IsEnabled(EnableCap.DepthTest) && !gl.GetBoolean(GetPName.DepthWritemask));
        var mask = new bool[4]; gl.GetBoolean(GetPName.ColorWritemask, mask);
        Check(mask.SequenceEqual(new[] { true, false, true, false }));
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Disable(EnableCap.ScissorTest); gl.DepthMask(true); gl.ColorMask(true, true, true, true);
        gl.Viewport(0, 0, 640, 240);

        Check(ReferenceEquals(Camera.Active, captureActive) && capture.Camera.AspectRatio == 7);
        Check(target.Statistics.DrawCalls == 3 && target.Statistics.PostProcessDrawCalls == 0);
        Check(Red(CapturedPixel(96, 32)) && Blue(CapturedPixel(96, 96)));
        using (var monitorCamera = Camera.Create())
        {
            monitorCamera.Orthographic = true;
            monitorCamera.OrthoSize = 4;
            monitorCamera.AspectRatio = 640f / 240;
            graphics.BeginDraw(monitorCamera);
            graphics.DrawMesh(captureQuad, new Vector3(0, 0, -3), Quaternion.Identity,
                new Vector3(32f / 3, 4, 1), targetTexture, Color.White, lit: false);
            var screenPixel = new byte[4];
            unsafe
            {
                fixed (byte* data = screenPixel) gl.ReadPixels(240, 179, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            }
            Check(Red(screenPixel));
            unsafe
            {
                fixed (byte* data = screenPixel) gl.ReadPixels(240, 59, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            }
            Check(Blue(screenPixel));
            graphics.EndDraw();
        }
        Check(cameraOwner.Starts == 0 && cameraOwner.Updates == 0);

        upper.GetComponent<ProjectShockGL.Core.MeshComponent>()!.Color = Color.Green;
        capture.IsEnabled = false; CaptureWorld();
        Check(Red(CapturedPixel(96, 32)));
        capture.IsEnabled = true;
        captureEntity.SetActive(false); CaptureWorld();
        Check(Red(CapturedPixel(96, 32)));
        captureEntity.SetActive(true); CaptureWorld();
        Check(CapturedPixel(96, 32)[1] > 120 && CapturedPixel(96, 32)[0] < 10);

        captureEntity.Width = 80;
        captureEntity.Height = 40;
        captureEntity.Filter = RenderTextureFilter.Linear;
        Check(ReferenceEquals(capture.Texture, targetTexture) && targetTexture.Handle == targetHandle &&
            target.Width == 80 && target.Height == 40 && target.Filter == RenderTextureFilter.Linear);
        CaptureWorld();
        Check(Blue(CapturedPixel(96, 96)));
        Throws<ArgumentOutOfRangeException>(() => target.Resize(0, 40));
        Check(target.Width == 80 && target.Height == 40);
        capture.LocalPosition = new Vector3(100, 0, 0);
        CaptureWorld();
        Check(capture.Camera.Transform.Position.X == 100 && CapturedPixel(96, 32).Take(3).All(c => c < 3));
        capture.LocalPosition = Vector3.Zero;

        var failure = cameraOwner.AddComponent<CaptureFaultComponent>();
        failure.ThrowOnRender = true;
        Throws<System.Reflection.TargetInvocationException>(CaptureWorld);
        Check(ReferenceEquals(Camera.Active, captureActive) && capture.Camera.AspectRatio == 7 &&
            gl.GetInteger(GetPName.DrawFramebufferBinding) == 0);
        failure.ThrowOnRender = false;
        CaptureWorld();
        Check(Blue(CapturedPixel(96, 96)));
        var camera = capture.Camera;
        var targetFramebuffer = (uint)fbField.GetValue(target)!;
        captureWorld.Clear();
        Check(target.IsDisposed && targetTexture.Handle == 0 && camera.IsDisposed &&
            !gl.IsTexture(targetHandle) && !gl.IsFramebuffer(targetFramebuffer));
        Throws<ObjectDisposedException>(() => target.Capture(captureWorld, captureActive));
        Check(gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Camera capture renders a live world to an upright monitor texture, preserves active camera/GL state, resizes and releases on unload");
    }
    finally
    {
        captureWorld.Clear();
        ProjectShockGL.Environment.Apply(captureEnvironment);
        editorProperty.SetValue(null, captureEditor);
        PostProcessSettings.Enabled = capturePostEnabled;
    }
    var monitorWorld = new World();
    var monitorEnvironment = ProjectShockGL.Environment.Capture();
    var monitorActive = Camera.Active;
    var monitorEditor = Application.IsEditor;
    using var monitorView = Camera.Create();
    try
    {
        editorProperty.SetValue(null, false);
        ProjectShockGL.Environment.IsFogEnabled = false;
        ProjectShockGL.Environment.SkyColorEnabled = false;
        monitorView.Orthographic = true;
        monitorView.OrthoSize = 4;
        monitorView.AspectRatio = 640f / 240;
        monitorView.ClearColor = Color.Green;
        var sourceA = monitorWorld.Spawn<ProjectShockGL.Placeables.CameraCaptureEntity>();
        sourceA.Name = "CameraA"; sourceA.Transform.Position = new Vector3(1000, 0, 0);
        sourceA.Camera.ClearColor = Color.Red; sourceA.Capture.SetResolution(64, 32);
        var sourceB = monitorWorld.Spawn<ProjectShockGL.Placeables.CameraCaptureEntity>();
        sourceB.Name = "CameraB"; sourceB.Transform.Position = new Vector3(1000, 0, 0);
        sourceB.Camera.ClearColor = Color.Blue; sourceB.Capture.SetResolution(32, 32);
        var ordinary = monitorWorld.Spawn<ProjectShockGL.Placeables.CameraEntity>(); ordinary.Name = "Ordinary";
        var monitor = (ProjectShockGL.Placeables.Monitor)ProjectShockGL.Core.Entity.Create("Placeables/Monitor", monitorWorld)!;
        monitor.Transform.Position = new Vector3(0, 0, -3);
        monitor.Transform.Scale = new Vector3(2, 1, 1);
        monitor.CameraName = "CameraA";
        monitorWorld.Update(0); // Start resolves authored CameraName after all objects have been created.
        typeof(World).GetMethod("RenderCameraCaptures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(monitorWorld, null);
        Check(ReferenceEquals(Camera.Active, monitorActive));
        var queue = new RenderQueue();
        byte[] DrawMonitor(int x = 320)
        {
            graphics.BeginDraw(monitorView);
            monitorWorld.Render(queue, false);
            queue.Flush(graphics);
            var pixel = new byte[4];
            unsafe { fixed (byte* data = pixel) gl.ReadPixels(x, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            graphics.EndDraw();
            return pixel;
        }
        bool Black(byte[] p) => p.Take(3).All(c => c < 3);
        Check(DrawMonitor()[0] > 240 && DrawMonitor()[2] < 3);
        Check(ReferenceEquals(monitor.Screen.Texture, sourceA.Texture));
        var outside = DrawMonitor(400);
        Check(outside[1] > 100 && outside[0] < 3);
        sourceA.Capture.SetResolution(128, 128);
        sourceA.Capture.Target.Capture(monitorWorld, sourceA.Camera);
        Check(DrawMonitor()[0] > 240 && DrawMonitor(400)[0] < 3 && monitor.Transform.Scale == new Vector3(2, 1, 1));
        monitor.Transform.Scale = new Vector3(4, 2, 1);
        Check(DrawMonitor(400)[0] > 240);
        monitor.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        Check(DrawMonitor()[1] > 100 && DrawMonitor()[0] < 3); // Back side is culled.
        monitor.Transform.Rotation = Quaternion.Identity;

        monitor.Setup("Missing");
        Check(Black(DrawMonitor()));
        monitor.Setup("Ordinary");
        Check(Black(DrawMonitor()));
        monitor.Setup("");
        Check(Black(DrawMonitor()));
        InputInvoker.Invoke(monitor, "Setup", "CameraB");
        Check(DrawMonitor()[2] > 240 && ReferenceEquals(monitor.Screen.Texture, sourceB.Texture));
        var textureB = sourceB.Texture;
        monitorWorld.Destroy(sourceB);
        Check(Black(DrawMonitor()) && textureB.Handle == 0);
        monitor.CameraName = "CameraA"; // Inspector-style change also updates the editor preview.
        Check(DrawMonitor()[0] > 240);
        var textureA = sourceA.Texture;
        var quad = monitor.Screen.Mesh!;
        monitorWorld.Destroy(monitor);
        Check(textureA.Handle != 0 && gl.IsTexture(textureA.Handle) && !quad.IsDisposed);
        Check(gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Monitor renders a single-sided Unlit quad, scales independently, switches cameras via Setup/Input and shows black for missing/destroyed targets");
    }
    finally
    {
        monitorWorld.Clear();
        ProjectShockGL.Environment.Apply(monitorEnvironment);
        editorProperty.SetValue(null, monitorEditor);
    }
    var gridEnvironment = ProjectShockGL.Environment.Capture();
    var previousGridShow = ProjectShockGLEditor.SceneGrid.Show;
    var previousGridStep = ProjectShockGLEditor.SceneGrid.Step;
    using var gridCamera = Camera.Create();
    using (var gridRenderer = new Renderer(gl, 640, 240))
    {
        var gridGraphics = new Graphics(gridRenderer);
        try
        {
            ProjectShockGLEditor.SceneGrid.Show = true;
            ProjectShockGLEditor.SceneGrid.Step = 1;
            ProjectShockGL.Environment.SkyColorEnabled = false;
            ProjectShockGL.Environment.IsFogEnabled = false;
            gridCamera.Orthographic = true;
            gridCamera.OrthoSize = 8;
            gridCamera.AspectRatio = 640f / 240;
            gridCamera.ClearColor = Color.Black;
            gridCamera.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            byte[] GridPixel()
            {
                var pixel = new byte[4];
                unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                return pixel;
            }
            byte[] DrawGridAt(float height)
            {
                gridCamera.Transform.Position = new Vector3(2000, height, 2000);
                gridGraphics.BeginDraw(gridCamera);
                ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
                Check(gridGraphics.Statistics.DrawCalls == 1);
                var pixel = GridPixel();
                gridGraphics.EndDraw();
                return pixel;
            }
            var near = DrawGridAt(10);
            Check(near[0] > 15 && Math.Abs(near[0] - near[1]) < 2);
            ProjectShockGL.Environment.IsFogEnabled = true;
            ProjectShockGL.Environment.FogColor = Color.Red;
            ProjectShockGL.Environment.FogNear = 0;
            ProjectShockGL.Environment.FogFar = 1;
            Check(DrawGridAt(10).SequenceEqual(near));
            var far = DrawGridAt(350);
            var vanished = DrawGridAt(600);
            Check(far[0] > 0 && far[0] < near[0] && vanished.Take(3).All(c => c < 2));

            byte[] FineGridImage(float step, float viewSize)
            {
                ProjectShockGLEditor.SceneGrid.Step = step;
                gridCamera.OrthoSize = viewSize;
                gridCamera.Transform.Position = new Vector3(10.12345f, 10, 20.23456f);
                gridGraphics.BeginDraw(gridCamera);
                ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
                var pixels = new byte[640 * 240 * 4];
                unsafe { fixed (byte* data = pixels) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                Check(gridGraphics.Statistics.DrawCalls == 1);
                gridGraphics.EndDraw();
                return pixels;
            }
            int GridRuns(byte[] pixels)
            {
                var count = 0; var inside = false;
                for (var x = 120; x < 520; x++)
                {
                    var line = pixels[(120 * 640 + x) * 4] > 5;
                    if (line && !inside) count++;
                    inside = line;
                }
                return count;
            }
            double GridMean(byte[] pixels)
            {
                long sum = 0;
                for (var i = 0; i < pixels.Length; i += 4) sum += pixels[i];
                return (double)sum / (640 * 240);
            }
            var fineGrid = FineGridImage(.01f, .16f);
            var widerStep = FineGridImage(.02f, .16f);
            var proportionalGrid = FineGridImage(.02f, .32f);
            var partialGrid = FineGridImage(.01f, 1.2f);
            var unresolvedGrid = FineGridImage(.01f, 3.2f);
            Check(GridRuns(fineGrid) is >= 24 and <= 29);
            Check(GridRuns(widerStep) is >= 12 and <= 15);
            var fineMean = GridMean(fineGrid);
            Check(fineMean > 1 && GridMean(proportionalGrid) > fineMean * .8 && GridMean(proportionalGrid) < fineMean * 1.2);
            Check(GridMean(partialGrid) > fineMean * .2 && GridMean(partialGrid) < fineMean * .9);
            Check(GridMean(unresolvedGrid) == 0 && GridMean(FineGridImage(.1f, 3.2f)) > 1);
            Check(FineGridImage(.001f, .16f).SequenceEqual(fineGrid));
            ProjectShockGLEditor.SceneGrid.Step = 1;
            gridCamera.OrthoSize = 8;
            Console.WriteLine("PASS: Grid step 0.01 is visible up close, line width scales with cells and subpixel cells fade without a solid fill");
            // A shallow perspective is the editor's normal case, unlike the overhead test.
            gridCamera.Orthographic = false;
            gridCamera.Transform.Position = new Vector3(0, 4, 8);
            gridCamera.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, .06f);
            gridGraphics.BeginDraw(gridCamera);
            ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
            var oblique = new byte[640 * 240 * 4];
            unsafe { fixed (byte* data = oblique) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }

            var farAxisClip = Vector4.Transform(new Vector4(0, 0, -142, 1),
                gridCamera.GetViewMatrix() * gridCamera.GetProjectionMatrix());
            var farAxisY = (int)((farAxisClip.Y / farAxisClip.W + 1) * 120);
            var farAxisBrightness = 0;
            for (var y = Math.Max(0, farAxisY - 1); y <= Math.Min(239, farAxisY + 1); y++)
            for (var x = 318; x <= 321; x++)
                farAxisBrightness = Math.Max(farAxisBrightness, oblique[(y * 640 + x) * 4]);

            Check(farAxisBrightness < 25); // No bright axis streak after the minor grid becomes subpixel.
            gridGraphics.EndDraw();
            gridCamera.Orthographic = true;
            gridCamera.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            gridCamera.Transform.Position = new Vector3(2000, 10, 2000);
            gridGraphics.BeginDraw(gridCamera);
            ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
            gridGraphics.DrawQuad(new Vector3(2000, 1, 2000), gridCamera.Transform.Rotation,
                new Vector2(2), Content.GetWhitePixel(), Color.White);
            var foggedWorld = GridPixel();
            Check(foggedWorld[0] > 240 && foggedWorld[1] < 3 && ProjectShockGL.Environment.IsFogEnabled);
            gridGraphics.EndDraw();

            ProjectShockGL.Environment.IsFogEnabled = false;
            gridGraphics.BeginDraw(gridCamera);
            gridGraphics.DrawQuad(new Vector3(2000, 2, 2000), gridCamera.Transform.Rotation,
                new Vector2(2), Content.GetWhitePixel(), Color.Blue);
            ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
            Check(GridPixel()[2] > 240 && GridPixel()[0] < 3);
            gridGraphics.EndDraw();

            gridGraphics.BeginDraw(gridCamera);
            gl.Disable(EnableCap.DepthTest);
            gl.Enable(EnableCap.CullFace);
            gl.Disable(EnableCap.Blend);
            gl.DepthMask(true);
            gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.DstAlpha, BlendingFactor.OneMinusDstAlpha);
            gl.BlendEquationSeparate(BlendEquationModeEXT.FuncReverseSubtract, BlendEquationModeEXT.Max);
            var stateProgram = gl.GetInteger(GetPName.CurrentProgram);
            ProjectShockGLEditor.SceneGrid.Render(gridGraphics);
            Check(!gl.IsEnabled(EnableCap.DepthTest) && gl.IsEnabled(EnableCap.CullFace) &&
                !gl.IsEnabled(EnableCap.Blend) && gl.GetBoolean(GetPName.DepthWritemask));
            Check(gl.GetInteger(GetPName.BlendSrcRgb) == (int)BlendingFactor.One &&
                gl.GetInteger(GetPName.BlendDstRgb) == (int)BlendingFactor.Zero &&
                gl.GetInteger(GetPName.BlendSrcAlpha) == (int)BlendingFactor.DstAlpha &&
                gl.GetInteger(GetPName.BlendEquationRgb) == (int)BlendEquationModeEXT.FuncReverseSubtract &&
                gl.GetInteger(GetPName.BlendEquationAlpha) == (int)BlendEquationModeEXT.Max &&
                gl.GetInteger(GetPName.CurrentProgram) == stateProgram);
            gl.Enable(EnableCap.DepthTest); gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            gridGraphics.EndDraw();
            var ownedGrid = typeof(Renderer).GetField("_gridRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(gridRenderer)!;
            var gridProgram = (ShaderProgram)ownedGrid.GetType().GetField("_program", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ownedGrid)!;
            var handle = gridProgram.Handle;
            gridRenderer.Dispose();
            Check(gridProgram.Handle == 0 && !gl.IsProgram(handle) && gl.GetError() == GLEnum.NoError);
            Console.WriteLine("PASS: Editor grid ignores fog, follows distant cameras, fades by distance, uses one draw and restores depth/blending state");
        }
        finally
        {
            ProjectShockGL.Environment.Apply(gridEnvironment);
            ProjectShockGLEditor.SceneGrid.Show = previousGridShow;
            ProjectShockGLEditor.SceneGrid.Step = previousGridStep;
        }
    }
    using (var viewRenderer = new Renderer(gl, 640, 240))
    using (var viewCamera = Camera.Create())
    {
        var viewGraphics = new Graphics(viewRenderer);
        var viewEnvironment = ProjectShockGL.Environment.Capture();
        try
        {
            ProjectShockGL.Environment.SkyColorEnabled = false;
            viewCamera.ClearColor = Color.Black;
            viewCamera.Orthographic = true;
            viewCamera.OrthoSize = 8;
            viewCamera.AspectRatio = 640f / 240;
            foreach (var plane in new[] { ProjectShockGL.Rendering.Plane.Xz, ProjectShockGL.Rendering.Plane.Xy, ProjectShockGL.Rendering.Plane.Yz })
            {
                viewCamera.Transform.Rotation = plane == ProjectShockGL.Rendering.Plane.Xz
                    ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2)
                    : plane == ProjectShockGL.Rendering.Plane.Yz
                        ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) : Quaternion.Identity;
                viewCamera.Transform.Position = -viewCamera.Transform.Forward * 500;
                viewGraphics.BeginDraw(viewCamera);
                viewGraphics.DrawGrid(1, 500, 50, 500, plane, planarFade: true);
                var image = new byte[640 * 240 * 4];
                unsafe { fixed (byte* data = image) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                Check(image.Where((_, index) => index % 4 == 0).Count(value => value > 5) > 1000);
                Check(viewGraphics.Statistics.DrawCalls == 1 && gl.GetError() == GLEnum.NoError);
                viewGraphics.EndDraw();
            }
            Console.WriteLine("PASS: Orthographic grid renders in XZ, XY and ZY planes without fading from camera stand-off distance");
        }
        finally { ProjectShockGL.Environment.Apply(viewEnvironment); }
    }
    var boundsSettingsPath = Path.Combine(AppContext.BaseDirectory, "GameSettings.ini");
    var boundsSettings = File.ReadAllText(boundsSettingsPath);
    var boundsEnvironment = ProjectShockGL.Environment.Capture();
    var boundsPostEnabled = PostProcessSettings.Enabled;
    var boundsPostShader = PostProcessSettings.Shader;
    var boundsFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var boundsParameters = (ShaderParameters)typeof(ShaderParameters).GetMethod("Snapshot", boundsFlags)!.Invoke(PostProcessSettings.Parameters, null)!;
    try
    {
        ProjectShockGL.Environment.IsFogEnabled = false;
        ProjectShockGL.Environment.SkyColorEnabled = false;
        PostProcessSettings.Shader = null;
        PostProcessSettings.Parameters.Set("uVignetteStrength", 1f);
        foreach (var scale in new[] { 1f, 0.5f, 0.1f })
        foreach (var effects in new[] { false, true })
        foreach (var filter in new[] { "Nearest", "Linear" })
        {
            File.WriteAllText(boundsSettingsPath, boundsSettings + "\n[Rendering]\nResolutionScale=" +
                scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\nResolutionFilter=" + filter + "\n");
            Settings.Initialize();
            PostProcessSettings.Enabled = effects;
            using var boundsRenderer = new Renderer(gl, 640, 240);
            var boundsGraphics = new Graphics(boundsRenderer);
            using var boundsCamera = Camera.Create();
            boundsCamera.Orthographic = true;
            boundsCamera.OrthoSize = 6;
            boundsCamera.AspectRatio = 640f / 240;
            var model = Matrix4x4.CreateTranslation(0, 0, -5);
            void BoundsWorld()
            {
                boundsGraphics.BeginDraw(boundsCamera);
                boundsGraphics.BeginWorld();
                boundsGraphics.DrawMesh(Content.LoadMesh("Cube")!, new Vector3(0, 0, -5), Quaternion.Identity, Vector3.One,
                    Content.GetWhitePixel(), new Color(0.1f, 0.1f, 0.1f), surfaceMode: SurfaceMode.Opaque);
                boundsGraphics.DrawQuad(new Vector3(2, 0, -2), Quaternion.Identity, new Vector2(4, 8),
                    Content.GetWhitePixel(), Color.Black, surfaceMode: SurfaceMode.Opaque);
                boundsGraphics.EndWorld();
            }
            byte[] BoundsImage()
            {
                gl.Finish();
                var result = new byte[640 * 240 * 4];
                unsafe { fixed (byte* data = result) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
                return result;
            }
            int Brightness(byte[] pixels, int x) => Enumerable.Range(x - 3, 7).Max(column => (int)pixels[(120 * 640 + column) * 4]);
            BoundsWorld();
            boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, model, new Vector2(640, 240), Vector2.Zero, new Vector2(640, 240));
            var image = BoundsImage();
            Check(Brightness(image, 280) > 230 && Brightness(image, 360) is > 20 and < 150);
            var stroke = Enumerable.Range(276, 9).Count(column => image[(120 * 640 + column) * 4] > 230);
            Check(stroke is >= 1 and <= 2);
            Check(boundsGraphics.Statistics.WorldWidth == Math.Max(1, (int)MathF.Round(640 * scale)));
            Check(gl.GetBoolean(GetPName.DepthWritemask));
            Check(gl.GetError() == GLEnum.NoError);
            boundsGraphics.EndDraw();

            // Clipping uses logical window coordinates even when the framebuffer resolution differs.
            BoundsWorld();
            boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, model, new Vector2(320, 120),
                Vector2.Zero, new Vector2(160, 120));
            image = BoundsImage();
            Check(Brightness(image, 280) > 230 && Brightness(image, 360) < 5);
            boundsGraphics.EndDraw();

            // Preserve all depth, blend and scissor settings for later debug/UI drawing.
            BoundsWorld();
            gl.Disable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Gequal);
            gl.DepthMask(true);
            gl.Enable(EnableCap.CullFace);
            gl.Disable(EnableCap.Blend);
            gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.DstAlpha, BlendingFactor.One);
            gl.BlendEquationSeparate(BlendEquationModeEXT.Max, BlendEquationModeEXT.Min);
            gl.Enable(EnableCap.ScissorTest);
            gl.Scissor(10, 20, 600, 200);
            var depthBefore = new float[1];
            unsafe { fixed (float* data = depthBefore) gl.ReadPixels(280, 120, 1, 1, PixelFormat.DepthComponent, PixelType.Float, data); }
            boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, model, new Vector2(640, 240), Vector2.Zero, new Vector2(640, 240));
            var depthAfter = new float[1];
            unsafe { fixed (float* data = depthAfter) gl.ReadPixels(280, 120, 1, 1, PixelFormat.DepthComponent, PixelType.Float, data); }
            var box = new int[4];
            gl.GetInteger(GetPName.ScissorBox, box);
            Check(depthBefore[0] == depthAfter[0] && !gl.IsEnabled(EnableCap.DepthTest) && gl.GetBoolean(GetPName.DepthWritemask));
            Check(gl.GetInteger(GetPName.DepthFunc) == (int)DepthFunction.Gequal &&
                !gl.IsEnabled(EnableCap.Blend) && gl.IsEnabled(EnableCap.CullFace) &&
                gl.GetInteger(GetPName.BlendSrcAlpha) == (int)BlendingFactor.DstAlpha &&
                gl.GetInteger(GetPName.BlendEquationRgb) == (int)BlendEquationModeEXT.Max &&
                gl.IsEnabled(EnableCap.ScissorTest) && box.SequenceEqual(new[] { 10, 20, 600, 200 }));
            gl.Disable(EnableCap.ScissorTest);
            gl.Enable(EnableCap.DepthTest); gl.DepthFunc(DepthFunction.Less);
            gl.Enable(EnableCap.Blend); gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            boundsGraphics.EndDraw();
            if (scale == 1 && !effects && filter == "Nearest")
            {
                boundsCamera.Orthographic = false;
                BoundsWorld();
                boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, model, new Vector2(640, 240), Vector2.Zero, new Vector2(640, 240));
                image = BoundsImage();
                var projection = boundsCamera.GetViewMatrix() * boundsCamera.GetProjectionMatrix();
                Check(ProjectShockGLEditor.GizmoMath.Project(new Vector3(-1, 0, -4), projection, new Vector2(640, 240), out var left));
                Check(ProjectShockGLEditor.GizmoMath.Project(new Vector3(1, 0, -4), projection, new Vector2(640, 240), out var right));
                Check(Brightness(image, (int)left.X) > 230 && Brightness(image, (int)right.X) is > 20 and < 150);
                boundsGraphics.EndDraw();
                BoundsWorld();
                var beforeBehind = BoundsImage();
                boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, Matrix4x4.CreateTranslation(0, 0, 5),
                    new Vector2(640, 240), Vector2.Zero, new Vector2(640, 240));
                Check(beforeBehind.SequenceEqual(BoundsImage()));
                boundsGraphics.DrawSelectionBounds(new Vector3(-0.05f, -0.05f, -0.5f), new Vector3(0.05f, 0.05f, 0.5f),
                    Matrix4x4.CreateTranslation(0, 0, -0.4f), new Vector2(640, 240), Vector2.Zero, new Vector2(640, 240));
                Check(BoundsImage().Where((_, index) => index % 4 == 0).Any(value => value > 230) && gl.GetError() == GLEnum.NoError);
                boundsGraphics.EndDraw();
                boundsRenderer.OnResize(320, 120);
                boundsGraphics.BeginDraw(boundsCamera);
                boundsGraphics.BeginWorld();
                boundsGraphics.EndWorld();
                boundsGraphics.DrawSelectionBounds(-Vector3.One, Vector3.One, model,
                    new Vector2(320, 120), Vector2.Zero, new Vector2(320, 120));
                Check(gl.GetError() == GLEnum.NoError);
                boundsGraphics.EndDraw();
            }
            var owned = typeof(Renderer).GetField("_selectionBounds", boundsFlags)!.GetValue(boundsRenderer)!;
            var vao = (uint)owned.GetType().GetField("_vao", boundsFlags)!.GetValue(owned)!;
            var vbo = (uint)owned.GetType().GetField("_vbo", boundsFlags)!.GetValue(owned)!;
            Check(gl.IsVertexArray(vao) && gl.IsBuffer(vbo));
            boundsRenderer.Dispose();
            Check(!gl.IsVertexArray(vao) && !gl.IsBuffer(vbo) && gl.GetError() == GLEnum.NoError);
        }
        Console.WriteLine("PASS: Selection bounds keep pixel width, dim occluded edges, preserve world depth and GL state across resolution scales, filters and postprocessing");
    }
    finally
    {
        File.WriteAllText(boundsSettingsPath, boundsSettings);
        Settings.Initialize();
        ProjectShockGL.Environment.Apply(boundsEnvironment);
        PostProcessSettings.Enabled = boundsPostEnabled;
        PostProcessSettings.Shader = boundsPostShader;
        typeof(ShaderParameters).GetMethod("CopyFrom", boundsFlags)!.Invoke(PostProcessSettings.Parameters, [boundsParameters]);
    }
    var debugDepthWorld = new World();
    var debugFlag = typeof(Application).GetProperty(nameof(Application.IsDebug))!;
    var previousDebugFlag = Application.IsDebug;
    var debugEnvironment = ProjectShockGL.Environment.Capture();
    var gridShow = ProjectShockGLEditor.SceneGrid.Show;
    var gridStep = ProjectShockGLEditor.SceneGrid.Step;
    using var debugCamera = Camera.Create();
    try
    {
        debugFlag.SetValue(null, true);
        ProjectShockGL.Environment.IsFogEnabled = false;
        ProjectShockGL.Environment.SkyColorEnabled = false;
        debugCamera.Orthographic = true;
        debugCamera.OrthoSize = 4;
        debugCamera.AspectRatio = 640f / 240;
        debugCamera.Transform.Position = new Vector3(0, 4, 4);
        debugCamera.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 4);
        var obj = debugDepthWorld.Spawn<DebugEntityProbe>();
        obj.Transform.Position = new Vector3(0, .6f, 0);
        Check(ReferenceEquals(obj.GetComponent<ProjectShockGL.Core.MeshComponent>(), obj.MainMesh));
        obj.Load(new Dictionary<string, object> { ["Mesh"] = "Cube", ["Texture"] = "Default" });
        var normal = new RenderQueue();
        obj.Render(normal, false);
        Check(((IReadOnlyList<RenderCommand>)typeof(RenderQueue).GetProperty("Commands", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(normal)!).Count == 1); // Runtime mesh must not become a debug overlay.
        var debugArguments = new Dictionary<string, object> { ["DebugMesh"] = "Cube", ["DebugBillboard"] = "Editor/Entity" };
        obj.Load(debugArguments);
        obj.Load(debugArguments);
        Check(ReferenceEquals(obj.GetComponent<ProjectShockGL.Core.MeshComponent>(), obj.MainMesh) &&
            !obj.MainMesh.IsDebug && !obj.MainMesh.IgnoreDepth);
        var queue = new RenderQueue();
        obj.Render(queue, true);
        var commands = (IReadOnlyList<RenderCommand>)typeof(RenderQueue).GetProperty("Commands", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(queue)!;
        Check(commands.Count == 2); // Repeated Load reuses the debug components.
        var meshCommand = commands.OfType<MeshCommand>().Single();
        var billboardCommand = commands.OfType<BillboardCommand>().Single();
        Check(meshCommand.IgnoreDepth && billboardCommand.IgnoreDepth);
        meshCommand.Color = billboardCommand.Color = Color.Red;
        meshCommand.Lit = false;
        meshCommand.Texture = billboardCommand.Texture = Content.GetWhitePixel();
        ProjectShockGLEditor.SceneGrid.Show = true;
        ProjectShockGLEditor.SceneGrid.Step = 1;
        var projected = Vector4.Transform(new Vector4(obj.Transform.Position, 1),
            debugCamera.GetViewMatrix() * debugCamera.GetProjectionMatrix());
        var px = (int)((projected.X / projected.W + 1) * 320);
        var py = (int)((projected.Y / projected.W + 1) * 120);
        byte[] Pixel()
        {
            var pixel = new byte[4];
            unsafe { fixed (byte* data = pixel) gl.ReadPixels(px, py, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            return pixel;
        }
        foreach (var command in commands)
        {
            graphics.BeginDraw(debugCamera);
            typeof(ProjectShockGLEditor.Editor).GetMethod("OnRenderBackground",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [graphics]);
            command.Execute(graphics);
            var pixel = Pixel();
            Check(pixel[0] > 240 && pixel[1] < 10 && pixel[2] < 10);
            graphics.EndDraw();

            graphics.BeginDraw(debugCamera);
            var wallPosition = (debugCamera.Transform.Position + obj.Transform.Position) * .5f;
            graphics.DrawQuad(wallPosition, debugCamera.Transform.Rotation, new Vector2(3),
                Content.GetWhitePixel(), Color.Blue);
            command.Execute(graphics);
            pixel = Pixel();
            Check(pixel[0] > 240 && pixel[2] < 10);
            graphics.EndDraw();
        }
        Check(gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Entity debug assets do not replace gameplay meshes and render over walls and the editor background grid");
    }
    finally
    {
        debugDepthWorld.Clear();
        debugFlag.SetValue(null, previousDebugFlag);
        ProjectShockGL.Environment.Apply(debugEnvironment);
        ProjectShockGLEditor.SceneGrid.Show = gridShow;
        ProjectShockGLEditor.SceneGrid.Step = gridStep;
    }
    var linkWorld = new World();
    var linkEditor = Application.IsEditor;
    var linkCamera = ProjectShockGLEditor.TransformState.Capture(Camera.Active.Transform);
    var linkAspect = Camera.Active.AspectRatio;
    var linkOrtho = Camera.Active.Orthographic;
    var linkOrthoSize = Camera.Active.OrthoSize;
    var linkClear = Camera.Active.ClearColor;
    var linkEnvironment = ProjectShockGL.Environment.Capture();
    try
    {
        editorProperty.SetValue(null, true);
        Camera.Active.Transform.Position = Vector3.Zero;
        Camera.Active.Transform.Rotation = Quaternion.Identity;
        Camera.Active.AspectRatio = 640f / 240;
        Camera.Active.Orthographic = true;
        Camera.Active.OrthoSize = 4;
        Camera.Active.ClearColor = Color.Black;
        ProjectShockGL.Environment.IsFogEnabled = false;
        var source = linkWorld.Spawn<ProjectShockGL.Core.Entity>();
        source.Transform.Position = new Vector3(-1, 0, -3);
        var target = linkWorld.Spawn<ProjectShockGL.Core.Object>();
        target.Name = "link-target"; target.Transform.Position = new Vector3(1, 0, -3);
        source.Outputs.Add(new ProjectShockGL.Core.OutputData { TargetName = target.Name });
        source.Outputs.Add(new ProjectShockGL.Core.OutputData { TargetName = target.Name, Event = "Other" });
        var renderLinks = typeof(ProjectShockGLEditor.Editor).Assembly.GetType("ProjectShockGLEditor.OutputLinks")!
            .GetMethod("Render", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        gl.Viewport(0, 0, 640, 240);
        graphics.BeginDraw(Camera.Active);
        gl.Enable(EnableCap.CullFace);
        graphics.DrawQuad(new Vector3(0, 0, -1), Quaternion.Identity, new Vector2(4),
            Content.GetWhitePixel(), Color.Red);
        renderLinks.Invoke(null, [graphics, linkWorld, new ProjectShockGL.Core.Object[] { source }]);
        Check(graphics.Statistics.DrawCalls == 2 && gl.IsEnabled(EnableCap.CullFace) && gl.IsEnabled(EnableCap.DepthTest));
        var pixel = new byte[4];
        unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
        Check(pixel[0] > 220 && pixel[1] > 180 && pixel[2] < 110);
        graphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, new Vector2(4),
            Content.GetWhitePixel(), Color.Blue);
        unsafe { fixed (byte* data = pixel) gl.ReadPixels(320, 120, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
        Check(pixel[0] > 220 && pixel[1] > 180 && pixel[2] < 110);
        graphics.EndDraw();
        editorProperty.SetValue(null, false);
        graphics.BeginDraw(Camera.Active);
        renderLinks.Invoke(null, [graphics, linkWorld, new ProjectShockGL.Core.Object[] { source }]);
        Check(graphics.Statistics.DrawCalls == 0);
        graphics.EndDraw();
        Check(gl.GetError() == GLEnum.NoError);
        Console.WriteLine("PASS: Selected Output links draw yellow through an occluding wall, merge duplicates and preserve depth/culling state");
    }
    finally
    {
        linkWorld.Clear();
        editorProperty.SetValue(null, linkEditor);
        linkCamera.Apply(Camera.Active.Transform);
        Camera.Active.AspectRatio = linkAspect;
        Camera.Active.Orthographic = linkOrtho;
        Camera.Active.OrthoSize = linkOrthoSize;
        Camera.Active.ClearColor = linkClear;
        ProjectShockGL.Environment.Apply(linkEnvironment);
    }
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


public sealed class CaptureProbe : ProjectShockGL.Core.Object
{
    public int Starts, Updates;
    public override void Start() { Starts++; }
    public override void Update(float dt) { Updates++; base.Update(dt); }
}

public sealed class CaptureFaultComponent : ProjectShockGL.Core.RenderComponent
{
    public bool ThrowOnRender;
    public override void Render(RenderQueue queue)
    {
        if (ThrowOnRender) throw new InvalidOperationException("Capture failure");
    }
}
public sealed class DebugEntityProbe : ProjectShockGL.Core.Entity
{
    public ProjectShockGL.Core.MeshComponent MainMesh { get; private set; } = null!;

    public override void Create()
    {
        base.Create();
        MainMesh = AddComponent<ProjectShockGL.Core.MeshComponent>();
    }

    public override void Load(Dictionary<string, object> arguments)
    {
        base.Load(arguments);
        var mesh = GetComponent<ProjectShockGL.Core.MeshComponent>()!;
        if (arguments.TryGetValue("Mesh", out var name)) mesh.Mesh = Content.LoadMesh((string)name);
        if (arguments.TryGetValue("Texture", out var texture)) mesh.Texture = Content.LoadTexture((string)texture);
    }
}
public static class PreviewActionFixture
{
    public static int Calls;
    [UIAction("PreviewTest/Increment")]
    public static void Increment() => Calls++;
}