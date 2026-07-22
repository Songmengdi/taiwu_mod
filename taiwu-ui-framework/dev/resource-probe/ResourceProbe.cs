using System.Reflection;
using System.Text;
using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;

namespace TaiwuUi.ResourceProbe;

public sealed class ResourceProbe
{
    public static string Snapshot { get; private set; } = "not run";

    public ResourceProbe() => Run();

    private static void Run()
    {
        var output = new StringBuilder();

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        output.AppendLine("FONTS");
        foreach (var group in fonts.GroupBy(font => font.name).OrderByDescending(group => group.Count()).Take(20))
            output.AppendLine($"{group.Key} x{group.Count()}");

        output.AppendLine().AppendLine("ACTIVE TEXT FONTS");
        foreach (var group in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                     .Where(text => text != null && text.gameObject.activeInHierarchy && text.font != null)
                     .GroupBy(text => text.font.name)
                     .OrderByDescending(group => group.Count()))
            output.AppendLine($"{group.Key} x{group.Count()}");

        output.AppendLine().AppendLine("ACTIVE NINE-SLICE IMAGES");
        foreach (CImage image in Resources.FindObjectsOfTypeAll<CImage>()
                     .Where(image => image != null && image.gameObject.activeInHierarchy && image.sprite != null)
                     .Where(image => BorderSize(image.sprite!.border) > 0f)
                     .OrderByDescending(image => BorderSize(image.sprite!.border))
                     .Take(40))
        {
            Sprite sprite = image.sprite!;
            output.AppendLine(
                $"{PathOf(image.transform)} | sprite={sprite.name} | border={Vector(sprite.border)} | " +
                $"type={image.type} | color={ColorOf(image.color)}");
        }

        output.AppendLine().AppendLine("SPRITE CATALOG CANDIDATES");
        foreach (var group in Resources.FindObjectsOfTypeAll<Sprite>()
                     .Where(sprite => sprite != null && BorderSize(sprite.border) > 0f)
                     .Where(sprite =>
                         sprite.name.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                         sprite.name.Contains("btn", StringComparison.OrdinalIgnoreCase) ||
                         sprite.name.Contains("window", StringComparison.OrdinalIgnoreCase) ||
                         sprite.name.Contains("popup", StringComparison.OrdinalIgnoreCase) ||
                         sprite.name.Contains("frame", StringComparison.OrdinalIgnoreCase) ||
                         sprite.name.Contains("back_base", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(sprite => sprite.name)
                     .OrderBy(group => group.Key)
                     .Take(100))
        {
            Sprite sprite = group.First();
            output.AppendLine($"{sprite.name} | border={Vector(sprite.border)} | size={sprite.rect.width:0}x{sprite.rect.height:0}");
        }

        output.AppendLine().AppendLine("ACTIVE BUTTON TARGETS");
        foreach (CButton button in Resources.FindObjectsOfTypeAll<CButton>()
                     .Where(button => button != null && button.gameObject.activeInHierarchy)
                     .Take(60))
        {
            CImage? image = button.targetGraphic as CImage;
            string sprite = image?.sprite?.name ?? "<none>";
            output.AppendLine(
                $"{PathOf(button.transform)} | sprite={sprite} | image={ColorOf(image?.color ?? Color.white)} | " +
                $"normal={ColorOf(button.colors.normalColor)} highlighted={ColorOf(button.colors.highlightedColor)} " +
                $"pressed={ColorOf(button.colors.pressedColor)} disabled={ColorOf(button.colors.disabledColor)}");
        }

        Component[] buttonStyles = Resources.FindObjectsOfTypeAll<Component>()
            .Where(component => component != null && component.GetType().Name == "ButtonStyle")
            .ToArray();
        output.AppendLine().AppendLine("BUTTON STYLE INSTANCES");
        foreach (Component style in buttonStyles.Where(style => style.gameObject.activeInHierarchy).Take(30))
            output.AppendLine(PathOf(style.transform));

        output.AppendLine().AppendLine("BUTTON STYLE MEMBERS");
        Type? buttonStyleType = buttonStyles.FirstOrDefault()?.GetType();
        if (buttonStyleType != null)
        {
            foreach (MemberInfo member in buttonStyleType.GetMembers(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(member => member.MemberType is MemberTypes.Field or MemberTypes.Property)
                         .OrderBy(member => member.Name))
                output.AppendLine($"{member.MemberType}: {member.Name}");
        }

        Snapshot = output.ToString();
    }

    private static float BorderSize(Vector4 border) => border.x + border.y + border.z + border.w;

    private static string Vector(Vector4 value) =>
        $"{value.x:0.#},{value.y:0.#},{value.z:0.#},{value.w:0.#}";

    private static string ColorOf(Color color) => ColorUtility.ToHtmlStringRGBA(color);

    private static string PathOf(Transform transform)
    {
        var names = new Stack<string>();
        for (Transform? current = transform; current != null; current = current.parent)
            names.Push(current.name);
        return string.Join("/", names);
    }

    public override string ToString() => Snapshot;
}
