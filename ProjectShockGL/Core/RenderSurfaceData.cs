using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public sealed class RenderSurfaceData
{
    public string ComponentType { get; init; } = "";
    public int Index { get; init; }
    public SurfaceMode SurfaceMode { get; init; } = SurfaceMode.Cutout;
    public float AlphaCutoff { get; init; } = 0.5f;

    public static RenderSurfaceData[] Capture(Object obj)
    {
        if (obj is BrushBox)
        {
            return [];
        }
        
        return Components(obj).GroupBy(component => component.GetType().FullName!)
            .SelectMany(group => group.Select((component, index) => new RenderSurfaceData
            {
                ComponentType = group.Key, Index = index,
                SurfaceMode = component.SurfaceMode, AlphaCutoff = component.AlphaCutoff
            })).ToArray();
    }

    public static void Apply(Object obj, IReadOnlyList<RenderSurfaceData>? data)
    {
        if (data == null)
        {
            return;
        }
        
        var components = Components(obj).GroupBy(component => component.GetType().FullName!)
            .ToDictionary(group => group.Key, group => group.ToArray());
        
        foreach (var entry in data)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ComponentType) || !Enum.IsDefined(entry.SurfaceMode) ||
                entry.Index < 0 ||
                !float.IsFinite(entry.AlphaCutoff) || entry.AlphaCutoff < 0 || entry.AlphaCutoff > 1)
            {
                throw new InvalidDataException("Invalid render surface settings.");
            }
        }
        
        foreach (var entry in data)
        {
            if (!components.TryGetValue(entry.ComponentType, out var group) || entry.Index >= group.Length)
            {
                continue;
            }
            
            group[entry.Index].SurfaceMode = entry.SurfaceMode;
            group[entry.Index].AlphaCutoff = entry.AlphaCutoff;
        }
    }

    public static IEnumerable<RenderComponent> Components(Object obj)
    {
        return obj.GetRenderComponents().Where(component => !component.IsDebug &&
                                                            component is MeshComponent or BillboardComponent
                                                                or QuadComponent);
    }
}
