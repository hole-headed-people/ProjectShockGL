using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public class Mesh : IDisposable
{
    private readonly GL _gl;

    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    
    private uint _lightmapVbo;
    private uint _colorVbo;
    
    private int _colorCapacity;
    
    private Vector2[] _lightmapUv = [];

    private readonly uint _indexCount;
    
    private float[] _vertices;
    private uint[] _indices;
    
    internal ReadOnlyMemory<Vector2> LightmapUv => _lightmapUv;
    internal ReadOnlyMemory<float> Vertices => _vertices;
    internal ReadOnlyMemory<uint> Indices => _indices;
    
    internal int Revision { get; private set; }
    internal uint? DrawIndexCount { get; set; }
    internal long TriangleCount => (DrawIndexCount ?? _indexCount) / 3;

    public bool IsDisposed => _vao == 0;
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Vector3 BoundsCenter => (BoundsMin + BoundsMax) * 0.5f;
    public Vector3 BoundsHalfExtents => (BoundsMax - BoundsMin) * 0.5f;
    
    internal InstancedMesh CreateInstances(ReadOnlySpan<Matrix4x4> transforms, ReadOnlySpan<Vector4> lightmapRegions = default)
    {
        return new InstancedMesh(_gl, this, _vbo, _ebo, _indexCount, transforms, _lightmapVbo, lightmapRegions);
    }
    
    internal Mesh CreateCombined(float[] vertices, uint[] indices, Vector2[]? lightmapUv = null)
    {
        return new Mesh(_gl, vertices, indices, lightmapUv);
    }

    public unsafe Mesh(GL gl, float[] vertices, uint[] indices, Vector2[]? lightmapUv = null)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;
        
        if (vertices.Length % 8 != 0)
        {
            throw new ArgumentException("Mesh vertices must contain position, normal and UV.", nameof(vertices));
        }

        if (lightmapUv != null && (lightmapUv.Length != vertices.Length / 8 ||
                                   lightmapUv.Any(uv => !float.IsFinite(uv.X) || !float.IsFinite(uv.Y))))
        {
            throw new ArgumentException("Lightmap UV count must match vertices and contain finite values.", nameof(lightmapUv));
        }

        try
        {
            _vao = gl.GenVertexArray();

            gl.BindVertexArray(_vao);

            _vbo = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            fixed (float* ptr = vertices)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }

            _ebo = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            fixed (uint* ptr = indices)
            {
                gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                    (nuint)(indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);
            }

            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
            gl.EnableVertexAttribArray(2);

            if (lightmapUv != null)
            {
                _lightmapVbo = gl.GenBuffer();
                
                gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lightmapVbo);
                
                fixed (Vector2* uv = lightmapUv)
                    gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(lightmapUv.Length * sizeof(Vector2)), uv, BufferUsageARB.StaticDraw);
               
                gl.VertexAttribPointer(7, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vector2), (void*)0);
                gl.EnableVertexAttribArray(7);
                
                _lightmapUv = lightmapUv.ToArray();
            }

            CalculateBounds(vertices);
            
            _vertices = vertices.ToArray();
            _indices = indices.ToArray();
        }
        catch
        {
            Dispose();
            
            throw;
        }
    }

    private void CalculateBounds(float[] vertices)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (var i = 0; i < vertices.Length; i += 8)
        {
            var v = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        BoundsMin = min;
        BoundsMax = max;
    }

    public void Draw()
    {
        _gl.BindVertexArray(_vao);

        unsafe
        {
            _gl.DrawElements(PrimitiveType.Triangles, Math.Min(DrawIndexCount ?? _indexCount, _indexCount), DrawElementsType.UnsignedInt, (void*)0);
        }
    }

    public unsafe void UpdateVertices(float[] vertices)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        
        if (vertices.Length > _vertices.Length || vertices.Length % 8 != 0)
        {
            throw new ArgumentException("Updated vertices must fit the mesh and contain position, normal and UV.", nameof(vertices));
        }
        
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        fixed (float* ptr = vertices)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(vertices.Length * sizeof(float)),
                ptr);
        }
        
        vertices.CopyTo(_vertices, 0);
        
        CalculateBounds(_vertices);
        Revision++;
    }

    internal unsafe void UpdateQuadVertices(ReadOnlySpan<float> vertices)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (vertices.Length > _vertices.Length || vertices.Length % 32 != 0)
            throw new ArgumentException("Vertices must fit the mesh and contain complete quads.", nameof(vertices));
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = vertices)
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), data);
        DrawIndexCount = (uint)(vertices.Length / 32 * 6);
    }
    internal unsafe void UpdateVertexColors(Vector4[] colors)
    {
        ObjectDisposedException.ThrowIf(IsDisposed,this);
        if (colors.Length != _vertices.Length / 8) throw new ArgumentException("Vertex color count must match the mesh.",nameof(colors));
        _gl.BindVertexArray(_vao);
        if (_colorVbo == 0) _colorVbo=_gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer,_colorVbo);
        fixed(Vector4* data=colors)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,(nuint)(colors.Length*sizeof(Vector4)),data,BufferUsageARB.DynamicDraw);
        _colorCapacity = colors.Length;
        _gl.VertexAttribPointer(9,4,VertexAttribPointerType.Float,false,(uint)sizeof(Vector4),(void*)0);
        _gl.EnableVertexAttribArray(9);
    }
    // Transient particle geometry is consumed only by Draw: no CPU mesh copy,
    // bounds scan or static-batch revision is needed for each camera pass.
    internal unsafe void UpdateParticleVertices(ReadOnlySpan<float> vertices, ReadOnlySpan<Vector4> colors)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (vertices.Length > _vertices.Length || vertices.Length % 32 != 0 || colors.Length != vertices.Length / 8)
        {
            throw new ArgumentException("Particle vertices and colors must fit the mesh and contain complete quads.");
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* data = vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), data);
        }

        _gl.BindVertexArray(_vao);
        if (_colorVbo == 0)
        {
            _colorVbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colorVbo);
            _gl.VertexAttribPointer(9, 4, VertexAttribPointerType.Float, false, (uint)sizeof(Vector4), (void*)0);
            _gl.EnableVertexAttribArray(9);
        }
        else
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colorVbo);
        }

        var capacity = _vertices.Length / 8;
        if (_colorCapacity != capacity)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(capacity * sizeof(Vector4)), (void*)0, BufferUsageARB.DynamicDraw);
            _colorCapacity = capacity;
        }
        fixed (Vector4* data = colors)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(colors.Length * sizeof(Vector4)), data);
        }
        DrawIndexCount = (uint)(vertices.Length / 32 * 6);
    }
    public void Dispose()
    {
        if (_colorVbo != 0) _gl.DeleteBuffer(_colorVbo);
        _colorVbo=0;
        if (_lightmapVbo != 0)
        {
            _gl.DeleteBuffer(_lightmapVbo);
        }
        
        _lightmapVbo = 0;
        _lightmapUv = [];
        
        if (_vbo != 0)
        {
            _gl.DeleteBuffer(_vbo);
        }

        if (_ebo != 0)
        {
            _gl.DeleteBuffer(_ebo);
        }

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
        }
        
        _vbo = _ebo = _vao = 0;
        
        _vertices = [];
        _indices = [];
        
        Revision++;
    }
}
