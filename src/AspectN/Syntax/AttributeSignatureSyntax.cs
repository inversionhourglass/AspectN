namespace AspectN.Syntax;

public sealed class AttributeSignatureSyntax : SignatureSyntax
{
    public AttributeSignatureSyntax(IEnumerable<SyntaxNode> parts)
        : base(SyntaxKind.AttributeSignature, parts)
    {
    }
}
