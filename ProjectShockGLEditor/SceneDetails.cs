using System.Numerics;
using Newtonsoft.Json;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Tools;

namespace ProjectShockGLEditor;

public static class SceneDetails
{
    private static void PrepareAsset(string path)
    {
        var asset = Content.LoadDetail(path) ?? throw new FileNotFoundException($"Detail asset '{path}' was not found.");
        
        _ = Content.LoadMesh(asset.Mesh) ?? throw new FileNotFoundException($"Detail mesh '{asset.Mesh}' was not found.");
        _ = asset.Texture == "WhitePixel" ? Content.GetWhitePixel()
            : Content.LoadTexture(asset.Texture, asset.TextureFormat)
                ?? throw new FileNotFoundException($"Detail texture '{asset.Texture}' was not found.");
    }

    public static DetailPlacement Create(string path, Vector3? position = null)
    {
        PrepareAsset(path);
        
        ObjectManipulator.Commit();
        UndoRedo.CommitPending();
        
        var world = ProjectShockGL.Core.Game.Instance.World;
        var previous = Selection.Targets;
        var placement = world.Details.Add(new DetailPlacement { AssetPath = path, Position = position ?? Vector3.Zero });
        
        try
        {
            Selection.SelectDetail(placement.Id);
            EditorLevelManager.RefreshDirty();
        }
        catch
        {
            world.Details.Remove(placement.Id);
            Selection.SetTargets(previous);
            throw;
        }
        
        UndoRedo.Register(new PropertyChangeAction<bool>(exists =>
        {
            if (exists)
            {
                if (world.Details.Find(placement.Id) == null)
                {
                    world.Details.Add(placement);
                }
                
                Selection.SetTarget(EditorSelectionTarget.ForDetail(world, placement.Id));
            }
            else
            {
                world.Details.Remove(placement.Id);
                
                Selection.SetTargets(previous);
            }
            
            EditorLevelManager.RefreshDirty();
        }, false, true));
        
        return placement;
    }

    internal static void SetTransform(EditorSelectionTarget target, TransformState transform, string field, bool editing)
    {
        var detail = target.Detail!;
        
        Change(target, new DetailPlacement
        {
            Id = detail.Id, AssetPath = detail.AssetPath, Position = transform.Position,
            Rotation = transform.Rotation, Scale = transform.Scale, EditorGroups = detail.EditorGroups
        }, field, editing);
    }

    internal static void SetAsset(EditorSelectionTarget target, string path)
    {
        PrepareAsset(path);
        
        var detail = target.Detail!;
        
        Change(target, new DetailPlacement
        {
            Id = detail.Id, AssetPath = path, Position = detail.Position, Rotation = detail.Rotation,
            Scale = detail.Scale, EditorGroups = detail.EditorGroups
        }, "Detail/Asset", false);
    }

    private static void Change(EditorSelectionTarget target, DetailPlacement placement, string field, bool editing)
    {
        var before = JsonConvert.SerializeObject(target.Detail, JsonHelper.GetSettings());
        target.World!.Details.Update(placement);
        
        var after = JsonConvert.SerializeObject(target.Detail, JsonHelper.GetSettings());
        
        UndoRedo.RecordEdit(target, field, before, after, json =>
        {
            if (target.IsDestroyed)
            {
                return;
            }
            
            var state = JsonConvert.DeserializeObject<DetailPlacement>(json, JsonHelper.GetSettings())!;
            
            target.World.Details.Update(state);
        }, editing);
    }
}