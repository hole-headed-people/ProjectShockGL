using System.Numerics;
using ImGuiNET;
using ProjectShockGL.Rendering;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public enum ManipulationMode
{
    Move,
    Rotate, 
    Scale, 
    Resize
}

public static class ObjectManipulator
{
    private static EditorSelectionTarget? _target;
    
    private static TransformState _before;
    
    private static int _dragAxis = -1;

    private static Vector3 _axis;
    private static Vector3 _planeNormal;
    private static Vector3 _lastDirection;
    
    private static Vector2 _startMouse;

    private static float _startOffset;
    private static float _length;
    private static float _angle;
    
    private static readonly Vector3[] Axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
    private static readonly uint[] Colors = [0xFF5555EF, 0xFF65CF65, 0xFFFFAA55];

    private static (EditorSelectionTarget Target, TransformState State)[] _members = [];
    private static readonly ProjectShockGL.Core.Transform GroupTransform = new();
    
    public static ManipulationMode Mode { get; set; } = ManipulationMode.Move;
    public static bool Local { get; set; }
    public static bool SnapEnabled { get; set; }
    public static float RotationStep { get; set; } = 15;
    public static float ScaleStep { get; set; } = 0.1f;
    public static bool IsDragging => _target != null || BrushResizer.IsDragging;
    private static bool IsGroup => _members.Length > 1;
    
    public static Vector3 Pivot => GetPivot(Selection.Targets.Where(o => o.IsEditorVisible).ToArray());
    
    private static Vector3 GetPivot(EditorSelectionTarget[] objects)
    {
        return objects.Length == 0
            ? Vector3.Zero
            : objects.Aggregate(Vector3.Zero, (sum, obj) => sum + obj.Transform.Position) / objects.Length;
    }

    public static void Cancel()
    {
        BrushResizer.Cancel();
        
        foreach (var member in _members)
        {
            if (!member.Target.IsDestroyed)
            {
                member.Target.Apply(member.State);
            }
        }
        
        _target = null;
        _members = [];
        _dragAxis = -1;
    }

    public static void Commit()
    {
        BrushResizer.Commit();

        if (_target == null)
        {
            return;
        }
        
        var members = _members;
        var before = members.Select(m => m.State).ToArray();
        var after = members.Select(m => TransformState.Capture(m.Target.Transform)).ToArray();
        
        _target = null;
        _members = [];
        _dragAxis = -1;

        if (before.SequenceEqual(after))
        {
            return;
        }
        
        UndoRedo.Register(new PropertyChangeAction<TransformState[]>(states =>
        {
            for (var i = 0; i < members.Length; i++)
            {
                if (!members[i].Target.IsDestroyed)
                {
                    members[i].Target.Apply(states[i]);
                }
            }
        }, before, after));
    }
    
