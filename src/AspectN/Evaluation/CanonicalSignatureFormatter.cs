using System.Text;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal static class CanonicalSignatureFormatter
{
    // Standard 定义签名的泛型形参按扁平槽位渲染为 T1..Tn（外层类型从左到右、
    // 再内层、最后方法泛型），总数为 1 时渲染为裸 T；封闭构造仍展开实际实参。
    public static string Format(MethodSignature candidate, CancellationToken cancellationToken = default) =>
        Format(candidate, new TypeSystemQueryContext(new AspectNMatchOptions(), cancellationToken));

    internal static string Format(MethodSignature candidate, TypeSystemQueryContext context)
    {
        var cancellationToken = context.CancellationToken;
        var builder = new BoundedSignatureBuilder(context);
        builder.ValidateText(candidate.Name);
        builder.ValidateArity(candidate.GenericParameters.Count);
        var genericNames = GenericParameterNamer.CreatePositionalNames(
            candidate, builder.ValidateEffectiveArity, context, cancellationToken);
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
            if (candidate.GenericArguments.Count != GenericParameterNamer.GetMethodArity(candidate))
                throw new CanonicalSignatureUnavailableException("Constructed callable generic arguments are incomplete.");
            AppendTypeList(builder, candidate.GenericArguments, genericNames);
            return;
        }

        if (candidate.GenericParameters.Count != GenericParameterNamer.GetMethodArity(candidate))
            throw new CanonicalSignatureUnavailableException("Callable generic declaration slots are incomplete.");
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
        BoundedSignatureBuilder builder, TypeSignature type,
        IReadOnlyDictionary<GenericParameterKey, string> genericNames)
    {
        var pending = new Stack<(TypeSignature? Type, string? Text, int Commas)>();
        pending.Push((type, null, 0));
        while (pending.Count > 0)
        {
            builder.Context.SpendWork();
            var item = pending.Pop();
            if (item.Text is not null) { builder.Append(item.Text); continue; }
            if (item.Type is null) { builder.Append(',', item.Commas); continue; }
            switch (item.Type)
            {
                case ArrayTypeSignature array:
                    pending.Push((null, "]", 0));
                    pending.Push((null, null, array.Rank - 1));
                    pending.Push((null, "[", 0));
                    pending.Push((array.ElementType, null, 0));
                    break;
                case NamedTypeSignature named:
                    builder.ValidateEffectiveArity(named);
                    var chain = GenericParameterNamer.GetTypeChain(named.Identity, builder.Context);
                    var arguments = GenericParameterNamer.GetEffectiveTypeArguments(named, builder.Context);
                    var offset = arguments.Length;
                    // 反向压入声明链和实参，输出仍按由外到内、从左到右的原顺序。
                    for (var index = chain.Count - 1; index >= 0; index--)
                    {
                        var identity = chain[index];
                        var arity = identity.GenericArity;
                        if (arity > 0)
                        {
                            pending.Push((null, ">", 0));
                            offset -= arity;
                            for (var position = arity - 1; position >= 0; position--)
                            {
                                builder.Context.SpendWork();
                                pending.Push((arguments[offset + position], null, 0));
                                if (position > 0) pending.Push((null, ", ", 0));
                            }
                            pending.Push((null, "<", 0));
                        }
                        pending.Push((null, identity.MetadataName, 0));
                        if (index > 0) pending.Push((null, "/", 0));
                        else if (!string.IsNullOrEmpty(identity.Namespace))
                        {
                            pending.Push((null, ".", 0));
                            pending.Push((null, identity.Namespace, 0));
                        }
                    }
                    break;
                case GenericParameterTypeSignature parameter:
                    AppendGenericParameter(builder, parameter.Parameter, genericNames);
                    break;
                case UnsupportedTypeSignature unsupported:
                    builder.Append(unsupported.DisplayName);
                    break;
                default:
                    throw new CanonicalSignatureUnavailableException("The type has no canonical text.");
            }
        }
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
    private sealed class BoundedSignatureBuilder(TypeSystemQueryContext context)
    {
        private readonly StringBuilder _builder = new();
        public TypeSystemQueryContext Context { get; } = context;

        public void ValidateEffectiveArity(NamedTypeSignature named)
        {
            long arity = 0;
            for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
            {
                Context.SpendWork();
                ValidateText(identity.MetadataName);
                ValidateText(identity.Namespace);
                arity += identity.GenericArity;
                ValidateArity(arity);
            }
        }

        public void ValidateText(string text) => Context.ValidateTextLength(0, text.Length,
            Context.Options.MaximumCanonicalSignatureLength, nameof(Context.Options.MaximumCanonicalSignatureLength));

        public void ValidateArity(long arity)
        {
            Context.CancellationToken.ThrowIfCancellationRequested();
            if (arity > 0) Context.CheckTextLength(0, arity, Context.Options.MaximumCanonicalSignatureLength, nameof(Context.Options.MaximumCanonicalSignatureLength));
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
            Context.CheckTextLength(_builder.Length, length, Context.Options.MaximumCanonicalSignatureLength,
                nameof(Context.Options.MaximumCanonicalSignatureLength));
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
