using AspectN.Binding;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class ModifierMatcher
{
    private static readonly MetadataAvailability _UnknownAccessibility = new(MetadataFacts.Accessibility);

    public static MatchStatus Evaluate(
        IReadOnlyList<BoundModifier> modifiers,
        Accessibility accessibility,
        bool isStatic,
        MetadataAvailability availability,
        BoundNode owner,
        EvaluationContext context)
    {
        var status = MatchStatus.Matched;
        for (var index = 0; index < modifiers.Count; index++)
        {
            var modifier = modifiers[index];
            var fact = modifier.Kind == BoundModifierKind.Static
                ? MetadataFacts.IsStatic
                : MetadataFacts.Accessibility;
            var source = fact == MetadataFacts.Accessibility && accessibility == Accessibility.Unknown &&
                availability.IsKnown(fact) ? _UnknownAccessibility : availability;
            var known = MetadataQuery.Require(source, fact, owner, context);
            var result = known == MatchStatus.Unknown ? known :
                IsMatch(modifier, accessibility, isStatic)
                    ? MatchStatus.Matched : MatchStatus.NotMatched;
            status = MatchLogic.And(status, result);
        }

        return status;
    }

    public static MatchStatus EvaluateRefKind(
        BoundTypeUsePattern pattern,
        TypeUseSignature candidate,
        BoundNode owner,
        EvaluationContext context)
    {
        if (!pattern.NegatedRefKind && pattern.RefKind == RefKind.None) return MatchStatus.Matched;
        var byReference = MetadataQuery.Require(candidate.Availability, MetadataFacts.ByReference, owner, context);
        if (byReference == MatchStatus.Unknown) return byReference;
        if (pattern.NegatedRefKind)
            return candidate.IsByReference ? MatchStatus.NotMatched : MatchStatus.Matched;
        if (!candidate.IsByReference) return MatchStatus.NotMatched;
        var classification = MetadataQuery.Require(candidate.Availability, MetadataFacts.RefKind, owner, context);
        if (classification == MatchStatus.Unknown) return classification;
        return IsRefKindMatch(pattern, candidate) ? MatchStatus.Matched : MatchStatus.NotMatched;
    }

    public static bool IsMatch(
        IReadOnlyList<BoundModifier> modifiers,
        Accessibility accessibility,
        bool isStatic)
    {
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (!IsMatch(modifiers[index], accessibility, isStatic)) return false;
        }

        return true;
    }

    private static bool IsMatch(BoundModifier modifier, Accessibility accessibility, bool isStatic)
    {
        if (modifier.Kind == BoundModifierKind.Static) return modifier.IsNegated != isStatic;
        if (modifier.IsNegated) return modifier.Accessibility != accessibility;
        return modifier.Alternatives is not null
            ? modifier.Alternatives.Contains(accessibility)
            : modifier.Accessibility == accessibility;
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
