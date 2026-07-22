using UnityEngine;

namespace VillageWorkOptimizer.Frontend;

internal sealed class NativeUiHotkey : MonoBehaviour
{
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F8))
            return;
        NativeOptimizerPanel panel = FindObjectOfType<NativeOptimizerPanel>(true);
        if (panel != null)
            panel.Toggle();
    }
}
