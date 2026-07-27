using UnityEngine;

namespace TaiwuUi;

internal sealed class WindowDefinition
{
    internal string OwnerId { get; }
    internal string WindowId { get; }
    internal string Key => OwnerId + ":" + WindowId;
    internal string Title { get; }
    internal Vector2 Size { get; }
    internal TaiwuWindowLayer Layer { get; }
    internal TaiwuWindowCover Cover { get; }
    internal TaiwuWindowPresentation Presentation { get; }
    internal TaiwuWindowLifetime Lifetime { get; }
    internal IReadOnlyList<UiNode> Nodes { get; }

    internal WindowDefinition(UiWindow source, IReadOnlyList<UiNode> nodes)
    {
        OwnerId = source.OwnerId;
        WindowId = source.WindowId;
        Title = source.Title;
        Size = new Vector2(source.Width, source.Height);
        Layer = source.Layer;
        Cover = source.Cover;
        Presentation = source.Presentation;
        Lifetime = source.Lifetime;
        Nodes = nodes;
    }
}

internal abstract class UiNode
{
    internal string? Key { get; set; }
    internal string Identity { get; set; } = string.Empty;
}

internal sealed class TextNode(string text, TaiwuTextOptions options) : UiNode
{
    internal string Text { get; } = text;
    internal TaiwuTextOptions Options { get; } = options;
}

internal sealed class ButtonNode(
    string label, Action onClick, TaiwuButtonOptions options) : UiNode
{
    internal string Label { get; } = label;
    internal Action OnClick { get; } = onClick;
    internal TaiwuButtonOptions Options { get; } = options;
}

internal sealed class RowNode(List<UiNode> children, float spacing) : UiNode
{
    internal List<UiNode> Children { get; } = children;
    internal float Spacing { get; } = spacing;
}

internal sealed class ColumnNode(List<UiNode> children, float spacing) : UiNode
{
    internal List<UiNode> Children { get; } = children;
    internal float Spacing { get; } = spacing;
}

internal sealed class FlexNode(List<UiNode> children, float grow) : UiNode
{
    internal List<UiNode> Children { get; } = children;
    internal float Grow { get; } = grow;
}

internal sealed class DynamicNode(
    TaiwuValue<UiElement> content,
    List<UiNode> children,
    float height) : UiNode
{
    internal TaiwuValue<UiElement> Content { get; } = content;
    internal List<UiNode> Children { get; } = children;
    internal float Height { get; } = height;
}

internal sealed class DividerNode : UiNode;

internal sealed class SpacerNode(float height) : UiNode
{
    internal float Height { get; } = height;
}

internal sealed class ScrollNode(List<UiNode> children, TaiwuScrollOptions options) : UiNode
{
    internal List<UiNode> Children { get; } = children;
    internal TaiwuScrollOptions Options { get; } = options;
}

internal sealed class NativeImageNode(NativeAssetRef asset, float width, float height) : UiNode
{
    internal NativeAssetRef Asset { get; } = asset;
    internal float Width { get; } = width;
    internal float Height { get; } = height;
}

internal sealed class NativeHostNode(
    float width, float height, Func<GameObject> factory, Action<GameObject>? release, bool deferred) : UiNode
{
    internal float Width { get; } = width;
    internal float Height { get; } = height;
    internal Func<GameObject> Factory { get; } = factory;
    internal Action<GameObject>? Release { get; } = release;
    internal bool Deferred { get; } = deferred;
}
