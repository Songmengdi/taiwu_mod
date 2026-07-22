namespace TaiwuUi;

internal static class TaiwuUiRuntime
{
    private static readonly Dictionary<string, FrameworkWindow> Windows = new();
    private static bool _initialized;

    internal static void Initialize() => _initialized = true;

    internal static ITaiwuWindow Mount(UiWindow window)
    {
        UiRenderPlan plan = UiRenderPlanCompiler.Compile(window);
        if (!_initialized)
            Initialize();
        if (UIManager.Instance == null)
            throw new InvalidOperationException("Taiwu UIManager is not ready.");
        if (Windows.TryGetValue(plan.Definition.Key, out FrameworkWindow existing))
        {
            existing.RenderPlan(plan);
            return existing;
        }

        var mounted = new FrameworkWindow(plan, () => Windows.Remove(plan.Definition.Key));
        Windows.Add(plan.Definition.Key, mounted);
        return mounted;
    }

    internal static void DisposeAll()
    {
        foreach (FrameworkWindow window in Windows.Values.ToArray())
            window.Dispose();
        Windows.Clear();
        _initialized = false;
    }
}
