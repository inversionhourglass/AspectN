using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class AttributeInheritanceTests
{
    private const string SOURCE = """
        using System;
        namespace Review;
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
        public sealed class InheritedMarkAttribute : Attribute {}
        [InheritedMark]
        public class BaseFixture
        {
            [InheritedMark] public virtual int M() => 0;
        }
        public class Fixture : BaseFixture
        {
            public override int M() => 1;
        }
        [InheritedMark]
        public class DirectFixture : BaseFixture
        {
            [InheritedMark] public override int M() => 2;
        }
        """;

    [Theory]
    [InlineData("BaseFixture", "type", MatchStatus.Matched)]
    [InlineData("BaseFixture", "call", MatchStatus.Matched)]
    [InlineData("Fixture", "type", MatchStatus.NotMatched)]
    [InlineData("Fixture", "call", MatchStatus.NotMatched)]
    [InlineData("DirectFixture", "type", MatchStatus.Matched)]
    [InlineData("DirectFixture", "call", MatchStatus.Matched)]
    public void Match_InheritedAttribute_OnlyDirectRecordsMatch(
        string typeName, string position, MatchStatus expected)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var metadataName = "Review." + typeName;
        var nativeType = fixture.Assembly.GetType(metadataName)!;
        var nativeMethod = nativeType.GetMethod("M")!;
        var nativeAttribute = fixture.Assembly.GetType("Review.InheritedMarkAttribute")!;

        // 先证明运行时继承查询可以看到标记，防止用根本不存在的特性作负例。
        Assert.True(Attribute.IsDefined(nativeType, nativeAttribute, inherit: true));
        Assert.True(Attribute.IsDefined(nativeMethod, nativeAttribute, inherit: true));
        Assert.Equal(expected == MatchStatus.Matched,
            Attribute.IsDefined(nativeType, nativeAttribute, inherit: false));
        Assert.Equal(expected == MatchStatus.Matched,
            Attribute.IsDefined(nativeMethod, nativeAttribute, inherit: false));

        var methods = new List<(string Backend, ITypeSystem System, MethodSignature Method)>
        {
            ("Reflection", fixture.Reflection, fixture.Reflection.Mapper.MapMethod(nativeMethod)),
            ("Cecil", fixture.Cecil, fixture.Cecil.Mapper.MapMethod(
                fixture.Cecil.Module.GetType(metadataName).Methods.Single(method => method.Name == "M")))
        };
        foreach (var (backend, system) in new[]
                 { ("Roslyn source", fixture.Roslyn), ("Roslyn PE", fixture.RoslynMetadata) })
        {
            var symbol = system.Compilation.GetTypeByMetadataName(metadataName)!
                .GetMembers("M").OfType<IMethodSymbol>().Single();
            methods.Add((backend, system, system.Mapper.MapMethod(symbol)));
        }
        var compilation = new AspectNEngine().CompileMemberExpression(
            $"attr({position} Review.InheritedMarkAttribute)");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        foreach (var (backend, system, method) in methods)
        {
            var result = pattern.Match(method, system);
            Assert.True(expected == result.Status, backend + ": " + typeName + "/" + position);
            Assert.Empty(result.Diagnostics);
        }
    }
}
