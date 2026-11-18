using System.Numerics;
using System.Diagnostics;

namespace ProjectShockGL.Rendering;

internal sealed class DebugStatisticsOverlay
{
    private Font? _font;
    private bool _fontRequested;
    private TextLayout? _layout;
    private long _lastRefresh;

    internal void Draw(Graphics graphics, Vector2 windowSize)
    {
        if (!_fontRequested)
        {
            _fontRequested = true;
            _font = Content.LoadFont("NotoSans-Regular", 16);
        }

        if (_font == null || _font.IsDisposed)
        {
            return;
        }
        
        var stats = graphics.Statistics;
        
        if (_layout == null || Stopwatch.GetElapsedTime(_lastRefresh).TotalSeconds >= 0.25)
        {
            _lastRefresh = Stopwatch.GetTimestamp();
            
            var uiAndDebug = stats.DrawCalls - stats.WorldDrawCalls - stats.PostProcessDrawCalls;
            var text = $"FPS: {Application.Fps:F0}   Frame: {Application.FrameTimeMs:F2} ms\n" +
                       $"CPU Update: {Application.UpdateTimeMs:F2} ms   Render: {Application.RenderTimeMs:F2} ms\n" +
                       $"Draw calls (excl. stats): {stats.DrawCalls}\n" +
                       $"World: {stats.WorldDrawCalls}   Post: {stats.PostProcessDrawCalls}   UI/Debug: {uiAndDebug}\n" +
                       $"Triangles (incl. UI): {stats.Triangles:N0}\n" +
                       $"World: {stats.WorldWidth} x {stats.WorldHeight}   Window: {windowSize.X:F0} x {windowSize.Y:F0}\n" +
                       $"Managed memory: {GC.GetTotalMemory(false) / (1024.0 * 1024.0):F1} MB";
            
            _layout = _font.Atlas.LayoutText(text, float.PositiveInfinity);
        }

        graphics.BeginUI(windowSize);
        
        try
        {
            graphics.DrawUIRect(new Vector2(8, 100), _layout.Size + new Vector2(16), new Color(0, 0, 0, 0.8f));
            
            foreach (var placed in _layout.Glyphs)
            {
                graphics.DrawUIRegion(new Vector2(16, 108) + placed.Position, placed.Glyph.Size,
                    _font.Texture, placed.Glyph.UvRect, Color.White);
            }
        }
        finally
        {
            graphics.EndUI();
        }
    }
}