using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class DeepStructuralConformanceTests
{
    [Fact]
    public async Task IsAssignableTo_SharedNeutralTypeGraph_PreservesUnknownWithoutExpandingEveryPath()
    {
        TypeSignature left = new UnsupportedTypeSignature("unsupported", "fixture");
        TypeSignature right = new UnsupportedTypeSignature("unsupported", "fixture");
        var actualIdentity = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Pair", 2, TypeKind.Class);
        var expectedIdentity = new TypeIdentity(new AssemblyIdentity(""), "Example", "Pair", 2, TypeKind.Unknown);
        for (var index = 0; index < 40; index++)
        {
            left = new NamedTypeSignature(actualIdentity, [left, left]);
            right = new NamedTypeSignature(expectedIdentity, [right, right]);
        }
        using var cecil = new CecilTypeSystem(ModuleDefinition.CreateModule("Shared", ModuleKind.Dll));
        var comparison = Task.Run(() => new[]
        {
            cecil.IsAssignableTo(left, right),
            new ReflectionTypeSystem().IsAssignableTo(left, right),
            new RoslynTypeSystem(CSharpCompilation.Create("Shared")).IsAssignableTo(left, right)
        });
        var results = await comparison.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.All(results, result => Assert.Equal(MatchStatus.Unknown, result.Status));
    }

    [Fact]
    public void IsAssignableTo_DeepNeutralArrays_DoesNotRequireNativeMappingOrRecursiveComparison()
    {
        TypeSignature left = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"),
            "Example", "Item", 0, TypeKind.Class));
        TypeSignature right = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity(""),
            "Example", "Item", 0, TypeKind.Unknown));
        for (var index = 0; index < 100_000; index++)
        {
            left = new ArrayTypeSignature(left, 1);
            right = new ArrayTypeSignature(right, 1);
        }
        using var cecil = new CecilTypeSystem(ModuleDefinition.CreateModule("Deep", ModuleKind.Dll));
        Assert.Equal(MatchStatus.Matched, cecil.IsAssignableTo(left, right).Status);
        Assert.Equal(MatchStatus.Matched, new ReflectionTypeSystem().IsAssignableTo(left, right).Status);
        Assert.Equal(MatchStatus.Matched, new RoslynTypeSystem(CSharpCompilation.Create("Deep")).IsAssignableTo(left, right).Status);
    }
}
