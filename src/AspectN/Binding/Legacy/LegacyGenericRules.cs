using AspectN.Metadata;
using AspectN.Syntax;

namespace AspectN.Binding.Legacy;

internal static class LegacyGenericRules
{
    public static BoundGenericPattern CreateOmittedGenericPattern()
    {
        return new BoundGenericPattern(BoundGenericArityKind.Any, 0);
    }

    public static bool TryGetImplicitVariableName(
        NamedTypePatternSyntax syntax,
        bool declaration,
        Func<string, bool> isAlreadyDeclared,
        out string? name)
    {
        name = null;
        if (syntax.Segments.Count != 1 || syntax.Separators.Count != 0)
        {
            return false;
        }

        var segment = syntax.Segments[0];
        if (segment.GenericArguments is not null || segment.NameParts.Count != 1)
        {
            return false;
        }

        var candidate = segment.NameParts[0].Text;
        if (declaration || isAlreadyDeclared(candidate))
        {
            name = candidate;
            return true;
        }

        return false;
    }

    public static TypeSignature[] GetEffectiveTypeArguments(
        NamedTypeSignature candidate)
    {
        return GenericParameterNamer.GetEffectiveTypeArguments(candidate);
    }

    public static TypeSignature[] GetEffectiveMethodArguments(
        MethodSignature candidate)
    {
        if (candidate.GenericArguments.Count > 0)
        {
            return candidate.GenericArguments.ToArray();
        }

        return candidate.GenericParameters
            .Select(parameter =>
                (TypeSignature)new GenericParameterTypeSignature(parameter))
            .ToArray();
    }

    public static bool IsNullableDefinition(TypeIdentity identity)
    {
        return identity.GenericArity == 1 &&
               StringComparer.Ordinal.Equals(identity.Namespace, "System") &&
               StringComparer.Ordinal.Equals(
                   identity.MetadataName,
                   "Nullable");
    }

    public static bool IsTupleDefinition(TypeIdentity identity)
    {
        return StringComparer.Ordinal.Equals(identity.Namespace, "System") &&
               (StringComparer.Ordinal.Equals(
                    identity.MetadataName,
                    "Tuple") ||
                StringComparer.Ordinal.Equals(
                    identity.MetadataName,
                    "ValueTuple"));
    }

}
