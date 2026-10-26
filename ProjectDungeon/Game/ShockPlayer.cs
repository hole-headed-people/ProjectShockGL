using System.Numerics;
using ProjectDungeon.Core;
using ProjectShockGL;
using Silk.NET.Input;

namespace ProjectDungeon.Game;

public class ShockPlayer : Player
{
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
            //if (Physics.Raycast(_camera.Transform.Position, _camera.Transform.Forward, 2f, Layers.Get("Default"), out var hit))
            //{
            //    Console.Write($"{hit.Object} Hit!");
            //}
        }
        
        character.Move(new Vector3(horizontalAxis, 0f, verticalAxis));
        
        var lookVector = Input.MouseDeltaPosition;
        lookVector.X *= -1f;
        
        character.Turn(lookVector.X);
        character.LookAt(lookVector.Y);
    }
}