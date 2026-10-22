using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class ResourceBudgetTests
{
    [Fact]
    public async Task Match_ConcurrentCalls_HaveIndependentTotalWork()
    {
        var compilation = new AspectNEngine().CompileTypePattern("*", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(20))));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var candidate = Named();
        var backend = new StubTypeSystem();
        var results = await Task.WhenAll(Enumerable.Range(0, 128).Select(_ => Task.Run(() => pattern.Match(candidate, backend))));
        Assert.All(results, result => Assert.Equal(MatchStatus.Matched, result.Status));
    }

    [Fact]
    public void AreEqual_LongUnsupportedDisplayName_ChecksBudgetBeforeDiagnosticAllocation()
    {
        var candidate = new UnsupportedTypeSignature(new string('N', 1_000_000), "fixture");
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = StructuralTypeComparer.AreEqual(candidate, candidate,
            new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(3)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before unsupported diagnostic checks.");
    }

    [Fact]
    public void Match_DeclarationSlot_ChecksBudgetBeforeGeneratingOwner()
    {
        var identity = new TypeIdentity(new AssemblyIdentity("Example"), "Example", new string('N', 1_000_000), 1, TypeKind.Class);
        var parameter = new GenericParameterTypeSignature(new GenericParameterSignature("T", GenericParameterOwnerKind.Type,
            identity.FullMetadataName, identity.Assembly, 0));
        var candidate = new NamedTypeSignature(identity, [parameter]);
        var compilation = new AspectNEngine().CompileTypePattern("*<$T>", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(1_000_100))));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = pattern.Match(candidate, new StubTypeSystem());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before declaration owner checks.");
    }

    [Fact]
    public void Match_WideMethodSlots_ChecksBudgetBeforeSynthesizingArguments()
    {
        const int count = 20_000;
        var parameters = Enumerable.Range(0, count).Select(index => new GenericParameterSignature("T", GenericParameterOwnerKind.Method,
            "M", new AssemblyIdentity("Example"), index)).ToArray();
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Named(), new TypeUseSignature(Named(), RefKind.None, TypeUseKind.Return), [], parameters, [], [], []);
        var source = "method(* *<$T" + new string(',', count - 1) + ">(..))";
        var parse = new AspectNParseOptions(maximumSourceLength: ResourceLimit.Unlimited, maximumTokens: ResourceLimit.Unlimited,
            maximumSyntaxNodes: ResourceLimit.Unlimited, maximumMethodGenericParameters: ResourceLimit.Unlimited);
        var compilation = new AspectNEngine().CompileMemberExpression(source, parse,
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(100),
                maximumMethodGenericParameters: ResourceLimit.Unlimited)));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = pattern.Match(candidate, new StubTypeSystem());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before method argument checks.");
    }

    [Fact]
    public void Match_LongMemberWildcard_ConsumesCharacterWork()
    {
        var candidate = new MethodSignature(new string('M', 1_000_000), MethodKind.OrdinaryMethod, Accessibility.Public,
            false, false, Named(), new TypeUseSignature(Named(), RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var compilation = new AspectNEngine().CompileMemberExpression("method(* *Z(..))", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(100))));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var result = pattern.Match(candidate, new StubTypeSystem());
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
    }

    [Fact]
    public void Match_LongNamespace_ChecksWorkBeforeSplittingText()
    {
        var candidate = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            string.Concat(Enumerable.Repeat("A.", 100_000)), "C", 0, TypeKind.Class));
        var compilation = new AspectNEngine().CompileTypePattern("X..C", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(100))));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = pattern.Match(candidate, new StubTypeSystem());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before checking namespace splitting work.");
    }

    [Fact]
    public void Match_LongOpenGenericName_ChecksBudgetBeforeOwnerTextAllocation()
    {
        var declaring = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", new string('N', 1_000_000), 1, TypeKind.Class));
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public,
            false, false, declaring, new TypeUseSignature(Named(), RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var compilation = new AspectNEngine().CompileMemberExpression("regex(.*)", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumCanonicalSignatureLength: ResourceLimit.Finite(8))));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = pattern.Match(candidate, new StubTypeSystem());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before checking the owner text budget.");
    }

    [Fact]
    public void AreEqual_WideInputAndTinyWork_DoesNotAllocateAFullTraversalStack()
    {
        var leaf = Named();
        var type = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", "Wide", 100_000, TypeKind.Class), Enumerable.Repeat(leaf, 100_000));
        var options = new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(2),
            maximumTypeGenericParameters: ResourceLimit.Unlimited);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = StructuralTypeComparer.AreEqual(type, type, options);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
        Assert.True(allocated < 128 * 1024, $"Allocated {allocated} bytes before a two-unit budget stopped traversal.");
    }

    [Fact]
    public void Match_CanonicalTextLimit_AbortsSuccessfulOr()
    {
        var candidate = new MethodSignature(new string('M', 20), MethodKind.OrdinaryMethod, Accessibility.Public,
            false, false, Named(), new TypeUseSignature(Named(), RefKind.None, TypeUseKind.Return), [], [], [], [], []);
        var compilation = new AspectNEngine().CompileMemberExpression("regex(.*) || method(* *(..))", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumCanonicalSignatureLength: ResourceLimit.Finite(8))));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var result = pattern.Match(candidate, new StubTypeSystem());
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
    }

    [Fact]
    public void Match_DeclarationTotalWork_ExhaustionIsUnknown()
    {
        var compilation = new AspectNEngine().CompileTypePattern("*", compilationOptions:
            new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(1))));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var result = pattern.Match(Named(), new StubTypeSystem());
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3008");
    }

    [Theory]
    [InlineData("AN3007")]
    [InlineData("AN3008")]
    public void Match_ThirdPartyResourceFailure_CannotBeCoveredBySuccessfulOr(string code)
    {
        var backend = new StubTypeSystem
        {
            Closure = (_, _) => new InheritanceClosure([], TypeRelationResult.Unknown(
                new AspectNDiagnostic(code, DiagnosticSeverity.Warning, "fixture resource failure")))
        };
        var compilation = new AspectNEngine().CompileTypePattern("Missing*+ || *");
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var result = pattern.Match(Named(), backend);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void AreEqual_IndependentStructureOptions_ApplyAndCanBeUnlimited()
    {
        TypeSignature type = new ArrayTypeSignature(Named(), 1);
        var result = StructuralTypeComparer.AreEqual(type, type,
            new AspectNMatchOptions(maximumTypeDepth: ResourceLimit.Finite(1)));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3005");
        Assert.Equal(MatchStatus.Matched, StructuralTypeComparer.AreEqual(type, type,
            new AspectNMatchOptions(maximumTypeDepth: ResourceLimit.Unlimited)).Status);
    }

    private static NamedTypeSignature Named() => new(new TypeIdentity(new AssemblyIdentity("Example"),
        "Example", "C", 0, TypeKind.Class));
}
