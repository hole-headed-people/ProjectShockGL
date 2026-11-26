using System.Numerics;
using System.Reflection;
using ProjectShockGL;
using ProjectShockGL.Core;
using Newtonsoft.Json.Linq;
using ProjectShockGL.Rendering;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Texture = ProjectShockGL.Rendering.Texture;

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
foreach (var file in Directory.GetFiles(Path.Combine(root, "StarterContent/Shaders")))
    File.Copy(file, Path.Combine(fixture, "Shaders", Path.GetFileName(file)), overwrite: true);
File.Copy(Path.Combine(root, "StarterContent/PostProcessProfiles/Default.json"),
    Path.Combine(profiles, "Default.json"), overwrite: true);
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "DefaultSettings.ini"), "[Content]\nCustomPath=" + fixture + "\n");
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

if (args.Contains("--debug-draw"))
{
    var draw = new DebugDraw(graphics);
    var actions = new Action[]
    {
        () => draw.Line(new Vector3(-1, 0, -2), new Vector3(1, 0, -2), Color.Black),
        () => draw.Box(new Vector3(0, 0, -2), Quaternion.CreateFromYawPitchRoll(.4f, .3f, .2f), Vector3.One, Color.Black),
        () => draw.Sphere(new Vector3(0, 0, -2), .5f, Color.Black),
        () => draw.Billboard(new Vector3(0, 0, -2), Vector2.One, Content.GetWhitePixel(), Color.Black)
    };
    PostProcessSettings.Enabled = false;

    foreach (var action in actions)
    {
        graphics.BeginDraw(Camera.Active);
        gl.Enable(EnableCap.CullFace);
        action();
        Check(gl.IsEnabled(EnableCap.CullFace));
        var image = new byte[640 * 240 * 4];
        unsafe
        {
            fixed (byte* data = image)
            {
                gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            }
        }
        Check(Enumerable.Range(0, 640 * 240).Any(i => image[i * 4] < 100));
        Check(gl.GetError() == GLEnum.NoError);
        graphics.EndDraw();
    }
    Console.WriteLine("PASS: Debug line, rotated wire box, sphere and billboard draw with CullFace enabled and preserve its state");
    return;
}

if (args.Contains("--simple-pbr"))
{
    var program = Content.LoadShaderProgram("Lit", "SimplePBR");
    _ = Content.LoadShaderProgram("Deform", "SimplePBR");
    using var scene = new PbrScene();
    var environment = ProjectShockGL.Environment.Capture();
    byte[] Draw(float roughness, float metallic, ShaderParameters? supplied = null)
    {
        var parameters = supplied ?? new ShaderParameters();
        if (supplied == null)
        {
            parameters.Set("uRoughness", roughness);
            parameters.Set("uMetallic", metallic);
        }
        graphics.BeginDraw(Camera.Active);
        graphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
            Content.GetWhitePixel(), new Color(.5f, .2f, .1f), shader: program, shaderParameters: parameters);
        return Pixel(320, 120);
    }
    try
    {
        ProjectShockGL.Environment.AmbientColor = new Color(0, 0, 0);
        ProjectShockGL.Environment.IsFogEnabled = false;
        var light = scene.World.Spawn<Light>();
        light.Type = LightType.Directional;
        light.Intensity = .1f;
        Run("SimplePBR roughness changes direct specular lighting", () =>
        {
            var smooth = Draw(.4f, 0); var rough = Draw(1, 0);
            Check(smooth[0] >= rough[0] + 2 && rough[0] > 0);
        });
        Run("SimplePBR metallic changes reflection and parameters reset between draws", () =>
        {
            var dielectric = Draw(.6f, 0); var metal = Draw(.6f, 1);
            Check(!dielectric.SequenceEqual(metal));
            Check(dielectric.SequenceEqual(Draw(0, 0, new ShaderParameters())));
        });
        light.SetActive(false);
        var point = scene.World.Spawn<Light>(); point.Intensity = 1; point.Radius = 10;
        Run("SimplePBR supports point radius and spot direction", () =>
        {
            Check(Draw(1, 0)[0] > 5);
            point.Radius = 1; Check(Draw(1, 0)[0] == 0);
            point.Radius = 10; point.Type = LightType.Spot;
            Check(Draw(1, 0)[0] > 5);
            point.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
            Check(Draw(1, 0)[0] == 0);
        });
        Run("SimplePBR applies fog and has no metallic ambient reflections", () =>
        {
            point.SetActive(false);
            ProjectShockGL.Environment.AmbientColor = Color.White;
            Check(Draw(.6f, 0)[0] > 100 && Draw(.6f, 1)[0] == 0);
            ProjectShockGL.Environment.IsFogEnabled = true;
            ProjectShockGL.Environment.FogNear = 0; ProjectShockGL.Environment.FogFar = 1;
            ProjectShockGL.Environment.FogColor = new Color(0, 1, 0);
            var fog = Draw(.6f, 1); Check(fog[0] == 0 && fog[1] > 250);
        });
        Run("SimplePBR obeys the editor Unlit preview", () =>
        {
            ProjectShockGL.Environment.IsFogEnabled = false;
            using var unlitRenderer = new Renderer(gl, 640, 240, new RenderPreviewSettings { Unlit = true });
            var unlitGraphics = new Graphics(unlitRenderer);
            unlitGraphics.BeginDraw(Camera.Active);
            unlitGraphics.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
                Content.GetWhitePixel(), new Color(.5f, .2f, .1f), shader: program);
            var pixel = Pixel(320, 120);
            Check(pixel[0] is > 120 and < 135 && pixel[1] is > 45 and < 58);
        });
    }
    finally { ProjectShockGL.Environment.Apply(environment); }
    Console.WriteLine("All SimplePBR checks passed.");
    return;
}

