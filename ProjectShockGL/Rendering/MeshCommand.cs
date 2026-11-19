using System.Numerics;

namespace ProjectShockGL.Rendering;

public class MeshCommand : RenderCommand
{
    internal bool StaticBatchEligible { get; set; }
    public Mesh? Mesh { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; }
    public Texture? Texture { get; set; }
    public Color Color { get; set; }
    public ShaderProgram? Shader { get; set; }
    public ShaderParameters? ShaderParameters { get; set; }
    public bool BackfaceCulling { get; set; } = true;
    public bool Lit { get; set; }
    public bool IgnoreDepth { get; set; }

    internal override Vector3 SortPosition => Mesh == null ? Position : Vector3.Transform((Mesh.BoundsMin + Mesh.BoundsMax) * 0.5f, Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position));
    internal override bool IsOverlay => IgnoreDepth;

    public override void Execute(Graphics graphics)
    {
        if (Mesh != null && Texture != null)
        {
            graphics.DrawMesh(Mesh, Position, Rotation, Scale, Texture, Color, Lit, IgnoreDepth, Shader, ShaderParameters, BackfaceCulling, SurfaceMode, AlphaCutoff);
        }
    }
}