using FrameWork;
using GameData.Utilities;
using TaiwuUi;

namespace LiverFriendlyInteractions.Frontend;

internal sealed class InteractionHubSettingsWindow : IDisposable
{
    private readonly InteractionHubPreferences _preferences;
    private readonly Action _changed;
    private ITaiwuWindow? _window;
    private IReadOnlyList<InteractionCatalogItem> _catalog = Array.Empty<InteractionCatalogItem>();

    internal bool IsShowing => _window?.IsShowing == true;

    internal InteractionHubSettingsWindow(InteractionHubPreferences preferences, Action changed)
    {
        _preferences = preferences;
        _changed = changed;
    }

    internal void Show(IReadOnlyList<InteractionCatalogItem> catalog)
    {
        _catalog = catalog;
        Render();
        _window!.Show();
    }

    internal void Hide() => _window?.Hide();

    private void Render()
    {
        var children = new List<UiElement>
        {
            Ui.Row(Ui.Muted("修改后立即保存，所有存档共用。"), Ui.Spacer(12f),
                Ui.Button("恢复默认", Reset, new TaiwuButtonOptions { Width = 160f, Height = 48f,
                    Style = TaiwuButtonStyle.Outlined })) with { Key = "settings-head" },
            Ui.Divider(),
            Ui.Heading("常用交互") with { Key = "favorite-heading" },
        };

        foreach (string key in _preferences.Favorites)
        {
            InteractionCatalogItem? item = _catalog.FirstOrDefault(value => value.PreferenceKey == key);
            string name = item?.Name ?? key;
            children.Add(Ui.Row(
                Ui.Text(name, new TaiwuTextOptions { Width = 500f }),
                Ui.Button("上移", () => Move(key, -1), SmallButton()),
                Ui.Button("下移", () => Move(key, 1), SmallButton()),
                Ui.Button("移除", () => Remove(key), SmallButton())) with { Key = "favorite-" + key });
        }

        children.Add(Ui.Divider());
        children.Add(Ui.Heading("全部交互") with { Key = "catalog-heading" });
        foreach (InteractionCatalogItem item in _catalog.OrderBy(value => value.NativeOrder))
        {
            if (_preferences.Contains(item.PreferenceKey)) continue;
            string key = item.PreferenceKey;
            children.Add(Ui.Row(
                Ui.Text(item.Name, new TaiwuTextOptions { Width = 650f }),
                Ui.Button("加入常用", () => Add(key), new TaiwuButtonOptions
                {
                    Width = 180f, Height = 48f, Style = TaiwuButtonStyle.Outlined,
                })) with { Key = "catalog-" + key });
        }

        UiWindow document = new("LiverFriendlyInteractions", "InteractionHubSettings",
            Ui.Scroll(new UiColumnElement(children, 6f), new TaiwuScrollOptions
            {
                Height = 720f, ShowBackground = false,
            }), title: "交互偏好设置", width: 1180f, height: 900f,
            layer: TaiwuWindowLayer.VeryTop, cover: TaiwuWindowCover.Dimmed,
            lifetime: TaiwuWindowLifetime.KeepAlive);
        if (_window == null) _window = TaiwuUiApi.Mount(document);
        else _window.Render(document);
    }

    private static TaiwuButtonOptions SmallButton() => new()
    {
        Width = 110f, Height = 48f, Style = TaiwuButtonStyle.Outlined,
    };

    private void Add(string key) { _preferences.Add(key); Changed(); }
    private void Remove(string key) { _preferences.Remove(key); Changed(); }
    private void Move(string key, int offset) { _preferences.Move(key, offset); Changed(); }
    private void Reset()
    {
        global::UIElement.Dialog.SetOnInitArgs(EasyPool.Get<ArgumentBox>().SetObject("Cmd", new DialogCmd
        {
            Title = "恢复默认常用交互",
            Content = "确定恢复默认顺序？当前自定义排序将被替换。",
            Yes = () => { _preferences.Reset(); Changed(); },
        }));
        UIManager.Instance.MaskUI(global::UIElement.Dialog);
    }
    private void Changed() { Render(); _changed(); }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
    }
}
