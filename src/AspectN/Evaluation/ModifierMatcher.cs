using AspectN.Binding;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class ModifierMatcher
{
    public static bool IsMatch(
        IReadOnlyList<BoundModifier> modifiers,
        Accessibility accessibility,
        bool isStatic)
    {
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

            var matches = modifier.Alternatives is not null
                ? modifier.Alternatives.Contains(accessibility)
                : modifier.Accessibility == accessibility;
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsRefKindMatch(
        BoundTypeUsePattern pattern,
        TypeUseSignature candidate)
    {
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
