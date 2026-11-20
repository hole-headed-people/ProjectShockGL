using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class SelectionBoundsRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _program;
    private uint _vao;
    private uint _vbo;

    private static readonly (int A, int B)[] Edges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0), (4, 5), (5, 7), (7, 6), (6, 4),
        (0, 4), (1, 5), (2, 6), (3, 7)
    ];
    private static readonly Vector4[] Planes =
    [
        new(1, 0, 0, 1), new(-1, 0, 0, 1), new(0, 1, 0, 1),
        new(0, -1, 0, 1), new(0, 0, 1, 0), new(0, 0, -1, 1)
    ];

    internal SelectionBoundsRenderer(GL gl)
    {
        _gl = gl;
        using var state = new RenderTargetState(gl);
        using var vertex = new Shader(gl, ShaderType.VertexShader, """
            #version 330 core
            layout(location = 0) in vec4 endpoint;
            layout(location = 1) in vec4 otherEndpoint;
            layout(location = 2) in float side;
            uniform vec2 viewportSize;
            uniform float lineWidth;
            void main()
            {
                vec2 direction = (otherEndpoint.xy / otherEndpoint.w - endpoint.xy / endpoint.w) * viewportSize;
                vec2 normal = normalize(vec2(-direction.y, direction.x));
                gl_Position = endpoint;
                gl_Position.xy += normal * side * lineWidth / viewportSize * endpoint.w;
                // Keep coincident edges visible without changing the world's depth buffer.
                gl_Position.z -= 0.000002 * endpoint.w;
            }
            """);
        using var fragment = new Shader(gl, ShaderType.FragmentShader, """
            #version 330 core
            uniform float opacity;
            out vec4 FragColor;
            void main() { FragColor = vec4(1.0, 1.0, 1.0, opacity); }
            """);
        _program = new ShaderProgram(gl, vertex, fragment);
        try
        {
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                gl.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)0);
                gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)(4 * sizeof(float)));
                gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)(8 * sizeof(float)));
            }
            gl.EnableVertexAttribArray(0);
            gl.EnableVertexAttribArray(1);
            gl.EnableVertexAttribArray(2);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal int Draw(Vector3 min, Vector3 max, Matrix4x4 matrix, Vector2 logicalSize,
        Vector2 clipMin, Vector2 clipMax, float width)
    {
        if (logicalSize.X <= 0 || logicalSize.Y <= 0 || clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) return 0;
        var corners = new Vector4[8];
        for (var i = 0; i < corners.Length; i++)
            corners[i] = Vector4.Transform(new Vector4(
                (i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z, 1), matrix);
        var vertices = new List<float>(12 * 6 * 9);
        var viewport = new int[4];
        _gl.GetInteger(GetPName.Viewport, viewport);
        if (viewport[2] <= 0 || viewport[3] <= 0) return 0;
        foreach (var (a, b) in Edges)
        {
            if (!Clip(corners[a], corners[b], out var start, out var end)) continue;
            var delta = new Vector2(end.X / end.W - start.X / start.W, end.Y / end.W - start.Y / start.W);
            if (delta.LengthSquared() < 1e-12f) continue;
            Add(start, end, 1); Add(start, end, -1); Add(end, start, -1);
            Add(end, start, -1); Add(start, end, -1); Add(end, start, 1);
        }
        if (vertices.Count == 0) return 0;
        using var state = new RenderTargetState(_gl);
        var depthFunc = _gl.GetInteger(GetPName.DepthFunc);
        var scissor = new int[4];
        _gl.GetInteger(GetPName.ScissorBox, scissor);
        var srcRgb = _gl.GetInteger(GetPName.BlendSrcRgb);
        var dstRgb = _gl.GetInteger(GetPName.BlendDstRgb);
        var srcAlpha = _gl.GetInteger(GetPName.BlendSrcAlpha);
        var dstAlpha = _gl.GetInteger(GetPName.BlendDstAlpha);
        var eqRgb = _gl.GetInteger(GetPName.BlendEquationRgb);
        var eqAlpha = _gl.GetInteger(GetPName.BlendEquationAlpha);
        try
        {
            var scale = new Vector2(viewport[2] / logicalSize.X, viewport[3] / logicalSize.Y);
            var x0 = viewport[0] + (int)MathF.Ceiling(MathF.Max(0, clipMin.X) * scale.X);
            var y0 = viewport[1] + (int)MathF.Ceiling((logicalSize.Y - MathF.Min(logicalSize.Y, clipMax.Y)) * scale.Y);
            var x1 = viewport[0] + (int)MathF.Floor(MathF.Min(logicalSize.X, clipMax.X) * scale.X);
            var y1 = viewport[1] + (int)MathF.Floor((logicalSize.Y - MathF.Max(0, clipMin.Y)) * scale.Y);
            if (_gl.IsEnabled(EnableCap.ScissorTest))
            {
                x0 = Math.Max(x0, scissor[0]); y0 = Math.Max(y0, scissor[1]);
                x1 = Math.Min(x1, scissor[0] + scissor[2]); y1 = Math.Min(y1, scissor[1] + scissor[3]);
            }
            _gl.Enable(EnableCap.ScissorTest);
            _gl.Scissor(x0, y0, (uint)Math.Max(0, x1 - x0), (uint)Math.Max(0, y1 - y0));
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
            _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            _gl.UseProgram(_program.Handle);
            _program.Write("viewportSize", new Vector2(viewport[2], viewport[3]));
            _program.Write("lineWidth", width);
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                var data = vertices.ToArray();
                fixed (float* pointer = data)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), pointer, BufferUsageARB.StreamDraw);
            }
            _program.Write("opacity", 0.2f);
            _gl.DepthFunc(DepthFunction.Greater);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(vertices.Count / 9));
            _program.Write("opacity", 1f);
            _gl.DepthFunc(DepthFunction.Lequal);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(vertices.Count / 9));
            return vertices.Count / 9 / 3 * 2;
        }
        finally
        {
            _gl.DepthFunc((DepthFunction)depthFunc);
            _gl.Scissor(scissor[0], scissor[1], (uint)scissor[2], (uint)scissor[3]);
            _gl.BlendFuncSeparate((BlendingFactor)srcRgb, (BlendingFactor)dstRgb, (BlendingFactor)srcAlpha, (BlendingFactor)dstAlpha);
            _gl.BlendEquationSeparate((BlendEquationModeEXT)eqRgb, (BlendEquationModeEXT)eqAlpha);
        }

        void Add(Vector4 point, Vector4 other, float side) =>
            vertices.AddRange([point.X, point.Y, point.Z, point.W, other.X, other.Y, other.Z, other.W, side]);
    }

    private static bool Clip(Vector4 a, Vector4 b, out Vector4 start, out Vector4 end)
    {
        start = end = default;
        if (!Finite(a) || !Finite(b)) return false;
        var first = 0f;
        var last = 1f;
        foreach (var plane in Planes)
        {
            var da = Vector4.Dot(a, plane);
            var db = Vector4.Dot(b, plane);
            if (da < 0 && db < 0) return false;
            if (da >= 0 && db >= 0) continue;
            var t = da / (da - db);
            if (da < 0) first = MathF.Max(first, t); else last = MathF.Min(last, t);
            if (first > last) return false;
        }
        start = Vector4.Lerp(a, b, first);
        end = Vector4.Lerp(a, b, last);
        return start.W > 0.000001f && end.W > 0.000001f;
    }

    private static bool Finite(Vector4 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z) && float.IsFinite(point.W);

    public void Dispose()
    {
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        _vbo = _vao = 0;
        _program.Dispose();
    }
}
