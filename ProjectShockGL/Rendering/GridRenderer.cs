using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class GridRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _program;

    internal GridRenderer(GL gl)
    {
        _gl = gl;
        using var vertex = new Shader(gl, ShaderType.VertexShader, """
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            uniform mat4 uProjection;
            uniform mat4 uView;
            uniform vec2 gridCenter;
            uniform vec3 gridU;
            uniform vec3 gridV;
            uniform float gridRadius;
            out vec3 worldPosition;
            void main()
            {
                vec2 xz = gridCenter + aPosition.xy * gridRadius * 2.0;
                worldPosition = gridU * xz.x + gridV * xz.y;
                gl_Position = uProjection * uView * vec4(worldPosition, 1.0);
            }
            """);
        using var fragment = new Shader(gl, ShaderType.FragmentShader, """
            #version 330 core
            in vec3 worldPosition;
            out vec4 FragColor;
            uniform vec3 gridCamera;
            uniform vec3 gridU;
            uniform vec3 gridV;
            uniform float gridStep;
            uniform float gridFadeStart;
            uniform float gridFadeEnd;
            // Integrate a fixed-width periodic line over the pixel footprint.
            // Unlike expanding lines by fwidth, this preserves opacity at grazing angles.
            float lineIntegral(float coordinate, float width)
            {
                float shifted = coordinate + width * 0.5;
                return floor(shifted) * width + min(fract(shifted), width);
            }
            float periodicLine(float coordinate, float footprint, float width)
            {
                coordinate = fract(coordinate + 0.5) - 0.5;
                float span = min(footprint, 1.0);
                return clamp((lineIntegral(coordinate + span * 0.5, width) -
                    lineIntegral(coordinate - span * 0.5, width)) / span, 0.0, 1.0);
            }
            float axisLine(float coordinate, float footprint, float halfWidth)
            {
                float lower = max(coordinate - footprint * 0.5, -halfWidth);
                float upper = min(coordinate + footprint * 0.5, halfWidth);
                return clamp((upper - lower) / footprint, 0.0, 1.0);
            }
            void main()
            {
                vec2 gridPosition = vec2(dot(worldPosition, gridU), dot(worldPosition, gridV));
                vec2 dx = dFdx(gridPosition);
                vec2 dy = dFdy(gridPosition);
                vec2 footprint = max(vec2(length(vec2(dx.x, dy.x)),
                    length(vec2(dx.y, dy.y))), vec2(0.00001));
                vec2 coordinate = gridPosition / gridStep;
                float width = 0.04; // Four percent of the cell, independent of grid step.
                vec2 lines = vec2(
                    periodicLine(coordinate.x, footprint.x / gridStep, width),
                    periodicLine(coordinate.y, footprint.y / gridStep, width));
                float minor = 1.0 - (1.0 - lines.x) * (1.0 - lines.y);
                vec2 axis = vec2(axisLine(gridPosition.x, footprint.x, gridStep * 0.02), axisLine(gridPosition.y, footprint.y, gridStep * 0.02));
                float major = 1.0 - (1.0 - axis.x) * (1.0 - axis.y);
                float coverage = max(minor, major);
                float fade = 1.0 - smoothstep(gridFadeStart, gridFadeEnd, distance(worldPosition, gridCamera));
                float pixelSpan = max(footprint.x, footprint.y) / gridStep;
                // Fade both line directions and axes together as cells shrink from four pixels to one.
                // A shared factor avoids directional cutouts at grazing camera angles.
                float detailFade = 1.0 - smoothstep(0.25, 1.0, pixelSpan);
                float alpha = coverage * fade * detailFade;
                if (alpha < 0.001) discard;
                vec3 color = mix(vec3(0.25), vec3(0.55), clamp(major / max(coverage, 0.00001), 0.0, 1.0));
                FragColor = vec4(color, alpha);
            }
            """);
        _program = new ShaderProgram(gl, vertex, fragment);
    }

    internal void Draw(Mesh quad, Camera camera, Matrix4x4 projection, Matrix4x4 view,
        float step, float radius, float fadeStart, float fadeEnd, Plane plane = Plane.Xz, bool planarFade = false)
    {
        using var state = new RenderTargetState(_gl);
        var srcRgb = _gl.GetInteger(GetPName.BlendSrcRgb);
        var dstRgb = _gl.GetInteger(GetPName.BlendDstRgb);
        var srcAlpha = _gl.GetInteger(GetPName.BlendSrcAlpha);
        var dstAlpha = _gl.GetInteger(GetPName.BlendDstAlpha);
        var equationRgb = _gl.GetInteger(GetPName.BlendEquationRgb);
        var equationAlpha = _gl.GetInteger(GetPName.BlendEquationAlpha);
        try
        {
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
            _gl.UseProgram(_program.Handle);
            _program.Write("uProjection", projection);
            _program.Write("uView", view);
            var position = camera.Transform.Position;
            var u = plane == Plane.Yz ? Vector3.UnitZ : Vector3.UnitX;
            var v = plane == Plane.Xz ? Vector3.UnitZ : Vector3.UnitY;
            _program.Write("gridU", u);
            _program.Write("gridV", v);
            var center = new Vector2(Vector3.Dot(position, u), Vector3.Dot(position, v));
            _program.Write("gridCenter", new Vector2(MathF.Floor(center.X / step) * step, MathF.Floor(center.Y / step) * step));
            if (planarFade) position = u * center.X + v * center.Y;
            _program.Write("gridCamera", position);
            _program.Write("gridRadius", radius);
            _program.Write("gridStep", step);
            _program.Write("gridFadeStart", fadeStart);
            _program.Write("gridFadeEnd", fadeEnd);
            quad.Draw();
        }
        finally
        {
            _gl.BlendFuncSeparate((BlendingFactor)srcRgb, (BlendingFactor)dstRgb,
                (BlendingFactor)srcAlpha, (BlendingFactor)dstAlpha);
            _gl.BlendEquationSeparate((BlendEquationModeEXT)equationRgb, (BlendEquationModeEXT)equationAlpha);
        }
    }

    public void Dispose() => _program.Dispose();
}
