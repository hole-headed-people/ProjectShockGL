using System.Numerics;
using ImGuiNET;
using ProjectShockGL.Rendering;
using Object = ProjectShockGL.Core.Object;

namespace ProjectShockGLEditor;

public enum ManipulationMode { Move, Rotate, Scale }

public static class ObjectManipulator
{
    private static Object? _target;
    
    private static TransformState _before;
    
    private static int _dragAxis = -1;
    
    private static Vector3 _axis, _planeNormal, _lastDirection;
    private static Vector2 _startMouse;
    
    private static float _startOffset, _length, _angle;
    
    private static readonly Vector3[] Axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
    private static readonly uint[] Colors = [0xFF5555EF, 0xFF65CF65, 0xFFFFAA55];

    public static ManipulationMode Mode { get; set; } = ManipulationMode.Move;
    public static bool Local { get; set; }
    public static bool SnapEnabled { get; set; }
    public static float RotationStep { get; set; } = 15;
    public static float ScaleStep { get; set; } = 0.1f;
    public static bool IsDragging => _target != null;

    public static void Cancel()
    {
        if (_target != null && !_target.IsDestroyed)
        {
            _before.Apply(_target.Transform);
        }
        
        _target = null;
        _dragAxis = -1;
    }

    public static void Commit()
    {
        if (_target == null)
        {
            return;
        }
        
        var target = _target;
        
        if (!target.IsDestroyed)
        {
            var after = TransformState.Capture(target.Transform);
            
            if (after != _before)
            {
                var reference = EditorObjectReference.Get(target);
                
                UndoRedo.Register(new PropertyChangeAction<TransformState>(state =>
                {
                    if (!reference.Current.IsDestroyed)
                    {
                        state.Apply(reference.Current.Transform);
                    }
                }, _before, after));
            }
        }
        
        _target = null;
        _dragAxis = -1;
    }

    public static bool Draw(Vector2 screenSize, Vector2 sceneMin, Vector2 sceneMax, bool allowInput)
    {
        if (_target != null && (_target != Selection.Object || _target.IsDestroyed))
        {
            Cancel();
        }
        
        var target = Selection.Object;

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
        var position = target.Transform.Position;
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
            BeginDrag(target, hovered, mouse, length, camera.Transform.Forward, vp, screenSize);
            
            return true;
        }
        
        return allowInput && hovered >= 0;
    }

    private static Vector3 GetAxis(Object target, int index)
    {
        return Local || Mode == ManipulationMode.Scale
            ? Vector3.Transform(Axes[index], target.Transform.Rotation)
            : Axes[index];
    }

    internal static void BeginDrag(Object target, int axisIndex, Vector2 mouse, float length,
        Vector3 cameraForward, Matrix4x4 vp, Vector2 size)
    {
        _before = TransformState.Capture(target.Transform);
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
        var transform = _target!.Transform;
        
        if (_dragAxis == 3)
        {
            var delta = mouse - _startMouse;
            var factor = MathF.Exp(Math.Clamp((delta.X - delta.Y) * 0.01f, -8, 8));

            if (snap)
            {
                factor = MathF.Max(0.01f, GizmoMath.Snap(factor, ScaleStep));
            }
            
            transform.Scale = Vector3.Max(new Vector3(0.01f), _before.Scale * factor);
            
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
                scale.X = MathF.Max(0.01f, _before.Scale.X * factor);
            }

            if (_dragAxis == 1)
            {
                scale.Y = MathF.Max(0.01f, _before.Scale.Y * factor);
            }

            if (_dragAxis == 2)
            {
                scale.Z = MathF.Max(0.01f, _before.Scale.Z * factor);
            }
            
            transform.Scale = scale;
        }
    }
}

