using System.Text;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class CanonicalSignatureFormatter
{
    public static string Format(
        CallableSignature candidate,
        AspectNLanguageVersion languageVersion)
    {
        var builder = new StringBuilder();
        builder.Append(
            FormatAccessibility(candidate.Accessibility, languageVersion));
        if (candidate.IsStatic) builder.Append(" static");
        if (candidate.IsAsync) builder.Append(" async");
        builder.Append(' ');
        AppendTypeUse(builder, candidate.ReturnType);
        builder.Append(' ');
        AppendType(builder, candidate.DeclaringType);
        builder.Append('.');
        builder.Append(candidate.Name);
        AppendCallableGeneric(builder, candidate);
        builder.Append('(');
        for (var index = 0; index < candidate.Parameters.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            AppendTypeUse(builder, candidate.Parameters[index].Type);
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static void AppendCallableGeneric(
        StringBuilder builder,
        CallableSignature candidate)
    {
        if (candidate.GenericArguments.Count > 0)
        {
            AppendTypeList(builder, candidate.GenericArguments);
            return;
        }

        if (candidate.GenericParameters.Count == 0) return;
        builder.Append('<');
        for (var index = 0; index < candidate.GenericParameters.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            builder.Append(candidate.GenericParameters[index].Name);
        }

        builder.Append('>');
    }

    private static void AppendTypeUse(
        StringBuilder builder,
        TypeUseSignature typeUse)
    {
        builder.Append(typeUse.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnly => "ref readonly ",
            _ => string.Empty
        });
        AppendType(builder, typeUse.Type);
    }

    private static void AppendType(
        StringBuilder builder,
        TypeSignature type)
    {
        switch (type)
        {
            case NamedTypeSignature named:
                AppendNamedType(builder, named);
                break;
            case ArrayTypeSignature array:
                AppendType(builder, array.ElementType);
                builder.Append('[');
                builder.Append(',', array.Rank - 1);
                builder.Append(']');
                break;
            case GenericParameterTypeSignature parameter:
                builder.Append(parameter.Parameter.Name);
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
        NamedTypeSignature named)
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
                ref argumentOffset);
        }
    }

    private static void AppendNamedGeneric(
        StringBuilder builder,
        int arity,
        IReadOnlyList<TypeSignature> arguments,
        ref int argumentOffset)
    {
        if (arity == 0) return;
        builder.Append('<');
        if (arguments.Count >= argumentOffset + arity)
        {
            for (var index = 0; index < arity; index++)
            {
                if (index > 0) builder.Append(", ");
                AppendType(builder, arguments[argumentOffset + index]);
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
        IReadOnlyList<TypeSignature> arguments)
    {
        builder.Append('<');
        for (var index = 0; index < arguments.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            AppendType(builder, arguments[index]);
        }

        builder.Append('>');
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

    private static string FormatAccessibility(
        Accessibility accessibility,
        AspectNLanguageVersion languageVersion)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.ProtectedInternal
                when languageVersion == AspectNLanguageVersion.Legacy =>
                "protectedinternal",
            Accessibility.PrivateProtected
                when languageVersion == AspectNLanguageVersion.Legacy =>
                "privateprotected",
            Accessibility.ProtectedInternal => "protected internal",
            Accessibility.PrivateProtected => "private protected",
            _ => "unknown"
        };
    }
}