    public static bool Draw(Vector2 screenSize, Vector2 sceneMin, Vector2 sceneMax, bool allowInput)
    {
        if (Mode == ManipulationMode.Resize)
        {
            if (Selection.Details.Length > 0) return false;
            return BrushResizer.Draw(screenSize, sceneMin, sceneMax, allowInput);
        }
        
        if (_target != null && (!Selection.Contains(_target) ||
            _members.Any(m => m.Target.IsDestroyed || !m.Target.IsEditorVisible)))
        {
            Cancel();
        }
        
        var target = Selection.Targets.LastOrDefault(o => o.IsEditorVisible);

        if (target == null || target.IsDestroyed || !target.IsEditorVisible)
        {
            return false;
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

        if (screenSize.X <= 0 || screenSize.Y <= 0)
        {
            return IsDragging;
        }

        var camera = Camera.Active;
        var vp = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        var position = IsDragging ? (IsGroup ? GroupTransform.Position : target.Transform.Position) : Pivot;
        var depth = Vector3.Dot(position - camera.Transform.Position, camera.Transform.Forward);
        var length = camera.Orthographic ? camera.OrthoSize * 90 / screenSize.Y
            : 2 * depth * MathF.Tan(camera.Fov * 0.5f) * 90 / screenSize.Y;

        if (length <= 0 || !GizmoMath.Project(position, vp, screenSize, out var center))
        {
            return IsDragging;
        }

        var mouse = ImGui.GetIO().MousePos;
        var draw = ImGui.GetBackgroundDrawList();
        
        draw.PushClipRect(sceneMin, sceneMax, true);
        
        var hovered = -1;
        var bestDistance = 9f;
        
        for (var i = 0; i < 3; i++)
        {
            var axis = GetAxis(target, i);
            if (camera.Orthographic && Mode == ManipulationMode.Rotate && MathF.Abs(Vector3.Dot(axis, camera.Transform.Forward)) < 0.001f) continue;
            var color = IsDragging && _dragAxis == i ? 0xFF55FFFF : Colors[i];
            
            if (Mode == ManipulationMode.Rotate)
            {
                var u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
                var v = Vector3.Cross(axis, u);
                
                for (var segment = 0; segment < 64; segment++)
                {
                    var a = segment * MathF.Tau / 64;
                    var b = (segment + 1) * MathF.Tau / 64;
                    
                    if (!GizmoMath.Project(position + (u * MathF.Cos(a) + v * MathF.Sin(a)) * length, vp, screenSize, out var p1) ||
                        !GizmoMath.Project(position + (u * MathF.Cos(b) + v * MathF.Sin(b)) * length, vp, screenSize, out var p2)) continue;
                    {
                        draw.AddLine(p1, p2, color, 2.5f);
                    }
                    
                    var distance = GizmoMath.SegmentDistance(mouse, p1, p2);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance; hovered = i;
                    }
                }
            }
            else if (GizmoMath.Project(position + axis * length, vp, screenSize, out var end) &&
                     Vector2.Distance(center, end) >= 14)
            {
                var direction = Vector2.Normalize(end - center);
                var start = center + direction * 12;
                
                draw.AddLine(start, end, color, 3);
                
                if (Mode == ManipulationMode.Move)
                {
                    var side = new Vector2(-direction.Y, direction.X) * 5;
                    draw.AddTriangleFilled(end + direction * 5, end - direction * 9 + side, end - direction * 9 - side, color);
                }
                else
                {
                    draw.AddRectFilled(end - new Vector2(5), end + new Vector2(5), color);
                }
                
                draw.AddText(end + new Vector2(7, -7), color, "XYZ"[i].ToString());
                
                var distance = GizmoMath.SegmentDistance(mouse, start, end);

                if (distance < bestDistance)
                {
                    bestDistance = distance; hovered = i;
                }
            }
        }

        if (Mode == ManipulationMode.Scale)
        {
            draw.AddRectFilled(center - new Vector2(6), center + new Vector2(6), 0xFFFFFFFF);
            
            if (Vector2.Distance(mouse, center) <= 9)
            {
                hovered = 3;
            }
        }
        else
        {
            draw.AddCircleFilled(center, 4, 0xFFFFFFFF);
        }

        if (hovered >= 0 && !IsDragging && allowInput)
        {
            draw.AddCircle(mouse, 9, 0xFF55FFFF, 16, 2);
        }
        
        draw.PopClipRect();

        if (IsDragging)
        {
            ApplyDrag(mouse, vp, screenSize, SnapEnabled ||
                ProjectShockGL.Input.IsKey(Silk.NET.Input.Key.ShiftLeft) ||
                ProjectShockGL.Input.IsKey(Silk.NET.Input.Key.ShiftRight));
            
            return true;
        }
        
        if (allowInput && hovered >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            BeginTargetDrag(target, hovered, mouse, length, camera.Transform.Forward, vp, screenSize);
            
            return true;
        }
        
        return allowInput && hovered >= 0;
    }

    private static Vector3 GetAxis(EditorSelectionTarget target, int index)
    {
        return Local || (Mode == ManipulationMode.Scale && Selection.Targets.Count(o => o.IsEditorVisible) <= 1)
            ? Vector3.Transform(Axes[index], target.Transform.Rotation)
            : Axes[index];
    }

    internal static void BeginDrag(Object target, int axisIndex, Vector2 mouse, float length,
        Vector3 cameraForward, Matrix4x4 vp, Vector2 size) =>
        BeginTargetDrag(EditorSelectionTarget.ForObject(target), axisIndex, mouse, length, cameraForward, vp, size);

    internal static void BeginTargetDrag(EditorSelectionTarget target, int axisIndex, Vector2 mouse, float length,
        Vector3 cameraForward, Matrix4x4 vp, Vector2 size)
    {
        var selected = Selection.Targets.Where(o => o.IsEditorVisible).ToArray();

        if (!selected.Any(item => item.Matches(target)))
        {
            selected = [target];
        }
        
        _members = selected.Select(o => (o, TransformState.Capture(o.Transform))).ToArray();
        
        GroupTransform.Position = GetPivot(selected);
        GroupTransform.Rotation = Local ? target.Transform.Rotation : Quaternion.Identity;
        GroupTransform.Scale = Vector3.One;
        
        _before = TransformState.Capture(IsGroup ? GroupTransform : target.Transform);
        _dragAxis = axisIndex;
        _startMouse = mouse;
        _length = length;
        _angle = 0;

        if (axisIndex == 3)
        {
            _target = target; 
            
            return;
        }

        _axis = GetAxis(target, axisIndex);
        _planeNormal = Mode == ManipulationMode.Rotate ? _axis
            : cameraForward - _axis * Vector3.Dot(cameraForward, _axis);

        if (_planeNormal.LengthSquared() < 1e-6f)
        {
            return;
        }
        
        _planeNormal = Vector3.Normalize(_planeNormal);

        if (!GizmoMath.ScreenRay(mouse, size, vp, out var origin, out var direction) ||
            !GizmoMath.RayPlane(origin, direction, _before.Position, _planeNormal, out var hit))
        {
            return;
        }
        
        _startOffset = Vector3.Dot(hit - _before.Position, _axis);
        
        if (Mode == ManipulationMode.Rotate)
        {
            if (Vector3.DistanceSquared(hit, _before.Position) < 1e-8f)
            {
                return;
            }
            
            _lastDirection = Vector3.Normalize(hit - _before.Position);
        }
        
        _target = target;
    }

