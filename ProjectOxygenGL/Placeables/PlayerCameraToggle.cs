using ProjectOxygenGL.Core;
using ProjectShockGL.Core;
using Game = ProjectShockGL.Core.Game;

namespace ProjectOxygenGL.Placeables;

public class PlayerCameraToggle : Entity
{
    [Input("MakeActive")]
    public void MakeActive()
    {
        if (Game.Instance is not Core.Game game)
        {
            return;
        }

        var playerCharacter = game.GetPlayerCharacter();

        if (playerCharacter?.GetController() is not Player)
        {
            return;
        }

        var cameraComponent = playerCharacter.GetComponent<CameraComponent>();
        cameraComponent?.MakeActive();
    }
}