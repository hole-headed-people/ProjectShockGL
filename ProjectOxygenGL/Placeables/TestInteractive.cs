using ProjectOxygenGL.Core;
using ProjectShockGL;
using ProjectShockGL.Core;

namespace ProjectOxygenGL.Placeables;

[OutputEvent("OnInteract")]
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
        
        mesh.Shader = Content.LoadShaderProgram("Lit", "SimplePBR");
        mesh.ShaderParameters.Set("uRoughness", 0.4f);
        mesh.ShaderParameters.Set("uMetallic", 0.0f);
    }

    public override void Interact(Character character)
    {
        InvokeEvent("OnInteract");
    }
}
