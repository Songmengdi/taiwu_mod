namespace MapSkillFinder.Frontend;

internal readonly record struct PageTargetChoice(sbyte Type, sbyte State);

/// <summary>
/// 太吾对当前选中书籍的了解程度，按页存位掩码。
/// 功法：第 0 页（总纲）按总纲类型 0..4 占位，正文页按方向 0=正 1=逆 占位；
/// 技艺：每页只用 bit0。拥有只统计完整页（残/佚不可读，不算拥有）。
/// </summary>
internal sealed record TaiwuBookKnowledge(bool Combat, int[] OwnedTypeMasks, int[] ReadTypeMasks)
{
    internal static readonly TaiwuBookKnowledge Empty = new(true, new int[0], new int[0]);
}

/// <summary>
/// Pure per-page coverage decisions over TaiwuBookKnowledge. "Covered" means
/// 太吾已读该页或已有完整页——这个书页无需再寻找。已读与已有不区分，共用一种提示。
/// Kept free of game assemblies so the domain tests can link this file directly.
/// </summary>
internal static class TaiwuPageMarking
{
    internal static int VariantBit(bool combat, sbyte type)
    {
        if (!combat)
            return 1;
        return type is >= 0 and <= 7 ? 1 << type : 0;
    }

    // 单个书页选项（页 + 方向/总纲类型）是否已覆盖：用于背景色标记。
    internal static bool IsVariantCoveredByTaiwu(
        TaiwuBookKnowledge knowledge,
        int page,
        PageTargetChoice target)
    {
        if (target.State < 0 || page < 0 || page >= knowledge.OwnedTypeMasks.Length)
            return false;
        int bit = VariantBit(knowledge.Combat, target.Type);
        int covered = knowledge.OwnedTypeMasks[page] | knowledge.ReadTypeMasks[page];
        return (covered & bit) != 0;
    }

    // 整页是否无需再寻访（默认"不限"的判定）：
    // 功法正文页要求正/逆两个方向都已读或已有完整页；
    // 总纲与技艺书没有正逆之分，任意一种已读或已有完整页即可。
    internal static bool IsPageFullyCoveredByTaiwu(TaiwuBookKnowledge knowledge, int page)
    {
        if (page < 0 || page >= knowledge.OwnedTypeMasks.Length)
            return false;
        int covered = knowledge.OwnedTypeMasks[page] | knowledge.ReadTypeMasks[page];
        if (covered == 0)
            return false;
        if (!knowledge.Combat || page == 0)
            return true;
        return (covered & 0b11) == 0b11;
    }

    internal static bool HasAnyMark(TaiwuBookKnowledge knowledge)
    {
        for (int page = 0; page < knowledge.OwnedTypeMasks.Length; page++)
        {
            if (knowledge.OwnedTypeMasks[page] != 0 || knowledge.ReadTypeMasks[page] != 0)
                return true;
        }
        return false;
    }
}
