using System.Text;
using AspectN.Metadata;

namespace AspectN.Evaluation.Legacy;

internal static class LegacyCanonicalSignatureFormatter
{
    public static string Format(CallableSignature candidate)
    {
        var genericNames = CreateGenericNames(candidate);
        var builder = new StringBuilder();
        builder.Append(FormatAccessibility(candidate.Accessibility));
        if (candidate.IsStatic) builder.Append(" static");
        builder.Append(' ');
        AppendTypeUse(builder, candidate.ReturnType, genericNames);
        builder.Append(' ');
        AppendType(builder, candidate.DeclaringType, genericNames);
        builder.Append('.');
        builder.Append(candidate.Name);
        AppendCallableGeneric(builder, candidate, genericNames);
        builder.Append('(');
        for (var index = 0; index < candidate.Parameters.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendTypeUse(
                builder,
                candidate.Parameters[index].Type,
                genericNames);
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static Dictionary<GenericParameterKey, string> CreateGenericNames(
        CallableSignature candidate)
    {
        var typeParameters = EnumerateTypeArguments(candidate.DeclaringType)
            .OfType<GenericParameterTypeSignature>()
            .Select(parameter => parameter.Parameter)
            .Where(parameter =>
                parameter.OwnerKind == GenericParameterOwnerKind.Type)
            .ToArray();
        var methodParameters = candidate.GenericParameters.ToArray();
        var totalCount = typeParameters.Length + methodParameters.Length;
        var names = new Dictionary<GenericParameterKey, string>();
        if (totalCount == 1)
        {
            var parameter = typeParameters.FirstOrDefault() ??
                methodParameters[0];
            names[new GenericParameterKey(parameter)] = "T";
            return names;
        }

        for (var index = 0; index < typeParameters.Length; index++)
        {
            names[new GenericParameterKey(typeParameters[index])] =
                $"T{typeParameters.Length - index}";
        }

        for (var index = 0; index < methodParameters.Length; index++)
        {
            names[new GenericParameterKey(methodParameters[index])] =
                $"T{typeParameters.Length + index + 1}";
        }

        return names;
    }

    private static IEnumerable<TypeSignature> EnumerateTypeArguments(
        TypeSignature type)
    {
        return type is NamedTypeSignature named
            ? named.TypeArguments
            : Array.Empty<TypeSignature>();
    }

    private static void AppendCallableGeneric(
        StringBuilder builder,
        CallableSignature candidate,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        if (candidate.GenericArguments.Count > 0)
        {
            AppendTypeList(builder, candidate.GenericArguments, genericNames);
            return;
        }

        if (candidate.GenericParameters.Count == 0) return;
        builder.Append('<');
        for (var index = 0; index < candidate.GenericParameters.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendGenericParameter(
                builder,
                candidate.GenericParameters[index],
                genericNames);
        }

        builder.Append('>');
    }

    private static void AppendTypeUse(
        StringBuilder builder,
        TypeUseSignature typeUse,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        AppendType(builder, typeUse.Type, genericNames);
        if (typeUse.RefKind != RefKind.None) builder.Append('&');
    }

    private static void AppendType(
        StringBuilder builder,
        TypeSignature type,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        switch (type)
        {
            case NamedTypeSignature named:
                AppendNamedType(builder, named, genericNames);
                break;
            case ArrayTypeSignature array:
                AppendType(builder, array.ElementType, genericNames);
                builder.Append('[');
                builder.Append(',', array.Rank - 1);
                builder.Append(']');
                break;
            case GenericParameterTypeSignature parameter:
                AppendGenericParameter(
                    builder,
                    parameter.Parameter,
                    genericNames);
                break;
            case UnsupportedTypeSignature unsupported:
                builder.Append(unsupported.DisplayName);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported type signature '{type.Kind}'.");
        }
    }

    private static void AppendNamedType(
        StringBuilder builder,
        NamedTypeSignature named,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        var chain = GetTypeChain(named.Identity);
        var argumentOffset = 0;
        for (var index = 0; index < chain.Count; index++)
        {
            var identity = chain[index];
            if (index == 0 && !string.IsNullOrEmpty(identity.Namespace))
            {
                builder.Append(identity.Namespace);
                builder.Append('.');
            }
            else if (index > 0)
            {
                builder.Append('/');
            }

            builder.Append(identity.MetadataName);
            AppendNamedGeneric(
                builder,
                identity.GenericArity,
                named.TypeArguments,
                ref argumentOffset,
                genericNames);
        }
    }

    private static void AppendNamedGeneric(
        StringBuilder builder,
        int arity,
        IReadOnlyList<TypeSignature> arguments,
        ref int argumentOffset,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        if (arity == 0) return;
        builder.Append('<');
        if (arguments.Count >= argumentOffset + arity)
        {
            for (var index = 0; index < arity; index++)
            {
                if (index > 0) builder.Append(',');
                AppendType(
                    builder,
                    arguments[argumentOffset + index],
                    genericNames);
            }

            argumentOffset += arity;
        }
        else
        {
            builder.Append(',', arity - 1);
        }

        builder.Append('>');
    }

    private static void AppendTypeList(
        StringBuilder builder,
        IReadOnlyList<TypeSignature> arguments,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        builder.Append('<');
        for (var index = 0; index < arguments.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendType(builder, arguments[index], genericNames);
        }

        builder.Append('>');
    }

    private static void AppendGenericParameter(
        StringBuilder builder,
        GenericParameterSignature parameter,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        builder.Append(
            genericNames.TryGetValue(
                new GenericParameterKey(parameter),
                out var name)
                ? name
                : parameter.Name);
    }

    private static List<TypeIdentity> GetTypeChain(TypeIdentity identity)
    {
        var result = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            result.Add(current);
        }

        result.Reverse();
        return result;
    }

    private static string FormatAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.ProtectedInternal => "protectedinternal",
            Accessibility.PrivateProtected => "privateprotected",
            _ => "unknown"
        };
    }

    private readonly struct GenericParameterKey :
        IEquatable<GenericParameterKey>
    {
        private readonly GenericParameterOwnerKind _ownerKind;
        private readonly string _ownerId;
        private readonly int _position;

        public GenericParameterKey(GenericParameterSignature parameter)
        {
            _ownerKind = parameter.OwnerKind;
            _ownerId = parameter.OwnerId;
            _position = parameter.Position;
        }

        public bool Equals(GenericParameterKey other)
        {
            return _ownerKind == other._ownerKind &&
                   _position == other._position &&
                   StringComparer.Ordinal.Equals(_ownerId, other._ownerId);
        }

        public override bool Equals(object? obj)
        {
            return obj is GenericParameterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)_ownerKind;
                hash = (hash * 397) ^ _position;
                hash = (hash * 397) ^
                       StringComparer.Ordinal.GetHashCode(_ownerId);
                return hash;
            }
        }
    }
}
