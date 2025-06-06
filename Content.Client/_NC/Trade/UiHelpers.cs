using Robust.Client.UserInterface;


namespace Content.Client._NC.Trade;


public static class UiHelpers
{
    public static T GetChild<T>(this Control control, string name) where T : Control
    {
        foreach (var child in GetAllChildrenRecursive(control))
            if (child.Name == name && child is T matched)
                return matched;

        throw new KeyNotFoundException($"UI: Control '{name}' not found");
    }

    private static IEnumerable<Control> GetAllChildrenRecursive(Control parent)
    {
        foreach (var child in parent.Children)
        {
            yield return child;
            foreach (var subChild in GetAllChildrenRecursive(child))
                yield return subChild;
        }
    }
}
