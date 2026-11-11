using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectOxygenGL.Placeables;

public class Cube : Entity
{
    public override void Create()
    {
        base.Create();

        var mesh = AddComponent<MeshComponent>();
        
        mesh.Mesh = Content.LoadMesh("Cube");
        mesh.Texture = Content.GetWhitePixel();
        
        mesh.Shader = Content.LoadShaderProgram("Lit", "SimplePBR");
        
        mesh.ShaderParameters.Set("uRoughness", 0.2f);
        mesh.ShaderParameters.Set("uMetallic", 0.0f);
        
        AddComponent<BoxCollider>();
        AddComponent<Rigidbody>();
    }
}