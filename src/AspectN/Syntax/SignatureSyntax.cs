namespace AspectN.Syntax;

public abstract class SignatureSyntax : SyntaxNode
{
    protected SignatureSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> parts)
        : base(kind, parts)
    {
        Parts = Children;
    }

    public IReadOnlyList<SyntaxNode> Parts { get; }
}

public sealed class CallableSignatureSyntax : SignatureSyntax
{
    public CallableSignatureSyntax(IEnumerable<SyntaxNode> parts)
        : base(SyntaxKind.CallableSignature, parts)
    {
    }
}

public sealed class PropertySignatureSyntax : SignatureSyntax
{
    public PropertySignatureSyntax(IEnumerable<SyntaxNode> parts)
        : base(SyntaxKind.PropertySignature, parts)
    {
    }
}

public sealed class ConstructorSignatureSyntax : SignatureSyntax
{
    public ConstructorSignatureSyntax(IEnumerable<SyntaxNode> parts)
        : base(SyntaxKind.ConstructorSignature, parts)
    {
    }
}

public sealed class ParameterSyntax : SyntaxNode
{
    public ParameterSyntax(TypeUseSyntax typeUse)
        : base(SyntaxKind.Parameter, SyntaxChildren.Create(typeUse))
    {
        TypeUse = typeUse;
    }

    public TypeUseSyntax TypeUse { get; }
}

public sealed class ModifierSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _tokens;

    public ModifierSyntax(IEnumerable<SyntaxToken> tokens)
        : this(tokens.ToArray())
    {
    }

    private ModifierSyntax(IReadOnlyList<SyntaxToken> tokens)
        : base(SyntaxKind.Modifier, tokens)
    {
        _tokens = tokens;
    }

    public IReadOnlyList<SyntaxToken> Tokens => _tokens;
}

public enum GenericPatternForm
{
    FixedArity,
    AnyPositiveArity,
    Arguments
}

public sealed class GenericPatternListSyntax : SyntaxNode
{
    private readonly IReadOnlyList<TypePatternSyntax> _arguments;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    public GenericPatternListSyntax(
        SyntaxToken lessThanToken,
        GenericPatternForm form,
        int arity,
        IEnumerable<TypePatternSyntax> arguments,
        IEnumerable<SyntaxToken> separators,
        SyntaxToken? wildcardToken,
        SyntaxToken greaterThanToken)
        : this(
            lessThanToken,
            form,
            arity,
            arguments.ToArray(),
            separators.ToArray(),
            wildcardToken,
            greaterThanToken)
    {
    }

    private GenericPatternListSyntax(
        SyntaxToken lessThanToken,
        GenericPatternForm form,
        int arity,
        IReadOnlyList<TypePatternSyntax> arguments,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken? wildcardToken,
        SyntaxToken greaterThanToken)
        : base(
            SyntaxKind.GenericPatternList,
            BuildChildren(
                lessThanToken,
                arguments,
                separators,
                wildcardToken,
                greaterThanToken))
    {
        LessThanToken = lessThanToken;
        Form = form;
        Arity = arity;
        _arguments = arguments;
        _separators = separators;
        WildcardToken = wildcardToken;
        GreaterThanToken = greaterThanToken;
    }

    public SyntaxToken LessThanToken { get; }

    public GenericPatternForm Form { get; }

    public int Arity { get; }

    public IReadOnlyList<TypePatternSyntax> Arguments => _arguments;

    public IReadOnlyList<SyntaxToken> Separators => _separators;

    public SyntaxToken? WildcardToken { get; }

    public SyntaxToken GreaterThanToken { get; }

    private static IReadOnlyList<SyntaxNode> BuildChildren(
        SyntaxToken lessThanToken,
        IReadOnlyList<TypePatternSyntax> arguments,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken? wildcardToken,
        SyntaxToken greaterThanToken)
    {
        IEnumerable<SyntaxNode> content = arguments.Count == 0
            ? separators
            : SyntaxChildren.Interleave(arguments, separators);
        return SyntaxChildren.Create(
            lessThanToken,
            content,
            wildcardToken,
            greaterThanToken);
    }
}

public sealed class TypeUseSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _refKindTokens;

    public TypeUseSyntax(
        IEnumerable<SyntaxToken> refKindTokens,
        TypePatternSyntax typePattern)
        : this(refKindTokens.ToArray(), typePattern)
    {
    }

    private TypeUseSyntax(
        IReadOnlyList<SyntaxToken> refKindTokens,
        TypePatternSyntax typePattern)
        : base(
            SyntaxKind.TypeUse,
            SyntaxChildren.Create(refKindTokens, typePattern))
    {
        _refKindTokens = refKindTokens;
        TypePattern = typePattern;
    }

    public IReadOnlyList<SyntaxToken> RefKindTokens => _refKindTokens;

    public TypePatternSyntax TypePattern { get; }
}
