using System.Numerics;
using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;



public class RenderComponent : Component
{
    private SurfaceMode _surfaceMode = SurfaceMode.Cutout;
    private float _alphaCutoff = 0.5f;

    public SurfaceMode SurfaceMode
    {
        get => _surfaceMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _surfaceMode = value;
        }
    }

    public float AlphaCutoff
    {
        get => _alphaCutoff;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            _alphaCutoff = value;
        }
    }

    public Color Color { get; set; } = Color.White;
    public ShaderProgram? Shader { get; set; }
    public ShaderParameters ShaderParameters { get; } = new();
    public Vector3 Offset { get; set; }
    public float Scale { get; set; } = 1f;
    public bool UseParentScale { get; set; } = true;
    public bool Lit { get; set; } = false;
    public bool IsDebug { get; set; } = false;
    public bool IgnoreDepth { get; set; } = false;
    public int RenderOrder { get; set; } = 0;
    
    public virtual void Render(RenderQueue queue){}
}
