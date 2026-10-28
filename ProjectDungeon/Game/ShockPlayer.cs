using System.Numerics;
using ProjectDungeon.Core;
using ProjectShockGL;
using Silk.NET.Input;

namespace ProjectDungeon.Game;

public class ShockPlayer : Player
{
    private const float MouseSensitivity = 0.05f;
    
    protected override void OnGameInput()
    {
        base.OnGameInput();

        if (GetCharacter() is not ShockPlayerCharacter character)
        {
            return;
        }
        
        var horizontalAxis = 0f;
        var verticalAxis = 0f;
        
        if (Input.IsKey(Key.D))
        {
            horizontalAxis = 1f;
        }

        if (Input.IsKey(Key.A))
        {
            horizontalAxis = -1f;
        }

        if (Input.IsKey(Key.W))
        {
            verticalAxis = -1f;
        }

        if (Input.IsKey(Key.S))
        {
            verticalAxis = 1f;
        }

        if (Input.IsKeyDown(Key.Escape))
        {
            Application.Quit();
        }

        if (Input.IsKeyDown(Key.F))
        {
            character.Interact();
        }
        
        character.Move(new Vector3(horizontalAxis, 0f, verticalAxis));
        
        var lookVector = Input.MouseDeltaPosition;
        lookVector.X *= -1f;
        
        character.Turn(lookVector.X * MouseSensitivity);
        character.LookAt(lookVector.Y * MouseSensitivity);
    }
}