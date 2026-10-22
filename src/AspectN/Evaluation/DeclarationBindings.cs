using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

// 缓存唯一声明事实。来源固定为原始候选，遍历祖先、特性或 OR 时不产生新取值。
internal sealed class DeclarationBindings(TypeSignature? type, MethodSignature? method)
{
    private readonly Dictionary<DeclarationSlot, SlotResolution> _slots = [];

    public MatchStatus Compare(BoundVariableTypePattern variable, TypeSignature candidate, EvaluationContext context)
    {
        if (candidate is not (GenericParameterTypeSignature or UnsupportedTypeSignature)) return MatchStatus.NotMatched;
        var slot = variable.Variable.Declaration;
        if (slot is null) return MatchStatus.NotMatched;
        if (!_slots.TryGetValue(slot, out var resolution))
        {
            resolution = Resolve(slot, context.QueryContext);
            _slots.Add(slot, resolution);
        }

        if (resolution.Status == MatchStatus.Unknown)
        {
            if (resolution.Availability is { } availability)
                return MetadataQuery.Require(availability, MetadataFacts.AssociatedProperty, variable, context);
            context.AddDiagnostic(new AspectNDiagnostic(DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning, "The candidate's own generic declaration slot is unavailable.", variable.Span));
            return MatchStatus.Unknown;
        }
        if (resolution.Status == MatchStatus.NotMatched) return MatchStatus.NotMatched;

        // 严格消费声明身份，不把未限定查询规则或后端关系查询用于槽位相等。
        var result = StructuralTypeComparer.AreEqual(resolution.Parameter!, candidate, context.QueryContext);
        context.AddDiagnostics(result.Diagnostics);
        return result.Status;
    }

    private SlotResolution Resolve(DeclarationSlot slot, TypeSystemQueryContext context)
    {
        context.SpendWork();
        if (slot.Source == DeclarationSource.Method)
        {
            var declaration = method?.OriginalDefinition ?? method;
            if (declaration is null) return new(MatchStatus.NotMatched);
            if (slot.Position >= declaration.GenericParameters.Count)
                return new(MatchStatus.Unknown);
            var parameter = declaration.GenericParameters[slot.Position];
            return parameter.OwnerKind == GenericParameterOwnerKind.Method && parameter.Position == slot.Position
                ? new(MatchStatus.Matched, new GenericParameterTypeSignature(parameter))
                : new(MatchStatus.NotMatched);
        }

        TypeSignature? source;
        switch (slot.Source)
        {
            case DeclarationSource.RootType:
                source = type;
                break;
            case DeclarationSource.DeclaringType:
                source = method?.DeclaringType;
                break;
            case DeclarationSource.PropertyDeclaringType:
                if (method is null) return new(MatchStatus.NotMatched);
                if (!method.Availability.IsKnown(MetadataFacts.AssociatedProperty))
                    return new(MatchStatus.Unknown, availability: method.Availability);
                source = method.AssociatedProperty?.DeclaringType;
                break;
            default:
                return new(MatchStatus.NotMatched);
        }

        foreach (var step in slot.Path)
        {
            context.SpendWork();
            if (source is UnsupportedTypeSignature) return new(MatchStatus.Unknown);
            if (step == DeclarationTypeStep.ArrayElement)
            {
                if (source is not ArrayTypeSignature array) return new(MatchStatus.NotMatched);
                source = array.ElementType;
            }
            else if (source is NamedTypeSignature nullable && nullable.Identity.Namespace == "System" &&
                nullable.Identity.MetadataName == "Nullable" && nullable.Identity.GenericArity == 1)
            {
                source = GenericParameterNamer.GetEffectiveTypeArguments(nullable, context)[0];
            }
        }

        if (source is UnsupportedTypeSignature) return new(MatchStatus.Unknown);
        if (source is not NamedTypeSignature named) return new(MatchStatus.NotMatched);
        var chain = GenericParameterNamer.GetTypeChain(named.Identity, context);
        var level = chain.Count - slot.ContainingDepth - 1;
        if (level < 0 || slot.Position >= chain[level].GenericArity) return new(MatchStatus.NotMatched);
        var position = slot.Position;
        for (var index = 0; index < level; index++) { context.SpendWork(); position += chain[index].GenericArity; }
        var owner = chain[level];
        return new(MatchStatus.Matched, new GenericParameterTypeSignature(new GenericParameterSignature(
            $"T{position + 1}", GenericParameterOwnerKind.Type, GenericParameterNamer.GetOwnerId(owner, context), owner.Assembly, position)));
    }

    private sealed class SlotResolution(MatchStatus status, TypeSignature? parameter = null,
        MetadataAvailability? availability = null)
    {
        public MatchStatus Status { get; } = status;
        public TypeSignature? Parameter { get; } = parameter;
        public MetadataAvailability? Availability { get; } = availability;
    }
}
