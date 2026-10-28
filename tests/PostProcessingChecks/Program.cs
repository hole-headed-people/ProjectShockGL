using System.Numerics;
using System.Reflection;
using ProjectShockGL;
using ProjectShockGL.Core;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Rendering;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
var options = WindowOptions.Default;
options.Size = new Vector2D<int>(640, 240);
options.IsVisible = false;
options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
using var window = Window.Create(options);
window.Initialize();
using var gl = window.CreateOpenGL();
var fixture = Path.Combine(AppContext.BaseDirectory, "ProfileCheckContent");
var profiles = Path.Combine(fixture, "PostProcessProfiles");
Directory.CreateDirectory(profiles);
Directory.CreateDirectory(Path.Combine(fixture, "Shaders"));
foreach (var file in Directory.GetFiles(Path.Combine(root, "ProjectDungeon/Content/Shaders")))
    File.Copy(file, Path.Combine(fixture, "Shaders", Path.GetFileName(file)), overwrite: true);
File.Copy(Path.Combine(root, "ProjectDungeon/Content/PostProcessProfiles/Default.json"),
    Path.Combine(profiles, "Default.json"), overwrite: true);
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Settings.ini"), "[Content]\nCustomPath=" + fixture + "\n");
Settings.Initialize();
Content.Initialize(gl);
Camera.Initialize(window);
Camera.Active.ClearColor = Color.White;
Camera.Active.Orthographic = true;
Camera.Active.OrthoSize = 4;

PostProcessSettings.Enabled = true;
PostProcessSettings.Parameters.Set("uVignetteStrength", 0.8f);
using var renderer = new Renderer(gl, 640, 240);
var graphics = new Graphics(renderer);

Run("Vignette preserves the center, darkens corners and keeps UI/overlays untouched", () =>
{
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    Check(gl.GetInteger(GetPName.DrawFramebufferBinding) != 0);
    graphics.EndWorld();
    Check(gl.GetInteger(GetPName.DrawFramebufferBinding) == 0);
    Check(Pixel(320, 120)[0] > 245 && Pixel(3, 3)[0] < 100);
    graphics.BeginUI(new Vector2(640, 240));
    graphics.DrawUIRect(new Vector2(0, 220), new Vector2(20, 20), Color.Red);
    graphics.EndUI();
    Check(Pixel(5, 5)[0] > 245 && Pixel(5, 5)[1] < 5);
    graphics.DrawQuad(new Vector3(-4.5f, -1.7f, -1), Quaternion.Identity, new Vector2(0.5f),
        Content.GetWhitePixel(), Color.White, ignoreDepth: true);
    Check(Pixel(50, 18)[0] > 245);
    graphics.EndDraw();
});

Run("The depth buffer survives postprocessing for editor grid and selection geometry", () =>
{
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    graphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
        Content.GetWhitePixel(), Color.Red);
    graphics.EndWorld();
    graphics.DrawQuad(new Vector3(0, 0, -3), Quaternion.Identity, Vector2.One,
        Content.GetWhitePixel(), new Color(0, 1, 0));
    Check(Pixel(320, 120)[0] > 245 && Pixel(320, 120)[1] < 5);
    graphics.DrawQuad(new Vector3(0, 0, -1), Quaternion.Identity, Vector2.One,
        Content.GetWhitePixel(), new Color(0, 1, 0));
    Check(Pixel(320, 120)[1] > 245 && Pixel(320, 120)[0] < 5);
});

