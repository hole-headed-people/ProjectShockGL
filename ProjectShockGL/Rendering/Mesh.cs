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
    
    private Vector2[] _lightmapUv = [];

    private readonly uint _indexCount;
    
    private float[] _vertices;
    private uint[] _indices;
    
    internal ReadOnlyMemory<Vector2> LightmapUv => _lightmapUv;
    internal ReadOnlyMemory<float> Vertices => _vertices;
    internal ReadOnlyMemory<uint> Indices => _indices;
    
    internal int Revision { get; private set; }
    internal long TriangleCount => _indexCount / 3;

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
            _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, (void*)0);
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

    public void Dispose()
    {
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
