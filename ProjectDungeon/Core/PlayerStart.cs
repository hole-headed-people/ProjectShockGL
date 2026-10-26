using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectDungeon.Core;

public class PlayerStart : Entity
{
    public override void Create()
    {
        base.Create();

        if (Application.IsEditor)
        {
            var arrowComponent = AddComponent<MeshComponent>();
            
            arrowComponent.Mesh = Content.LoadMesh("Arrow");
            arrowComponent.Texture = Content.GetWhitePixel();
            arrowComponent.Color = Color.Blue;
            arrowComponent.Scale = 0.2f;
        }
    }

    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        return (new Vector3(-0.5f, -1f, -0.5f), new Vector3(0.5f, 1f, 0.5f));
    }
}