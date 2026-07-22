using FrameWork;
using FrameWork.UISystem.UIElements;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VillageWorkOptimizer.Frontend;

internal sealed class NativeOptimizerPanel : MonoBehaviour
{
    private static readonly string[] ObjectiveNames = { "资产", "人才培养", "威望", "招人", "资源收获" };
    private static readonly int[] DefaultPriorities = { 0, 1, 4, 2, 3 };

    private readonly List<int> _priorities = new(DefaultPriorities);
    private readonly List<GameObject> _priorityRows = new();
    private readonly List<GameObject> _resultRows = new();
    private bool _initialized;
    private bool _loading;
    private CButton? _buttonTemplate;
    private TextMeshProUGUI? _textTemplate;
    private GameObject? _root;
    private RectTransform? _priorityRoot;
    private RectTransform? _content;
    private TextMeshProUGUI? _status;

    internal void Initialize(
        Transform owner,
        CButton buttonTemplate,
        Transform buttonLayout,
        TextMeshProUGUI textTemplate)
    {
        if (_initialized)
            return;
        _initialized = true;
        _buttonTemplate = buttonTemplate;
        _textTemplate = textTemplate;

        CButton openButton = CreateNativeButton(buttonLayout, "村务排班", new Vector2(170, 52), Toggle);
        openButton.name = "VillageWorkOptimizerOpenButton";
        openButton.gameObject.SetActive(true);
        LayoutElement openLayout = openButton.GetComponent<LayoutElement>();
        if (openLayout == null)
            openLayout = openButton.gameObject.AddComponent<LayoutElement>();
        openLayout.preferredWidth = 170;
        openLayout.preferredHeight = 52;

        BuildPanel(owner);
        _root!.SetActive(false);
    }

    internal void Toggle()
    {
        if (_root == null)
            return;
        _root.SetActive(!_root.activeSelf);
    }

    private void BuildPanel(Transform parent)
    {
        _root = CreateRect(
            "VillageWorkOptimizerPanel",
            parent,
            new Vector2(0.10f, 0.08f),
            new Vector2(0.90f, 0.92f),
            Vector2.zero,
            Vector2.zero).gameObject;
        var background = _root.AddComponent<Image>();
        background.color = new Color(0.035f, 0.095f, 0.10f, 0.985f);
        var outline = _root.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.48f, 0.31f, 0.9f);
        outline.effectDistance = new Vector2(2, -2);

        RectTransform title = CreateRect("Title", _root.transform, new Vector2(0.03f, 0.90f), new Vector2(0.78f, 0.98f), Vector2.zero, Vector2.zero);
        CreateText(title, "太吾村本月排班", 30, TextAlignmentOptions.Left);
        CreateNativeButton(_root.transform, "关闭", new Vector2(110, 48), Toggle,
            new Vector2(0.91f, 0.94f), new Vector2(0.91f, 0.94f));

        RectTransform left = CreateRect("PriorityPanel", _root.transform, new Vector2(0.025f, 0.16f), new Vector2(0.29f, 0.88f), Vector2.zero, Vector2.zero);
        var leftBg = left.gameObject.AddComponent<Image>();
        leftBg.color = new Color(0.08f, 0.14f, 0.14f, 0.95f);
        CreateText(CreateRect("PriorityTitle", left, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero),
            "目标优先级", 23, TextAlignmentOptions.Center);
        _priorityRoot = CreateRect("PriorityRows", left, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);
        RebuildPriorities();
        CreateNativeButton(left, "计算本月排班", new Vector2(250, 56), RequestPlan,
            new Vector2(0.5f, 0.13f), new Vector2(0.5f, 0.13f));

        _status = CreateText(CreateRect("Status", _root.transform, new Vector2(0.32f, 0.84f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero),
            "调整优先级后计算。资源包括食材、木材、金铁、玉石、药材、毒物与织物。", 19, TextAlignmentOptions.Left);

        BuildResultScroll();
    }

    private void BuildResultScroll()
    {
        RectTransform scrollRoot = CreateRect("ResultScroll", _root!.transform, new Vector2(0.32f, 0.10f), new Vector2(0.97f, 0.82f), Vector2.zero, Vector2.zero);
        var scrollBg = scrollRoot.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.02f, 0.055f, 0.06f, 0.92f);
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();

        RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0, 0, 0, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        _content = CreateRect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        _content.pivot = new Vector2(0.5f, 1);
        var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = _content;
        scroll.horizontal = false;
    }

    private void RebuildPriorities()
    {
        foreach (GameObject row in _priorityRows)
            Destroy(row);
        _priorityRows.Clear();
        if (_priorityRoot == null)
            return;

        float height = 1f / _priorities.Count;
        for (int i = 0; i < _priorities.Count; i++)
        {
            int index = i;
            RectTransform row = CreateRect("Priority" + i, _priorityRoot,
                new Vector2(0, 1 - (i + 1) * height), new Vector2(1, 1 - i * height), new Vector2(4, 4), new Vector2(-4, -4));
            row.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.19f, 0.18f, 0.95f);
            CreateText(CreateRect("Label", row, new Vector2(0.04f, 0.05f), new Vector2(0.62f, 0.95f), Vector2.zero, Vector2.zero),
                $"{i + 1}. {ObjectiveNames[_priorities[i]]}", 20, TextAlignmentOptions.Left);
            CreateNativeButton(row, "↑", new Vector2(46, 38), () => Move(index, -1), new Vector2(0.72f, 0.5f), new Vector2(0.72f, 0.5f));
            CreateNativeButton(row, "↓", new Vector2(46, 38), () => Move(index, 1), new Vector2(0.89f, 0.5f), new Vector2(0.89f, 0.5f));
            _priorityRows.Add(row.gameObject);
        }
    }

    private void Move(int index, int delta)
    {
        int target = index + delta;
        if (target < 0 || target >= _priorities.Count)
            return;
        (_priorities[index], _priorities[target]) = (_priorities[target], _priorities[index]);
        RebuildPriorities();
    }

    private void RequestPlan()
    {
        if (_loading || _status == null)
            return;
        _loading = true;
        _status.text = "正在读取太吾村并计算综合排班……";
        OptimizerBackendClient.Request(_priorities, OnPlanReceived);
    }

    private void OnPlanReceived(bool success, string reason, List<PlanRowView> rows)
    {
        _loading = false;
        if (_status != null)
            _status.text = reason;
        ClearResults();
        if (!success || _content == null)
            return;
        foreach (PlanRowView row in rows)
            AddResultRow(row);
    }

    private void ClearResults()
    {
        foreach (GameObject row in _resultRows)
            Destroy(row);
        _resultRows.Clear();
    }

    private void AddResultRow(PlanRowView data)
    {
        RectTransform row = CreateRect("ResultRow", _content!, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        row.sizeDelta = new Vector2(0, 76);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 76;
        row.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.16f, 0.15f, 0.96f);
        CreateText(CreateRect("Building", row, new Vector2(0.02f, 0.08f), new Vector2(0.19f, 0.92f), Vector2.zero, Vector2.zero), data.Building, 19, TextAlignmentOptions.Left);
        CreateText(CreateRect("Leader", row, new Vector2(0.20f, 0.08f), new Vector2(0.34f, 0.92f), Vector2.zero, Vector2.zero), data.Leader, 18, TextAlignmentOptions.Left);
        CreateText(CreateRect("Members", row, new Vector2(0.35f, 0.08f), new Vector2(0.70f, 0.92f), Vector2.zero, Vector2.zero), data.Members, 17, TextAlignmentOptions.Left);
        CreateText(CreateRect("Purpose", row, new Vector2(0.71f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero), data.Purpose, 18, TextAlignmentOptions.Left);
        _resultRows.Add(row.gameObject);
    }

    private CButton CreateNativeButton(
        Transform parent,
        string label,
        Vector2 size,
        UnityAction action,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null)
    {
        CButton button = Instantiate(_buttonTemplate!, parent);
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        StripLocalizationBindings(button.gameObject);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rect.anchorMax = anchorMax ?? rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI text = texts.FirstOrDefault(x => x.name == "Label") ?? texts.FirstOrDefault();
        if (text != null)
            text.text = label;
        return button;
    }

    private static void StripLocalizationBindings(GameObject root)
    {
        // 原生按钮的 TextLanguage 会在下一次语言刷新时把克隆文本重置为“确认”。
        // 克隆只复用图片、按钮状态和字体外观，不继承文本的本地化键。
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null && component.GetType().Name == "TextLanguage")
                DestroyImmediate(component);
        }
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string value, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = _textTemplate!.font;
        text.fontSharedMaterial = _textTemplate.fontSharedMaterial;
        text.color = new Color(0.88f, 0.84f, 0.72f, 1);
        text.fontSize = size;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.text = value;
        return text;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
}
