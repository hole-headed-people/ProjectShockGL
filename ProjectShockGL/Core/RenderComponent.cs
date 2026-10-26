using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;



public class RenderComponent : Component
{
    public Color Color { get; set; } = Color.White;
    public Vector3 Offset { get; set; }
    public float Scale { get; set; } = 1f;
    public bool UseParentScale { get; set; } = true;
    public bool Lit { get; set; } = false;
    public bool IsDebug { get; set; } = false;
    public bool IgnoreDepth { get; set; } = false;
    public int RenderOrder { get; set; } = 0;
    
    public virtual void Render(RenderQueue queue){}
}