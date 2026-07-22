using FrameWork.UISystem.UIElements;
using Game.Views.Bottom;
using HarmonyLib;
using UnityEngine;

namespace MapSkillFinder.Frontend;

[HarmonyPatch(typeof(ViewBottom), nameof(ViewBottom.OnInit))]
internal static class ViewBottomSkillFinderEntryPatch
{
    [HarmonyPostfix]
    private static void Postfix(ViewBottom __instance)
    {
        MapSkillFinderEntry entry = __instance.GetComponent<MapSkillFinderEntry>();
        if (entry == null)
            entry = __instance.gameObject.AddComponent<MapSkillFinderEntry>();
        entry.Initialize(__instance);
    }
}

internal sealed class MapSkillFinderEntry : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<ViewBottom, CButton> MapFindButton =
        AccessTools.FieldRefAccess<ViewBottom, CButton>("mapFind");

    private bool _initialized;
    private SkillFinderWindow? _window;

    internal void Initialize(ViewBottom owner)
    {
        if (_initialized)
            return;
        _initialized = true;

        _window = owner.GetComponent<SkillFinderWindow>();
        if (_window == null)
            _window = owner.gameObject.AddComponent<SkillFinderWindow>();
        _window.Initialize(owner);

        CButton template = MapFindButton(owner);
        GameObject clone = Instantiate(template.gameObject, template.transform.parent);
        clone.name = "TaiwuFinderButton";
        RectTransform rect = (RectTransform)clone.transform;
        RectTransform source = (RectTransform)template.transform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, 78f);
        rect.sizeDelta = source.sizeDelta;
        rect.localScale = source.localScale;
        clone.GetComponent<CButton>().onClick.ResetListener(() => _window.Open());
        AddNativeBookBadge(clone.transform);
        ExploredMarkCleaner.Attach(owner.gameObject);
    }

    private static void AddNativeBookBadge(Transform parent)
    {
        var badgeObject = new GameObject("MapSkillFinderBookBadge", typeof(RectTransform), typeof(CImage));
        badgeObject.transform.SetParent(parent, false);
        badgeObject.name = "TaiwuFinderBookBadge";
        RectTransform rect = (RectTransform)badgeObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(13f, -13f);
        rect.sizeDelta = new Vector2(27f, 27f);
        CImage badge = badgeObject.GetComponent<CImage>();
        badge.raycastTarget = false;
        badge.preserveAspect = true;
        badge.SetSprite("icon_SkillBook_wangxiababu");
    }
}
