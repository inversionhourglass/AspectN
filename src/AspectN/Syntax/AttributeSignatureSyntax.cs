namespace AspectN.Syntax;

/// <summary>Represents a attribute signature syntax node.</summary>
/// <param name="parts">Specifies the parts.</param>
public sealed class AttributeSignatureSyntax(IEnumerable<SyntaxNode> parts) : SignatureSyntax(SyntaxKind.AttributeSignature, parts)
{
}