var detailAssets = Path.Combine(fixture, "Details", "InstanceTests");
Directory.CreateDirectory(detailAssets);
Directory.CreateDirectory(Path.Combine(fixture, "Meshes"));
Directory.CreateDirectory(Path.Combine(fixture, "Textures"));
File.WriteAllText(Path.Combine(fixture, "Meshes", "InstancePlane.obj"), """
    v -0.5 -0.5 0
    v 0.5 -0.5 0
    v 0.5 0.5 0
    v -0.5 0.5 0
    vt 0 0
    vt 1 0
    vt 1 1
    vt 0 1
    vn 0.6 0 0.8
    f 1/1/1 2/2/1 3/3/1
    f 1/1/1 3/3/1 4/4/1
    """);
File.Copy(Path.Combine(root, "StarterContent/Textures/Test.jpg"),
    Path.Combine(fixture, "Textures", "InstanceTest.jpg"), true);
foreach (var lit in new[] { true, false })
    File.WriteAllText(Path.Combine(detailAssets, lit ? "Lit.json" : "Unlit.json"),
        "{\"Mesh\":\"InstancePlane\",\"Texture\":\"InstanceTest\",\"Color\":[0.9,0.8,0.7,1],\"BackfaceCulling\":false,\"Lit\":" +
        (lit ? "true" : "false") + "}");
File.WriteAllText(Path.Combine(detailAssets, "Culled.json"),
    "{\"Mesh\":\"InstancePlane\",\"Texture\":\"WhitePixel\",\"Lit\":false}");
File.WriteAllText(Path.Combine(detailAssets, "MissingModel.json"),
    "{\"Mesh\":\"MissingInstanceModel\",\"Lit\":false}");

