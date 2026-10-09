using AspectN.Evaluation;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class RelationshipBranchTests
{
    [Fact]
    public void IsAssignableTo_UnknownAncestorBeforeKnownInterface_KnownMatchWins()
    {
        using var fixture = new CompiledAdapterFixture("namespace Example { public interface I<T> {} public unsafe class C : I<int*[]>, I<int[]> {} }");
        var candidate = fixture.Assembly.GetType("Example.C")!;
        var target = fixture.Assembly.GetType("Example.I`1")!.MakeGenericType(typeof(int[]));
        Assert.True(target.IsAssignableFrom(candidate));
        Assert.Equal(MatchStatus.Matched, fixture.Reflection.IsAssignableTo(
            fixture.Reflection.Mapper.MapType(candidate), fixture.Reflection.Mapper.MapType(target)).Status);
        var cecilTarget = new GenericInstanceType(fixture.Cecil.Module.GetType("Example.I`1"));
        cecilTarget.GenericArguments.Add(new ArrayType(fixture.Cecil.Module.TypeSystem.Int32));
        Assert.Equal(MatchStatus.Matched, fixture.Cecil.IsAssignableTo(
            fixture.Cecil.Mapper.MapType(fixture.Cecil.Module.GetType("Example.C")), fixture.Cecil.Mapper.MapType(cecilTarget)).Status);
        CheckRoslyn(fixture.Roslyn);
        CheckRoslyn(fixture.RoslynMetadata);
    }

    private static void CheckRoslyn(RoslynTypeSystem system)
    {
        var target = system.Compilation.GetTypeByMetadataName("Example.I`1")!.Construct(
            system.Compilation.CreateArrayTypeSymbol(system.Compilation.GetSpecialType(SpecialType.System_Int32)));
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(
            system.Mapper.MapType(system.Compilation.GetTypeByMetadataName("Example.C")!), system.Mapper.MapType(target)).Status);
    }
}
