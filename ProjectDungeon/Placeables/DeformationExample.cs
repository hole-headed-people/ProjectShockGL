using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectDungeon.Placeables;

public class DeformationExample : Entity
{
    private MeshComponent _mesh = null!;
    
    private float _amplitude = 0.2f;
    private float _speed = 2f;
    private float _frequency = 3f;

    [Editable]
    public float Amplitude
    {
        get => _amplitude;
        set { Validate(value); _amplitude = value; _mesh?.ShaderParameters.Set("uAmplitude", value); }
    }

    [Editable]
    public float Speed
    {
        get => _speed;
        set { Validate(value); _speed = value; _mesh?.ShaderParameters.Set("uSpeed", value); }
    }

    [Editable]
    public float Frequency
    {
        get => _frequency;
        set { Validate(value); _frequency = value; _mesh?.ShaderParameters.Set("uFrequency", value); }
    }

    public override void Create()
    {
        base.Create();
        _mesh = AddComponent<MeshComponent>();
        _mesh.Mesh = Content.LoadMesh("Cube");
        _mesh.Texture = Content.LoadTexture("Default");
        _mesh.Color = new Color(0.2f, 0.8f, 1f);
        _mesh.Shader = Content.LoadShaderProgram("Deform", "Pulse");
        _mesh.ShaderParameters.Set("uAmplitude", Amplitude);
        _mesh.ShaderParameters.Set("uSpeed", Speed);
        _mesh.ShaderParameters.Set("uFrequency", Frequency);
    }

    private static void Validate(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
