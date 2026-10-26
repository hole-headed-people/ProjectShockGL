using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public class Geometry : Object
{
    private bool _hasVisual;
    
    private MeshComponent _meshComponent = null!;

    public string Path { get; private set; } = "";
    
    public override void Create()
    {
        base.Create();

        _meshComponent = AddComponent<MeshComponent>();
        
        IsStatic = true;
    }

    public void SetVisual(string mesh, string texture, Color color, bool lit)
    {
        if (Application.IsEditor && _hasVisual)
        {
            return;
        }
        
        _meshComponent.Mesh = Content.LoadMesh(mesh);
        _meshComponent.Texture = !string.IsNullOrEmpty(texture) ? texture == "WhitePixel" ? Content.GetWhitePixel() : Content.LoadTexture(texture) : null;
        _meshComponent.Color = color;
        _meshComponent.Lit = lit;
        
        _hasVisual = true;
    }

    public static Geometry? Create(string name, World? world = null)
    {
        var data = Content.LoadGeometry(name);

        if (data == null)
        {
            return null;
        }
        
        var geometry = (world ?? Game.Instance.World).Spawn<Geometry>();

        geometry.Path = name;
        geometry.SetVisual(data.Model, data.Texture, data.Color, data.Lit);

        switch (data.ColliderType)
        {
            case ColliderType.Box:
                var boxCollider = geometry.AddComponent<BoxCollider>();
                
                boxCollider.HalfExtents = data.Size;
                boxCollider.Center = data.Offset;
                
                break;
            case ColliderType.Sphere:
                var sphereCollider = geometry.AddComponent<SphereCollider>();
                
                sphereCollider.Radius = data.Radius;
                sphereCollider.Center = data.Offset;
                
                break;
            case ColliderType.Capsule:
                var capsuleCollider = geometry.AddComponent<CapsuleCollider>();
                
                capsuleCollider.Radius = data.Radius;
                capsuleCollider.Height = data.Height;
                capsuleCollider.Center = data.Offset;
                
                break;
            case ColliderType.None:
            default:
                break;
        }
        
        return geometry;
    }
}
