using FrameWork.UISystem.UIElements;
using Game.Components.Building;
using Game.Views.Building;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace VillageWorkOptimizer.Frontend;

[HarmonyPatch(typeof(ViewBuildingArea), nameof(ViewBuildingArea.OnInit))]
internal static class ViewBuildingAreaOnInitPatch
{
    private static readonly AccessTools.FieldRef<ViewBuildingArea, bool> IsTaiwuVillage =
        AccessTools.FieldRefAccess<ViewBuildingArea, bool>("_isTaiwuVillage");

    private static readonly AccessTools.FieldRef<ViewBuildingArea, BuildingAreaInfo> AreaInfo =
        AccessTools.FieldRefAccess<ViewBuildingArea, BuildingAreaInfo>("buildingAreaInfo");

    private static readonly AccessTools.FieldRef<ViewBuildingArea, CButton> ConfirmButton =
        AccessTools.FieldRefAccess<ViewBuildingArea, CButton>("buttonConfirmPlanBuilding");

    private static readonly AccessTools.FieldRef<BuildingAreaInfo, RectTransform> LeftHolder =
        AccessTools.FieldRefAccess<BuildingAreaInfo, RectTransform>("leftHolder");

    private static readonly AccessTools.FieldRef<BuildingAreaInfo, TextMeshProUGUI> SpaceText =
        AccessTools.FieldRefAccess<BuildingAreaInfo, TextMeshProUGUI>("spaceLimitText");

    [HarmonyPostfix]
    private static void Postfix(ViewBuildingArea __instance)
    {
        if (!IsTaiwuVillage(__instance))
            return;

        BuildingAreaInfo areaInfo = AreaInfo(__instance);
        NativeOptimizerPanel panel = __instance.GetComponent<NativeOptimizerPanel>();
        if (panel == null)
            panel = __instance.gameObject.AddComponent<NativeOptimizerPanel>();
        panel.Initialize(
            __instance.transform,
            ConfirmButton(__instance),
            LeftHolder(areaInfo),
            SpaceText(areaInfo));
    }
}
