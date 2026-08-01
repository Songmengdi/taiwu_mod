using GameData.Domains.Map;
using TaiwuUi;
using TMPro;
using UnityEngine;

namespace MapSkillFinder.Frontend;

/// <summary>Finder adapter for the framework-owned native character card.</summary>
internal static class NativeCharacterCard
{
    internal static GameObject CreatePlaceholder()
    {
        var root = new GameObject("FinderCharacterPlaceholder", typeof(RectTransform));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 102f);
        return root;
    }

    internal static void ReleasePlaceholder(GameObject root) => UnityEngine.Object.Destroy(root);

    internal static GameObject Create(
        int characterId,
        string name,
        string position,
        sbyte grade,
        string displayData,
        short areaId,
        short blockId,
        Action<int> onInteract)
    {
        bool canInteract = IsAtTaiwuLocation(areaId, blockId);
        GameObject root = TaiwuNativeCharacterCard.Create(
            new TaiwuNativeCharacterCardData(characterId, displayData),
            () => onInteract(characterId),
            new TaiwuNativeCharacterCardOptions
            {
                Width = 320f,
                Interactable = canInteract,
                ShowStatus = false,
                ShowGuardIcon = false,
                EnableHotkeyTooltip = true,
            });

        // Search results may intentionally expose a known alias. Keep that text
        // while retaining the native rich organization/grade line from Set().
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0) texts[0].text = name;
        return root;
    }

    internal static void Release(GameObject root) => TaiwuNativeCharacterCard.Release(root);

    internal static bool IsAtTaiwuLocation(short areaId, short blockId)
    {
        try
        {
            Location current = SingletonObject.getInstance<WorldMapModel>().CurrentLocation;
            return current.AreaId == areaId && current.BlockId == blockId;
        }
        catch
        {
            return false;
        }
    }
}
