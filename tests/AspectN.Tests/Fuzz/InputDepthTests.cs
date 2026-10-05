using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Fuzz;

public sealed class InputDepthTests
{
    [Fact]
    public async Task StructuralComparison_SharedTypeGraph_DoesNotExpandEveryPath()
    {
        TypeSignature node = new UnsupportedTypeSignature("unsupported", "fixture");
        var identity = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Pair", 2, TypeKind.Class);
        for (var index = 0; index < 40; index++) node = new NamedTypeSignature(identity, [node, node]);
        var comparison = Task.Run(() => AspectN.TypeSystem.StructuralTypeComparer.AreEqual(node, node));
        var result = await comparison.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void Match_SharedCandidateGraph_TraversesEachReferenceOnce()
    {
        TypeSignature type = DeepArray(0);
        var identity = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 2, TypeKind.Class);
        for (var index = 0; index < 50; index++) type = new NamedTypeSignature(identity, [type, type]);
        var compilation = new AspectNEngine().CompileTypePattern("*");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.Equal(MatchStatus.Matched, pattern!.Match(type, new StubTypeSystem(), cancellation.Token).Status);
    }

    [Fact]
    public void ParseMemberExpression_DeepParentheses_ReturnsDepthDiagnostic()
    {
        var source = new string('(', 300) + "method(* *(..))" + new string(')', 300);
        AssertDepthFailure(AspectNParser.ParseMemberExpression(source));
    }

    [Fact]
    public void ParseMemberExpression_LongBooleanChain_ReturnsDepthDiagnostic()
    {
        var source = string.Join(" || ", Enumerable.Repeat("method(* *(..))", 300));
        AssertDepthFailure(AspectNParser.ParseMemberExpression(source));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseTypePattern_DeepGenericOrArray_ReturnsDepthDiagnostic(bool generic)
    {
        var source = generic
            ? string.Concat(Enumerable.Repeat("List<", 300)) + "int" + new string('>', 300)
            : "int" + string.Concat(Enumerable.Repeat("[]", 300));
        AssertDepthFailure(AspectNParser.ParseTypePattern(source));
    }

    [Fact]
    public void Match_DeepCandidate_ReturnsUnknownWithDepthDiagnostic()
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseTypePattern("*"));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var result = pattern!.Match(DeepArray(1_000), new StubTypeSystem());
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3005");
    }

    [Fact]
    public void Match_DeepAttribute_ReturnsUnknownWithDepthDiagnostic()
    {
        var attribute = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", "Deep", 1, TypeKind.Class), [DeepArray(1_000)]);
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public,
            false, false, DeepArray(0), new TypeUseSignature(DeepArray(0), RefKind.None, TypeUseKind.Return), [], [], [],
            [new AttributeSignature(attribute, AttributeTargetKind.Method)], []);
        var compilation = new AspectNEngine().CompileMemberExpression("attr(call Example.Deep<*>)");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var result = pattern!.Match(candidate, new StubTypeSystem());
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3005");
    }

    [Fact]
    public void Match_SharedNodeReachedOnDeeperPath_StillChecksDepth()
    {
        var shared = DeepArray(200);
        TypeSignature longer = shared;
        for (var index = 0; index < 100; index++) longer = new ArrayTypeSignature(longer, 1);
        var candidate = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", "Box", 2, TypeKind.Class), [longer, shared]);
        var compilation = new AspectNEngine().CompileTypePattern("*");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Equal(MatchStatus.Unknown, pattern!.Match(candidate, new StubTypeSystem()).Status);
    }

    [Fact]
    public void AreEqual_OneHundredThousandArrayLayers_UsesBoundedCallStack()
    {
        Assert.Equal(MatchStatus.Matched,
            StructuralTypeComparer.AreEqual(DeepArray(100_000), DeepArray(100_000)).Status);
    }

    [Fact]
    public void TypeIdentity_DeepContainingTypes_EqualityHashAndNameUseBoundedCallStack()
    {
        var assembly = new AssemblyIdentity("Example");
        TypeIdentity? left = null;
        TypeIdentity? right = null;
        for (var index = 0; index < 20_000; index++)
        {
            left = new TypeIdentity(assembly, "", "Nested", 0, TypeKind.Class, left);
            right = new TypeIdentity(assembly, "", "Nested", 0, TypeKind.Class, right);
        }

        Assert.Equal(left, right);
        Assert.Equal(left!.GetHashCode(), right!.GetHashCode());
        Assert.Equal(20_000, left.FullMetadataName.Split('/').Length);
    }

    private static void AssertDepthFailure(AspectN.Syntax.AspectNSyntaxTree tree)
    {
        Assert.Contains(tree.Diagnostics, diagnostic => diagnostic.Code == "AN1002");
        var compilation = AspectNCompilation.Create(tree);
        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    private static TypeSignature DeepArray(int depth)
    {
        TypeSignature type = new NamedTypeSignature(
            new TypeIdentity(new AssemblyIdentity("Example"), "System", "Int32", 0, TypeKind.Struct));
        for (var index = 0; index < depth; index++) type = new ArrayTypeSignature(type, 1);
        return type;
    }
}
