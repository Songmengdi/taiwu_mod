using FrameWork;
using Game.Views.Bottom;
using GameData.Domains.Map;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace MapSkillFinder.Frontend;

/// <summary>Development entry used by the Taiwu probe to preview a uniquely named hot-loaded build.</summary>
public static class RuntimePreview
{
    private static SkillFinderWindow? _last;

    public static void Show(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            throw new ArgumentException("The active MapSkillFinder mod ID is required.", nameof(modId));

        FrontendPlugin.UseRuntimeModId(modId);
        ViewBottom owner = UnityEngine.Object.FindObjectOfType<ViewBottom>()
            ?? throw new InvalidOperationException("ViewBottom is not active.");
        SkillFinderWindow window = owner.gameObject.AddComponent<SkillFinderWindow>();
        window.Initialize(owner);
        window.Open();
        _last = window;
        ExploredMarkCleaner.Attach(owner.gameObject);

        // The running game may still be using an older framework assembly whose
        // popup canvas tied with the native layer at sorting order 600.  Keep the
        // development preview visible while the rebuilt framework awaits restart.
        BringToFront();
    }

    public static string BringToFront()
    {
        int count = 0;
        foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas.gameObject.name != "TaiwuUi_MapSkillFinder_TaiwuFinder")
                continue;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 810;
            canvas.transform.SetAsLastSibling();
            count++;
        }
        return count.ToString();
    }

    /// <summary>Reopens the last preview window after it was closed, to exercise
    /// the framework's native-element rebuild path.</summary>
    public static string ReopenLast()
    {
        if (_last == null)
            return "none";
        _last.Open();
        return "reopened";
    }

    /// <summary>
    /// Repairs framework UIBase instances created by a pre-fix hot-loaded build.
    /// Native UI initializes RelativeAtlases during resource preparation, while
    /// directly mounted framework views need an explicit empty array.
    /// </summary>
    public static string RepairRelativeAtlases()
    {
        int count = 0;
        foreach (UIBase view in Resources.FindObjectsOfTypeAll<UIBase>())
        {
            if (view.RelativeAtlases != null)
                continue;
            view.RelativeAtlases = Array.Empty<SpriteAtlas>();
            count++;
        }
        return count.ToString();
    }

    public static string ScrollCurrentPageToBottom()
    {
        int count = 0;
        foreach (ScrollRect scroll in Resources.FindObjectsOfTypeAll<ScrollRect>())
        {
            if (!scroll.isActiveAndEnabled ||
                scroll.GetComponentInParent<Canvas>()?.gameObject.name != "TaiwuUi_MapSkillFinder_TaiwuFinder" ||
                scroll.gameObject.name != "Scroll")
                continue;
            scroll.verticalNormalizedPosition = 0f;
            count++;
        }
        Canvas.ForceUpdateCanvases();
        return count.ToString();
    }

    /// <summary>Dev self-test: mark removal through the real GEvent wiring.
    /// Fires the same events the model raises, so subscription, argument keys,
    /// tracker logic and the UI refresh are all exercised.</summary>
    public static string SelfTestMarkCleaner()
    {
        WorldMapModel map = SingletonObject.getInstance<WorldMapModel>();
        Location here = map.CurrentLocation;
        MapBlockData block = map.CurrentBlockData;
        int baseCount = map.FindMapBlockMarkLocationList.Count;

        MapMarkTracker.ReplaceMarks(map, new List<Location> { here }, "self-test");
        string result;
        if (map.FindMapBlockMarkLocationList.Count != baseCount + 1)
        {
            result = "FAIL: mark was not added";
        }
        else
        {
            GEvent.OnEvent(UiEvents.WorldMapBlockDataChange, EasyPool.Get<ArgumentBox>().SetObject("Data", block));
            if (map.FindMapBlockMarkLocationList.Count != baseCount)
            {
                result = "FAIL: explored mark was not removed";
            }
            else
            {
                MapMarkTracker.ReplaceMarks(map, new List<Location> { here }, "self-test");
                GEvent.OnEvent(UiEvents.WorldMapPlayerBlockChange, EasyPool.Get<ArgumentBox>());
                result = map.FindMapBlockMarkLocationList.Count == baseCount
                    ? "OK: explore and arrival both remove the mod's mark"
                    : "FAIL: arrival mark was not removed";
            }
        }
        UnityEngine.Debug.Log($"[MapSkillFinder] SelfTestMarkCleaner: {result}");
        return result;
    }

    /// <summary>Dev self-test for the "不限" wildcard page target.</summary>
    public static string SelfTestPageWildcard()
    {
        var book = new BookCopyView("b", 0, 0, 0, 0);
        var any = new PageTargetChoice(-1, -1);
        if (!BookHoldingWorkspace.Matches(book, 2, any, combat: false))
            return "FAIL: wildcard does not match";
        if (!BookHoldingWorkspace.Matches(book, 0, any, combat: true))
            return "FAIL: combat wildcard does not match";
        var holders = new[]
        {
            new BookHolderView(1, "甲", 0, 0, "门派", 0, new[] { book }),
            new BookHolderView(2, "乙", 0, 0, "门派", 0, new[] { book }),
        };
        var targets = Enumerable.Repeat(any, BookHoldingWorkspace.LifePageCount).ToArray();
        IReadOnlyList<BookHolderSet> sets = BookHoldingWorkspace.FindHolderSets(holders, targets, combat: false);
        // Every holder covers all pages under a full wildcard, so both 1-holder
        // sets must appear (plus the 2-holder superset the list also offers).
        string result = sets.Count(set => set.Holders.Count == 1) == 2
            ? "OK: wildcard matches any page; all-wildcard yields a 1-holder set per holder"
            : $"FAIL: sets={sets.Count} oneHolderSets={sets.Count(set => set.Holders.Count == 1)}";
        UnityEngine.Debug.Log($"[MapSkillFinder] SelfTestPageWildcard: {result}");
        return result;
    }
}
