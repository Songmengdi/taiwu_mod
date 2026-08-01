using FrameWork.UISystem.UIElements;
using TaiwuUi;
using UnityEngine;

namespace LiverFriendlyInteractions.Frontend;

/// <summary>Adapts interaction-hub people to the framework's native character card.</summary>
internal static class InteractionHubNativeCharacterCard
{
    internal static GameObject Create(InteractionPersonView person, bool selected, Action onClick) =>
        TaiwuNativeCharacterCard.Create(
            new TaiwuNativeCharacterCardData(
                person.TargetId,
                person.DisplayData,
                person.Kind == InteractionPersonKind.Caravan
                    ? TaiwuNativeCharacterKind.Caravan
                    : TaiwuNativeCharacterKind.Character),
            onClick,
            new TaiwuNativeCharacterCardOptions
            {
                Width = 340f,
                Selected = selected,
                ShowStatus = true,
                ShowGuardIcon = false,
                EnableHotkeyTooltip = person.Kind != InteractionPersonKind.Caravan,
            });

    internal static void Release(GameObject root) => TaiwuNativeCharacterCard.Release(root);

    internal static CImage? FindSelectionVisual(GameObject root) =>
        root.transform.Find("TaiwuUiSelection")?.GetComponent<CImage>();

    internal static void SetSelected(CImage visual, bool selected) =>
        visual.gameObject.SetActive(selected);

    internal static Game.Views.MapBlockCharList.MapBlockChar? ResolveTemplate() =>
        Resources.FindObjectsOfTypeAll<Game.Views.MapBlockCharList.MapBlockChar>()
            .FirstOrDefault(item => item.gameObject.scene.IsValid() &&
                                    item.GetComponentInParent<Canvas>() != null);
}
