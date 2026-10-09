using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class InteropRefKindTests
{
    private const string SOURCE = """
        namespace Review;
        public class Interop
        {
            public void InFlag([System.Runtime.InteropServices.In] ref int value) {}
            public void InOutFlag([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] ref int value) {}
            public void Ref(ref int value) {}
            public void Out(out int value) { value = 0; }
            public void In(in int value) {}
            public void Readonly(ref readonly int value) {}
        }
        """;

    [Theory]
    [InlineData("InFlag", AspectN.Metadata.RefKind.Ref, "ref")]
    [InlineData("InOutFlag", AspectN.Metadata.RefKind.Ref, "ref")]
    [InlineData("Ref", AspectN.Metadata.RefKind.Ref, "ref")]
    [InlineData("Out", AspectN.Metadata.RefKind.Out, "out")]
    [InlineData("In", AspectN.Metadata.RefKind.In, "in")]
    [InlineData("Readonly", AspectN.Metadata.RefKind.RefReadOnly, "ref readonly")]
    public void MapMethod_InteropDirectionFlags_PreservesLanguageRefKind(
        string name, AspectN.Metadata.RefKind expected, string annotation)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var native = fixture.Assembly.GetType("Review.Interop")!.GetMethod(name)!;
        var methods = new List<(ITypeSystem System, MethodSignature Method)>
        {
            (fixture.Reflection, fixture.Reflection.Mapper.MapMethod(native)),
            (fixture.Cecil, fixture.Cecil.Mapper.MapMethod(fixture.Cecil.Module.GetType("Review.Interop").Methods.Single(method => method.Name == name)))
        };
        foreach (var system in new[] { fixture.Roslyn, fixture.RoslynMetadata })
        {
            var symbol = system.Compilation.GetTypeByMetadataName("Review.Interop")!
                .GetMembers(name).OfType<IMethodSymbol>().Single();
            methods.Add((system, system.Mapper.MapMethod(symbol)));
        }

        var compilation = new AspectNEngine().CompileMemberExpression($"method(* {name}({annotation} int))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        foreach (var (system, method) in methods)
        {
            Assert.Equal(expected, method.Parameters[0].Type.RefKind);
            Assert.Equal(MatchStatus.Matched, pattern.Match(method, system).Status);
        }
    }
}
