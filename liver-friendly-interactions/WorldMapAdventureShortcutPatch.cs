using System.Reflection;
using FrameWork.UISystem.UIElements;
using Game.Views.Map;
using GameData.Domains.Map;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LiverFriendlyInteractions.Frontend;

[HarmonyPatch(typeof(ViewWorldMap), "Update")]
internal static class WorldMapAdventureShortcutPatch
{
    private static readonly MethodInfo EnterAdventureMethod =
        AccessTools.Method(typeof(MapElementAdventureRemake), "OnClickAdventure");

    [HarmonyPostfix]
    private static void EnterCurrentBlockAdventure(ViewWorldMap __instance)
    {
        bool shortcutPressed = Input.GetKeyDown(KeyCode.Alpha1) ||
                               Input.GetKeyDown(KeyCode.Keypad1);
        if (!shortcutPressed)
        {
            return;
        }

        bool worldMapHasFocus = UIManager.Instance.IsFocusElement(__instance.Element);
        GameObject? selectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        bool textInputHasFocus = selectedObject != null &&
                                 selectedObject.GetComponentInParent<TMP_InputField>() != null;

        MapElementAdventureRemake? currentAdventure = FindCurrentBlockAdventure(__instance);

        if (!AdventureNumberShortcutPolicy.ShouldHandleWorldMapAdventure(
                shortcutPressed,
                worldMapHasFocus,
                textInputHasFocus,
                ViewWorldMap.InAdventureRemake,
                currentAdventure != null))
        {
            return;
        }

        EnterAdventureMethod.Invoke(currentAdventure, null);
    }

    private static MapElementAdventureRemake? FindCurrentBlockAdventure(
        ViewWorldMap worldMap)
    {
        WorldMapModel model = SingletonObject.getInstance<WorldMapModel>();
        Location currentLocation = model.CurrentLocation;

        foreach (MapElementAdventureRemake adventure in
                 worldMap.GetComponentsInChildren<MapElementAdventureRemake>(false))
        {
            Location blockLocation = Traverse.Create(adventure)
                .Property<Location>("BlockLocation")
                .Value;
            if (blockLocation.Equals(currentLocation))
            {
                return adventure;
            }
        }

        return null;
    }
}
