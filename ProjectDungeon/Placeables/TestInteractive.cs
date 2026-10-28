using ProjectDungeon.Core;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectDungeon.Placeables;

public class TestInteractive : Interactive
{
    
    public override void Create()
    {
        base.Create();

        var sphereCollider = AddComponent<SphereCollider>();
        sphereCollider.Radius = 1f;

        var mesh = AddComponent<MeshComponent>();
        
        mesh.Mesh = Content.LoadMesh("Suzanne");
        mesh.Texture = Content.LoadTexture("Default");
        mesh.Lit = true;
    }

    public override void Interact(Character character)
    {
        InvokeEvent("OnInteract");
    }
}