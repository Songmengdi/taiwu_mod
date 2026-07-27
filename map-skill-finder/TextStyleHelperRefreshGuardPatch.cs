using System.Reflection;
using HarmonyLib;

namespace MapSkillFinder.Frontend;

/// <summary>
/// TextStyleHelper.OnEnable schedules a delayed Refresh on the global
/// YieldHelper runner. Rebuilding finder pages can destroy the text before that
/// frame runs, and the coroutine then dies with a NullReferenceException inside
/// GetComponent. Skip the refresh when its target no longer exists.
/// </summary>
internal static class TextStyleHelperRefreshGuardPatch
{
    internal static void Install(Harmony harmony)
    {
        MethodInfo original = AccessTools.Method(typeof(TextStyleHelper), "Refresh")
            ?? throw new MissingMethodException(typeof(TextStyleHelper).FullName, "Refresh");
        MethodInfo prefix = AccessTools.Method(typeof(TextStyleHelperRefreshGuardPatch), nameof(Prefix))
            ?? throw new MissingMethodException(typeof(TextStyleHelperRefreshGuardPatch).FullName, nameof(Prefix));
        harmony.Patch(original, prefix: new HarmonyMethod(prefix));
    }

    // A destroyed Unity object compares equal to null; the delayed coroutine is
    // safe to drop because the text it would style is gone.
    private static bool Prefix(TextStyleHelper __instance) => __instance != null;
}
