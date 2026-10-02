using AspectN.Binding;
using AspectN.Binding.Legacy;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class ModifierMatcher
{
    public static bool IsMatch(
        IReadOnlyList<BoundModifier> modifiers,
        Accessibility accessibility,
        bool isStatic,
        AspectNLanguageVersion languageVersion)
    {
        if (languageVersion == AspectNLanguageVersion.Legacy)
        {
            return LegacyModifierRules.IsMatch(
                modifiers,
                accessibility,
                isStatic);
        }

        var hasPositiveAccessibility = false;
        var positiveAccessibilityMatched = false;
        foreach (var modifier in modifiers)
        {
            if (modifier.Kind == BoundModifierKind.Static)
            {
                if (modifier.IsNegated == isStatic) return false;
                continue;
            }

            if (modifier.IsNegated)
            {
                if (modifier.Accessibility == accessibility) return false;
                continue;
            }

            hasPositiveAccessibility = true;
            positiveAccessibilityMatched |=
                modifier.Alternatives is not null
                    ? modifier.Alternatives.Contains(accessibility)
                    : modifier.Accessibility == accessibility;
            if (!positiveAccessibilityMatched)
            {
                return false;
            }
        }

        return !hasPositiveAccessibility || positiveAccessibilityMatched;
    }

    public static bool IsRefKindMatch(
        BoundTypeUsePattern pattern,
        TypeUseSignature candidate,
        bool isParameter,
        AspectNLanguageVersion languageVersion)
    {
        if (languageVersion == AspectNLanguageVersion.Legacy)
        {
            // Legacy（冻结）：通配类型的 ref kind 豁免保留 Shared 行为
            // （旧 ExecutionPattern 不比较返回 RefKind），其余精确比较。
            if (pattern.RefKind == RefKind.None &&
                pattern.TypePattern is BoundWildcardTypePattern)
            {
                return true;
            }

            return pattern.RefKind == candidate.RefKind;
        }

        // Standard（规范 §11）：未标注 = 不约束（任意 ref kind）；
        // !ref = 仅非 by-ref；ref/out/in/ref readonly = 精确匹配。
        if (pattern.NegatedRefKind)
        {
            return candidate.RefKind == RefKind.None;
        }

        if (pattern.RefKind == RefKind.None)
        {
            return true;
        }

        return pattern.RefKind == candidate.RefKind;
    }
}
