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
        // 合成形参的 Position 必须沿用包含链的扁平 ECMA 槽位（与三个适配器一致），
        // 否则嵌套泛型定义签名中同一形参的两种形态仅 Position 不同而判不等；
        // 名字 T1..Tn 随扁平槽位编号，跨层级不重复。
        var slot = 0;
        foreach (var identity in identities)
        {
            for (var localPosition = 0;
                 localPosition < identity.GenericArity;
                 localPosition++,
                 slot++)
            {
                arguments.Add(
                    new GenericParameterTypeSignature(
                        new GenericParameterSignature(
                            $"T{slot + 1}",
                            GenericParameterOwnerKind.Type,
                            identity.FullMetadataName,
                            identity.Assembly,
                            slot)));
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
