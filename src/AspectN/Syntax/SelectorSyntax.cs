namespace AspectN.Syntax;

/// <summary>Represents a selector syntax node.</summary>
public abstract class SelectorSyntax : ExpressionSyntax
{
    protected SelectorSyntax(
        SyntaxKind kind,
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        SyntaxNode? body,
        SyntaxToken closeParenToken)
        : base(
            kind,
            SyntaxChildren.Create(
                keywordToken,
                openParenToken,
                body,
                closeParenToken))
    {
        KeywordToken = keywordToken;
        OpenParenToken = openParenToken;
        Body = body;
        CloseParenToken = closeParenToken;
    }

    protected SelectorSyntax(SyntaxKind kind, IEnumerable<SyntaxNode> children)
        : base(kind, children)
    {
    }

    /// <summary>Gets the keyword token.</summary>
    public virtual SyntaxToken? KeywordToken { get; }

    /// <summary>Gets the open paren token.</summary>
    public virtual SyntaxToken OpenParenToken { get; } = null!;

    /// <summary>Gets the body.</summary>
    public virtual SyntaxNode? Body { get; }

    /// <summary>Gets the close paren token.</summary>
    public virtual SyntaxToken CloseParenToken { get; } = null!;
}

/// <summary>Represents a callable selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class CallableSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    CallableSignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.CallableSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a method selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class MethodSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    CallableSignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.MethodSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a accessor selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class AccessorSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    PropertySignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.AccessorSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a getter selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class GetterSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    PropertySignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.GetterSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a setter selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class SetterSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    PropertySignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.SetterSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a constructor selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="signature">Specifies the signature.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class ConstructorSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    ConstructorSignatureSyntax signature,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.ConstructorSelector,
        keywordToken,
        openParenToken,
        signature,
        closeParenToken)
{
}

/// <summary>Represents a static constructor selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="typePattern">Specifies the type pattern.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class StaticConstructorSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    TypePatternSyntax typePattern,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.StaticConstructorSelector,
        keywordToken,
        openParenToken,
        typePattern,
        closeParenToken)
{
}

/// <summary>Represents a attribute selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="body">Specifies the body.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class AttributeSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    SyntaxNode body,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.AttributeSelector,
        keywordToken,
        openParenToken,
        body,
        closeParenToken)
{
}

/// <summary>Represents a regex selector syntax node.</summary>
/// <param name="keywordToken">Specifies the keyword token.</param>
/// <param name="openParenToken">Specifies the open paren token.</param>
/// <param name="regexBodyToken">Specifies the regex body token.</param>
/// <param name="closeParenToken">Specifies the close paren token.</param>
public sealed class RegexSelectorSyntax(
    SyntaxToken keywordToken,
    SyntaxToken openParenToken,
    SyntaxToken regexBodyToken,
    SyntaxToken closeParenToken) : SelectorSyntax(
        SyntaxKind.RegexSelector,
        keywordToken,
        openParenToken,
        regexBodyToken,
        closeParenToken)
{
    /// <summary>Gets the regex body token.</summary>
    public SyntaxToken RegexBodyToken { get; } = regexBodyToken;
}

/// <summary>Represents a extension selector syntax node.</summary>
public sealed class ExtensionSelectorSyntax : SelectorSyntax
{
    private readonly IReadOnlyList<SyntaxNode> _arguments;
    private readonly IReadOnlyList<SyntaxToken> _nameTokens;
    private readonly IReadOnlyList<SyntaxToken> _separators;

    /// <summary>Initializes a new instance of the ExtensionSelectorSyntax class.</summary>
    /// <param name="nameToken">Specifies the name token.</param>
    /// <param name="openParenToken">Specifies the open paren token.</param>
    /// <param name="closeParenToken">Specifies the close paren token.</param>
    public ExtensionSelectorSyntax(
        SyntaxToken nameToken,
        SyntaxToken openParenToken,
        SyntaxToken closeParenToken)
        : this(
            new[] { nameToken },
            openParenToken,
            Array.Empty<SyntaxNode>(),
            Array.Empty<SyntaxToken>(),
            closeParenToken)
    {
    }

    /// <summary>Initializes a new instance of the ExtensionSelectorSyntax class.</summary>
    /// <param name="nameTokens">Specifies the name tokens.</param>
    /// <param name="openParenToken">Specifies the open paren token.</param>
    /// <param name="arguments">Specifies the arguments.</param>
    /// <param name="separators">Specifies the separators.</param>
    /// <param name="closeParenToken">Specifies the close paren token.</param>
    public ExtensionSelectorSyntax(
        IEnumerable<SyntaxToken> nameTokens,
        SyntaxToken openParenToken,
        IEnumerable<SyntaxNode> arguments,
        IEnumerable<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : this(
            nameTokens.ToArray(),
            openParenToken,
            arguments.ToArray(),
            separators.ToArray(),
            closeParenToken)
    {
    }

    private ExtensionSelectorSyntax(
        IReadOnlyList<SyntaxToken> nameTokens,
        SyntaxToken openParenToken,
        IReadOnlyList<SyntaxNode> arguments,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.ExtensionSelector,
            BuildChildren(
                nameTokens,
                openParenToken,
                arguments,
                separators,
                closeParenToken))
    {
        _nameTokens = nameTokens;
        _arguments = arguments;
        _separators = separators;
        OpenParenToken = openParenToken;
        CloseParenToken = closeParenToken;
    }

    /// <summary>Gets the name tokens.</summary>
    public IReadOnlyList<SyntaxToken> NameTokens => _nameTokens;

    /// <summary>Gets the arguments.</summary>
    public IReadOnlyList<SyntaxNode> Arguments => _arguments;

    /// <summary>Gets the separators.</summary>
    public IReadOnlyList<SyntaxToken> Separators => _separators;

    /// <summary>Gets the keyword token.</summary>
    public override SyntaxToken? KeywordToken => null;

    /// <summary>Gets the open paren token.</summary>
    public override SyntaxToken OpenParenToken { get; }

    /// <summary>Gets the body.</summary>
    public override SyntaxNode? Body => null;

    /// <summary>Gets the close paren token.</summary>
    public override SyntaxToken CloseParenToken { get; }

    private static IReadOnlyList<SyntaxNode> BuildChildren(
        IReadOnlyList<SyntaxToken> nameTokens,
        SyntaxToken openParenToken,
        IReadOnlyList<SyntaxNode> arguments,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken closeParenToken)
    {
        return SyntaxChildren.Create(
            nameTokens,
            openParenToken,
            SyntaxChildren.Interleave(arguments, separators),
            closeParenToken);
    }
}