Run("Postprocessing restores GL state required by UI and ImGui", () =>
{
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    gl.Enable(EnableCap.ScissorTest);
    gl.Scissor(0, 0, 1, 1);
    gl.Disable(EnableCap.DepthTest);
    gl.Enable(EnableCap.CullFace);
    gl.Enable(EnableCap.Blend);
    gl.ActiveTexture(TextureUnit.Texture3);
    var program = gl.GetInteger(GetPName.CurrentProgram);
    var vao = gl.GetInteger(GetPName.VertexArrayBinding);
    graphics.EndWorld();
    Check(gl.IsEnabled(EnableCap.ScissorTest) && !gl.IsEnabled(EnableCap.DepthTest));
    Check(gl.IsEnabled(EnableCap.CullFace) && gl.IsEnabled(EnableCap.Blend));
    Check(gl.GetInteger(GetPName.ActiveTexture) == (int)TextureUnit.Texture3);
    Check(gl.GetInteger(GetPName.CurrentProgram) == program && gl.GetInteger(GetPName.VertexArrayBinding) == vao);
    Check(Pixel(630, 230)[0] < 100); // The composite itself ignores the 1x1 scissor.
    gl.Disable(EnableCap.ScissorTest);
    gl.Enable(EnableCap.DepthTest);
    gl.ActiveTexture(TextureUnit.Texture0);
});

var processor = typeof(Renderer).GetField("_postProcessor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
uint Handle(string name) => (uint)processor.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(processor)!;

Run("Resize replaces complete targets, releases old resources and tolerates zero size", () =>
{
    var oldFramebuffer = Handle("_framebuffer");
    var oldColor = Handle("_color");
    var oldDepth = Handle("_depth");
    renderer.OnResize(320, 120);
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    graphics.EndWorld();
    Check(!gl.IsFramebuffer(oldFramebuffer) && !gl.IsTexture(oldColor) && !gl.IsRenderbuffer(oldDepth));
    Check(gl.IsFramebuffer(Handle("_framebuffer")) && gl.IsTexture(Handle("_color")));
    Check(Pixel(160, 60)[0] > 245 && Pixel(3, 3)[0] < 100);
    var framebuffer = Handle("_framebuffer");
    renderer.OnResize(0, 0);
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    graphics.EndWorld();
    Check(Handle("_framebuffer") == framebuffer && gl.GetInteger(GetPName.DrawFramebufferBinding) == 0);
    renderer.OnResize(640, 240);
});

Run("Disabling the effect and zero strength both reproduce an unchanged image", () =>
{
    PostProcessSettings.Enabled = false;
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    Check(gl.GetInteger(GetPName.DrawFramebufferBinding) == 0);
    graphics.EndWorld();
    Check(Pixel(3, 3)[0] > 245);
    PostProcessSettings.Enabled = true;
    PostProcessSettings.Shader = Content.LoadShaderProgram("PostProcess", "PostProcess");
    PostProcessSettings.Parameters.Set("uVignetteStrength", 0f);
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    graphics.EndWorld();
    Check(Pixel(3, 3)[0] > 245);
    PostProcessSettings.Parameters.Set("uVignetteStrength", 0.8f);
});

Run("Application presents the world before RenderOverlay, RenderUI and AfterRender", () =>
{
    typeof(Application).GetField("_window", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, window);
    typeof(Application).GetField("_graphics", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, graphics);
    var calls = new List<string>();
    void World(Graphics g) { calls.Add("World"); Check(gl.GetInteger(GetPName.DrawFramebufferBinding) != 0); }
    void Overlay(Graphics g) { calls.Add("Overlay"); Check(gl.GetInteger(GetPName.DrawFramebufferBinding) == 0); }
    void UI(Graphics g)
    {
        calls.Add("UI");
        g.BeginUI(new Vector2(640, 240));
        g.DrawUIRect(new Vector2(0, 220), new Vector2(20), Color.Red);
        g.EndUI();
    }
    void Editor(float dt)
    {
        calls.Add("Editor");
        Check(Pixel(5, 5)[0] > 245 && Pixel(5, 5)[1] < 5);
        Check(Pixel(630, 230)[0] < 100);
    }
    Application.Render += World;
    Application.RenderOverlay += Overlay;
    Application.RenderUI += UI;
    Application.AfterRender += Editor;
    try
    {
        typeof(Application).GetMethod("OnRender", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [0d]);
        Check(calls.SequenceEqual(new[] { "World", "Overlay", "UI", "Editor" }));
    }
    finally
    {
        Application.Render -= World;
        Application.RenderOverlay -= Overlay;
        Application.RenderUI -= UI;
        Application.AfterRender -= Editor;
    }
});

Run("Pulse fragment shader accepts per-object parameters with the existing vertex shader", () =>
{
    var pulse = Content.LoadShaderProgram("Unlit", "Pulse");
    var parameters = new ShaderParameters();
    parameters.Set("uPulseColor", new Vector3(0, 1, 0));
    parameters.Set("uPulseSpeed", 0f);
    parameters.Set("uPulseStrength", 1f);
    graphics.BeginDraw(Camera.Active);
    graphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
        Content.GetWhitePixel(), Color.White, shader: pulse, shaderParameters: parameters);
    var pixel = Pixel(320, 120);
    Check(pixel[0] is > 120 and < 135 && pixel[1] > 245 && pixel[2] is > 120 and < 135);
});

