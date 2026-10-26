using System.Numerics;
using ProjectShockGL;
using Silk.NET.Input;
using Core_Object = ProjectShockGL.Core.Object;

namespace ProjectDungeon.Game;

public class Player : Core_Object
{
    private PlayerCharacter? _character;

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (_character == null)
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
        
        _character.Move(new Vector3(horizontalAxis, 0f, verticalAxis));
        
        var lookVector = Input.MouseDeltaPosition;
        lookVector.X *= -1f;
        
        _character.Turn(lookVector.X);
        _character.LookAt(lookVector.Y);
    }

    public void SetCharacter(PlayerCharacter character)
    {
        _character = character;
    }
}
