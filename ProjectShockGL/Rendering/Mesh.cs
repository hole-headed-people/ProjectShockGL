using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

public class Mesh : IDisposable
{
    private readonly GL _gl;
    
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;
    
    private readonly uint _indexCount;
    
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    public Vector3 BoundsCenter => (BoundsMin + BoundsMax) * 0.5f;
    public Vector3 BoundsHalfExtents => (BoundsMax - BoundsMin) * 0.5f;

    public unsafe Mesh(GL gl, float[] vertices, uint[] indices)
    {
        _gl = gl;
        _indexCount = (uint)indices.Length;

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
        
        CalculateBounds(vertices);
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
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        
        fixed (float* ptr = vertices)
        {
            _gl.BufferSubData(
                BufferTargetARB.ArrayBuffer,
                0,
                (nuint)(vertices.Length * sizeof(float)),
                ptr);
        }
    }

    public void Dispose()
    {
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteVertexArray(_vao);
    }
}