Run("Detail CastBakedShadow excludes direct and GI ray hits while preserving lightmap reception", () =>
{
    var oldAmbient = ProjectShockGL.Environment.AmbientColor;
    try
    {
        ProjectShockGL.Environment.AmbientColor = new Color(.05f, .05f, .05f);
        foreach (var casts in new[] { true, false })
        {
            var name = casts ? "Casts" : "ReceivesOnly";
            File.WriteAllText(Path.Combine(detailAssets, name + ".json"), new JObject
            {
                ["Mesh"] = "InstancePlane", ["Texture"] = "WhitePixel", ["Lit"] = true,
                ["BackfaceCulling"] = false, ["CastBakedShadow"] = casts
            }.ToString());
            using var scene = new PbrScene();
            var detail = new DetailPlacement
            {
                AssetPath = "InstanceTests/" + name, Position = new Vector3(0, 1, 0),
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2)
            };
            scene.World.Details.Add(detail);
            var light = scene.World.Spawn<ProjectShockGL.Core.Light>();
            light.Mode = ProjectShockGL.Core.LightMode.Baked;
            light.Type = ProjectShockGL.Core.LightType.Directional;
            light.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
            light.Intensity = 1;
            var geometry = scene.World.PrepareLightmapGeometry(new LightmapPreparationOptions { AtlasSize = 64, SurfaceSize = 32, Padding = 1 });
            Check(geometry.Surfaces.Count == 1 && geometry.Surfaces[0].Id.IsDetail);
            Check(geometry.Surfaces[0].Materials[0].ReceivesLight && geometry.Surfaces[0].Materials[0].CastBakedShadow == casts);
            var input = DirectLightBaker.Capture(scene.World, geometry);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            var sample = (Vector3)typeof(DirectLightBaker).GetMethod("Sample", flags)!.Invoke(null,
                new object[] { input, Vector3.Zero, Vector3.UnitY, .001f })!;
            Check(casts ? sample.X < .051f : sample.X > 1);
            var triangles = geometry.WorldTriangles();
            var sceneType = typeof(DirectLightBaker).Assembly.GetType("ProjectShockGL.Rendering.LightmapOcclusion")!;
            var alphaType = typeof(DirectLightBaker).Assembly.GetType("ProjectShockGL.Rendering.LightmapAlpha")!;
            var occlusion = Activator.CreateInstance(sceneType, flags, null,
                new object[] { triangles, Array.CreateInstance(alphaType, triangles.Length), default(CancellationToken) }, null)!;
            var nearest = sceneType.GetMethod("Nearest", flags)!;
            var arguments = new object?[] { Vector3.Zero, Vector3.UnitY, -1, .001f, null };
            Check((bool)nearest.Invoke(occlusion, arguments)! == casts);
            var baked = DirectLightBaker.Bake(input, new DirectLightBakeOptions { IndirectSamples = 8 });
            Check(baked.Pages[0].Coverage.Span.Contains((byte)1));
            Check(baked.Pages[0].Pixels.ToArray().Any(pixel => pixel.X > .1f));
        }
        Check(new ProjectShockGL.Core.DetailAsset { Mesh = "InstancePlane" }.CastBakedShadow);
    }
    finally { ProjectShockGL.Environment.AmbientColor = oldAmbient; }
});
Run("Custom Detail shaders preserve instancing, isolate parameters and animate with game time", () =>
{
    var detailPreview = new RenderPreviewSettings();
    using var detailRenderer = new Renderer(gl, 640, 240, detailPreview);
    var detailGraphics = new Graphics(detailRenderer);
    var time = Time.ElapsedTime;
    var unscaled = Time.UnscaledElapsedTime;
    var environment = ProjectShockGL.Environment.Capture();
    try
    {
        ProjectShockGL.Environment.IsFogEnabled = false;
        ProjectShockGL.Environment.AmbientColor = Color.White;
        foreach (var (name, strength) in new[] { ("Wind", .5f), ("Still", 0f) })
        {
            File.WriteAllText(Path.Combine(detailAssets, name + ".json"), new JObject
            {
                ["Mesh"] = "InstancePlane", ["Texture"] = "WhitePixel", ["Lit"] = false,
                ["BackfaceCulling"] = false, ["Shader"] = "InstancedWind", ["Color"] = new JArray(.2, .7, .3, 1),
                ["ShaderParameters"] = new JObject
                {
                    ["uWindStrength"] = strength, ["uWindSpeed"] = 2,
                    ["uWindBase"] = -.5, ["uWindHeight"] = 1,
                    ["uWindDirection"] = new JArray(1, 0), ["uWindFrequency"] = 0
                }
            }.ToString());
        }
        using var scene = new PbrScene();
        var world = scene.World;
        foreach (var (name, x) in new[] { ("Wind", -1.5f), ("Wind", -.3f), ("Still", 1.3f) })
            world.Details.Add(new DetailPlacement { AssetPath = "InstanceTests/" + name, Position = new Vector3(x, 0, -3) });
        world.Update(0);
        byte[] Draw(double gameTime, double realTime)
        {
            Time.ElapsedTime = gameTime;
            Time.UnscaledElapsedTime = realTime;
            detailGraphics.BeginDraw(Camera.Active);
            var queue = new RenderQueue();
            world.Render(queue, false);
            queue.Flush(detailGraphics);
            var pixels = new byte[640 * 240 * 4];
            unsafe { fixed (byte* data = pixels) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            detailGraphics.EndDraw();
            Check(detailGraphics.Statistics.DrawCalls == 2 && gl.GetError() == GLEnum.NoError);
            return pixels;
        }
        var start = Draw(0, 0);
        var moving = Draw(Math.PI / 4, 0);
        Check(!start.SequenceEqual(moving));
        Check(moving.SequenceEqual(Draw(Math.PI / 4, 100))); // uTime must ignore unscaled changes.
        Check(start.SequenceEqual(Draw(0, 100))); // Cached groups still get fresh uniforms.
        for (var y = 0; y < 240; y++)
        for (var x = 440; x < 640; x++)
        for (var channel = 0; channel < 4; channel++)
            Check(start[(y * 640 + x) * 4 + channel] == moving[(y * 640 + x) * 4 + channel]);
        detailPreview.Unlit = true;
        Draw(Math.PI / 4, 100);
        detailPreview.Unlit = false;
        Content.LoadShaderProgram("InstancedWind", "Lit");
        File.WriteAllText(Path.Combine(detailAssets, "BadShader.json"),
            """{"Mesh":"InstancePlane","Shader":"Lit","Lit":true}""");
        using var invalidScene = new PbrScene();
        invalidScene.World.Details.Add(new DetailPlacement { AssetPath = "InstanceTests/BadShader" });
        var rejected = false;
        try { invalidScene.World.Render(new RenderQueue(), false); }
        catch (InvalidDataException error) { rejected = error.Message.Contains("aInstanceModel"); }
        Check(rejected);
        new DetailAsset { Mesh = "InstancePlane" }.Validate();
        var reserved = false;
        try { new DetailAsset { Mesh = "InstancePlane", ShaderParameters = new() { ["uTime"] = 1 } }.Validate(); }
        catch (InvalidDataException) { reserved = true; }
        Check(reserved);
    }
    finally
    {
        Time.ElapsedTime = time;
        Time.UnscaledElapsedTime = unscaled;
        ProjectShockGL.Environment.Apply(environment);
        detailPreview.Unlit = false;
    }
});
Run("Details use instancing with matching Lit/Unlit, normals, UVs, lights, fog, statistics and full-resolution UI", () =>
{
    using var scene = new PbrScene();
    var world = scene.World;
    var environment = ProjectShockGL.Environment.Capture();
    try
    {
        ProjectShockGL.Environment.AmbientColor = new Color(.15f, .1f, .2f);
        ProjectShockGL.Environment.IsFogEnabled = true;
        ProjectShockGL.Environment.FogNear = 1;
        ProjectShockGL.Environment.FogFar = 20;
        ProjectShockGL.Environment.FogColor = new Color(.2f, .4f, .5f);
        var sun = world.Spawn<Light>();
        sun.Type = LightType.Directional; sun.Intensity = .6f;
        var point = world.Spawn<Light>();
        point.Transform.Position = new Vector3(2, 2, 0); point.Radius = 10; point.Intensity = .4f;
        var spot = world.Spawn<Light>();
        spot.Type = LightType.Spot; spot.OuterAngle = 100; spot.InnerAngle = 60;
        spot.Transform.Position = new Vector3(-1, 0, 0); spot.Radius = 10; spot.Intensity = .5f;
        for (var i = 0; i < 5; i++)
            world.Details.Add(new DetailPlacement
            {
                AssetPath = i < 3 ? "InstanceTests/Lit" : "InstanceTests/Unlit",
                Position = new Vector3((i % 3 - 1) * 2, i < 3 ? .7f : -.7f, -4),
                Rotation = Quaternion.CreateFromYawPitchRoll(.3f, .1f, .2f),
                Scale = new Vector3(i == 1 ? -.9f : .9f, .7f, 1.4f)
            });
        world.Update(0);
        (byte[] Image, object[] Instances, int Calls, long Triangles) Draw(bool instanced)
        {
            graphics.BeginDraw(Camera.Active);
            object[] instances = [];
            if (instanced)
            {
                var queue = new RenderQueue();
                world.Render(queue, false);
                instances = DetailInstances(queue);
                gl.Disable(EnableCap.CullFace);
                queue.Flush(graphics);
                Check(!gl.IsEnabled(EnableCap.CullFace));
            }
            else
            {
                foreach (var detail in world.Details)
                {
                    var asset = Content.LoadDetail(detail.AssetPath)!;
                    graphics.DrawMesh(Content.LoadMesh(asset.Mesh)!, detail.Position, detail.Rotation, detail.Scale,
                        Content.LoadTexture(asset.Texture, asset.TextureFormat)!, asset.Color, asset.Lit,
                        backfaceCulling: asset.BackfaceCulling);
                }
            }
            var image = new byte[640 * 240 * 4];
            unsafe { fixed (byte* data = image) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            graphics.EndDraw();
            return (image, instances, graphics.Statistics.DrawCalls, graphics.Statistics.Triangles);
        }
        var plain = Draw(false);
        var instanced = Draw(true);
        Check(plain.Calls == 5 && instanced.Calls == 2 && instanced.Triangles == plain.Triangles && plain.Triangles == 10);
        Check(plain.Image.Zip(instanced.Image).Count(pair => Math.Abs(pair.First - pair.Second) > 2) < 100);
        Check(instanced.Instances.Length == 2 && instanced.Instances.Sum(InstanceCount) == 5);
        var cached = Draw(true);
        Check(cached.Instances.SequenceEqual(instanced.Instances));
        sun.Intensity = .1f; point.Intensity = .1f; spot.Intensity = .1f;
        var relit = Draw(true);
        Check(relit.Instances.SequenceEqual(instanced.Instances) && !relit.Image.SequenceEqual(instanced.Image));
        Check(relit.Image.Zip(Draw(false).Image).Count(pair => Math.Abs(pair.First - pair.Second) > 2) < 100);

        graphics.BeginDraw(Camera.Active);
        graphics.BeginWorld();
        var queue = new RenderQueue(); world.Render(queue, false); queue.Flush(graphics);
        graphics.EndWorld();
        Check(graphics.Statistics.WorldDrawCalls == 2 && graphics.Statistics.PostProcessDrawCalls == 1);
        graphics.BeginUI(new Vector2(640, 240));
        graphics.DrawUIRect(new Vector2(0, 220), new Vector2(20), Color.Red);
        graphics.EndUI();
        Check(Pixel(5, 5)[0] > 245 && Pixel(5, 5)[1] < 5);
        graphics.EndDraw();
        var sourceMesh = Content.LoadMesh("InstancePlane")!;
        var sourceTexture = Content.LoadTexture("InstanceTest")!;
        var handles = instanced.Instances.Select(InstanceBuffer).ToArray();
        var vaos = instanced.Instances.Select(InstanceVao).ToArray();
        Check(handles.All(handle => gl.IsBuffer(handle)) && vaos.All(handle => gl.IsVertexArray(handle)));
        world.Clear();
        Check(handles.All(handle => !gl.IsBuffer(handle)) && vaos.All(handle => !gl.IsVertexArray(handle)) &&
              instanced.Instances.All(InstanceDisposed));
        Check(!sourceMesh.IsDisposed && sourceTexture.Handle != 0);
    }
    finally { ProjectShockGL.Environment.Apply(environment); }
});

Run("Details rebuild instance buffers for revisions and editor visibility and preserve the cache on failed rebuild", () =>
{
    using var scene = new PbrScene();
    var world = scene.World;
    var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    var wasEditor = Application.IsEditor;
    var first = world.Details.Add(new DetailPlacement
    {
        AssetPath = "InstanceTests/Unlit", Position = new Vector3(-1, 0, -3), EditorGroups = new[] { "hidden-detail-test" }
    });
    world.Details.Add(new DetailPlacement { AssetPath = "InstanceTests/Unlit", Position = new Vector3(1, 0, -3) });
    world.Update(0);
    object[] Draw()
    {
        graphics.BeginDraw(Camera.Active);
        var queue = new RenderQueue();
        world.Render(queue, false);
        var result = DetailInstances(queue);
        queue.Flush(graphics);
        graphics.EndDraw();
        return result;
    }
    try
    {
        var original = Draw().Single();
        Check(InstanceCount(original) == 2);
        editor.SetValue(null, true);
        world.Details.Update(new DetailPlacement
        {
            Id = first.Id, AssetPath = first.AssetPath, Position = new Vector3(-2, 0, -3), EditorGroups = first.EditorGroups
        });
        var edited = Draw().Single();
        Check(InstanceDisposed(original) && !ReferenceEquals(original, edited));
        World.HiddenEditorGroups.Add("hidden-detail-test");
        var visible = Draw().Single();
        Check(InstanceCount(visible) == 1 && InstanceDisposed(edited));
        editor.SetValue(null, false);
        Check(InstanceCount(Draw().Single()) == 2);
        editor.SetValue(null, true);
        World.HiddenEditorGroups.Remove("hidden-detail-test");
        var beforeFailure = Draw().Single();
        var bad = world.Details.Add(new DetailPlacement { AssetPath = "InstanceTests/MissingModel" });
        try { Draw(); throw new Exception("Missing Detail model was accepted."); }
        catch (FileNotFoundException) {}
        Check(!InstanceDisposed(beforeFailure) && gl.IsBuffer(InstanceBuffer(beforeFailure)));
        world.Details.Remove(bad.Id);
        Check(InstanceCount(Draw().Single()) == 2 && InstanceDisposed(beforeFailure));
        world.Details.Clear();
        Check(Draw().Length == 0 && graphics.Statistics.DrawCalls == 0);
    }
    finally
    {
        World.HiddenEditorGroups.Remove("hidden-detail-test");
        editor.SetValue(null, wasEditor);
    }
});

Run("Detail instancing obeys editor Unlit and Environment preview overrides", () =>
{
    using var scene = new PbrScene();
    scene.World.Details.Add(new DetailPlacement { AssetPath = "InstanceTests/Lit", Position = new Vector3(0, 0, -3) });
    scene.World.Update(0);
    using var previewRenderer = new Renderer(gl, 640, 240, new RenderPreviewSettings { Unlit = true, EnvironmentEnabled = false });
    var preview = new Graphics(previewRenderer);
    preview.BeginDraw(Camera.Active);
    var queue = new RenderQueue(); scene.World.Render(queue, false); queue.Flush(preview);
    var instanced = Pixel(320, 120);
    preview.EndDraw();
    var asset = Content.LoadDetail("InstanceTests/Lit")!;
    preview.BeginDraw(Camera.Active);
    preview.DrawMesh(Content.LoadMesh(asset.Mesh)!, new Vector3(0, 0, -3), Quaternion.Identity, Vector3.One,
        Content.LoadTexture(asset.Texture, asset.TextureFormat)!, asset.Color, false, backfaceCulling: false);
    Check(instanced.SequenceEqual(Pixel(320, 120)));
    preview.EndDraw();
});
Run("Instanced Details respect BackfaceCulling and leave ordinary meshes usable", () =>
{
    using var scene = new PbrScene();
    var detail = new DetailPlacement
    {
        AssetPath = "InstanceTests/Culled", Position = new Vector3(0, 0, -3),
        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI)
    };
    scene.World.Details.Add(detail);
    scene.World.Update(0);
    graphics.BeginDraw(Camera.Active);
    gl.Enable(EnableCap.CullFace);
    var queue = new RenderQueue(); scene.World.Render(queue, false); queue.Flush(graphics);
    Check(Pixel(320, 120)[1] > 245 && gl.IsEnabled(EnableCap.CullFace));
    graphics.DrawMesh(Content.LoadMesh("InstancePlane")!, new Vector3(0, 0, -2), Quaternion.Identity, Vector3.One,
        Content.GetWhitePixel(), Color.Red);
    Check(Pixel(320, 120)[0] > 245 && Pixel(320, 120)[1] < 5);
    graphics.EndDraw();
});
Run("Static Geometry and BrushBox share cached batches with matching lighting, fog, transforms and cleanup", () =>
{
    using var scene = new PbrScene();
    var world = scene.World;
    var environment = ProjectShockGL.Environment.Capture();
    float[] vertices = [
        -.5f, -.5f, 0, .6f, 0, .8f, 0, 0,
         .5f, -.5f, 0, .6f, 0, .8f, 1, 0,
         .5f,  .5f, 0, .6f, 0, .8f, 1, 1,
        -.5f,  .5f, 0, .6f, 0, .8f, 0, 1
    ];
    using var plane = new Mesh(gl, vertices, [0, 1, 2, 0, 2, 3]);
    try
    {
        ProjectShockGL.Environment.AmbientColor = new Color(.15f, .1f, .2f);
        ProjectShockGL.Environment.IsFogEnabled = true;
        ProjectShockGL.Environment.FogNear = 1;
        ProjectShockGL.Environment.FogFar = 20;
        ProjectShockGL.Environment.FogColor = new Color(.2f, .4f, .5f);
        var sun = world.Spawn<Light>();
        sun.Type = LightType.Directional;
        sun.Intensity = .6f;
        var point = world.Spawn<Light>();
        point.Transform.Position = new Vector3(2, 2, 0);
        point.Radius = 10;
        point.Intensity = .4f;
        for (var i = 0; i < 2; i++)
        {
            var geometry = world.Spawn<Geometry>();
            geometry.Transform.Position = new Vector3(i * 2 - 1, .8f, -4);
            geometry.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(.3f, .1f, .2f);
            geometry.Transform.Scale = new Vector3(i == 0 ? .9f : -.9f, .7f, 1.4f);
            var component = geometry.GetComponent<MeshComponent>()!;
            component.Mesh = plane;
            component.Texture = Content.GetWhitePixel();
            component.Lit = true;
            component.BackfaceCulling = false;
        }
        var brushes = new List<BrushBox>();
        for (var i = 0; i < 2; i++)
        {
            var brush = world.Spawn<BrushBox>();
            brush.Size = new Vector3(.8f, .7f, .5f);
            brush.Transform.Position = new Vector3(i * 2 - 1, -.8f, -4);
            brush.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(.4f, .2f, -.1f);
            brush.Transform.Scale = new Vector3(1.2f, .8f, 1.5f);
            brush.SurfaceMode = SurfaceMode.Opaque; // Match Geometry for a single compatible batch.
            brush.GetComponent<MeshComponent>()!.BackfaceCulling = false;
            brushes.Add(brush);
        }
        world.Update(0);
        (byte[] Image, Mesh[] Meshes, int Calls) Draw(bool batching)
        {
            world.StaticBatchingEnabled = batching;
            graphics.BeginDraw(Camera.Active);
            var queue = new RenderQueue();
            world.Render(queue, false);
            var commands = (List<RenderCommand>)typeof(RenderQueue).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(queue)!;
            var meshes = commands.OfType<MeshCommand>().Select(command => command.Mesh!).ToArray();
            queue.Flush(graphics);
            var image = new byte[640 * 240 * 4];
            unsafe { fixed (byte* data = image) gl.ReadPixels(0, 0, 640, 240, PixelFormat.Rgba, PixelType.UnsignedByte, data); }
            graphics.EndDraw();
            return (image, meshes, graphics.Statistics.DrawCalls);
        }
        void SameImage(byte[] first, byte[] second) =>
            Check(first.Zip(second).Count(pair => Math.Abs(pair.First - pair.Second) > 2) < 100);

        var plain = Draw(false);
        var batched = Draw(true);
        Check(plain.Calls == 4 && batched.Calls == 1);
        SameImage(plain.Image, batched.Image);
        var cached = Draw(true);
        Check(ReferenceEquals(cached.Meshes[0], batched.Meshes[0]));
        var brushMesh = brushes[0].GetComponent<MeshComponent>()!.Mesh!;
        brushes[0].Destroy();
        Check(!brushes[0].IsDestroyed && !brushMesh.IsDisposed);
        SameImage(cached.Image, Draw(true).Image);
        sun.Intensity = .2f;
        var changedLight = Draw(true);
        Check(ReferenceEquals(changedLight.Meshes[0], batched.Meshes[0]) &&
              !changedLight.Image.SequenceEqual(batched.Image));
        SameImage(changedLight.Image, Draw(false).Image);
        Check(batched.Meshes[0].IsDisposed);

        var previous = Draw(true).Meshes[0];
        vertices[0] -= .1f;
        plane.UpdateVertices(vertices);
        var updated = Draw(true);
        Check(previous.IsDisposed && !ReferenceEquals(previous, updated.Meshes[0]));
        SameImage(updated.Image, Draw(false).Image);

        var finalBatch = Draw(true).Meshes[0];
        world.Clear();
        Check(finalBatch.IsDisposed && brushMesh.IsDisposed && !plane.IsDisposed);
    }
    finally { ProjectShockGL.Environment.Apply(environment); }
});

Run("Static batching preserves UVs and Unlit and excludes editor, custom shaders, transparency and movable objects", () =>
{
    using var scene = new PbrScene();
    var world = scene.World;
    var environment = ProjectShockGL.Environment.Capture();
    var editor = typeof(Application).GetProperty(nameof(Application.IsEditor))!;
    var wasEditor = Application.IsEditor;
    using var texture = Texture.FromRgba(gl, 2, 2, new byte[] {
        255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 0, 255 });
    using var transparent = Texture.CreateSolid(gl, 255, 255, 255, 128);
    using var plane = new Mesh(gl, [
        -.5f, -.5f, 0, 0, 0, 1, 0, 0, .5f, -.5f, 0, 0, 0, 1, 1, 0,
         .5f,  .5f, 0, 0, 0, 1, 1, 1, -.5f, .5f, 0, 0, 0, 1, 0, 1
    ], [0, 1, 2, 0, 2, 3]);
    try
    {
        ProjectShockGL.Environment.IsFogEnabled = false;
        var components = new List<MeshComponent>();
        for (var i = 0; i < 2; i++)
        {
            var geometry = world.Spawn<Geometry>();
            geometry.Transform.Position = new Vector3(i * 2 - 1, 0, -3);
            var component = geometry.GetComponent<MeshComponent>()!;
            component.Mesh = plane;
            component.Texture = texture;
            components.Add(component);
        }
        world.Update(0);
        int Draw()
        {
            graphics.BeginDraw(Camera.Active);
            var queue = new RenderQueue();
            world.Render(queue, false);
            queue.Flush(graphics);
            graphics.EndDraw();
            return graphics.Statistics.DrawCalls;
        }
        Check(Draw() == 1);
        var left = Pixel(260, 110); var right = Pixel(380, 110);
        Check(left.SequenceEqual(right) && left[0] != left[1]);
        editor.SetValue(null, true);
        Check(Draw() == 2);
        editor.SetValue(null, false);
        Check(Draw() == 1);
        var component0 = components[0];
        component0.Shader = Content.LoadShaderProgram("Unlit");
        Check(Draw() == 2);
        component0.Shader = null;
        component0.Texture = transparent;
        Check(Draw() == 2);
        component0.Texture = texture;
        component0.Color = new Color(1, 1, 1, .5f);
        Check(Draw() == 2);
        component0.Color = Color.White;
        component0.IgnoreDepth = true;
        Check(Draw() == 2);
        component0.IgnoreDepth = false;
        component0.BackfaceCulling = false;
        Check(Draw() == 2);
        component0.BackfaceCulling = true;
        component0.RenderOrder = 1;
        Check(Draw() == 2);
        component0.RenderOrder = 0;
        component0.Lit = true;
        Check(Draw() == 2);
        component0.Lit = false;
        component0.IsEnabled = false;
        Check(Draw() == 1);
        component0.IsEnabled = true;
        var movable = world.Spawn<BrushBox>();
        movable.Movable = true;
        movable.Transform.Position = new Vector3(3, 0, -3);
        world.Update(0);
        Check(Draw() == 2);
        var entity = world.Spawn<Entity>();
        var entityMesh = entity.AddComponent<MeshComponent>();
        entityMesh.Mesh = plane;
        entityMesh.Texture = texture;
        entity.Transform.Position = new Vector3(-3, 0, -3);
        world.Update(0);
        Check(Draw() == 3);
    }
    finally
    {
        editor.SetValue(null, wasEditor);
        ProjectShockGL.Environment.Apply(environment);
    }
});
Run("YAxis billboards face camera position, remain vertical and handle a camera directly above", () =>
{
    var camera = Camera.Active;
    var position = camera.Transform.Position;
    var rotation = camera.Transform.Rotation;
    var build = typeof(Graphics).GetMethod("CreateBillboardModel", BindingFlags.Static | BindingFlags.NonPublic)!;
    Matrix4x4 Model(BillboardMode mode) =>
        (Matrix4x4)build.Invoke(null, [camera, Vector3.Zero, new Vector2(2, 3), mode])!;
    try
    {
        camera.Transform.Position = new Vector3(4, 10, 5);
        camera.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(.5f, .7f, .3f);
        var model = Model(BillboardMode.YAxis);
        var up = Vector3.TransformNormal(Vector3.UnitY, model);
        var normal = Vector3.TransformNormal(Vector3.UnitZ, model);
        var right = Vector3.TransformNormal(Vector3.UnitX, model);
        Check(up == Vector3.UnitY * 3);
        Check(Vector3.Distance(normal, Vector3.Normalize(new Vector3(4, 0, 5))) < .0001f);
        Check(MathF.Abs(right.Length() - 2) < .0001f && MathF.Abs(Vector3.Dot(right, normal)) < .0001f);
        camera.Transform.Rotation = Quaternion.Identity;
        Check(Model(BillboardMode.YAxis) == model);
        var view = Model(BillboardMode.ViewAligned);
        Check(Vector3.TransformNormal(Vector3.UnitY, view) == camera.Transform.Up * 3 &&
              Vector3.TransformNormal(Vector3.UnitZ, view) == camera.Transform.Forward);
        camera.Transform.Position = new Vector3(0, 10, 0);
        Check(Vector3.TransformNormal(Vector3.UnitZ, Model(BillboardMode.YAxis)) == Vector3.UnitZ);
        camera.Transform.Position = Vector3.Zero;
        Check(Vector3.TransformNormal(Vector3.UnitZ, Model(BillboardMode.YAxis)) == Vector3.UnitZ);
        graphics.BeginDraw(camera);
        gl.Enable(EnableCap.CullFace);
        new BillboardCommand
        {
            Mode = BillboardMode.YAxis, Position = new Vector3(0, 0, -2), Size = Vector2.One,
            Texture = Content.GetWhitePixel(), Color = Color.Red
        }.Execute(graphics);
        var pixel = Pixel(320, 120);
        Check(pixel[0] > 245 && pixel[1] < 5 && gl.GetError() == GLEnum.NoError);
        graphics.EndDraw();
        Check(new BillboardComponent().Mode == BillboardMode.ViewAligned);
    }
    finally
    {
        camera.Transform.Position = position;
        camera.Transform.Rotation = rotation;
    }
});
Run("Render counters separate world, postprocessing and UI, reset every frame and draw the debug overlay", () =>
{
    graphics.BeginDraw(Camera.Active);
    Check(graphics.Statistics.DrawCalls == 0 && graphics.Statistics.Triangles == 0);
    graphics.BeginWorld();
    for (var i = 0; i < 2; i++)
        graphics.DrawQuad(new Vector3(i, 0, -2), Quaternion.Identity, Vector2.One,
            Content.GetWhitePixel(), Color.Red);
    graphics.EndWorld();
    var stats = graphics.Statistics;
    Check(stats.WorldDrawCalls == 2 && stats.PostProcessDrawCalls == 1);
    Check(stats.DrawCalls == 3 && stats.Triangles == 5);
    Check(stats.WorldWidth == 640 && stats.WorldHeight == 240);
    graphics.BeginUI(new Vector2(640, 240));
    graphics.DrawUIRect(new Vector2(620, 220), new Vector2(10), Color.Red);
    graphics.EndUI();
    Check(graphics.Statistics.DrawCalls == 4 && graphics.Statistics.Triangles == 7);

    var fonts = Path.Combine(fixture, "Fonts");
    Directory.CreateDirectory(fonts);
    File.Copy(Path.Combine(root, "StarterContent/Fonts/NotoSans-Regular.ttf"),
        Path.Combine(fonts, "NotoSans-Regular.ttf"), true);
    var overlayType = typeof(Renderer).Assembly.GetType("ProjectShockGL.Rendering.DebugStatisticsOverlay")!;
    var overlay = Activator.CreateInstance(overlayType, nonPublic: true)!;
    overlayType.GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(overlay, [graphics, new Vector2(640, 240)]);
    Check(graphics.Statistics.DrawCalls > stats.DrawCalls + 10);
    Check(Pixel(9, 230)[0] < 100);
    Check(gl.GetError() == GLEnum.NoError);
    graphics.EndDraw();

    graphics.BeginDraw(Camera.Active);
    Check(graphics.Statistics.DrawCalls == 0 && graphics.Statistics.WorldDrawCalls == 0 &&
        graphics.Statistics.PostProcessDrawCalls == 0 && graphics.Statistics.Triangles == 0);
    graphics.EndDraw();
});
Run("Mesh backfaces are optional, Lit reverses their normals and CullFace is restored", () =>
{
    var environment = ProjectShockGL.Environment.Capture();
    using var scene = new PbrScene();
    using var mesh = new Mesh(gl, [
        -.5f, -.5f, 0, 0, 0, 1, 0, 0,
         .5f, -.5f, 0, 0, 0, 1, 1, 0,
         .5f,  .5f, 0, 0, 0, 1, 1, 1,
        -.5f,  .5f, 0, 0, 0, 1, 0, 1
    ], [0, 1, 2, 0, 2, 3]);
    try
    {
        ProjectShockGL.Environment.AmbientColor = Color.Black;
        ProjectShockGL.Environment.IsFogEnabled = false;
        var light = scene.World.Spawn<Light>();
        light.Type = LightType.Directional;
        light.Intensity = 1;
        foreach (var initiallyEnabled in new[] { true, false })
        {
            foreach (var culling in new[] { true, false })
            {
                graphics.BeginDraw(Camera.Active);
                if (initiallyEnabled) gl.Enable(EnableCap.CullFace);
                else gl.Disable(EnableCap.CullFace);
                var command = new MeshCommand
                {
                    Mesh = mesh, Texture = Content.GetWhitePixel(), Color = Color.Red,
                    Position = new Vector3(0, 0, -2), Scale = Vector3.One,
                    Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI),
                    Lit = true, BackfaceCulling = culling
                };
                command.Execute(graphics);
                var pixel = Pixel(320, 120);
                Check(pixel[0] > 245 && (culling ? pixel[1] > 245 : pixel[1] < 5));
                Check(gl.IsEnabled(EnableCap.CullFace) == initiallyEnabled);
                Check(gl.GetError() == GLEnum.NoError);
                graphics.EndDraw();
            }
        }
        Check(new MeshComponent().BackfaceCulling && new MeshCommand().BackfaceCulling);
    }
    finally { ProjectShockGL.Environment.Apply(environment); }
});
Run("Resolution scale preserves full-resolution UI, depth and viewport with Linear/Nearest and effects on/off", () =>
{
    var settingsPath = Path.Combine(AppContext.BaseDirectory, "DefaultSettings.ini");
    var originalSettings = File.ReadAllText(settingsPath);
    var originalEffects = PostProcessSettings.Enabled;
    try
    {
        foreach (var filter in new[] { "Linear", "Nearest" })
        foreach (var effects in new[] { false, true })
        {
            File.WriteAllText(settingsPath, originalSettings + "\n[Rendering]\nResolutionScale=0.5\nResolutionFilter=" + filter + "\n");
            Settings.Initialize();
            PostProcessSettings.Enabled = effects;
            PostProcessSettings.Parameters.Set("uVignetteStrength", 0f);
            using var scaledRenderer = new Renderer(gl, 640, 240);
            var scaled = new Graphics(scaledRenderer);
            gl.Viewport(0, 0, 640, 240);
            scaled.BeginDraw(Camera.Active);
            scaled.BeginWorld();
            var viewport = new int[4];
            gl.GetInteger(GetPName.Viewport, viewport.AsSpan());
            Check(viewport[2] == 320 && viewport[3] == 120);

            gl.ClearColor(0, 0, 0, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            gl.Enable(EnableCap.ScissorTest);
            gl.Scissor(0, 0, 1, 120);
            gl.ClearColor(1, 1, 1, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            gl.Disable(EnableCap.ScissorTest);
            scaled.DrawQuad(new Vector3(0, 0, -2), Quaternion.Identity, Vector2.One,
                Content.GetWhitePixel(), Color.Red);
            scaled.EndWorld();
            gl.GetInteger(GetPName.Viewport, viewport.AsSpan());
            Check(viewport.SequenceEqual(new[] { 0, 0, 640, 240 }));
            var edge = Pixel(1, 10)[0];
            Check(filter == "Nearest" ? edge > 250 : edge is > 150 and < 230);
            scaled.DrawQuad(new Vector3(0, 0, -3), Quaternion.Identity, Vector2.One,
                Content.GetWhitePixel(), new Color(0, 1, 0));
            Check(Pixel(320, 120)[0] > 245 && Pixel(320, 120)[1] < 5);
            scaled.BeginUI(new Vector2(640, 240));
            scaled.DrawUIRect(new Vector2(10, 10), Vector2.One, Color.Red);
            scaled.EndUI();
            Check(Pixel(10, 229)[0] > 245 && Pixel(11, 229)[0] < 5);
            Check(gl.GetError() == GLEnum.NoError);
            scaled.EndDraw();
        }
    }
    finally
    {
        File.WriteAllText(settingsPath, originalSettings);
        Settings.Initialize();
        PostProcessSettings.Enabled = originalEffects;
        PostProcessSettings.Parameters.Set("uVignetteStrength", 0.8f);
    }
});
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

Run("Level unload releases Detail instance buffers while shared Content meshes and textures survive", () =>
{
    ProjectShockGL.Core.Game.Initialize();
    var world = new World();
    typeof(ProjectShockGL.Core.Game).GetProperty(nameof(ProjectShockGL.Core.Game.World))!
        .SetValue(ProjectShockGL.Core.Game.Instance, world);
    try
    {
        LevelManager.Load(new Level { Details = [new DetailPlacement
            { AssetPath = "InstanceTests/Unlit", Position = new Vector3(0, 0, -3) }] });
        graphics.BeginDraw(Camera.Active);
        var queue = new RenderQueue(); world.Render(queue, false);
        var instance = DetailInstances(queue).Single();
        queue.Flush(graphics);
        graphics.EndDraw();
        var buffer = InstanceBuffer(instance);
        var mesh = Content.LoadMesh("InstancePlane")!;
        LevelManager.Unload();
        Check(world.Details.Count == 0 && !gl.IsBuffer(buffer) && InstanceDisposed(instance) && !mesh.IsDisposed);
    }
    finally { LevelManager.Unload(); }
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

static object[] DetailInstances(RenderQueue queue)
{
    var commands = (List<RenderCommand>)typeof(RenderQueue).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(queue)!;
    return commands.Where(command => command.GetType().Name == "InstancedMeshCommand")
        .Select(command => command.GetType().GetProperty("Instances", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(command)!).ToArray();
}
static uint InstanceVao(object instance) =>
    (uint)instance.GetType().GetField("_vao", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
static uint InstanceBuffer(object instance) =>
    (uint)instance.GetType().GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
static int InstanceCount(object instance) =>
    (int)instance.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
static bool InstanceDisposed(object instance) =>
    (bool)instance.GetType().GetProperty("IsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
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

sealed class PbrScene : IDisposable
{
    public World World { get; } = new();
    public void Dispose()
    {
        World.Clear();
    }
}