Run("Zero grain and aberration preserve the original image and the Default profile keeps their strengths zero", () =>
{
    PostProcessSettings.Apply(new PostProcessProfile { Parameters = new() { ["uVignetteStrength"] = 0 } });
    PostProcessSettings.Enabled = false;
    var original = CapturePost(0, DrawSplit);
    PostProcessSettings.Enabled = true;
    var zeroEffects = CapturePost(10, DrawSplit);
    Check(original.SequenceEqual(zeroEffects));
    PostProcessSettings.Apply("Default");
    CapturePost(0);
    Near(ReadUniform("uGrainStrength")[0], 0);
    Near(ReadUniform("uChromaticAberrationPixels")[0], 0);
    Near(ReadUniform("uGrainSize")[0], 1);
    var defaultImage = CapturePost(1, DrawSplit);
    PostProcessSettings.Apply(new PostProcessProfile { Parameters = new() {
        ["uVignetteStrength"] = 0.45f, ["uVignetteRadius"] = 0.3f, ["uVignetteSoftness"] = 0.5f
    } });
    Check(defaultImage.SequenceEqual(CapturePost(5, DrawSplit)));
});

Run("Film grain is monochrome, balanced, animated at 24 fps and sized in physical pixels", () =>
{
    var oldClear = Camera.Active.ClearColor;
    try
    {
        Camera.Active.ClearColor = new Color(0.5f, 0.5f, 0.5f);
        PostProcessSettings.Apply(new PostProcessProfile { Parameters = new() {
            ["uVignetteStrength"] = 0, ["uGrainStrength"] = 0
        } });
        var original = CapturePost(1);
        PostProcessSettings.Parameters.Set("uGrainStrength", 0.2f);
        var noisy = CapturePost(1);
        Check(noisy.SequenceEqual(CapturePost(1.01f)));
        Check(!noisy.SequenceEqual(CapturePost(1.1f)));
        var changed = 0;
        double difference = 0;
        for (var i = 0; i < noisy.Length; i += 4)
        {
            if (noisy[i] != original[i]) changed++;
            difference += noisy[i] - original[i];
            Check(noisy[i] == noisy[i + 1] && noisy[i] == noisy[i + 2] && noisy[i + 3] == original[i + 3]);
        }
        Check(changed > 50000 && Math.Abs(difference / (640 * 240)) < 1.5);
        PostProcessSettings.Parameters.Set("uGrainSize", 4f);
        var blocks = CapturePost(1);
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++) Check(blocks[(y * 640 + x) * 4] == blocks[0]);
        Check(Enumerable.Range(1, 20).Any(i => blocks[i * 4 * 4] != blocks[0]));
        PostProcessSettings.Parameters.Set("uGrainSize", 0f);
        Check(CapturePost(1).SequenceEqual(noisy)); // Size is clamped to at least one pixel.
    }
    finally { Camera.Active.ClearColor = oldClear; PostProcessSettings.Apply("Default"); }
});

