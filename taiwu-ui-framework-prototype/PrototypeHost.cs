using System.Collections;
using UnityEngine;

namespace TaiwuUiFrameworkPrototype;

internal sealed class PrototypeHost : MonoBehaviour
{
    private PrototypeWindow? _window;
    private int _generation;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            Toggle();

        _window?.Tick();
    }

    internal void Toggle()
    {
        EnsureWindow();
        _window!.Toggle();
    }

    private void EnsureWindow()
    {
        if (_window == null)
            _window = new PrototypeWindow(this, ++_generation);
    }

    internal void DestroyAndRecreate()
    {
        _window?.Dispose();
        _window = null;
        StartCoroutine(RecreateNextFrame());
    }

    private IEnumerator RecreateNextFrame()
    {
        yield return null;
        EnsureWindow();
        _window!.Show();
    }

    private void OnDestroy()
    {
        _window?.Dispose();
        _window = null;
    }
}
