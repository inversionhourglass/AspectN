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
/// <param name="parts">The signature nodes and tokens in source order, including modifiers, types, names, and parameters as applicable.</param>
public sealed class MethodSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.MethodSignature, parts)
{
}

/// <summary>Represents a property signature syntax node.</summary>
/// <param name="parts">The signature nodes and tokens in source order, including modifiers, types, names, and parameters as applicable.</param>
public sealed class PropertySignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.PropertySignature, parts)
{
}

/// <summary>Represents a constructor signature syntax node.</summary>
/// <param name="parts">The signature nodes and tokens in source order, including modifiers, types, names, and parameters as applicable.</param>
public sealed class ConstructorSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.ConstructorSignature, parts)
{
}

/// <summary>Represents a parameter syntax node.</summary>
/// <param name="typeUse">The parameter type pattern and its ref annotations.</param>
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
    /// <param name="tokens">The modifier tokens in source order, including combined accessibility forms.</param>
    public ModifierSyntax(IEnumerable<SyntaxToken> tokens)
        : this(Array.AsReadOnly(tokens.ToArray()))
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
    AnyArity,
    Arguments
}

/// <summary>Represents a generic pattern list syntax node.</summary>
public sealed class GenericPatternListSyntax : SyntaxNode
{
    private readonly IReadOnlyList<TypePatternSyntax> _arguments;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    /// <summary>Initializes a new instance of the GenericPatternListSyntax class.</summary>
    /// <param name="lessThanToken">The opening generic-clause delimiter, which may be missing during error recovery.</param>
    /// <param name="form">The recorded fixed-arity, wildcard-arity, or argument-pattern form.</param>
    /// <param name="arity">The recorded arity for forms that specify a fixed slot count.</param>
    /// <param name="arguments">Type argument patterns in declaration order for the argument-pattern form.</param>
    /// <param name="separators">Comma tokens retained between generic slots or argument patterns.</param>
    /// <param name="wildcardToken">The arity wildcard token when present, or null otherwise.</param>
    /// <param name="greaterThanToken">The closing generic-clause delimiter, which may be missing during error recovery.</param>
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
            Array.AsReadOnly(CollectionSnapshot.Copy(arguments, nameof(arguments))),
            Array.AsReadOnly(CollectionSnapshot.Copy(separators, nameof(separators))),
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
    private readonly IReadOnlyList<SyntaxToken> _negatedRefTokens;
    private readonly IReadOnlyList<SyntaxToken> _refKindTokens;

    /// <summary>Initializes a new instance of the TypeUseSyntax class.</summary>
    /// <param name="refKindTokens">The ordered ref, out, in, or readonly annotation tokens preceding the type pattern.</param>
    /// <param name="typePattern">The underlying type pattern after any ref annotations.</param>
    /// <param name="negatedRefTokens">Specifies the '!ref' negation tokens.</param>
    public TypeUseSyntax(
        IEnumerable<SyntaxToken> refKindTokens,
        TypePatternSyntax typePattern,
        IReadOnlyList<SyntaxToken>? negatedRefTokens = null)
        : this(
            Array.AsReadOnly(refKindTokens.ToArray()),
            typePattern,
            Array.AsReadOnly((negatedRefTokens ?? Array.Empty<SyntaxToken>()).ToArray()))
    {
    }

    private TypeUseSyntax(
        IReadOnlyList<SyntaxToken> refKindTokens,
        TypePatternSyntax typePattern,
        IReadOnlyList<SyntaxToken> negatedRefTokens)
        : base(
            SyntaxKind.TypeUse,
            SyntaxChildren.Create(
                negatedRefTokens.Concat(refKindTokens),
                typePattern))
    {
        _refKindTokens = refKindTokens;
        _negatedRefTokens = negatedRefTokens;
        TypePattern = typePattern;
    }

    /// <summary>Gets the ref kind tokens.</summary>
    public IReadOnlyList<SyntaxToken> RefKindTokens => _refKindTokens;

    /// <summary>Gets the '!ref' negation tokens (Standard only).</summary>
    public IReadOnlyList<SyntaxToken> NegatedRefTokens => _negatedRefTokens;

    /// <summary>Gets the type pattern.</summary>
    public TypePatternSyntax TypePattern { get; }
}
