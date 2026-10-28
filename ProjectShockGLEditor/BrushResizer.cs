using System.Numerics;
using ImGuiNET;
using ProjectShockGL;
using ProjectShockGL.Core;
using ProjectShockGL.Rendering;

namespace ProjectShockGLEditor;

internal static class BrushResizer
{
    private readonly record struct State(Vector3 Size, TransformState Transform)
    {
        public static State Capture(BrushBox brush)
        {
            return new State(brush.Size, TransformState.Capture(brush.Transform));
        }

        public void Apply(BrushBox brush)
        {
            brush.Size = Size; 
            Transform.Apply(brush.Transform);
        }
    }
    
    private static BrushBox? _target;
    private static State _before;
    
    private static int _face;

    private static Vector3 _axis;
    private static Vector3 _plane;
    private static Vector3 _faceStart;

    private static float _startOffset;
    private static float _stretch;
    
    private static readonly Vector3[] Axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
    private static readonly uint[] Colors = [0xFF5555EF, 0xFF65CF65, 0xFFFFAA55];
    public static bool IsDragging => _target != null;

    public static void Cancel()
    {
        if (_target is { IsDestroyed: false })
        {
            _before.Apply(_target);
        }
        
        _target = null;
    }

    public static void Commit()
    {
        if (_target == null)
        {
            return;
        }
        
        var target = _target;
        _target = null;

        if (target.IsDestroyed)
        {
            return;
        }
        
        var before = _before;
        var after = State.Capture(target);

        if (before == after)
        {
            return;
        }
        
        var reference = EditorObjectReference.Get(target);
        
        UndoRedo.Register(new PropertyChangeAction<State>(state =>
        {
            if (reference.Current is BrushBox { IsDestroyed: false } brush)
            {
                state.Apply(brush);
            }
        }, before, after));
    }

    internal static bool BeginDrag(BrushBox brush, int face, Vector2 mouse, Vector3 cameraForward, Matrix4x4 vp, Vector2 screen)
    {
        if (face is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(face));
        }
        
        var index = face / 2;
        var scale = brush.Transform.Scale[index];

        if (Math.Abs(scale) < 1e-6f || !float.IsFinite(scale))
        {
            return false;
        }
        
        var axis = Vector3.Transform(Axes[index] * (face % 2 == 0 ? -1 : 1) * MathF.Sign(scale), brush.Transform.Rotation);
        var plane = cameraForward - axis * Vector3.Dot(cameraForward, axis);

        if (plane.LengthSquared() < 1e-6f)
        {
            return false;
        }
        
        plane = Vector3.Normalize(plane);
        
        var faceStart = brush.Transform.Position + axis * (brush.Size[index] * Math.Abs(scale) * .5f);

        if (!GizmoMath.ScreenRay(mouse, screen, vp, out var origin, out var direction) ||
            !GizmoMath.RayPlane(origin, direction, faceStart, plane, out var hit))
        {
            return false;
        }
        
        _target = brush;
        _face = face;
        _before = State.Capture(brush);
        _axis = axis;
        _plane = plane;
        _faceStart = faceStart;
        _stretch = Math.Abs(scale);
        _startOffset = Vector3.Dot(hit - faceStart, axis);
        
        return true;
    }

    internal static void ApplyDrag(Vector2 mouse, Matrix4x4 vp, Vector2 screen, bool snap)
    {
        if (_target == null || !GizmoMath.ScreenRay(mouse, screen, vp, out var origin, out var direction) ||
            !GizmoMath.RayPlane(origin, direction, _faceStart, _plane, out var hit))
        {
            return;
        }
        
        var distance = Vector3.Dot(hit - _faceStart, _axis) - _startOffset;

        if (snap)
        {
            distance = GizmoMath.Snap(distance, SceneGrid.Step);
        }
        
        var index = _face / 2;
        var size = _before.Size;
        
        size[index] = Math.Max(.01f, _before.Size[index] + distance / _stretch);
        
        var actualDistance = (size[index] - _before.Size[index]) * _stretch;

        if (!float.IsFinite(actualDistance))
        {
            return;
        }
        
        _target.Size = size;
        _target.Transform.Position = _before.Transform.Position + _axis * (actualDistance * .5f);
    }

    public static bool Draw(Vector2 screen, Vector2 sceneMin, Vector2 sceneMax, bool allowInput)
    {
        if (_target != null && (_target.IsDestroyed || !_target.IsEditorVisible ||
                                Selection.Objects.Length != 1 || Selection.Object != _target))
        {
            Cancel();
        }

        if (IsDragging && ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Cancel(); 
            return true;
        }

        if (IsDragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            Commit(); 
            return true;
        }

        if (Selection.Objects.Length != 1 || Selection.Object is not BrushBox { IsEditorVisible: true } brush)
        {
            return false;
        }
        
        var camera = Camera.Active;
        var vp = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        var mouse = ImGui.GetIO().MousePos;
        var draw = ImGui.GetBackgroundDrawList();
        var hovered = -1;
        var closest = float.PositiveInfinity;
        
        draw.PushClipRect(sceneMin, sceneMax, true);
        
        for (var face = 0; face < 6; face++)
        {
            var index = face / 2;

            if (Math.Abs(brush.Transform.Scale[index]) < 1e-6f)
            {
                continue;
            }
            
            var local = Axes[index] * brush.Size[index] * .5f * (face % 2 == 0 ? -1 : 1);
            var world = brush.Transform.TransformPoint(local);

            if (!GizmoMath.Project(world, vp, screen, out var point))
            {
                continue;
            }
            
            var color = IsDragging && _face == face ? 0xFF55FFFF : Colors[index];
            
            draw.AddRectFilled(point - new Vector2(5), point + new Vector2(5), color);
            draw.AddRect(point - new Vector2(6), point + new Vector2(6), 0xFF202020);

            if (Vector2.Distance(mouse, point) > 10)
            {
                continue;
            }
            
            var depth = Vector3.DistanceSquared(world, camera.Transform.Position);

            if (depth < closest)
            {
                closest = depth; 
                hovered = face;
            }
        }
        
        draw.PopClipRect();
        
        if (IsDragging)
        {
            ApplyDrag(mouse, vp, screen, ObjectManipulator.SnapEnabled ||
                Input.IsKey(Silk.NET.Input.Key.ShiftLeft) || Input.IsKey(Silk.NET.Input.Key.ShiftRight));
            
            return true;
        }

        if (allowInput && hovered >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            BeginDrag(brush, hovered, mouse, camera.Transform.Forward, vp, screen);
        }
        
        return allowInput && hovered >= 0;
    }
}
