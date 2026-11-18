namespace ProjectShockGL.UI;

internal static class WidgetSearch
{
    internal static T? Find<T>(IReadOnlyList<Widget> children, string name) where T : Widget
    {
        ArgumentNullException.ThrowIfNull(name);
        
        foreach (var child in children)
        {
            if (child.IsDestroyed)
            {
                continue;
            }

            if (child.Name == name && child is T match)
            {
                return match;
            }
            
            var nested = Find<T>(child.Children, name);

            if (nested != null)
            {
                return nested;
            }
        }
        
        return null;
    }
}
