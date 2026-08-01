using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TaiwuUi;

/// <summary>
/// Owns the native second-level tab visual state. The game uses independent
/// hover and checkmark layers rather than tinting one SpriteSwap target.
/// </summary>
internal sealed class TaiwuSecondaryTabVisual : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    private CButton? _button;
    private GameObject? _hover;
    private GameObject? _checkmark;
    private bool _selected;
    private bool _hovered;

    internal RectTransform Root => (RectTransform)transform;
    internal CButton Button => _button!;

    internal static TaiwuSecondaryTabVisual Create(
        Transform parent,
        string name,
        string label,
        float fontSize,
        TaiwuTheme theme,
        Action onClick)
    {
        RectTransform root = UiFactory.Rect(name, parent);
        TaiwuSecondaryTabVisual visual = root.gameObject.AddComponent<TaiwuSecondaryTabVisual>();

        RectTransform background = UiFactory.Rect("Background", root);
        UiFactory.Stretch(background, Vector2.zero, Vector2.zero);
        CImage backgroundImage = background.gameObject.AddComponent<CImage>();

        RectTransform hover = UiFactory.Rect("Hover", background);
        UiFactory.Stretch(hover, Vector2.zero, Vector2.zero);
        CImage hoverImage = hover.gameObject.AddComponent<CImage>();

        RectTransform checkmark = UiFactory.Rect("Checkmark", background);
        UiFactory.Stretch(checkmark, Vector2.zero, Vector2.zero);
        CImage checkmarkImage = checkmark.gameObject.AddComponent<CImage>();
        theme.ApplySecondaryTabLayers(backgroundImage, hoverImage, checkmarkImage);

        CButton button = root.gameObject.AddComponent<CButton>();
        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick.Invoke);

        TextMeshProUGUI text = UiFactory.Text(
            "Label", root, label, fontSize, theme,
            TaiwuTextStyle.Body, TextAlignmentOptions.Center);
        UiFactory.Stretch(text.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        visual._button = button;
        visual._hover = hover.gameObject;
        visual._checkmark = checkmark.gameObject;
        visual.RefreshLayers();
        return visual;
    }

    internal void SetState(bool selected, bool interactable)
    {
        _selected = selected;
        _button!.interactable = interactable;
        RefreshLayers();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        RefreshLayers();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RefreshLayers();
    }

    private void OnDisable()
    {
        _hovered = false;
        RefreshLayers();
    }

    private void RefreshLayers()
    {
        if (_hover != null)
            _hover.SetActive(_hovered && !_selected && _button != null && _button.interactable);
        if (_checkmark != null)
            _checkmark.SetActive(_selected);
    }
}
