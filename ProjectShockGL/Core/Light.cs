using ProjectShockGL.Rendering;

namespace ProjectShockGL.Core;

public enum LightType
{
    Directional,
    Point
}

public class Light : Entity
{
    private LightType _type = LightType.Point;

    [Editable]
    public LightType Type
    {
        get => _type;
        set
        {
            if (_type == value)
            {
                return;
            }
            
            _type = value;
            
            OnTypeChanged();
        }
    }

    [Editable] public Color Color { get; set; } = Color.White;
    
    [Editable] public float Intensity { get; set; } = 1f;
    [Editable] public float Radius { get; set; } = 10f;
    
    public static Light? Main { get; private set; }
    public static List<Light> All { get; private set; } = [];

    public override void Create()
    {
        base.Create();

        OnTypeChanged();
    }

    internal void OnTypeChanged()
    {
        if (Type == LightType.Point)
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

            if (Main == null)
            {
                Main = this;
            }
            else if (Main != this)
            {
                Console.WriteLine($"Warning: multiple directional lights, Main is already {Main.Name}");
            }
        }
    }

    public override void Destroy()
    {
        base.Destroy();

        if (Main == this)
        {
            Main = null;
        }
        
        All.Remove(this);
    }
}
