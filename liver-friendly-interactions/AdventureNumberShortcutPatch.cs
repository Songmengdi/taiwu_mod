using FrameWork;
using FrameWork.UISystem.UIElements;
using Game.Views.Adventure;
using GameData.Adventure;
using GameData.Domains.Adventure;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LiverFriendlyInteractions.Frontend;

[HarmonyPatch(typeof(ViewAdventureRemake), "Update")]
internal static class AdventureNumberShortcutPatch
{
    [HarmonyPostfix]
    private static void HandleFirstOption(ViewAdventureRemake __instance)
    {
        bool shortcutPressed = Input.GetKeyDown(KeyCode.Alpha1) ||
                               Input.GetKeyDown(KeyCode.Keypad1);
        bool adventureHasFocus = UIManager.Instance.IsFocusElement(__instance.Element);
        GameObject? selectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        bool textInputHasFocus = selectedObject != null &&
                                 selectedObject.GetComponentInParent<TMP_InputField>() != null;
        IReadOnlyList<ViewAdventureRemake.ElementDisplayItem> items = __instance.DisplayItems;

        if (!AdventureNumberShortcutPolicy.ShouldHandleFirstOption(
                shortcutPressed,
                adventureHasFocus,
                textInputHasFocus,
                items.Count))
        {
            return;
        }

        ViewAdventureRemake.ElementDisplayItem first = items[0];
        AdventureBlockIndex taiwuIndex =
            SingletonObject.getInstance<AdventureRemakeModel>().AdventureTaiwu.Index;
        if (!first.BlockIndex.Equals(taiwuIndex))
        {
            return;
        }

        if (first.IsExitItem)
        {
            GEvent.OnEvent(UiEvents.AdventureExitClick);
            return;
        }

        AdventureDomainMethod.Call.InteractElement(
            __instance.Element.GameDataListenerId,
            first.Element.Id);
    }
}
