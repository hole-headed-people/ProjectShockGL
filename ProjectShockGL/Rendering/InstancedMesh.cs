using System.Numerics;
using Silk.NET.OpenGL;

namespace ProjectShockGL.Rendering;

internal sealed class InstancedMesh : IDisposable
{
    private readonly GL _gl;
    private readonly Mesh _source;
    private readonly uint _indexCount;
    
    private uint _vao;
    private uint _buffer;
    
    internal int Count { get; }
    internal long TriangleCount => _source.TriangleCount * Count;
    internal bool IsDisposed => _vao == 0;

    internal unsafe InstancedMesh(GL gl, Mesh source, uint vertices, uint indices, uint indexCount,
        ReadOnlySpan<Matrix4x4> transforms)
    {
        ObjectDisposedException.ThrowIf(source.IsDisposed, source);
        
        if (transforms.Length == 0)
        {
            throw new ArgumentException("At least one instance is required.", nameof(transforms));
        }
        
        _gl = gl;
        _source = source;
        _indexCount = indexCount;
        
        Count = transforms.Length;
        
        var previousVao = gl.GetInteger(GetPName.VertexArrayBinding);
        var previousBuffer = gl.GetInteger(GetPName.ArrayBufferBinding);
        
        try
        {
            _vao = gl.GenVertexArray();
            
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertices);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices);
            
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
            gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));

            for (uint i = 0; i < 3; i++)
            {
                gl.EnableVertexAttribArray(i);
            }

            _buffer = gl.GenBuffer();
            
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _buffer);
            
            fixed (Matrix4x4* data = transforms)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)checked(transforms.Length * sizeof(Matrix4x4)),
                    data, BufferUsageARB.StaticDraw);
            }

            for (uint i = 0; i < 4; i++)
            {
                gl.VertexAttribPointer(3 + i, 4, VertexAttribPointerType.Float, false, (uint)sizeof(Matrix4x4),
                    (void*)(i * 4 * sizeof(float)));
                gl.EnableVertexAttribArray(3 + i);
                gl.VertexAttribDivisor(3 + i, 1);
            }
        }
        catch
        {
            Dispose();
            
            throw;
        }
        finally
        {
            gl.BindVertexArray((uint)previousVao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)previousBuffer);
        }
    }

    internal unsafe void Draw()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ObjectDisposedException.ThrowIf(_source.IsDisposed, _source);
        
        _gl.BindVertexArray(_vao);
        _gl.DrawElementsInstanced(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt,
            (void*)0, (uint)Count);
    }

    public void Dispose()
    {
        if (_buffer != 0)
        {
            _gl.DeleteBuffer(_buffer);
        }

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
        }
        
        _buffer = _vao = 0;
    }
}