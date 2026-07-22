using System;
using HarmonyLib;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace TaiwuDebugMod
{
    [PluginConfig("TaiwuDebugMod", "dev", "1.0.0")]
    public class ModPlugin : TaiwuRemakePlugin
    {
        private Harmony _harmony;

        public override void Initialize()
        {
            try
            {
                Debug.Log("[TaiwuDebugMod] Initialize start");
                Debug.Log("[TaiwuDebugMod] ModIdStr: " + ModIdStr);

                _harmony = new Harmony("com.taiwu.debugmod");
                _harmony.PatchAll(typeof(ModPlugin).Assembly);
                Debug.Log("[TaiwuDebugMod] Harmony initialized: " + _harmony.Id);

                Debug.Log("[TaiwuDebugMod] === Loaded successfully! ===");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TaiwuDebugMod] Initialize failed: " + ex);
            }
        }

        public override void Dispose()
        {
            Debug.Log("[TaiwuDebugMod] Dispose");
            if (_harmony != null)
            {
                try { _harmony.UnpatchSelf(); }
                catch (Exception) { /* ignore unpatch errors */ }
            }
        }

        public override void OnModSettingUpdate()
        {
            Debug.Log("[TaiwuDebugMod] OnModSettingUpdate");
        }

        public override void OnEnterNewWorld()
        {
            Debug.Log("[TaiwuDebugMod] OnEnterNewWorld");
        }
    }
}