Run("Chromatic aberration splits edge colors in pixel units without wrapping across screen borders", () =>
{
    var oldClear = Camera.Active.ClearColor;
    try
    {
        Camera.Active.ClearColor = Color.Black;
        PostProcessSettings.Apply(new PostProcessProfile { Parameters = new() {
            ["uVignetteStrength"] = 0, ["uChromaticAberrationPixels"] = 0
        } });
        var original = CapturePost(1, DrawSplit);
        PostProcessSettings.Parameters.Set("uChromaticAberrationPixels", 12f);
        var shifted = CapturePost(1, DrawSplit);
        var colored = 0;
        for (var i = 0; i < shifted.Length; i += 4)
        {
            Check(original[i] == original[i + 1] && original[i] == original[i + 2]);
            if (Math.Abs(shifted[i] - shifted[i + 2]) > 4) colored++;
            Check(shifted[i + 3] == original[i + 3]);
        }
        Check(colored > 100);
        var center = (120 * 640 + 320) * 4;
        Check(shifted.AsSpan(center, 4).SequenceEqual(original.AsSpan(center, 4)));
        Check(shifted[(120 * 640) * 4] > 245 && shifted[(120 * 640 + 639) * 4 + 2] < 5);
        int Fringe(byte[] image, int width, int y) => Enumerable.Range(0, width)
            .Count(x => Math.Abs(image[(y * width + x) * 4] - image[(y * width + x) * 4 + 2]) > 4);
        var fullWidth = Fringe(shifted, 640, 120);
        renderer.OnResize(320, 120);
        var half = CapturePost(1, DrawSplit, width: 320, height: 120);
        Check(fullWidth > 0 && Math.Abs(fullWidth - Fringe(half, 320, 60)) <= 2);
    }
    finally { renderer.OnResize(640, 240); Camera.Active.ClearColor = oldClear; PostProcessSettings.Apply("Default"); }
});

Run("Profiles configure both effects while UI remains untouched and switching profiles removes them", () =>
{
    File.WriteAllText(Path.Combine(profiles, "FilmEffects.json"), """
        { "Parameters": { "uVignetteStrength":0, "uGrainStrength":0.3,
          "uGrainSize":2, "uChromaticAberrationPixels":12 } }
        """);
    PostProcessSettings.Apply("FilmEffects");
    var withUI = CapturePost(1, DrawSplit, g => {
        g.BeginUI(new Vector2(640,240));
        g.DrawUIRect(new Vector2(0,220), new Vector2(20), Color.Red);
        g.EndUI();
    });
    for (var y = 0; y < 20; y++)
        for (var x = 0; x < 20; x++)
        {
            var i = (y * 640 + x) * 4;
            Check(withUI[i] == 255 && withUI[i + 1] == 0 && withUI[i + 2] == 0);
        }
    Near(ReadUniform("uGrainStrength")[0], 0.3f);
    Near(ReadUniform("uChromaticAberrationPixels")[0], 12);
    PostProcessSettings.Apply("Default");
    CapturePost(1);
    Near(ReadUniform("uGrainStrength")[0], 0);
    Near(ReadUniform("uChromaticAberrationPixels")[0], 0);
});