    internal static void ApplyDrag(Vector2 mouse, Matrix4x4 vp, Vector2 size, bool snap)
    {
        if (_target == null)
        {
            return;
        }
        
        ApplyDragInternal(mouse, vp, size, snap);

        if (!IsGroup)
        {
            _target.FlushTransform();
            return;
        }
        
        var deltaRotation = Quaternion.Normalize(GroupTransform.Rotation * Quaternion.Inverse(_before.Rotation));
        var factor = GroupTransform.Scale / _before.Scale;
        
        foreach (var member in _members)
        {
            if (member.Target.IsDestroyed)
            {
                continue;
            }
            
            var state = member.State;
            var relative = Vector3.Transform(state.Position - _before.Position, Quaternion.Inverse(_before.Rotation));
            
            relative *= factor;
            relative = Vector3.Transform(relative, _before.Rotation);
            
            var transform = member.Target.Transform;
            
            transform.Position = GroupTransform.Position + Vector3.Transform(relative, deltaRotation);
            transform.Rotation = Mode == ManipulationMode.Rotate ? Quaternion.Normalize(deltaRotation * state.Rotation) : state.Rotation;
            transform.Scale = Mode == ManipulationMode.Scale ? Vector3.Max(new Vector3(.01f), state.Scale * factor) : state.Scale;
            if (Mode == ManipulationMode.Scale && member.Target.IsDetail)
            {
                var scaled = state.Scale * factor;
                transform.Scale = new Vector3(MathF.CopySign(MathF.Max(.01f, MathF.Abs(scaled.X)), scaled.X),
                    MathF.CopySign(MathF.Max(.01f, MathF.Abs(scaled.Y)), scaled.Y),
                    MathF.CopySign(MathF.Max(.01f, MathF.Abs(scaled.Z)), scaled.Z));
            }
            member.Target.FlushTransform();
        }
    }

    private static float ScaleValue(float value) => _target is { IsDetail: true }
        ? MathF.CopySign(MathF.Max(.01f, MathF.Abs(value)), value) : MathF.Max(.01f, value);

    private static void ApplyDragInternal(Vector2 mouse, Matrix4x4 vp, Vector2 size, bool snap)
    {
        var transform = IsGroup ? GroupTransform : _target!.Transform;
        
        if (_dragAxis == 3)
        {
            var delta = mouse - _startMouse;
            var factor = MathF.Exp(Math.Clamp((delta.X - delta.Y) * 0.01f, -8, 8));

            if (snap)
            {
                factor = MathF.Max(0.01f, GizmoMath.Snap(factor, ScaleStep));
            }
            
            transform.Scale = new Vector3(ScaleValue(_before.Scale.X * factor), ScaleValue(_before.Scale.Y * factor), ScaleValue(_before.Scale.Z * factor));
            
            return;
        }

        if (!GizmoMath.ScreenRay(mouse, size, vp, out var origin, out var direction) ||
            !GizmoMath.RayPlane(origin, direction, _before.Position, _planeNormal, out var hit))
        {
            return;
        }

        if (Mode == ManipulationMode.Rotate)
        {
            if (Vector3.DistanceSquared(hit, _before.Position) < 1e-8f)
            {
                return;
            }
            
            var current = Vector3.Normalize(hit - _before.Position);
            
            _angle += GizmoMath.SignedAngle(_lastDirection, current, _axis);
            _lastDirection = current;
            
            var angle = snap ? GizmoMath.Snap(_angle, RotationStep * MathF.PI / 180) : _angle;
            transform.Rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(_axis, angle) * _before.Rotation);
            
            return;
        }

        var offset = Vector3.Dot(hit - _before.Position, _axis) - _startOffset;
        
        if (Mode == ManipulationMode.Move)
        {
            if (snap)
            {
                offset = GizmoMath.Snap(offset, SceneGrid.Step);
            }
            
            transform.Position = _before.Position + _axis * offset;
        }
        else
        {
            var factor = MathF.Max(0.01f, 1 + offset / _length);

            if (snap)
            {
                factor = MathF.Max(0.01f, GizmoMath.Snap(factor, ScaleStep));
            }
            
            var scale = _before.Scale;

            if (_dragAxis == 0)
            {
                scale.X = ScaleValue(_before.Scale.X * factor);
            }

            if (_dragAxis == 1)
            {
                scale.Y = ScaleValue(_before.Scale.Y * factor);
            }

            if (_dragAxis == 2)
            {
                scale.Z = ScaleValue(_before.Scale.Z * factor);
            }
            
            transform.Scale = scale;
        }
    }
}


