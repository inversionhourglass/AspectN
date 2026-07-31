namespace AspectN.Syntax;

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

    public virtual SyntaxToken? KeywordToken { get; }

    public virtual SyntaxToken OpenParenToken { get; } = null!;

    public virtual SyntaxNode? Body { get; }

    public virtual SyntaxToken CloseParenToken { get; } = null!;
}

public sealed class CallableSelectorSyntax : SelectorSyntax
{
    public CallableSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        CallableSignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.CallableSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class MethodSelectorSyntax : SelectorSyntax
{
    public MethodSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        CallableSignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.MethodSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class AccessorSelectorSyntax : SelectorSyntax
{
    public AccessorSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        PropertySignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.AccessorSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class GetterSelectorSyntax : SelectorSyntax
{
    public GetterSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        PropertySignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.GetterSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class SetterSelectorSyntax : SelectorSyntax
{
    public SetterSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        PropertySignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.SetterSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class ConstructorSelectorSyntax : SelectorSyntax
{
    public ConstructorSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        ConstructorSignatureSyntax signature,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.ConstructorSelector,
            keywordToken,
            openParenToken,
            signature,
            closeParenToken)
    {
    }
}

public sealed class StaticConstructorSelectorSyntax : SelectorSyntax
{
    public StaticConstructorSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        TypePatternSyntax typePattern,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.StaticConstructorSelector,
            keywordToken,
            openParenToken,
            typePattern,
            closeParenToken)
    {
    }
}

public sealed class AttributeSelectorSyntax : SelectorSyntax
{
    public AttributeSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        SyntaxNode body,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.AttributeSelector,
            keywordToken,
            openParenToken,
            body,
            closeParenToken)
    {
    }
}

public sealed class RegexSelectorSyntax : SelectorSyntax
{
    public RegexSelectorSyntax(
        SyntaxToken keywordToken,
        SyntaxToken openParenToken,
        SyntaxToken regexBodyToken,
        SyntaxToken closeParenToken)
        : base(
            SyntaxKind.RegexSelector,
            keywordToken,
            openParenToken,
            regexBodyToken,
            closeParenToken)
    {
        RegexBodyToken = regexBodyToken;
    }

    public SyntaxToken RegexBodyToken { get; }
}

public sealed class ExtensionSelectorSyntax : SelectorSyntax
{
    private readonly IReadOnlyList<SyntaxNode> _arguments;
    private readonly IReadOnlyList<SyntaxToken> _nameTokens;
    private readonly IReadOnlyList<SyntaxToken> _separators;

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

    public IReadOnlyList<SyntaxToken> NameTokens => _nameTokens;

    public IReadOnlyList<SyntaxNode> Arguments => _arguments;

    public IReadOnlyList<SyntaxToken> Separators => _separators;

    public override SyntaxToken? KeywordToken => null;

    public override SyntaxToken OpenParenToken { get; }

    public override SyntaxNode? Body => null;

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