Run("JSON profiles load through Content, cache resources and replace old parameters without aliasing", () =>
{
    Check(Content.LoadPostProcessProfile("Missing") == null);
    var profile = Content.LoadPostProcessProfile("Default")!;
    Check(ReferenceEquals(profile, Content.LoadPostProcessProfile("Default")));
    PostProcessSettings.Apply(profile);
    PostProcessSettings.Parameters.Set("uVignetteStrength", 0.9f);
    DrawPost();
    Check(Math.Abs(Convert.ToDouble(profile.Parameters["uVignetteStrength"]) - 0.45) < 0.0001);
    PostProcessSettings.Parameters.Set("uVignetteRadius", 0.9f);
    DrawPost();
    PostProcessSettings.Apply(new PostProcessProfile { Parameters = new() { ["uVignetteStrength"] = 1L } });
    DrawPost();
    Near(ReadUniform("uVignetteStrength")[0], 1);
    Near(ReadUniform("uVignetteRadius")[0], 0.3f);
    Check(Pixel(3, 3)[0] < 20);
    PostProcessSettings.Apply(new PostProcessProfile { Enabled = false });
    DrawPost();
    Check(Pixel(3, 3)[0] > 245);
    PostProcessSettings.Apply("Default");
});

Run("Malformed profile JSON reports its file and never enters the Content cache", () =>
{
    foreach (var (name, json) in new[] {
        ("Empty", ""), ("Array", "[]"), ("Null", "null"),
        ("BadParameters", "{\"Parameters\":null}"),
        ("BadShader", "{\"FragmentShader\":\" \"}"),
        ("Typo", "{\"Parametrs\":{}}"),
        ("Duplicate", "{\"Parameters\":{\"uVignetteStrength\":0.1,\"uVignetteStrength\":0.9}}") })
    {
        File.WriteAllText(Path.Combine(profiles, name + ".json"), json);
        try { Content.LoadPostProcessProfile(name); Check(false); }
        catch (InvalidDataException error) { Check(error.Message.Contains(name + ".json")); }
        File.WriteAllText(Path.Combine(profiles, name + ".json"), "{}");
        Check(Content.LoadPostProcessProfile(name) != null);
    }
});

File.WriteAllText(Path.Combine(fixture, "Shaders", "ProfileProbe.frag"), """
    #version 330 core
    in vec2 vTexCoord;
    out vec4 FragColor;
    uniform sampler2D uTexture;
    uniform float amount;
    uniform int count;
    uniform bool enabled;
    uniform vec2 shift;
    uniform vec3 color;
    uniform vec4 tint;
    uniform mat4 transform = mat4(1.0);
    uniform float weights[2];
    uniform sampler2D extraSampler;
    void main() {
        vec4 c = transform * vec4(color, 1.0);
        vec3 source = texture(uTexture,vTexCoord+shift).rgb * texture(extraSampler,vTexCoord).rgb;
        FragColor=vec4(source*c.rgb*tint.rgb*amount*float(count)*(enabled?1.0:0.0)+vec3(weights[0]+weights[1]),tint.a);
    }
    """);
File.WriteAllText(Path.Combine(profiles, "Uniforms.json"), """
    {
      "FragmentShader":"ProfileProbe",
      "Parameters": {
        "amount":1,
        "count":2.0,
        "enabled":true,
        "shift":[1,2],
        "color":{"R":0.1,"G":0.2,"B":0.3},
        "tint":[0.4,0.5,0.6,0.7],
        "transform":[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16]
      }
    }
    """);
var uniformProfile = Content.LoadPostProcessProfile("Uniforms")!;

