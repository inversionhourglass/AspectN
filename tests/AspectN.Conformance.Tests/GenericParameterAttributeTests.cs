using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class GenericParameterAttributeTests
{
    private const string SOURCE = """
        using System;
        namespace Review;
        [AttributeUsage(AttributeTargets.All)]
        public sealed class MarkerAttribute : Attribute
        {
            public MarkerAttribute() => throw new InvalidOperationException("不得实例化特性。");
        }
        [Marker]
        public class Fixture<[Marker] TMarked, TPlain>
        {
            [Marker]
            public TMarked M<[Marker] UMarked, UPlain>(UMarked first, UPlain second, TPlain third) => default;
        }
        public class ParameterOnly<[Marker] T>
        {
            public T M() => default;
        }
        """;

    [Theory]
    [InlineData(false, 0, MatchStatus.Matched)]
    [InlineData(false, 1, MatchStatus.NotMatched)]
    [InlineData(true, 0, MatchStatus.Matched)]
    [InlineData(true, 1, MatchStatus.NotMatched)]
    public void HasDirectAttribute_GenericParameterDeclaration_AllViewsAgree(bool methodParameter, int position, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, "Review.Fixture`2");
        foreach (var (backend, system, method) in fixture.Methods("M"))
        {
            Assert.Single(method.TypeAttributes);
            var marker = Assert.Single(method.Attributes).AttributeType;
            var parameter = methodParameter ? method.Parameters[position].Type.Type
                : position == 0 ? method.ReturnType.Type : method.Parameters[2].Type.Type;
            Assert.IsType<GenericParameterTypeSignature>(parameter);
            var result = system.HasDirectAttribute(AttributeTarget.ForType(parameter), marker);
            Assert.True(result.Status == expected, backend);
            Assert.Empty(result.Diagnostics);
        }
    }

    [Fact]
    public void Match_TypeAttributeSelector_DoesNotTargetGenericParameter()
    {
        using var fixture = new ReviewFixture(SOURCE, "Review.ParameterOnly`1");
        var marker = fixture.Cecil.Mapper.MapType(fixture.Cecil.Module.GetType("Review.MarkerAttribute"));
        Assert.True(new AspectNEngine().CompileMemberExpression("attr(type Review.MarkerAttribute)")
            .TryGetMemberPattern(out var pattern));
        foreach (var (backend, system, method) in fixture.Methods("M"))
        {
            Assert.Empty(method.TypeAttributes);
            var direct = system.HasDirectAttribute(AttributeTarget.ForType(method.ReturnType.Type), marker);
            Assert.True(direct.Status == MatchStatus.Matched, backend + " 泛型形参直接API");
            Assert.Empty(direct.Diagnostics);
            var expression = pattern.Match(method, system);
            Assert.True(expression.Status == MatchStatus.NotMatched, backend + " 声明类型DSL目标");
            Assert.Empty(expression.Diagnostics);
        }
    }
}
