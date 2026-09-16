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
        if (declaration ||
            isAlreadyDeclared(candidate) ||
            IsHistoricalImplicitName(candidate))
        {
            name = candidate;
            return true;
        }

        return false;
    }

    public static TypeSignature[] GetEffectiveTypeArguments(
        NamedTypeSignature candidate)
    {
        var identities = GetTypeChain(candidate.Identity);
        var totalArity = identities.Sum(identity => identity.GenericArity);
        if (candidate.TypeArguments.Count == totalArity)
        {
            return candidate.TypeArguments.ToArray();
        }

        if (candidate.TypeArguments.Count != 0)
        {
            return Array.Empty<TypeSignature>();
        }

        var arguments = new List<TypeSignature>(totalArity);
        foreach (var identity in identities)
        {
            for (var position = 0;
                 position < identity.GenericArity;
                 position++)
            {
                arguments.Add(
                    new GenericParameterTypeSignature(
                        new GenericParameterSignature(
                            $"T{position + 1}",
                            GenericParameterOwnerKind.Type,
                            identity.FullMetadataName,
                            identity.Assembly,
                            position)));
            }
        }

        return arguments.ToArray();
    }

    public static TypeSignature[] GetEffectiveCallableArguments(
        CallableSignature candidate)
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

    private static bool IsHistoricalImplicitName(string name)
    {
        return name == "T" ||
               (name.Length > 1 &&
                name[0] == 'T' &&
                name.Skip(1).All(char.IsDigit));
    }

    private static List<TypeIdentity> GetTypeChain(TypeIdentity identity)
    {
        var identities = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            identities.Add(current);
        }

        identities.Reverse();
        return identities;
    }
}
