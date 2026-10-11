using System.Collections.Immutable;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AspectN.Roslyn.Tests;

public sealed class RoslynQueryRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAssignableTo_UnreadableSibling_PreservesKnownMatchingBranch(bool failureFirst)
    {
        var (system, candidate, known, unrelated) = CreateBranches(failureFirst);

        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, known).Status);
        var incomplete = system.IsAssignableTo(candidate, unrelated);
        Assert.Equal(MatchStatus.Unknown, incomplete.Status);
        Assert.Contains(incomplete.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetInheritanceClosure_UnreadableSibling_PreservesKnownAncestors(bool failureFirst)
    {
        var (system, candidate, _, _) = CreateBranches(failureFirst);

        var closure = system.GetInheritanceClosure(candidate);

        Assert.Contains(closure.Ancestors, type => type is NamedTypeSignature { Identity.FullMetadataName: "IGood" });
        Assert.NotNull(closure.Failure);
        Assert.Contains(closure.Failure.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetInheritanceClosure_CancelledFastPath_ThrowsCancellation(bool nativeCandidate)
    {
        var compilation = RoslynTestCompilation.Create("public class C { }");
        var system = new RoslynTypeSystem(compilation);
        TypeSignature candidate = nativeCandidate
            ? system.Mapper.MapType(compilation.GetSpecialType(SpecialType.System_Object))
            : new UnsupportedTypeSignature("detached", "No native symbol.");

        Assert.Throws<OperationCanceledException>(() => system.GetInheritanceClosure(candidate, new CancellationToken(true)));
    }

    private static (RoslynTypeSystem System, TypeSignature Candidate, TypeSignature Known, TypeSignature Unrelated)
        CreateBranches(bool failureFirst)
    {
        var compilation = RoslynTestCompilation.Create("""
            public interface IBad { }
            public interface IGood { }
            public class Root : IBad, IGood { }
            public class Other { }
            """);
        var system = new RoslynTypeSystem(compilation);
        var bad = FaultingNamedTypeSymbol.Wrap(compilation.GetTypeByMetadataName("IBad")!, failArity: true);
        var good = compilation.GetTypeByMetadataName("IGood")!;
        var interfaces = failureFirst
            ? ImmutableArray.Create(bad, good)
            : ImmutableArray.Create(good, bad);
        var root = FaultingNamedTypeSymbol.Wrap(compilation.GetTypeByMetadataName("Root")!, interfaces: interfaces);
        return (system, system.Mapper.MapType(root), system.Mapper.MapType(good),
            system.Mapper.MapType(compilation.GetTypeByMetadataName("Other")!));
    }
}
