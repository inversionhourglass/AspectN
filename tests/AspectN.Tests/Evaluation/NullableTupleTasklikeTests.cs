using AspectN.Binding;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class NullableTupleTasklikeTests
{
    [Fact]
    public void Match_NullableReferencePattern_EqualsReferenceType()
    {
        var result = Match("string?", Named("System", "String"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_NullableValuePattern_MatchesNullableOfValueTypeOnly()
    {
        var intType = Named("System", "Int32", TypeKind.Struct);
        var nullable = Constructed(
            "System",
            "Nullable",
            TypeKind.Struct,
            intType);

        Assert.Equal(MatchStatus.Matched, Match("int?", nullable).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("int?", intType).Status);
    }

    [Fact]
    public void Match_StandardTuple_MatchesValueTupleOnly()
    {
        var valueTuple = Constructed(
            "System",
            "ValueTuple",
            TypeKind.Struct,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));
        var referenceTuple = Constructed(
            "System",
            "Tuple",
            TypeKind.Class,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));

        Assert.Equal(
            MatchStatus.Matched,
            Match("(int, string)", valueTuple).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match("(int, string)", referenceTuple).Status);
    }

    [Theory]
    [InlineData("ValueTuple", TypeKind.Struct)]
    [InlineData("Tuple", TypeKind.Class)]
    public void Match_LegacyTuple_AcceptsTupleAndValueTuple(
        string metadataName,
        TypeKind kind)
    {
        var candidate = Constructed(
            "System",
            metadataName,
            kind,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));

        var result = Match(
            "(int, string)",
            candidate,
            AspectNLanguageVersion.Legacy);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_LongValueTuple_FlattensRuntimeRestShape()
    {
        var rest = Constructed(
            "System",
            "ValueTuple",
            TypeKind.Struct,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));
        var arguments = Enumerable.Repeat<TypeSignature>(
                Named("System", "Int32", TypeKind.Struct),
                7)
            .Append(rest)
            .ToArray();
        var candidate = Constructed(
            "System",
            "ValueTuple",
            TypeKind.Struct,
            arguments);

        var result = Match(
            "(int, int, int, int, int, int, int, int, string)",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Theory]
    [InlineData("Task", TypeKind.Class)]
    [InlineData("ValueTask", TypeKind.Struct)]
    public void Match_TaskLikeGeneric_MatchesTaskAndValueTask(
        string metadataName,
        TypeKind kind)
    {
        var candidate = Constructed(
            "System.Threading.Tasks",
            metadataName,
            kind,
            Named("System", "String"));

        var result = Match("tasklike<string>", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Theory]
    [InlineData("Task", TypeKind.Class)]
    [InlineData("ValueTask", TypeKind.Struct)]
    public void Match_BareTaskLike_MatchesNonGenericTaskTypes(
        string metadataName,
        TypeKind kind)
    {
        var candidate = Named(
            "System.Threading.Tasks",
            metadataName,
            kind);

        var result = Match("tasklike", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Theory]
    [InlineData("Task", TypeKind.Class)]
    [InlineData("ValueTask", TypeKind.Struct)]
    public void Match_BareTaskLike_DoesNotMatchGenericTaskTypes(
        string metadataName,
        TypeKind kind)
    {
        var candidate = Constructed(
            "System.Threading.Tasks",
            metadataName,
            kind,
            Named("System", "String"));

        var result = Match("tasklike", candidate);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void Match_TaskLike_IsIndependentFromCallableAsyncFlag()
    {
        var returnType = Constructed(
            "System.Threading.Tasks",
            "Task",
            TypeKind.Class,
            Named("System", "String"));
        var synchronous = Callable(returnType, isAsync: false);
        var asynchronous = Callable(returnType, isAsync: true);
        var pattern = Compile("tasklike<string>");
        var typeSystem = new StubTypeSystem();

        Assert.False(synchronous.IsAsync);
        Assert.True(asynchronous.IsAsync);
        Assert.Equal(
            MatchStatus.Matched,
            pattern.Match(synchronous.ReturnType.Type, typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            pattern.Match(asynchronous.ReturnType.Type, typeSystem).Status);
    }

    [Fact]
    public void Match_LegacyAsyncType_MatchesGenericTaskShape()
    {
        var signature = BindLegacyCallable(
            "method(async string Service.Get(..))");
        var candidate = Constructed(
            "System.Threading.Tasks",
            "Task",
            TypeKind.Class,
            Named("System", "String"));
        var context = new EvaluationContext(
            new StubTypeSystem(),
            AspectNLanguageVersion.Legacy,
            CancellationToken.None);

        var result = TypePatternEvaluator.EvaluateLegacyAsyncReturn(
            signature.ReturnType.TypePattern,
            candidate,
            context);

        Assert.Equal(MatchStatus.Matched, result);
    }

    [Fact]
    public void Match_LegacyAsyncNull_MatchesNonGenericTaskShape()
    {
        var signature = BindLegacyCallable(
            "method(async null Service.Get(..))");
        var candidate = Named(
            "System.Threading.Tasks",
            "ValueTask",
            TypeKind.Struct);
        var context = new EvaluationContext(
            new StubTypeSystem(),
            AspectNLanguageVersion.Legacy,
            CancellationToken.None);

        var result = TypePatternEvaluator.EvaluateLegacyAsyncReturn(
            signature.ReturnType.TypePattern,
            candidate,
            context);

        Assert.Equal(MatchStatus.Matched, result);
    }

    private static BoundCallableSignature BindLegacyCallable(string source)
    {
        var tree = AspectNParser.ParseMemberExpression(
            source,
            new AspectNParseOptions(AspectNLanguageVersion.Legacy));
        var compilation = AspectNCompilation.Create(tree);
        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        return Assert.IsType<BoundCallableSignature>(selector.Payload);
    }

    private static CallableSignature Callable(
        TypeSignature returnType,
        bool isAsync)
    {
        return new CallableSignature(
            "Get",
            CallableKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync,
            Named("Example", "Service"),
            new TypeUseSignature(
                returnType,
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static MatchResult Match(
        string source,
        TypeSignature candidate,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        return Compile(source, languageVersion).Match(
            candidate,
            new StubTypeSystem());
    }

    private static CompiledTypePattern Compile(
        string source,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        var tree = AspectNParser.ParseTypePattern(
            source,
            new AspectNParseOptions(languageVersion));
        var compilation = AspectNCompilation.Create(tree);
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!;
    }

    private static NamedTypeSignature Constructed(
        string @namespace,
        string name,
        TypeKind kind,
        params TypeSignature[] arguments)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            arguments.Length,
            kind);
        var definition = new NamedTypeSignature(identity);
        return new NamedTypeSignature(identity, arguments, definition);
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
