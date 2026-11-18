namespace ProjectShockGL.Rendering;

public readonly record struct RenderStatistics(int DrawCalls, int WorldDrawCalls, int PostProcessDrawCalls,
    long Triangles, int WorldWidth, int WorldHeight);