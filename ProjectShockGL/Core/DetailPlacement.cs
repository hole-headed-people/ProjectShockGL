using System.Numerics;

namespace ProjectShockGL.Core;

public sealed class DetailPlacement
{
    private IReadOnlyList<string> _editorGroups = Array.Empty<string>();

    public Guid Id { get; init; } = Guid.NewGuid();
    public string AssetPath { get; init; } = "";
    public Vector3 Position { get; init; }
    public Quaternion Rotation { get; init; } = Quaternion.Identity;
    public Vector3 Scale { get; init; } = Vector3.One;
    public IReadOnlyList<string> EditorGroups
    {
        get => _editorGroups;
        init
        {
            if (value == null || value.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException("Detail editor groups must contain nonempty identifiers.");
            }

            _editorGroups = Array.AsReadOnly(value.Distinct(StringComparer.Ordinal).ToArray());
        }
    }

    public void Validate()
    {
        DetailAsset.ValidateReference(AssetPath, nameof(AssetPath));
        
        if (Id == Guid.Empty || !Finite(Position) || !Finite(Scale) ||
            Scale.X == 0 || Scale.Y == 0 || Scale.Z == 0 ||
            !float.IsFinite(Rotation.X) || !float.IsFinite(Rotation.Y) ||
            !float.IsFinite(Rotation.Z) || !float.IsFinite(Rotation.W) ||
            !float.IsFinite(Rotation.LengthSquared()) || Rotation.LengthSquared() < 1e-8f)
        {
            throw new InvalidDataException($"Detail '{Id}' has invalid identity or transform.");
        }
    }

    internal DetailPlacement Copy()
    {
        Validate();
        
        return new DetailPlacement
        {
            Id = Id, AssetPath = AssetPath.Replace('\\', '/'), Position = Position,
            Rotation = Quaternion.Normalize(Rotation), Scale = Scale, EditorGroups = EditorGroups
        };
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}