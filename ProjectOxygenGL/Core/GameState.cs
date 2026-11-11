using System.Numerics;

namespace ProjectOxygenGL.Core;

public class GameState : ProjectShockGL.Core.GameState
{
    public string Level { get; set; } = "";
    public Vector3 PlayerPosition { get; set; }
    public Quaternion PlayerRotation { get; set; } = Quaternion.Identity;

    internal void Validate()
    {
        var rotationLength = PlayerRotation.LengthSquared();

        if (string.IsNullOrWhiteSpace(Level) ||
            !float.IsFinite(PlayerPosition.X) || !float.IsFinite(PlayerPosition.Y) || !float.IsFinite(PlayerPosition.Z) ||
            !float.IsFinite(rotationLength) || rotationLength < 1e-8f)
        {
            throw new InvalidDataException("The game state has an invalid level or player transform.");
        }
    }
}
