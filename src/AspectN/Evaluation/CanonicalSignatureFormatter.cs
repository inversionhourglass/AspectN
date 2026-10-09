using System.Text;
using AspectN.Metadata;

namespace AspectN.Evaluation;

internal static class CanonicalSignatureFormatter
{
    // Standard 定义签名的泛型形参按扁平槽位渲染为 T1..Tn（外层类型从左到右、
    // 再内层、最后方法泛型），总数为 1 时渲染为裸 T；封闭构造仍展开实际实参。
    public static string Format(MethodSignature candidate, CancellationToken cancellationToken = default)
    {
        var builder = new BoundedSignatureBuilder(cancellationToken);
        builder.ValidateArity(candidate.GenericParameters.Count);
        var genericNames = GenericParameterNamer.CreatePositionalNames(
            candidate, builder.ValidateEffectiveArity, cancellationToken);
        builder.Append(
            FormatAccessibility(candidate.Accessibility));
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
        BoundedSignatureBuilder builder,
        MethodSignature candidate,
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
        BoundedSignatureBuilder builder,
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
        BoundedSignatureBuilder builder,
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
        BoundedSignatureBuilder builder,
        NamedTypeSignature named,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        builder.ValidateEffectiveArity(named);
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
        BoundedSignatureBuilder builder,
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
        BoundedSignatureBuilder builder,
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
        BoundedSignatureBuilder builder,
        GenericParameterSignature parameter,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        if (!genericNames.TryGetValue(new GenericParameterKey(parameter), out var name))
            throw new CanonicalSignatureUnavailableException(
                $"Cannot determine a positional generic name for declaration '{parameter.OwnerId}'.");
        builder.Append(name);
    }

    // 深度合规的共享图也可能展开成指数长度文本，必须在分配输出前限制累计大小。
    private sealed class BoundedSignatureBuilder(CancellationToken cancellationToken)
    {
        private const int MAX_LENGTH = 65_536;
        private readonly StringBuilder _builder = new();

        public void ValidateEffectiveArity(NamedTypeSignature named)
        {
            long arity = 0;
            for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
            {
                arity += identity.GenericArity;
                ValidateArity(arity);
            }
        }

        public void ValidateArity(long arity)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (arity > MAX_LENGTH)
                throw new CanonicalSignatureUnavailableException(
                    $"Generic arity exceeds the {MAX_LENGTH}-character formatting budget.");
        }

        public void Append(string? value)
        {
            EnsureSpace(value?.Length ?? 0);
            _builder.Append(value);
        }

        public void Append(char value)
        {
            EnsureSpace(1);
            _builder.Append(value);
        }

        public void Append(char value, int count)
        {
            EnsureSpace(count);
            _builder.Append(value, count);
        }

        public override string ToString() => _builder.ToString();

        private void EnsureSpace(int length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (length > MAX_LENGTH - _builder.Length)
                throw new CanonicalSignatureUnavailableException(
                    $"Canonical signature exceeds the {MAX_LENGTH}-character formatting limit.");
        }
    }

    private static string FormatAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.ProtectedInternal => "protected internal",
            Accessibility.PrivateProtected => "private protected",
            _ => "unknown"
        };
    }
}
