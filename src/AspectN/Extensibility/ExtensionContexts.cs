using System.Collections.ObjectModel;
using AspectN.Metadata;
using AspectN.Syntax;
using AspectN.Text;
using AspectN.TypeSystem;

namespace AspectN.Extensibility;

public enum ExtensionArgumentKind
{
    Text,
    Number,
    Boolean,
    TypePattern
}

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

    public ExtensionArgumentKind Kind { get; }

    public string? StringValue { get; }

    public long IntegerValue { get; }

    public bool BooleanValue { get; }

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

public sealed class ExtensionCompileContext
{
    private readonly ReadOnlyCollection<ExtensionArgument> _arguments;

    internal ExtensionCompileContext(
        string qualifiedName,
        AspectNLanguageVersion languageVersion,
        IEnumerable<ExtensionArgument> arguments,
        TextSpan sourceSpan)
    {
        QualifiedName = qualifiedName;
        LanguageVersion = languageVersion;
        _arguments = new ReadOnlyCollection<ExtensionArgument>(
            arguments.ToArray());
        SourceSpan = sourceSpan;
    }

    public string QualifiedName { get; }

    public AspectNLanguageVersion LanguageVersion { get; }

    public IReadOnlyList<ExtensionArgument> Arguments => _arguments;

    public TextSpan SourceSpan { get; }
}

public sealed class ExtensionMatchContext
{
    internal ExtensionMatchContext(
        CallableSignature candidate,
        ITypeSystem typeSystem,
        AspectNLanguageVersion languageVersion,
        CancellationToken cancellationToken)
    {
        Candidate = candidate;
        TypeSystem = typeSystem;
        LanguageVersion = languageVersion;
        CancellationToken = cancellationToken;
    }

    public CallableSignature Candidate { get; }

    public ITypeSystem TypeSystem { get; }

    public AspectNLanguageVersion LanguageVersion { get; }

    public CancellationToken CancellationToken { get; }
}
