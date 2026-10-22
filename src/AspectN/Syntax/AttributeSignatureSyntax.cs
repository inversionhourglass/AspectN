namespace AspectN.Syntax;

/// <summary>Describes an attribute location and its attribute type pattern.</summary>
/// <param name="parts">The ordered syntax nodes describing the attribute location and attribute type pattern.</param>
public sealed class AttributeSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.AttributeSignature, parts)
{
}
