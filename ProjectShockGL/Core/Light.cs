using DebugColor = ProjectShockGL.Rendering.Color;
using ProjectShockGL.Rendering;
using ProjectShockGL.Tools;

namespace ProjectShockGL.Core;

public enum LightType
{
    Directional,
    Point,
    Spot
}

public enum LightMode
{
    Realtime,
    Baked
}

public class Light : Entity
{
    private LightType _type = LightType.Point;
    private LightMode _mode = LightMode.Realtime;
    
    private float _innerAngle = 30f;
    private float _outerAngle = 45f;

    [Editable]
    public LightType Type
    {
        get => _type;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(Type));
            
            if (_type == value)
            {
                return;
            }
            
            _type = value;
            
            OnTypeChanged();
        }
    }
    
    [Editable]
    public LightMode Mode
    {
        get => _mode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Mode));
            }

            if (_mode == value)
            {
                return;
            }

            _mode = value;
            
            OnTypeChanged();
        }
    }

    [Editable] public Color Color { get; set; } = Color.White;
    
    [Editable] public float Intensity { get; set; } = 1f;
    [Editable] public float Radius { get; set; } = 10f;
    
    [Editable]
    public float InnerAngle
    {
        get => _innerAngle;
        set
        {
            ValidateAngle(value, 0, nameof(InnerAngle));
            _innerAngle = value;
            _outerAngle = MathF.Max(_outerAngle, value);
        }
    }
    
    [Editable]
    public float OuterAngle
    {
        get => _outerAngle;
        set
        {
            ValidateAngle(value, 0.1f, nameof(OuterAngle));
            
            _outerAngle = value;
            _innerAngle = MathF.Min(_innerAngle, value);
        }
    }
    
    public static Light? Main { get; private set; }
    public static List<Light> All { get; private set; } = [];

    public override void Create()
    {
        base.Create();

        OnTypeChanged();
    }
    
    private static void ValidateAngle(float angle, float minimum, string name)
    {
        if (!float.IsFinite(angle) || angle < minimum || angle > 179)
        {
            throw new ArgumentOutOfRangeException(name, $"Angle must be {minimum}..179 degrees.");
        }
    }

    protected override string GetDebugBillboardName(string defaultName)
    {
        var name = Type switch
        {
            LightType.Point => "Editor/PointLight",
            LightType.Spot => "Editor/SpotLight",
            LightType.Directional => "Editor/DirectionalLight",
            _ => defaultName
        };

        if (_mode == LightMode.Baked)
        {
            name += "Baked";
        }

        return File.Exists(System.IO.Path.Combine(Content.Path, "Textures", name + ".png"))
            ? name : defaultName;
    }

    internal void OnTypeChanged()
    {
        if (IsDestroyed)
        {
            return;
        }
        
        RefreshDebugBillboard();

        if (Type is LightType.Point or LightType.Spot)
        {
            if (Main == this)
            {
                Main = null;
            }

            if (!All.Contains(this))
            {
                All.Add(this);
            }
        }
        else if (Type == LightType.Directional)
        {
            All.Remove(this);

            if (Mode == LightMode.Baked)
            {
                if (Main == this)
                {
                    Main = OwningWorld?.GetAllObjects(true).OfType<Light>().FirstOrDefault(light =>
                        light != this && !light.IsDestroyed && light.Type == LightType.Directional &&
                        light.Mode == LightMode.Realtime);
                }

                return;
            }

            if (Main == null)
            {
                Main = this;
            }
            else if (Main != this)
            {
                Log.PrintWarning($"Multiple directional lights, Main is already {Main.Name}");
            }
        }
    }

    public override void Destroy()
    {
        try
        {
            base.Destroy();
        }
        finally
        {
            if (Main == this)
            {
                Main = null;
            }
            
            All.Remove(this);
        }
    }
    
    public override void DebugRenderSelected(DebugDraw draw)
    {
        if (Type == LightType.Directional)
        {
            var origin = Transform.Position;
            
            var forward = Transform.Forward;
            var right = Transform.Right;
            var up = Transform.Up;
            
            var tip = origin + forward * 2f;
            var head = tip - forward * 0.45f;
            
            draw.Line(origin, tip, DebugColor.Yellow, ignoreDepth: true, thickness: 0.025f);
            draw.Line(tip, head + right * 0.22f, DebugColor.Yellow, ignoreDepth: true, thickness: 0.025f);
            draw.Line(tip, head - right * 0.22f, DebugColor.Yellow, ignoreDepth: true, thickness: 0.025f);
            draw.Line(tip, head + up * 0.22f, DebugColor.Yellow, ignoreDepth: true, thickness: 0.025f);
            draw.Line(tip, head - up * 0.22f, DebugColor.Yellow, ignoreDepth: true, thickness: 0.025f);
            
            return;
        }

        if (Type == LightType.Point)
        {
            foreach (var (from, to) in Utils.GetRadiusLines(Transform.Position, Radius))
            {
                draw.Line(from, to, DebugColor.Yellow, ignoreDepth: true, thickness: 0.012f);
            }
            
            return;
        }

        if (Type != LightType.Spot)
        {
            return;
        }

        foreach (var (from, to) in Utils.GetConeLines(Transform, Radius, OuterAngle))
        {
            draw.Line(from, to, DebugColor.Yellow, ignoreDepth: true, thickness: 0.012f);
        }

        foreach (var (from, to) in Utils.GetConeLines(Transform, Radius, InnerAngle))
        {
            draw.Line(from, to, DebugColor.Cyan, ignoreDepth: true, thickness: 0.008f);
        }
        
        if (float.IsFinite(Radius) && Radius > 0)
        {
            draw.Line(Transform.Position, Transform.Position + Transform.Forward * Radius,
                DebugColor.White, ignoreDepth: true, thickness: 0.012f);
        }
    }
}
