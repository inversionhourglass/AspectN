using System.Text;
using AspectN.Evaluation.Legacy;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class CanonicalSignatureFormatter
{
    // Standard 定义签名的泛型形参按扁平槽位渲染为 T1..Tn（外层类型从左到右、
    // 再内层、最后方法泛型）；封闭构造仍展开实际实参。与 Legacy 的差异：
    // 总数为 1 时 Standard 仍是 T1（Legacy 用裸 T），分隔符保留 ", "。
    public static string Format(
        CallableSignature candidate,
        AspectNLanguageVersion languageVersion)
    {
        if (languageVersion == AspectNLanguageVersion.Legacy)
        {
            return LegacyCanonicalSignatureFormatter.Format(candidate);
        }

        var genericNames = GenericParameterNamer.CreatePositionalNames(
            candidate);
        var builder = new StringBuilder();
        builder.Append(
            FormatAccessibility(candidate.Accessibility, languageVersion));
        if (candidate.IsStatic) builder.Append(" static");
        if (candidate.IsAsync) builder.Append(" async");
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
            if (index > 0) builder.Append(", ");
            AppendTypeUse(
                builder,
                candidate.Parameters[index].Type,
                genericNames);
        }

        builder.Append(')');
        return builder.ToString();
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
            if (index > 0) builder.Append(", ");
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
        builder.Append(typeUse.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnly => "ref readonly ",
            _ => string.Empty
        });
        AppendType(builder, typeUse.Type, genericNames);
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
        var chain = GenericParameterNamer.GetTypeChain(named.Identity);
        // 开放定义没有实参，回退到按声明链合成的位置形参，类型子句因此与
        // 使用位置共用同一套 T1..Tn 编号，不会出现一侧抹名一侧保名的分裂。
        var arguments = GenericParameterNamer.GetEffectiveTypeArguments(named);
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
                arguments,
                ref argumentOffset,
                genericNames);
        }
    }

    private static void AppendNamedGeneric(
        StringBuilder builder,
        int arity,
        TypeSignature[] arguments,
        ref int argumentOffset,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        if (arity == 0) return;
        builder.Append('<');
        if (arguments.Length >= argumentOffset + arity)
        {
            for (var index = 0; index < arity; index++)
            {
                if (index > 0) builder.Append(", ");
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
            if (index > 0) builder.Append(", ");
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
