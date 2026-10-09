using AspectN.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspectN.Roslyn.Tests;

public sealed class UnavailableAncestorTests
{
    [Theory]
    [InlineData("I", MatchStatus.Matched)]
    [InlineData("J", MatchStatus.Unknown)]
    public void IsAssignableTo_UnresolvedBaseType_SearchesKnownInterfaces(string targetName, MatchStatus expected)
    {
        var compilation = RoslynTestCompilation.Create("public interface I {} public interface J {}")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class C : Missing, I {}"));
        Assert.Contains(compilation.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0246");
        var candidateSymbol = compilation.GetTypeByMetadataName("C");
        var targetSymbol = compilation.GetTypeByMetadataName(targetName);
        Assert.NotNull(candidateSymbol);
        Assert.NotNull(targetSymbol);
        Assert.Equal(TypeKind.Error, candidateSymbol.BaseType!.TypeKind);
        Assert.Equal("I", Assert.Single(candidateSymbol.Interfaces).Name);
        var typeSystem = new RoslynTypeSystem(compilation);

        var result = typeSystem.IsAssignableTo(typeSystem.Mapper.MapType(candidateSymbol), typeSystem.Mapper.MapType(targetSymbol));

        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown) Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void IsAssignableTo_UnresolvedBaseType_PreservesCancellation()
    {
        var compilation = RoslynTestCompilation.Create("public interface I {}")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class C : Missing, I {}"));
        var typeSystem = new RoslynTypeSystem(compilation);
        var candidate = typeSystem.Mapper.MapType(compilation.GetTypeByMetadataName("C")!);
        var target = typeSystem.Mapper.MapType(compilation.GetTypeByMetadataName("I")!);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => typeSystem.IsAssignableTo(candidate, target, cancellation.Token));
    }
}
