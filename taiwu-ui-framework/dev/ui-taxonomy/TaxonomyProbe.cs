using System.Reflection;
using System.Text;
using FrameWork.UISystem.UIElements;
using UnityEngine;

namespace TaiwuUi.TaxonomyProbe;

public sealed class TaxonomyProbe
{
    public static string Snapshot { get; private set; } = "not run";

    public TaxonomyProbe() => Run();

    private static void Run()
    {
        Assembly game = typeof(UIManager).Assembly;
        Type[] types = game.GetTypes();
        Type uiBase = typeof(UIBase);
        Type[] views = types
            .Where(type => type != uiBase && uiBase.IsAssignableFrom(type) && !type.IsAbstract)
            .OrderBy(type => type.FullName)
            .ToArray();

        var output = new StringBuilder();
        output.AppendLine($"ASSEMBLY={game.GetName().Name} UIBaseConcreteTypes={views.Length}");

        output.AppendLine().AppendLine("VIEW FAMILIES");
        foreach (var family in views.GroupBy(FamilyOf).OrderByDescending(group => group.Count()))
        {
            output.AppendLine($"[{family.Key}] {family.Count()}");
            foreach (Type type in family.Take(16))
                output.AppendLine("  " + type.Name);
        }

        output.AppendLine().AppendLine("REUSED VIEW BASES");
        foreach (var group in views.GroupBy(type => type.BaseType?.FullName ?? "<none>")
                     .OrderByDescending(group => group.Count()).Take(30))
            output.AppendLine($"{group.Key} -> {group.Count()}");

        output.AppendLine().AppendLine("ACTIVE UIBASE INSTANCES");
        foreach (UIBase view in Resources.FindObjectsOfTypeAll<UIBase>()
                     .Where(view => view != null && view.gameObject.activeInHierarchy)
                     .OrderBy(view => view.UiType).ThenBy(view => view.name))
        {
            output.AppendLine(
                $"{view.GetType().FullName} | go={view.name} | layer={view.UiType} | " +
                $"flags={view.UiFlags} | showing={view.Element?.IsShowing}");
        }

        output.AppendLine().AppendLine("COMPOSITE COMPONENT FAMILIES");
        Type component = typeof(Component);
        Type[] composites = types
            .Where(type => type != uiBase && component.IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type =>
                (type.Namespace?.StartsWith("Game.Components", StringComparison.Ordinal) == true ||
                 type.Namespace?.StartsWith("UICommon", StringComparison.Ordinal) == true) &&
                CompositeName(type.Name))
            .ToArray();
        foreach (var family in composites.GroupBy(CompositeFamily).OrderByDescending(group => group.Count()))
        {
            output.AppendLine($"[{family.Key}] {family.Count()}");
            foreach (Type type in family.OrderBy(type => type.Name).Take(20))
                output.AppendLine("  " + type.FullName);
        }

        output.AppendLine().AppendLine("UIBASE DECLARED LIFECYCLE");
        foreach (MethodInfo method in uiBase.GetMethods(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                     .Where(method => !method.IsSpecialName)
                     .OrderBy(method => method.Name))
            output.AppendLine(Signature(method));

        output.AppendLine().AppendLine("UIMANAGER PUBLIC UI METHODS");
        foreach (MethodInfo method in typeof(UIManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                     .Where(method => method.Name.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                                      method.Name.Contains("Element", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(method => method.Name))
            output.AppendLine(Signature(method));

        Snapshot = output.ToString();
    }

    private static string FamilyOf(Type type)
    {
        string ns = type.Namespace ?? "Global";
        const string prefix = "Game.Views.";
        if (!ns.StartsWith(prefix, StringComparison.Ordinal))
            return ns.StartsWith("Game.Views", StringComparison.Ordinal) ? "General" : "NonGameViews";
        string rest = ns[prefix.Length..];
        int separator = rest.IndexOf('.');
        return separator < 0 ? rest : rest[..separator];
    }

    private static bool CompositeName(string name) =>
        new[] { "Item", "Card", "Avatar", "Character", "Skill", "Scroll", "List", "Tab", "Panel", "Display" }
            .Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string CompositeFamily(Type type)
    {
        string name = type.Name;
        foreach (string token in new[] { "Scroll", "List", "Card", "Avatar", "Character", "Skill", "Item", "Tab", "Panel", "Display" })
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return token;
        return "Other";
    }

    private static string Signature(MethodInfo method) =>
        $"{method.ReturnType.Name} {method.Name}(" +
        string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name + " " + parameter.Name)) + ")";

    public override string ToString() => Snapshot;
}
