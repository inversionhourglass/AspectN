namespace AspectN.Syntax;

/// <summary>Describes an attribute location and its attribute type pattern.</summary>
/// <param name="parts">Specifies the parts.</param>
public sealed class AttributeSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.AttributeSignature, parts)
{
}
