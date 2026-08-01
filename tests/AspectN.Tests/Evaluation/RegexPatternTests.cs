using System.Globalization;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class RegexPatternTests
{
    [Fact]
    public void Format_ComplexConstructedCallable_IsCanonical()
    {
        var candidate = ComplexCandidate();
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var signature = CanonicalSignatureFormatter.Format(
                candidate,
                AspectNLanguageVersion.Standard);

            Assert.Equal(
                "public static async ref readonly System.String " +
                "Example.Outer<System.String>/Inner<System.Int32>." +
                "Map<System.String>(ref System.Int32, out System.String[])",
                signature);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Match_Regex_AppliesToCanonicalSignature()
    {
        var source =
            @"regex(^public static async ref readonly System\.String Example\.Outer<System\.String>/Inner<System\.Int32>\.Map<System\.String>\(ref System\.Int32, out System\.String\[\]\)$)";

        var result = Match(source, ComplexCandidate());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Format_Accessibility_IsVersioned()
    {
        var candidate = Callable(
            "Run",
            accessibility: Accessibility.PrivateProtected);

        var standard = CanonicalSignatureFormatter.Format(
            candidate,
            AspectNLanguageVersion.Standard);
        var legacy = CanonicalSignatureFormatter.Format(
            candidate,
            AspectNLanguageVersion.Legacy);

        Assert.StartsWith("private protected ", standard);
        Assert.StartsWith("privateprotected ", legacy);
    }
    [Fact]
    public void Match_CatastrophicRegexTimesOut_ReturnsUnknownDiagnostic()
    {
        var candidate = Callable(new string('a', 20_000));

        var result = Match(
            "regex(^(.+)+Z$)",
            candidate,
            new AspectNCompilationOptions(TimeSpan.FromMilliseconds(1)));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.RegexTimeout);
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate,
        AspectNCompilationOptions? options = null)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source),
            options);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static CallableSignature ComplexCandidate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var outerIdentity = new TypeIdentity(
            assembly,
            "Example",
            "Outer",
            1,
            TypeKind.Class);
        var innerIdentity = new TypeIdentity(
            assembly,
            string.Empty,
            "Inner",
            1,
            TypeKind.Class,
            outerIdentity);
        var definition = new NamedTypeSignature(innerIdentity);
        var declaringType = new NamedTypeSignature(
            innerIdentity,
            new TypeSignature[]
            {
                Named("System", "String"),
                Named("System", "Int32", TypeKind.Struct)
            },
            definition);
        return new CallableSignature(
            "Map",
            CallableKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: true,
            isAsync: true,
            declaringType,
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.RefReadOnly,
                TypeUseKind.Return),
            new[]
            {
                new ParameterSignature(
                    "value",
                    0,
                    new TypeUseSignature(
                        Named("System", "Int32", TypeKind.Struct),
                        RefKind.Ref,
                        TypeUseKind.Parameter)),
                new ParameterSignature(
                    "values",
                    1,
                    new TypeUseSignature(
                        new ArrayTypeSignature(
                            Named("System", "String"),
                            1),
                        RefKind.Out,
                        TypeUseKind.Parameter))
            },
            Array.Empty<GenericParameterSignature>(),
            new[] { Named("System", "String") },
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static CallableSignature Callable(
        string name,
        Accessibility accessibility = Accessibility.Public)
    {
        return new CallableSignature(
            name,
            CallableKind.OrdinaryMethod,
            accessibility,
            isStatic: false,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                kind));
    }
}
