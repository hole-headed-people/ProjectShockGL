using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Rendering;
using ProjectShockGL.UI;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;

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
    var preview = new RenderPreviewSettings();
    using var renderer = new Renderer(gl, 640, 240, preview);
    var graphics = new Graphics(renderer);
    Camera.Initialize(window);
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
    Check(font.IsDisposed);
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
