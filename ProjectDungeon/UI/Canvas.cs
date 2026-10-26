using System.Numerics;
using ProjectShockGL;
using ProjectShockGL.UI;

namespace ProjectDungeon.UI;

public class Canvas : ProjectShockGL.UI.Canvas
{
    protected override void OnCreate()
    {
        var image = Add<Image>();
        image.Rect.Position = new Vector2(0, 0);
        image.Rect.Size = new Vector2(1000, 10);
        image.Texture = Content.LoadTexture("Test");

        var text = Add<Text>();
        text.Rect.Position = new Vector2(24, 24);
        text.Rect.Size = new Vector2(450, 100);
        text.Font = Content.LoadFont("NotoSans-Regular", 24);
        text.Content = "Система готова. Ёжик и ёлка.\nSystem ready. Access granted.";
    }
}
