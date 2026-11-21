using System.Numerics;
using ProjectShockGL.Core;

namespace ProjectShockGL.Rendering;

internal sealed class DetailRenderer
{
    private readonly record struct GroupKey(Mesh Mesh, Texture Texture, Color Color, bool Lit, bool BackfaceCulling, Texture? Lightmap = null);
    private sealed record Group(GroupKey Key, InstancedMesh Instances);
    
    private List<Group> _groups = [];
    
    private int _revision = -1;
    private bool _wasEditor;
    
    private WorldLightmaps? _lightmaps;
    
    private readonly HashSet<string> _hiddenGroups = new(StringComparer.Ordinal);

    internal void Submit(DetailCollection details, RenderQueue queue, WorldLightmaps? lightmaps = null)
    {
        if (_revision != details.Revision || _wasEditor != Application.IsEditor || _lightmaps != lightmaps ||
            (Application.IsEditor && !_hiddenGroups.SetEquals(World.HiddenEditorGroups)))
        {
            Rebuild(details, lightmaps);
        }

        foreach (var group in _groups)
        {
            queue.Add(new InstancedMeshCommand
            {
                Instances = group.Instances, Texture = group.Key.Texture, Color = group.Key.Color,
                Lit = group.Key.Lit, BackfaceCulling = group.Key.BackfaceCulling, Lightmap = group.Key.Lightmap
            });
        }
    }

    private void Rebuild(DetailCollection details, WorldLightmaps? lightmaps)
    {
        var transforms = new Dictionary<GroupKey, List<Matrix4x4>>();
        var assets = new Dictionary<string, GroupKey>(StringComparer.Ordinal);
        var regions = new Dictionary<GroupKey, List<Vector4>>();
        var next = new List<Group>();
        
        try
        {
            foreach (var detail in details)
            {
                if (Application.IsEditor && detail.EditorGroups.Any(World.HiddenEditorGroups.Contains))
                {
                    continue;
                }
                
                if (!assets.TryGetValue(detail.AssetPath, out var key))
                {
                    var asset = Content.LoadDetail(detail.AssetPath)
                        ?? throw new FileNotFoundException($"Detail asset '{detail.AssetPath}' was not found.");
                    
                    var mesh = Content.LoadMesh(asset.Mesh)
                        ?? throw new FileNotFoundException($"Detail model '{asset.Mesh}' was not found.");
                    
                    var texture = asset.Texture == "WhitePixel" ? Content.GetWhitePixel()
                        : Content.LoadTexture(asset.Texture, asset.TextureFormat)
                            ?? throw new FileNotFoundException($"Detail texture '{asset.Texture}' was not found.");
                    
                    key = new GroupKey(mesh, texture, asset.Color, asset.Lit, asset.BackfaceCulling);
                    assets.Add(detail.AssetPath, key);
                }

                var region = new Vector4(1,1,0,0);
                if (key.Lit && lightmaps?.Detail(detail, key.Mesh, out var mappedMesh, out var lightmap, out region) ==
                    true)
                {
                    key = key with
                    {
                        Mesh = mappedMesh,
                        Lightmap = lightmap
                    };
                }

                if (!transforms.TryGetValue(key, out var group))
                {
                    group = [];
                    transforms.Add(key, group);
                    regions.Add(key, []);
                }
                
                regions[key].Add(region);
                group.Add(Matrix4x4.CreateScale(detail.Scale) *
                          Matrix4x4.CreateFromQuaternion(detail.Rotation) *
                          Matrix4x4.CreateTranslation(detail.Position));
            }

            foreach (var (key, group) in transforms)
            {
                _ = Content.LoadShaderProgram(key.Lit ? "InstancedLit" : "InstancedUnlit", key.Lit ? "Lit" : "Unlit");
                next.Add(new Group(key, key.Mesh.CreateInstances(group.ToArray(), key.Lightmap == null ? default : regions[key].ToArray())));
            }
        }
        catch
        {
            Cleanup.Run(next.Select<Group, Action>(group => group.Instances.Dispose).ToArray());
            
            throw;
        }

        var previous = _groups;
        
        _groups = next;
        
        _revision = details.Revision;
        _lightmaps = lightmaps;
        _wasEditor = Application.IsEditor;
        
        _hiddenGroups.Clear();

        if (_wasEditor)
        {
            _hiddenGroups.UnionWith(World.HiddenEditorGroups);
        }
        
        Cleanup.Run(previous.Select<Group, Action>(group => group.Instances.Dispose).ToArray());
    }

    internal void Clear()
    {
        var previous = _groups;
        
        _groups = [];
        
        _revision = -1;
        _lightmaps = null;
        
        _hiddenGroups.Clear();
        
        Cleanup.Run(previous.Select<Group, Action>(group => group.Instances.Dispose).ToArray());
    }
}