using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectOxygenGL.Placeables;

public enum PlayerStartFindType
{
    First,
    Random
}

public sealed class PlayerStart : Entity
{
    [Editable] public int Index { get; set; }
    [Editable] public int Priority { get; set; }
    
    public override void Create()
    {
        base.Create();

        if (Application.IsEditor)
        {
            var arrowComponent = AddComponent<MeshComponent>();
            
            arrowComponent.Mesh = Content.LoadMesh("Arrow");
            arrowComponent.Texture = Content.GetWhitePixel();
            arrowComponent.Color = Color.Blue;
            arrowComponent.Scale = 0.2f;
            arrowComponent.UseParentScale = false;
            arrowComponent.LocalRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        }
    }

    public override (Vector3 min, Vector3 max) GetLocalBounds()
    {
        return (new Vector3(-0.5f, -1f, -0.5f), new Vector3(0.5f, 1f, 0.5f));
    }
    
    public static PlayerStart? GetHighestPriority(int index = -1)
    {
        var all = Game.Instance.World.FindAll<PlayerStart>();
        var maxPriority = int.MinValue;

        PlayerStart? priorityPlayerStart = null;

        foreach (var playerStart in all)
        {
            if (index > -1 && playerStart.Index != index)
            {
                continue;
            }
                
            if (playerStart.Priority <= maxPriority)
            {
                continue;
            }
                
            maxPriority = playerStart.Priority;
            priorityPlayerStart = playerStart;
        }

        return priorityPlayerStart;
    }
    
    
    public static PlayerStart? GetLowestPriority(int index = -1)
    {
        var all = Game.Instance.World.FindAll<PlayerStart>();
        var maxPriority = int.MaxValue;
            
        PlayerStart? priorityPlayerStart = null;

        foreach (var playerStart in all)
        {
            if (index > -1 && playerStart.Index != index)
            {
                continue;
            }
                
            if (playerStart.Priority >= maxPriority)
            {
                continue;
            }
                
            maxPriority = playerStart.Priority;
            priorityPlayerStart = playerStart;
        }

        return priorityPlayerStart;
    }

    public static PlayerStart? GetRandom()
    {
        var all = Game.Instance.World.FindAll<PlayerStart>();
            
        return all.Length == 0 ? null : all[ProjectShockGL.Tools.Random.Range(0, all.Length)];
    }

    public static PlayerStart? GetByIndex(int index, PlayerStartFindType findType)
    {
        var all = Game.Instance.World.FindAll<PlayerStart>();
        var list = new List<PlayerStart>();
            
        foreach (var playerStart in all)
        {
            if (playerStart.Index != index)
            {
                continue;
            }
                
            if (findType == PlayerStartFindType.First)
            {
                return playerStart;
            }
                
            list.Add(playerStart);
        }

        if (list.Count != 0)
        {
            return list[ProjectShockGL.Tools.Random.Range(0, list.Count)];
        }
            
        Log.PrintError("No player start found");
                
        return null;
    }
}
