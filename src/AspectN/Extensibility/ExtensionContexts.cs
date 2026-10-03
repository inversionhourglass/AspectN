using System.Collections.ObjectModel;
using AspectN.Metadata;
using AspectN.Syntax;
using AspectN.Text;
using AspectN.TypeSystem;

namespace AspectN.Extensibility;

/// <summary>Specifies the extension argument kind values.</summary>
public enum ExtensionArgumentKind
{
    Text,
    Number,
    Boolean,
    TypePattern
}

/// <summary>Represents extension argument.</summary>
public sealed class ExtensionArgument
{
    private ExtensionArgument(
        ExtensionArgumentKind kind,
        string? stringValue,
        long integerValue,
        bool booleanValue,
        TypePatternSyntax? typePattern)
    {
        Kind = kind;
        StringValue = stringValue;
        IntegerValue = integerValue;
        BooleanValue = booleanValue;
        TypePattern = typePattern;
    }

    /// <summary>Gets the kind.</summary>
    public ExtensionArgumentKind Kind { get; }

    /// <summary>Gets the string value.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the integer value.</summary>
    public long IntegerValue { get; }

    /// <summary>Gets the boolean value.</summary>
    public bool BooleanValue { get; }

    /// <summary>Gets the type pattern.</summary>
    public TypePatternSyntax? TypePattern { get; }

    internal static ExtensionArgument FromSyntax(SyntaxNode syntax)
    {
        return syntax switch
        {
            SyntaxToken { Kind: SyntaxKind.StringToken } token =>
                new ExtensionArgument(
                    ExtensionArgumentKind.Text,
                    token.Value as string,
                    0,
                    false,
                    null),
            SyntaxToken { Kind: SyntaxKind.IntegerToken } token =>
                new ExtensionArgument(
                    ExtensionArgumentKind.Number,
                    null,
                    token.Value is long value ? value : 0,
                    false,
                    null),
            SyntaxToken token when token.Text == "true" =>
                new ExtensionArgument(
                    ExtensionArgumentKind.Boolean,
                    null,
                    0,
                    true,
                    null),
            SyntaxToken => new ExtensionArgument(
                ExtensionArgumentKind.Boolean,
                null,
                0,
                false,
                null),
            TypePatternSyntax typePattern => new ExtensionArgument(
                ExtensionArgumentKind.TypePattern,
                null,
                0,
                false,
                typePattern),
            _ => throw new ArgumentException(
                "Unsupported extension argument syntax.",
                nameof(syntax))
        };
    }
}

/// <summary>Represents extension compile context.</summary>
public sealed class ExtensionCompileContext
{
    private readonly ReadOnlyCollection<ExtensionArgument> _arguments;

    internal ExtensionCompileContext(
        string qualifiedName,
        IEnumerable<ExtensionArgument> arguments,
        TextSpan sourceSpan)
    {
        QualifiedName = qualifiedName;
        _arguments = new ReadOnlyCollection<ExtensionArgument>(
            arguments.ToArray());
        SourceSpan = sourceSpan;
    }

    /// <summary>Gets the qualified name.</summary>
    public string QualifiedName { get; }

    /// <summary>Gets the arguments.</summary>
    public IReadOnlyList<ExtensionArgument> Arguments => _arguments;

    /// <summary>Gets the source span.</summary>
    public TextSpan SourceSpan { get; }
}

/// <summary>Represents extension match context.</summary>
public sealed class ExtensionMatchContext
{
    internal ExtensionMatchContext(
        MethodSignature candidate,
        ITypeSystem typeSystem,
        CancellationToken cancellationToken)
    {
        Candidate = candidate;
        TypeSystem = typeSystem;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the candidate.</summary>
    public MethodSignature Candidate { get; }

    /// <summary>Gets the type system.</summary>
    public ITypeSystem TypeSystem { get; }

    /// <summary>Gets the cancellation token.</summary>
    public CancellationToken CancellationToken { get; }
}
