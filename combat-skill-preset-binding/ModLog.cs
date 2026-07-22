using GameData.Utilities;

namespace CombatSkillPresetBinding.Backend;

internal static class ModLog
{
    private const string Tag = "运功预设绑定";

    internal static void Info(string message)
    {
        AdaptableLog.TagInfo(Tag, message);
    }

    internal static void Detail(string message)
    {
        if (BackendPlugin.DetailedLogging)
        {
            Info(message);
        }
    }

    internal static void Warning(string message)
    {
        AdaptableLog.TagWarning(Tag, message);
    }

    internal static void Error(string message)
    {
        AdaptableLog.TagError(Tag, message);
    }
}
