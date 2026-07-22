using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuUi.Sample;

[PluginConfig("TaiwuUi.Sample", "SMD", "2.0.0")]
public sealed class SamplePlugin : TaiwuRemakePlugin
{
    private GameObject? _host;

    public override void Initialize()
    {
        _host = new GameObject("TaiwuUi.Sample.Host");
        UnityEngine.Object.DontDestroyOnLoad(_host);
        _host.AddComponent<SampleHotkey>();
    }

    public override void Dispose()
    {
        if (_host != null)
            UnityEngine.Object.Destroy(_host);
        _host = null;
        SampleControl.Dispose();
    }
}

internal sealed class SampleHotkey : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
            SampleControl.Toggle();
    }

    private void OnDestroy() => SampleControl.Dispose();
}

public static class SampleControl
{
    private static ITaiwuWindow? _window;

    public static bool IsShowing => _window?.IsShowing == true;
    public static string Snapshot =>
        $"Api={TaiwuUiApi.ApiVersion}; Ready={TaiwuUiApi.IsReady}; WindowCreated={_window != null}; IsShowing={IsShowing}";

    public static void Toggle()
    {
        _window ??= TaiwuUiApi.Mount(new UiWindow(
            "TaiwuUi.Sample", "Hello",
            Ui.Column(
                Ui.Heading("原生主题与声明式布局") with { Key = "heading" },
                Ui.Text("该窗口由独立消费 MOD 使用 TaiwuUi.Core v2 创建。") with { Key = "body" },
                Ui.Muted($"API {TaiwuUiApi.ApiVersion} / Major {TaiwuUiApi.ApiMajor}") with { Key = "version" },
                Ui.Divider() with { Key = "divider" },
                Ui.Row(
                    Ui.Button("主要操作", () => Debug.Log("TaiwuUi primary button clicked"))
                        with { Key = "primary" },
                    Ui.Button("次要操作", () => Debug.Log("TaiwuUi secondary button clicked"),
                        new TaiwuButtonOptions { Style = TaiwuButtonStyle.Secondary })
                        with { Key = "secondary" }) with { Key = "actions" }) with { Key = "root" },
            "TaiwuUi v2", 920f, 600f));
        _window.Toggle();
    }

    public static void Hide() => _window?.Hide();

    public static void Dispose()
    {
        _window?.Dispose();
        _window = null;
    }
}
