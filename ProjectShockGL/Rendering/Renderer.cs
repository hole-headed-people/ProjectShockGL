using System.Numerics;
using ProjectShockGL.Core;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public class Renderer : IDisposable
{
    private readonly GL _gl;

    private int _screenWidth;
    private int _screenHeight;

    private uint _unlitProgram;
    private uint _litProgram;

    // Uniform-локации unlit
    private readonly int _unlitProjection;
    private readonly int _unlitView;
    private readonly int _unlitModel;
    private readonly int _unlitTexture;
    private readonly int _unlitColor;
    private readonly int _unlitUvRect;
    private readonly int _unlitAlphaCutoff;
    private readonly int _unlitCameraPos;
    private readonly int _unlitFogColor;
    private readonly int _unlitFogNear;
    private readonly int _unlitFogFar;
    private readonly int _unlitFogEnabled;

    // Uniform-локации lit
    private readonly int _litProjection;
    private readonly int _litView;
    private readonly int _litModel;
    private readonly int _litTexture;
    private readonly int _litColor;
    private readonly int _litMainLightEnabled;
    private readonly int _litLightDirection;
    private readonly int _litLightColor;
    private readonly int _litLightIntensity;
    private readonly int _litAmbientColor;
    private readonly int _litCameraPos;
    private readonly int _litFogColor;
    private readonly int _litFogNear;
    private readonly int _litFogFar;
    private readonly int _litFogEnabled;

    private readonly int _litPointLightCount;
    private readonly int _litPointLightPositions;
    private readonly int _litPointLightColors;
    private readonly int _litPointLightIntensities;
    private readonly int _litPointLightRadii;
    private readonly int _litPointLightDirections;
    private readonly int _litPointLightConeCosines;

    private readonly Mesh _quadMesh;
    private Matrix4x4 _frameProjection;
    private Matrix4x4 _frameView;
    private bool _isUI;
    private bool _uiCullFace;
    private bool _uiDepthTest;
    private readonly RenderPreviewSettings? _preview;
    private bool _frameFogEnabled;
    private bool _frameForceUnlit;

    public Renderer(GL gl, int screenWidth, int screenHeight, RenderPreviewSettings? preview = null)
    {
        _gl = gl;
        _preview = preview;

        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        try
        {
            _unlitProgram = CreateShaderProgram("Unlit");
            _unlitProjection = _gl.GetUniformLocation(_unlitProgram, "uProjection");
            _unlitView = _gl.GetUniformLocation(_unlitProgram, "uView");
            _unlitModel = _gl.GetUniformLocation(_unlitProgram, "uModel");
            _unlitTexture = _gl.GetUniformLocation(_unlitProgram, "uTexture");
            _unlitColor = _gl.GetUniformLocation(_unlitProgram, "uColor");
            _unlitUvRect = _gl.GetUniformLocation(_unlitProgram, "uUvRect");
            _unlitAlphaCutoff = _gl.GetUniformLocation(_unlitProgram, "uAlphaCutoff");
            _unlitCameraPos = _gl.GetUniformLocation(_unlitProgram, "uCameraPos");
            _unlitFogColor = _gl.GetUniformLocation(_unlitProgram, "uFogColor");
            _unlitFogNear = _gl.GetUniformLocation(_unlitProgram, "uFogNear");
            _unlitFogFar = _gl.GetUniformLocation(_unlitProgram, "uFogFar");
            _unlitFogEnabled = _gl.GetUniformLocation(_unlitProgram, "uFogEnabled");

            _litProgram = CreateShaderProgram("Lit");
            _litProjection = _gl.GetUniformLocation(_litProgram, "uProjection");
            _litView = _gl.GetUniformLocation(_litProgram, "uView");
            _litModel = _gl.GetUniformLocation(_litProgram, "uModel");
            _litTexture = _gl.GetUniformLocation(_litProgram, "uTexture");
            _litColor = _gl.GetUniformLocation(_litProgram, "uColor");
            _litMainLightEnabled = _gl.GetUniformLocation(_litProgram, "uMainLightEnabled");
            _litLightDirection = _gl.GetUniformLocation(_litProgram, "uLightDirection");
            _litLightColor = _gl.GetUniformLocation(_litProgram, "uLightColor");
            _litLightIntensity = _gl.GetUniformLocation(_litProgram, "uLightIntensity");
            _litAmbientColor = _gl.GetUniformLocation(_litProgram, "uAmbientColor");
            _litCameraPos = _gl.GetUniformLocation(_litProgram, "uCameraPos");
            _litFogColor = _gl.GetUniformLocation(_litProgram, "uFogColor");
            _litFogNear = _gl.GetUniformLocation(_litProgram, "uFogNear");
            _litFogFar = _gl.GetUniformLocation(_litProgram, "uFogFar");
            _litFogEnabled = _gl.GetUniformLocation(_litProgram, "uFogEnabled");
            _litPointLightCount = _gl.GetUniformLocation(_litProgram, "uPointLightCount");
            _litPointLightPositions = _gl.GetUniformLocation(_litProgram, "uPointLightPositions");
            _litPointLightColors = _gl.GetUniformLocation(_litProgram, "uPointLightColors");
            _litPointLightIntensities = _gl.GetUniformLocation(_litProgram, "uPointLightIntensities");
            _litPointLightRadii = _gl.GetUniformLocation(_litProgram, "uPointLightRadii");
            _litPointLightDirections = _gl.GetUniformLocation(_litProgram, "uPointLightDirections");
            _litPointLightConeCosines = _gl.GetUniformLocation(_litProgram, "uPointLightConeCosines");

            float[] quadVertices =
            [
                -0.5f, -0.5f, 0f,   0f, 0f, 1f,   0f, 1f,
                 0.5f, -0.5f, 0f,   0f, 0f, 1f,   1f, 1f,
                 0.5f,  0.5f, 0f,   0f, 0f, 1f,   1f, 0f,
                -0.5f,  0.5f, 0f,   0f, 0f, 1f,   0f, 0f,
            ];

            uint[] quadIndices = [0, 1, 2, 0, 2, 3];

            _quadMesh = new Mesh(_gl, quadVertices, quadIndices);

            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);

            if (!Application.IsDebug)
            {
                _gl.Enable(EnableCap.CullFace);
            }

            _gl.CullFace(TriangleFace.Back);
            _gl.FrontFace(FrontFaceDirection.Ccw);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        catch
        {
            Cleanup.Run(() => _quadMesh?.Dispose(),
                () => { if (_unlitProgram != 0) _gl.DeleteProgram(_unlitProgram); },
                () => { if (_litProgram != 0) _gl.DeleteProgram(_litProgram); });
            throw;
        }
    }

    public void BeginFrame(Camera camera)
    {
        var environmentEnabled = _preview?.EnvironmentEnabled ?? true;

        _frameFogEnabled = environmentEnabled && Environment.IsFogEnabled;
        _frameForceUnlit = _preview?.Unlit ?? false;

        _gl.Viewport(0, 0, (uint)_screenWidth, (uint)_screenHeight);
        _gl.ClearColor(camera.ClearColor.R, camera.ClearColor.G, camera.ClearColor.B, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var projection = camera.GetProjectionMatrix();
        var view = camera.GetViewMatrix();

        _frameProjection = projection;
        _frameView = view;

        _gl.UseProgram(_unlitProgram);

        unsafe
        {
            _gl.UniformMatrix4(_unlitProjection, 1, false, (float*)&projection);
            _gl.UniformMatrix4(_unlitView, 1, false, (float*)&view);
        }

        _gl.Uniform1(_unlitTexture, 0);

        _gl.Uniform3(_unlitCameraPos, camera.Transform.Position.X, camera.Transform.Position.Y, camera.Transform.Position.Z);
        _gl.Uniform3(_unlitFogColor, Environment.FogColor.R, Environment.FogColor.G, Environment.FogColor.B);
        _gl.Uniform1(_unlitFogNear, Environment.FogNear);
        _gl.Uniform1(_unlitFogFar, Environment.FogFar);
        _gl.Uniform1(_unlitFogEnabled, _frameFogEnabled ? 1 : 0);

        _gl.UseProgram(_litProgram);

        unsafe
        {
            _gl.UniformMatrix4(_litProjection, 1, false, (float*)&projection);
            _gl.UniformMatrix4(_litView, 1, false, (float*)&view);
        }

        _gl.Uniform1(_litTexture, 0);

        if (Light.Main is { IsActive: true, IsDestroyed: false })
        {
            var mainLightDirection = Light.Main.Transform.Forward;

            _gl.Uniform1(_litMainLightEnabled, 1f);
            _gl.Uniform3(_litLightDirection,
                mainLightDirection.X, mainLightDirection.Y, mainLightDirection.Z);
            _gl.Uniform3(_litLightColor,
                Light.Main.Color.R, Light.Main.Color.G, Light.Main.Color.B);
            _gl.Uniform1(_litLightIntensity, Light.Main.Intensity);
        }
        else
        {
            _gl.Uniform1(_litMainLightEnabled, 0f);
        }

        var ambient = environmentEnabled ? Environment.AmbientColor : Color.White;
        _gl.Uniform3(_litAmbientColor, ambient.R, ambient.G, ambient.B);

        _gl.Uniform3(_litCameraPos, camera.Transform.Position.X, camera.Transform.Position.Y, camera.Transform.Position.Z);
        _gl.Uniform3(_litFogColor, Environment.FogColor.R, Environment.FogColor.G, Environment.FogColor.B);
        _gl.Uniform1(_litFogNear, Environment.FogNear);
        _gl.Uniform1(_litFogFar, Environment.FogFar);
        _gl.Uniform1(_litFogEnabled, _frameFogEnabled ? 1 : 0);

        const int maxPointLights = 16;

        var positions = new float[maxPointLights * 3];
        var colors = new float[maxPointLights * 3];
        var intensities = new float[maxPointLights];
        var radii = new float[maxPointLights];
        var directions = new float[maxPointLights * 3];
        var coneCosines = new float[maxPointLights * 2];

        var activeLights = Light.All.Where(light => light.IsActive && !light.IsDestroyed).Take(maxPointLights).ToArray();
        var count = activeLights.Length;

        for (var i = 0; i < count; i++)
        {
            var light = activeLights[i];

            positions[i * 3 + 0] = light.Transform.Position.X;
            positions[i * 3 + 1] = light.Transform.Position.Y;
            positions[i * 3 + 2] = light.Transform.Position.Z;

            colors[i * 3 + 0] = light.Color.R;
            colors[i * 3 + 1] = light.Color.G;
            colors[i * 3 + 2] = light.Color.B;

            intensities[i] = light.Intensity;
            radii[i] = light.Radius;
            var direction = light.Transform.Forward;
            directions[i * 3] = direction.X;
            directions[i * 3 + 1] = direction.Y;
            directions[i * 3 + 2] = direction.Z;
            coneCosines[i * 2] = MathF.Cos(light.InnerAngle * MathF.PI / 360f);
            // Values below -1 identify ordinary point lights; they have no angular attenuation.
            coneCosines[i * 2 + 1] = light.Type == LightType.Spot
                ? MathF.Cos(light.OuterAngle * MathF.PI / 360f) : -2f;
        }

        _gl.Uniform1(_litPointLightCount, count);

        unsafe
        {
            fixed (float* p = positions)
            {
                _gl.Uniform3(_litPointLightPositions, (uint)count, p);
            }
            fixed (float* c = colors)
            {
                _gl.Uniform3(_litPointLightColors, (uint)count, c);
            }
            fixed (float* i = intensities)
            {
                _gl.Uniform1(_litPointLightIntensities, (uint)count, i);
            }
            fixed (float* r = radii)
            {
                _gl.Uniform1(_litPointLightRadii, (uint)count, r);
            }
            fixed (float* d = directions)
                _gl.Uniform3(_litPointLightDirections, (uint)count, d);
            fixed (float* c = coneCosines)
                _gl.Uniform2(_litPointLightConeCosines, (uint)count, c);
        }
    }

    public void EndFrame()
    {

    }

    public void BeginUI(Vector2 size)
    {
        if (_isUI)
        {
            throw new InvalidOperationException("A UI pass is already active.");
        }

        if (size.X <= 0 || size.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        _uiCullFace = _gl.IsEnabled(EnableCap.CullFace);
        _uiDepthTest = _gl.IsEnabled(EnableCap.DepthTest);

        _isUI = true;

        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);

        SetUnlitMatrices(Matrix4x4.CreateOrthographicOffCenter(0, size.X, size.Y, 0, -1, 1),
            Matrix4x4.Identity);
        _gl.Uniform1(_unlitFogEnabled, 0);
    }

    public void EndUI()
    {
        if (!_isUI)
        {
            throw new InvalidOperationException("No UI pass is active.");
        }

        _isUI = false;

        SetUnlitMatrices(_frameProjection, _frameView);
        _gl.Uniform1(_unlitFogEnabled, _frameFogEnabled ? 1 : 0);

        if (_uiCullFace)
        {
            _gl.Enable(EnableCap.CullFace);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }

        if (_uiDepthTest)
        {
            _gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Disable(EnableCap.DepthTest);
        }
    }

    private unsafe void SetUnlitMatrices(Matrix4x4 projection, Matrix4x4 view)
    {
        _gl.UseProgram(_unlitProgram);
        _gl.UniformMatrix4(_unlitProjection, 1, false, (float*)&projection);
        _gl.UniformMatrix4(_unlitView, 1, false, (float*)&view);
    }

    public void DrawQuadMesh(Texture texture, Matrix4x4 model, Color color, bool lit = false, bool ignoreDepth = false,
        Vector4? uvRect = null)
    {
        DrawMeshInternal(_quadMesh, texture, model, color, lit, ignoreDepth, uvRect);
    }

    public void DrawMesh(Mesh mesh, Texture texture, Matrix4x4 model, Color color, bool lit = false, bool ignoreDepth = false)
    {
        DrawMeshInternal(mesh, texture, model, color, lit, ignoreDepth);
    }

    private void DrawMeshInternal(Mesh mesh, Texture texture, Matrix4x4 model, Color color, bool lit,
        bool ignoreDepth = false, Vector4? uvRect = null)
    {
        if (_frameForceUnlit)
        {
            lit = false;
        }

        if (_isUI)
        {
            lit = false;
            ignoreDepth = true;
        }

        var program = lit ? _litProgram : _unlitProgram;
        var modelLoc = lit ? _litModel : _unlitModel;
        var colorLoc = lit ? _litColor : _unlitColor;

        _gl.UseProgram(program);

        if (!lit)
        {
            var uv = uvRect ?? new Vector4(0, 0, 1, 1);

            _gl.Uniform4(_unlitUvRect, uv.X, uv.Y, uv.Z, uv.W);
            _gl.Uniform1(_unlitAlphaCutoff, _isUI ? 0f : 0.5f);
        }

        unsafe
        {
            _gl.UniformMatrix4(modelLoc, 1, false, (float*)&model);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture.Handle);
        _gl.Uniform4(colorLoc, color.R, color.G, color.B, color.A);

        if (ignoreDepth)
        {
            _gl.Disable(EnableCap.DepthTest);
        }
        else
        {
            _gl.Enable(EnableCap.DepthTest);
        }

        mesh.Draw();

        if (!_isUI)
        {
            _gl.Enable(EnableCap.DepthTest);
        }
    }

    private uint CreateShaderProgram(string name)
    {
        var vertexShader = Content.LoadShader(name, ShaderType.VertexShader);
        var fragmentShader = Content.LoadShader(name, ShaderType.FragmentShader);

        if (vertexShader == null || fragmentShader == null)
        {
            throw new Exception($"Failed to load shaders: {name}");
        }

        var program = _gl.CreateProgram();
        try
        {

            _gl.AttachShader(program, vertexShader.Handle);
            _gl.AttachShader(program, fragmentShader.Handle);
            _gl.LinkProgram(program);

            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var status);

            if (status == 0)
            {
                throw new Exception($"Link error ({name}): {_gl.GetProgramInfoLog(program)}");
            }

            _gl.DetachShader(program, vertexShader.Handle);
            _gl.DetachShader(program, fragmentShader.Handle);

            return program;
        }
        catch
        {
            _gl.DeleteProgram(program);
            throw;
        }
    }

    public void OnResize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;
    }

    public void Dispose()
    {
        var unlit = _unlitProgram;
        var lit = _litProgram;
        
        _unlitProgram = _litProgram = 0;
        
        Cleanup.Run(_quadMesh.Dispose,
            () =>
            {
                if (unlit != 0)
                {
                    _gl.DeleteProgram(unlit);
                }
            },
            () =>
            {
                if (lit != 0)
                {
                    _gl.DeleteProgram(lit);
                }
            });
    }
}
