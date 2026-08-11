using Aprillz.MewUI.Controls;

namespace Aprillz.MewUI;

internal static class StyleScopeResolver
{
    internal static Style? Resolve(
        Control control,
        string? styleName,
        StyleSheet? applicationStyleSheet)
    {
        ArgumentNullException.ThrowIfNull(control);

        Type controlType = control.GetType();
        bool liveLookup = control.FindVisualRoot() is Window;
        for (Element? current = control; current != null; current = current.ContextParent)
        {
            if (current is not FrameworkElement { StyleSheet: { } sheet })
            {
                continue;
            }

            Style? style = Lookup(sheet, styleName, controlType, liveLookup);
            if (style != null)
            {
                return style;
            }
        }

        if (applicationStyleSheet == null)
        {
            return null;
        }

        return Lookup(applicationStyleSheet, styleName, controlType, liveLookup);
    }

    private static Style? Lookup(
        StyleSheet sheet,
        string? styleName,
        Type controlType,
        bool liveLookup)
    {
        if (styleName != null)
        {
            return liveLookup ? sheet.GetLive(styleName) : sheet.Get(styleName);
        }

        return liveLookup ? sheet.GetLiveByType(controlType) : sheet.GetByType(controlType);
    }

    internal static string DescribeScopes(Control control, bool includesApplication)
    {
        var scopes = new List<string>(capacity: 4);
        for (Element? current = control; current != null; current = current.ContextParent)
        {
            if (current is FrameworkElement { StyleSheet: not null })
            {
                string suffix = ReferenceEquals(current, control) ? " (self)" : string.Empty;
                scopes.Add(current.GetType().Name + suffix);
            }
        }

        if (includesApplication)
        {
            scopes.Add(nameof(Application));
        }

        return scopes.Count == 0 ? "(none)" : string.Join(" -> ", scopes);
    }
}
