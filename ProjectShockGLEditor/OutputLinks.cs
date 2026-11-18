using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

internal static class OutputLinks
{
    internal static IEnumerable<(Entity Source, Object Target)> Collect(World world, IEnumerable<Object> selected)
    {
        if (!Application.IsEditor) yield break;
        foreach (var source in selected.OfType<Entity>().Distinct())
        {
            if (source.IsDestroyed || !source.IsEditorVisible) continue;
            var targets = new HashSet<Object>();
            foreach (var output in source.Outputs)
            {
                if (string.IsNullOrWhiteSpace(output.TargetName)) continue;
                var target = world.Find(output.TargetName);
                if (target != null && targets.Add(target)) yield return (source, target);
            }
        }
    }

    internal static void Render(Graphics graphics, World world, IEnumerable<Object> selected)
    {
        var draw = new DebugDraw(graphics);
        foreach (var (source, target) in Collect(world, selected))
        {
            draw.Line(source.Transform.Position, target.Transform.Position, Color.Yellow, ignoreDepth: true, thickness: .02f);
        }
    }
}