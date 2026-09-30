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
        bool isParameter)
    {
        if (isParameter &&
            pattern.RefKind == RefKind.None &&
            pattern.TypePattern is BoundWildcardTypePattern)
        {
            return true;
        }

        return pattern.RefKind == candidate.RefKind;
    }
}
