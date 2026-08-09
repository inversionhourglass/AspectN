using AspectN.Metadata;

namespace AspectN.Binding.Legacy;

internal static class LegacyModifierRules
{
    public static string Normalize(string content)
    {
        return content.ToLowerInvariant();
    }

    public static bool IsMatch(
        IReadOnlyList<BoundModifier> modifiers,
        Accessibility accessibility,
        bool isStatic)
    {
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
                modifier.Accessibility == accessibility;
        }

        return !hasPositiveAccessibility || positiveAccessibilityMatched;
    }
}
