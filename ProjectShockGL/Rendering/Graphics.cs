using System.Numerics;

namespace ProjectShockGL.Rendering;

public enum Plane
{
    Xy, 
    Xz, 
    Yz
}

public class Graphics
{ 
    private Camera _camera = null!;
    private Renderer Renderer { get; }
    
    public Graphics(Renderer renderer)
    {
        Renderer = renderer;
    }

    public void BeginDraw(Camera camera)
    {
        _camera = camera;
        Renderer.BeginFrame(camera);
    }

    public void DrawLine2D(Vector3 from, Vector3 to, Plane plane, Texture texture, Color color, float thickness = 0.05f, bool ignoreDepth = false)
    {
        var normal = plane switch
        {
            Plane.Xy => Vector3.UnitZ,
            Plane.Xz => Vector3.UnitY,
            Plane.Yz => Vector3.UnitX,
            _ => Vector3.UnitY
        };

        DrawLine2DInternal(from, to, normal, texture, color, thickness, ignoreDepth);
    }
    
    private void DrawLine2DInternal(Vector3 from, Vector3 to, Vector3 normal, Texture texture, Color color, float thickness, bool ignoreDepth = false)
    {
        var dir = to - from;
        var length = dir.Length();
        
        if (length < 0.0001f)
        {
            return;
        }

        var forward = dir / length;
        var up = Vector3.Normalize(normal);
        var right = Vector3.Cross(forward, up);

        if (right.LengthSquared() < 0.0001f)
        {
            right = MathF.Abs(Vector3.Dot(forward, Vector3.UnitX)) < 0.9f
                ? Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitX))
                : Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        up = Vector3.Normalize(Vector3.Cross(right, forward));

        var center = (from + to) * 0.5f;

        var model = new Matrix4x4(
            right.X * thickness, right.Y * thickness, right.Z * thickness, 0f,
            forward.X * length,  forward.Y * length,  forward.Z * length,  0f,
            up.X,                up.Y,                up.Z,                0f,
            center.X,            center.Y,            center.Z,            1f
        );

        Renderer.DrawQuadMesh(texture, model, color, ignoreDepth: ignoreDepth);
    }
    
    public void DrawLine(Vector3 from, Vector3 to, Texture texture, Color color, float thickness = 0.05f, bool lit = false, bool ignoreDepth = false)
    {
        var dir = to - from;
        var length = dir.Length();
        
        if (length < 0.0001f)
        {
            return;
        }

        var forward = dir / length;

        var cameraForward = _camera.Transform.Forward;
        var cameraUp = _camera.Transform.Up;
        
        var right = Vector3.Cross(forward, cameraForward);
        right = right.LengthSquared() < 0.0001f ? cameraUp : Vector3.Normalize(right);
        
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        var center = (from + to) * 0.5f;

        var model = new Matrix4x4(
            right.X * thickness, right.Y * thickness, right.Z * thickness, 0f,
            forward.X * length,  forward.Y * length,  forward.Z * length,  0f,
            up.X,                up.Y,                up.Z,                0f,
            center.X,            center.Y,            center.Z,            1f
        );

        Renderer.DrawQuadMesh(texture, model, color, lit, ignoreDepth);
    }
    
    public void DrawBoxWireframe(
        Vector3 min, Vector3 max,
        Color color,
        float thickness = 0.02f)
    {
        var corners = new Vector3[]
        {
            new(min.X, min.Y, min.Z),
            new(max.X, min.Y, min.Z),
            new(max.X, max.Y, min.Z),
            new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z),
            new(max.X, min.Y, max.Z),
            new(max.X, max.Y, max.Z),
            new(min.X, max.Y, max.Z),
        };

        var edges = new (int a, int b)[]
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };

        foreach (var (a, b) in edges)
        {
            DrawLine(corners[a], corners[b], Content.GetWhitePixel(), color, thickness);
        }
    }

    public void DrawQuad(Vector3 position, Quaternion rotation, Vector2 size, Texture texture, Color color, bool lit = false, bool ignoreDepth = false)
    {
        var model = Matrix4x4.CreateScale(size.X, size.Y, 1f)
                    * Matrix4x4.CreateFromQuaternion(rotation)
                    * Matrix4x4.CreateTranslation(position);

        Renderer.DrawQuadMesh(texture, model, color, lit, ignoreDepth);
    }

    public void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Texture texture, Color color, bool lit = false, bool ignoreDepth = false)
    {
        var model = Matrix4x4.CreateScale(scale)
                  * Matrix4x4.CreateFromQuaternion(rotation)
                  * Matrix4x4.CreateTranslation(position);

        Renderer.DrawMesh(mesh, texture, model, color, lit, ignoreDepth);
    }

    public void DrawBillboard(Vector3 position, Vector2 size, Texture texture, Color color, bool lit = false, bool ignoreDepth = false)
    {
        var right = _camera.Transform.Right * size.X;
        var up = _camera.Transform.Up * size.Y;
        var forward = _camera.Transform.Forward;

        var model = new Matrix4x4(
            right.X,   right.Y,   right.Z,   0f,
            up.X,      up.Y,      up.Z,      0f,
            forward.X, forward.Y, forward.Z, 0f,
            position.X, position.Y, position.Z, 1f
        );

        Renderer.DrawQuadMesh(texture, model, color, lit, ignoreDepth);
    }

    public void EndDraw()
    {
        Renderer.EndFrame();
    }

    public void BeginUI(Vector2 size)
    {
        Renderer.BeginUI(size);
    }

    public void EndUI()
    {
        Renderer.EndUI();
    }
    
    public void DrawUIRect(Vector2 position, Vector2 size, Color color, Texture? texture = null)
    {
        var model = Matrix4x4.CreateScale(size.X, -size.Y, 1f)
                    * Matrix4x4.CreateTranslation(new Vector3(position + size * 0.5f, 0f));
        
        Renderer.DrawQuadMesh(texture ?? Content.GetWhitePixel(), model, color, ignoreDepth: true);
    }
    
    public void DrawUIRegion(Vector2 position, Vector2 size, Texture texture, Vector4 uvRect, Color color,
        Vector2? clipPosition = null, Vector2? clipSize = null)
    {
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }
        
        if (clipPosition.HasValue && clipSize.HasValue)
        {
            var min = Vector2.Max(position, clipPosition.Value);
            var max = Vector2.Min(position + size, clipPosition.Value + clipSize.Value);

            if (max.X <= min.X || max.Y <= min.Y)
            {
                return;
            }
            
            var from = (min - position) / size;
            var to = (max - position) / size;
            
            uvRect = new Vector4(uvRect.X + uvRect.Z * from.X, uvRect.Y + uvRect.W * from.Y,
                uvRect.Z * (to.X - from.X), uvRect.W * (to.Y - from.Y));
            
            position = min;
            size = max - min;
        }
        
        var model = Matrix4x4.CreateScale(size.X, -size.Y, 1f)
                    * Matrix4x4.CreateTranslation(new Vector3(position + size * 0.5f, 0));
        
        Renderer.DrawQuadMesh(texture, model, color, ignoreDepth: true, uvRect: uvRect);
    }
}
