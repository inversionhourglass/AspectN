namespace AspectN.Syntax;

/// <summary>Represents a signature syntax node.</summary>
public abstract class SignatureSyntax : SyntaxNode
{
    protected SignatureSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> parts)
        : base(kind, parts)
    {
        Parts = Children;
    }

    /// <summary>Gets the parts.</summary>
    public IReadOnlyList<SyntaxNode> Parts { get; }
}

/// <summary>Represents a callable signature syntax node.</summary>
/// <param name="parts">Specifies the parts.</param>
public sealed class CallableSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.CallableSignature, parts)
{
}

/// <summary>Represents a property signature syntax node.</summary>
/// <param name="parts">Specifies the parts.</param>
public sealed class PropertySignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.PropertySignature, parts)
{
}

/// <summary>Represents a constructor signature syntax node.</summary>
/// <param name="parts">Specifies the parts.</param>
public sealed class ConstructorSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.ConstructorSignature, parts)
{
}

/// <summary>Represents a parameter syntax node.</summary>
/// <param name="typeUse">Specifies the type use.</param>
public sealed class ParameterSyntax(TypeUseSyntax typeUse) : SyntaxNode(SyntaxKind.Parameter, SyntaxChildren.Create(typeUse))
{
    /// <summary>Gets the type use.</summary>
    public TypeUseSyntax TypeUse { get; } = typeUse;
}

/// <summary>Represents a modifier syntax node.</summary>
public sealed class ModifierSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _tokens;

    /// <summary>Initializes a new instance of the ModifierSyntax class.</summary>
    /// <param name="tokens">Specifies the tokens.</param>
    public ModifierSyntax(IEnumerable<SyntaxToken> tokens)
        : this(tokens.ToArray())
    {
    }

    private ModifierSyntax(IReadOnlyList<SyntaxToken> tokens)
        : base(SyntaxKind.Modifier, tokens)
    {
        _tokens = tokens;
    }

    /// <summary>Gets the tokens.</summary>
    public IReadOnlyList<SyntaxToken> Tokens => _tokens;
}

/// <summary>Specifies the generic pattern form values.</summary>
public enum GenericPatternForm
{
    FixedArity,
    AnyPositiveArity,
    Arguments
}

/// <summary>Represents a generic pattern list syntax node.</summary>
public sealed class GenericPatternListSyntax : SyntaxNode
{
    private readonly IReadOnlyList<TypePatternSyntax> _arguments;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    /// <summary>Initializes a new instance of the GenericPatternListSyntax class.</summary>
    /// <param name="lessThanToken">Specifies the less than token.</param>
    /// <param name="form">Specifies the form.</param>
    /// <param name="arity">Specifies the arity.</param>
    /// <param name="arguments">Specifies the arguments.</param>
    /// <param name="separators">Specifies the separators.</param>
    /// <param name="wildcardToken">Specifies the wildcard token.</param>
    /// <param name="greaterThanToken">Specifies the greater than token.</param>
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

    /// <summary>Gets the less than token.</summary>
    public SyntaxToken LessThanToken { get; }

    /// <summary>Gets the form.</summary>
    public GenericPatternForm Form { get; }

    /// <summary>Gets the arity.</summary>
    public int Arity { get; }

    /// <summary>Gets the arguments.</summary>
    public IReadOnlyList<TypePatternSyntax> Arguments => _arguments;

    /// <summary>Gets the separators.</summary>
    public IReadOnlyList<SyntaxToken> Separators => _separators;

    /// <summary>Gets the wildcard token.</summary>
    public SyntaxToken? WildcardToken { get; }

    /// <summary>Gets the greater than token.</summary>
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

/// <summary>Represents a type use syntax node.</summary>
public sealed class TypeUseSyntax : SyntaxNode
{
    private readonly IReadOnlyList<SyntaxToken> _refKindTokens;

    /// <summary>Initializes a new instance of the TypeUseSyntax class.</summary>
    /// <param name="refKindTokens">Specifies the ref kind tokens.</param>
    /// <param name="typePattern">Specifies the type pattern.</param>
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

    /// <summary>Gets the ref kind tokens.</summary>
    public IReadOnlyList<SyntaxToken> RefKindTokens => _refKindTokens;

    /// <summary>Gets the type pattern.</summary>
    public TypePatternSyntax TypePattern { get; }
}
