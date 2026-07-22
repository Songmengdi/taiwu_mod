using UnityEngine;

namespace TaiwuUi;

internal sealed class UiRuntimeState
{
    internal Dictionary<string, Vector2> ScrollPositions { get; } = new(StringComparer.Ordinal);
    internal string? Focused { get; set; }
}

internal sealed class UiElementIdentity : MonoBehaviour
{
    internal string Path { get; set; } = string.Empty;
    internal string Kind { get; set; } = string.Empty;
    internal string StateKey(int slot) => $"{Path}|{Kind}|{slot}";
}
