using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Extensibility;
using AspectN.Metadata;
using AspectN.Syntax;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Extensibility;

public sealed class ExtensionRegistryTests
{
    [Fact]
    public void Add_DuplicateQualifiedName_Throws()
    {
        var builder = AspectNExtensionRegistry.CreateBuilder("v1")
            .Add(new RecordingExtension("vendor.feature"));

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Add(new RecordingExtension("vendor.feature")));

        Assert.Contains("vendor.feature", exception.Message);
    }

    [Fact]
    public void Build_CopiesRegistrationsAndIsImmutable()
    {
        var builder = AspectNExtensionRegistry.CreateBuilder("v1")
            .Add(new RecordingExtension("vendor.first"));
        var registry = builder.Build();

        builder.Add(new RecordingExtension("vendor.second"));

        Assert.Equal(1, registry.Count);
        Assert.True(registry.Contains("vendor.first"));
        Assert.False(registry.Contains("vendor.second"));
    }

    [Fact]
    public void Build_DifferentVersions_UseDifferentCacheIdentities()
    {
        var first = AspectNExtensionRegistry.CreateBuilder("v1").Build();
        var second = AspectNExtensionRegistry.CreateBuilder("v2").Build();

        Assert.NotEqual(first.CacheIdentity, second.CacheIdentity);
        Assert.NotEqual(first.Version, second.Version);
    }

    [Fact]
    public void Create_RegisteredExtension_CompilesFixedArguments()
    {
        var extension = new RecordingExtension("vendor.feature");
        var registry = AspectNExtensionRegistry.CreateBuilder("v1")
            .Add(extension)
            .Build();
        var tree = AspectNParser.ParseMemberExpression(
            "vendor.feature(\"x\", -2, true, System.IDisposable+)");

        var compilation = AspectNCompilation.Create(
            tree,
            extensions: registry);

        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var context = Assert.IsType<ExtensionCompileContext>(
            extension.LastCompileContext);
        Assert.Collection(
            context.Arguments,
            argument =>
            {
                Assert.Equal(ExtensionArgumentKind.Text, argument.Kind);
                Assert.Equal("x", argument.StringValue);
            },
            argument =>
            {
                Assert.Equal(ExtensionArgumentKind.Number, argument.Kind);
                Assert.Equal(-2, argument.IntegerValue);
            },
            argument =>
            {
                Assert.Equal(ExtensionArgumentKind.Boolean, argument.Kind);
                Assert.True(argument.BooleanValue);
            },
            argument =>
            {
                Assert.Equal(ExtensionArgumentKind.TypePattern, argument.Kind);
                Assert.IsType<AssignableTypePatternSyntax>(
                    argument.TypePattern);
            });
        Assert.Equal(
            MatchStatus.Matched,
            pattern!.Match(Candidate(), new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_MissingRequiredCapability_ReturnsUnknownWithoutCallingExtension()
    {
        var extension = new RecordingExtension(
            "vendor.feature",
            TypeSystemCapabilities.DirectAttributeLookup);
        var registry = AspectNExtensionRegistry.CreateBuilder("v1")
            .Add(extension)
            .Build();
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression("vendor.feature()"),
            extensions: registry);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var typeSystem = new StubTypeSystem
        {
            Capabilities = TypeSystemCapabilities.TypeEquality
        };

        var result = pattern!.Match(Candidate(), typeSystem);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Code == DiagnosticCodes.MissingBackendCapability);
        Assert.False(extension.Matcher.WasCalled);
    }

    private static CallableSignature Candidate()
    {
        return new CallableSignature(
            "Run",
            CallableKind.OrdinaryMethod,
            Accessibility.Public,
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

    private sealed class RecordingExtension : IAspectNExtension
    {
        public RecordingExtension(
            string qualifiedName,
            TypeSystemCapabilities requiredCapabilities =
                TypeSystemCapabilities.None)
        {
            QualifiedName = qualifiedName;
            RequiredCapabilities = requiredCapabilities;
            Matcher = new RecordingMatcher();
        }

        public string QualifiedName { get; }

        public ExtensionTargetDomain TargetDomain => ExtensionTargetDomain.All;

        public TypeSystemCapabilities RequiredCapabilities { get; }

        public ExtensionCompileContext? LastCompileContext { get; private set; }

        public RecordingMatcher Matcher { get; }

        public IAspectNExtensionMatcher Compile(
            ExtensionCompileContext context)
        {
            LastCompileContext = context;
            return Matcher;
        }
    }

    private sealed class RecordingMatcher : IAspectNExtensionMatcher
    {
        public bool WasCalled { get; private set; }

        public MatchResult Match(ExtensionMatchContext context)
        {
            WasCalled = true;
            context.CancellationToken.ThrowIfCancellationRequested();
            return MatchResult.Matched;
        }
    }
}