Run("Uniform types drive JSON number, bool, vector, color and matrix conversion", () =>
{
    PostProcessSettings.Apply(uniformProfile);
    DrawPost();
    Near(ReadUniform("amount")[0], 1);
    Near(ReadUniform("count")[0], 2);
    Near(ReadUniform("enabled")[0], 1);
    Near(ReadUniform("shift")[1], 2);
    Near(ReadUniform("color")[2], 0.3f);
    Near(ReadUniform("tint")[3], 0.7f);
    Near(ReadUniform("transform")[14], 15);
    ((JArray)uniformProfile.Parameters["shift"])[0] = 99;
    PostProcessSettings.Parameters.Set("tint", Color.White);
    DrawPost();
    Near(ReadUniform("shift")[0], 1); // Cached JSON mutation cannot change the already applied profile.
    Near(((JArray)uniformProfile.Parameters["tint"])[3]!.Value<float>(), 0.7f);
    PostProcessSettings.Apply(new PostProcessProfile {
        FragmentShader = "ProfileProbe", Parameters = new() {
            ["shift"] = new Vector2(3,4), ["color"] = new Vector3(5,6,7),
            ["tint"] = new Color(0.2f,0.3f,0.4f,0.5f), ["enabled"] = 1,
            ["transform"] = Matrix4x4.Identity
        }
    });
    DrawPost();
    Near(ReadUniform("shift")[1], 4);
    Near(ReadUniform("color")[2], 7);
    Near(ReadUniform("tint")[3], 0.5f);
    Near(ReadUniform("transform")[0], 1);
    var nested = new PostProcessProfile {
        FragmentShader = "ProfileProbe", Parameters = new() {
            ["transform"] = JArray.Parse("[[2,0,0,0],[0,3,0,0],[0,0,4,0],[0,0,0,1]]")
        }
    };
    PostProcessSettings.Apply(nested);
    DrawPost();
    Near(ReadUniform("transform")[5], 3);
});

Run("Invalid conversions and missing shaders leave the whole active state unchanged", () =>
{
    PostProcessSettings.Apply(uniformProfile);
    var previousShader = PostProcessSettings.Shader;
    var badValues = new (string Name, object Value)[] {
        ("amount", "text"), ("amount", 1e100), ("amount", double.NaN), ("amount", JValue.CreateNull()),
        ("count", 1.5), ("count", 2147483648L), ("count", double.PositiveInfinity),
        ("enabled", "true"), ("enabled", 2),
        ("shift", JArray.Parse("[1]")), ("shift", JArray.Parse("[1,2,3]")), ("shift", JArray.Parse("[1,\"2\"]")),
        ("color", JObject.Parse("{\"X\":1,\"Y\":2}")),
        ("transform", JArray.Parse("[1,2,3]")),
        ("weights", JArray.Parse("[1,2]")), ("extraSampler", 0), ("uTime", 0)
    };
    foreach (var (name, value) in badValues)
    {
        var parameters = new Dictionary<string, object>(uniformProfile.Parameters) { [name] = value };
        var rejected = false;
        try { PostProcessSettings.Apply(new PostProcessProfile { Enabled = false, FragmentShader = "ProfileProbe", Parameters = parameters }); }
        catch (InvalidDataException error) { rejected = true; Check(error.Message.Contains(name)); }
        Check(rejected && ReferenceEquals(PostProcessSettings.Shader, previousShader) && PostProcessSettings.Enabled);
        DrawPost();
        Near(ReadUniform("amount")[0], 1);
        Near(ReadUniform("count")[0], 2);
    }
    var missingRejected = false;
    try { PostProcessSettings.Apply("Missing"); }
    catch (FileNotFoundException) { missingRejected = true; }
    Check(missingRejected && ReferenceEquals(PostProcessSettings.Shader, previousShader));
    missingRejected = false;
    try { PostProcessSettings.Apply(new PostProcessProfile { FragmentShader = "MissingShader" }); }
    catch (FileNotFoundException) { missingRejected = true; }
    Check(missingRejected && ReferenceEquals(PostProcessSettings.Shader, previousShader));
    File.WriteAllText(Path.Combine(fixture, "Shaders", "BrokenCompile.frag"), "invalid shader source");
    File.WriteAllText(Path.Combine(fixture, "Shaders", "BrokenLink.frag"), """
        #version 330 core
        in vec3 vTexCoord;
        out vec4 FragColor;
        void main() { FragColor = vec4(vTexCoord,1); }
        """);
    foreach (var fragment in new[] { "BrokenCompile", "BrokenLink" })
    {
        var rejected = false;
        try { PostProcessSettings.Apply(new PostProcessProfile { Enabled = false, FragmentShader = fragment }); }
        catch (Exception error)
        {
            rejected = true;
            Check(error.Message.Contains("compile error") || error.Message.Contains("link error"));
        }
        Check(rejected && ReferenceEquals(PostProcessSettings.Shader, previousShader) && PostProcessSettings.Enabled);
        DrawPost();
        Near(ReadUniform("amount")[0], 1);
    }

    PostProcessSettings.Apply("Default");
});

