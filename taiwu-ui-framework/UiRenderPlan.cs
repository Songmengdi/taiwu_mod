namespace TaiwuUi;

internal sealed record UiRenderPlan(WindowDefinition Definition, UiWindow Source);

internal static class UiRenderPlanCompiler
{
    internal static UiValidationResult Validate(UiWindow window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        var errors = new List<UiValidationIssue>();
        var warnings = new List<UiValidationIssue>();
        if (string.IsNullOrWhiteSpace(window.OwnerId))
            errors.Add(new("window.ownerId", "Owner ID is required."));
        if (string.IsNullOrWhiteSpace(window.WindowId))
            errors.Add(new("window.windowId", "Window ID is required."));
        if (window.Width <= 0f || window.Height <= 0f)
            errors.Add(new("window.size", "Window width and height must be positive."));

        ValidateElement(window.Content, "content", errors, warnings);
        return new UiValidationResult(errors, warnings);
    }

    internal static UiRenderPlan Compile(UiWindow window)
    {
        UiValidationResult validation = Validate(window);
        if (!validation.IsValid)
            throw new UiValidationException(validation.Errors);

        List<UiNode> nodes = UiElementCompiler.CompileContent(window.Content);
        AssignIdentities(nodes, "content");
        var definition = new WindowDefinition(window, nodes);
        return new UiRenderPlan(definition, window);
    }

    internal static void AssignIdentities(IReadOnlyList<UiNode> nodes, string parent)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            UiNode node = nodes[index];
            node.Identity = parent + "/" + (node.Key ?? index.ToString());
            switch (node)
            {
                case RowNode row:
                    AssignIdentities(row.Children, node.Identity);
                    break;
                case ColumnNode column:
                    AssignIdentities(column.Children, node.Identity);
                    break;
                case FlexNode flex:
                    AssignIdentities(flex.Children, node.Identity);
                    break;
                case DynamicNode dynamic:
                    AssignIdentities(dynamic.Children, node.Identity);
                    break;
                case ScrollNode scroll:
                    AssignIdentities(scroll.Children, node.Identity);
                    break;
                case TabViewNode tabs:
                    for (int page = 0; page < tabs.Pages.Count; page++)
                        AssignIdentities(tabs.Pages[page].Children, node.Identity + "/page-" + page);
                    break;
            }
        }
    }

    private static void ValidateElement(
        UiElement? element,
        string path,
        List<UiValidationIssue> errors,
        List<UiValidationIssue> warnings)
    {
        if (element == null)
        {
            errors.Add(new(path, "Element cannot be null."));
            return;
        }
        if (element.Key != null && string.IsNullOrWhiteSpace(element.Key))
            errors.Add(new(path, "Element key cannot be blank."));

        UiElement[] children = element.ChildElements.ToArray();
        var explicitKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < children.Length; index++)
        {
            UiElement child = children[index];
            string childPath = $"{path}/{child?.Key ?? index.ToString()}";
            if (child?.Key is { } key && !explicitKeys.Add(key))
                errors.Add(new(childPath, $"Duplicate sibling key '{key}'."));
            ValidateElement(child, childPath, errors, warnings);
        }
        if (children.Length > 1 && children.Any(child => child.Key == null))
            warnings.Add(new(path, "Dynamic children should use stable keys."));

        switch (element)
        {
            case UiFlexElement flex when flex.Grow <= 0f:
                errors.Add(new(path, "Flex grow must be positive."));
                break;
            case UiDynamicElement dynamic when dynamic.Height <= 0f:
                errors.Add(new(path, "Dynamic fragment height must be positive."));
                break;
            case UiSearchInputElement search when search.Width <= 0f:
                errors.Add(new(path, "Search input width must be positive."));
                break;
            case UiSliderElement slider when slider.Maximum <= slider.Minimum || slider.Step <= 0f:
                errors.Add(new(path, "Slider range and step are invalid."));
                break;
            case UiRangeSliderElement range when range.Maximum <= range.Minimum || range.Step <= 0f:
                errors.Add(new(path, "Range slider range and step are invalid."));
                break;
            case UiNativeImageElement image when image.Width <= 0f || image.Height <= 0f:
                errors.Add(new(path, "Native image size must be positive."));
                break;
            case UiNativeHostElement host when host.Width <= 0f || host.Height <= 0f:
                errors.Add(new(path, "Native host size must be positive."));
                break;
        }
    }
}

public sealed class UiValidationException : ArgumentException
{
    public IReadOnlyList<UiValidationIssue> Issues { get; }

    internal UiValidationException(IReadOnlyList<UiValidationIssue> issues)
        : base(string.Join("; ", issues.Select(issue => $"{issue.Path}: {issue.Message}")))
    {
        Issues = issues;
    }
}
