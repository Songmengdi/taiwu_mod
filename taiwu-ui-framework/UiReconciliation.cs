namespace TaiwuUi;

public sealed class UiUpdatePreview
{
    public IReadOnlyList<string> Reused { get; }
    public IReadOnlyList<string> Replaced { get; }
    public IReadOnlyList<string> Added { get; }
    public IReadOnlyList<string> Removed { get; }

    internal UiUpdatePreview(
        IReadOnlyList<string> reused,
        IReadOnlyList<string> replaced,
        IReadOnlyList<string> added,
        IReadOnlyList<string> removed)
    {
        Reused = reused;
        Replaced = replaced;
        Added = added;
        Removed = removed;
    }
}

internal static class UiReconciler
{
    internal static UiUpdatePreview Preview(UiWindow current, UiWindow next)
    {
        if (!string.Equals(current.Key, next.Key, StringComparison.Ordinal))
            throw new ArgumentException("Window keys must match for reconciliation.", nameof(next));
        UiValidationResult validation = UiRenderPlanCompiler.Validate(next);
        if (!validation.IsValid)
            throw new UiValidationException(validation.Errors);

        Dictionary<string, Type> before = Flatten(current.Content);
        Dictionary<string, Type> after = Flatten(next.Content);
        var reused = new List<string>();
        var replaced = new List<string>();
        var added = new List<string>();
        var removed = new List<string>();

        foreach ((string path, Type type) in after)
        {
            if (!before.TryGetValue(path, out Type? previous))
                added.Add(path);
            else if (previous == type)
                reused.Add(path);
            else
                replaced.Add(path);
        }
        foreach (string path in before.Keys)
            if (!after.ContainsKey(path))
                removed.Add(path);

        return new UiUpdatePreview(reused, replaced, added, removed);
    }

    private static Dictionary<string, Type> Flatten(UiElement root)
    {
        var result = new Dictionary<string, Type>(StringComparer.Ordinal);
        Visit(root, "content", result);
        return result;
    }

    private static void Visit(UiElement element, string path, Dictionary<string, Type> result)
    {
        result[path] = element.GetType();
        UiElement[] children = element.ChildElements.ToArray();
        for (int index = 0; index < children.Length; index++)
        {
            UiElement child = children[index];
            Visit(child, path + "/" + (child.Key ?? index.ToString()), result);
        }
    }
}