Run("A profile change during the world pass takes effect together on the next frame", () =>
{
    PostProcessSettings.Apply(uniformProfile);
    var capturedShader = PostProcessSettings.Shader!;
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    PostProcessSettings.Apply("Default");
    graphics.EndWorld();
    float value;
    unsafe { gl.GetUniform(capturedShader.Handle, gl.GetUniformLocation(capturedShader.Handle, "amount"), &value); }
    Near(value, 1);
    DrawPost();
    Near(ReadUniform("uVignetteStrength")[0], 0.45f);
});

Run("Disposal releases framebuffer, texture, depth and VAO while Content owns the program", () =>
{
    var framebuffer = Handle("_framebuffer");
    var color = Handle("_color");
    var depth = Handle("_depth");
    var vao = Handle("_vao");
    var program = PostProcessSettings.Shader!;
    renderer.Dispose();
    renderer.Dispose();
    Check(!gl.IsFramebuffer(framebuffer) && !gl.IsTexture(color) && !gl.IsRenderbuffer(depth) && !gl.IsVertexArray(vao));
    Check(gl.IsProgram(program.Handle));
    var cachedProfile = Content.LoadPostProcessProfile("Default");
    Content.UnloadAll();
    Check(!PostProcessSettings.Enabled && PostProcessSettings.Shader == null);
    Check(!ReferenceEquals(cachedProfile, Content.LoadPostProcessProfile("Default")));
    Check(program.Handle == 0);
});

Console.WriteLine("All postprocessing checks passed.");

void DrawSplit(Graphics g)
{
    var fullWidth = Camera.Active.OrthoSize * Camera.Active.AspectRatio;
    const float split = 0.8f;
    g.DrawQuad(new Vector3(-fullWidth * (1 - split) * 0.5f, 0, -2), Quaternion.Identity,
        new Vector2(fullWidth * split, Camera.Active.OrthoSize), Content.GetWhitePixel(), Color.White);
}
byte[] CapturePost(float time, Action<Graphics>? world = null, Action<Graphics>? overlay = null,
    int width = 640, int height = 240)
{
    graphics.BeginDraw(Camera.Active);
    typeof(Renderer).GetField("_frameTime", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(renderer, time);
    graphics.BeginWorld();
    world?.Invoke(graphics);
    graphics.EndWorld();
    overlay?.Invoke(graphics);
    gl.Finish();
    var image = new byte[width * height * 4];
    unsafe { fixed (byte* data = image) gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
    return image;
}

void DrawPost()
{
    graphics.BeginDraw(Camera.Active);
    graphics.BeginWorld();
    graphics.EndWorld();
}
float[] ReadUniform(string name)
{
    var data = new float[16];
    var program = PostProcessSettings.Shader!;
    unsafe { fixed (float* p = data) gl.GetUniform(program.Handle, gl.GetUniformLocation(program.Handle, name), p); }
    return data;
}
static void Near(float actual, float expected) => Check(MathF.Abs(actual - expected) < 0.0001f);

byte[] Pixel(int x, int y)
{
    gl.Finish();
    var pixel = new byte[4];
    unsafe { fixed (byte* data = pixel) gl.ReadPixels(x, y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
    return pixel;
}
void Run(string name, Action test)
{
    test();
    Check(gl.GetError() == GLEnum.NoError);
    Console.WriteLine("PASS: " + name);
}
static void Check(bool condition) { if (!condition) throw new Exception("Postprocessing check failed."); }
