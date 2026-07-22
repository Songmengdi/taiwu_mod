using HarmonyLib;
using UnityEngine;

namespace TaiwuDebugMod
{
    /// <summary>
    /// 验证性 Harmony Patch —— 拦截 UnityEngine.Debug.Log 确认 Harmony 工作正常。
    /// 在输出日志中标记 [TaiwuDebugMod] 前缀用于识别。
    /// </summary>
    public static class HarmonyPatches
    {
        /// <summary>
        /// 在每条 Debug.Log 消息前添加 Mod 标记，验证 Patch 已生效。
        /// </summary>
        [HarmonyPatch(typeof(Debug), "Log", new[] { typeof(object) })]
        [HarmonyPostfix]
        public static void DebugLog_Postfix(object message)
        {
            // 什么都不做，只是验证 Patch 挂载成功。
            // 如需调试，取消下面的注释即可看到所有 Unity Log：
            // Debug.Log("[TaiwuDebugMod Patch] " + message);
        }
    }
}
