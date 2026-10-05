using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class AttributeMetadataConformanceTests
{
    private const string SOURCE = """
        using System;
        namespace Review;
        public class TypeMarkAttribute : Attribute {}
        public class CallMarkAttribute : Attribute {}
        public class ReturnMarkAttribute : Attribute {}
        public class ParameterMarkAttribute : Attribute {}
        [TypeMark] public class Fixture
        {
            [CallMark] [return: ReturnMark]
            public int M([ParameterMark] int value) => value;
        }
        """;

    [Theory]
    [InlineData("attr(type Review.TypeMarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(type Review.*MarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(any Review.TypeMarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(type (Review.TypeMarkAttribute || Review.MissingAttribute))", MatchStatus.Matched)]
    [InlineData("attr(call Review.CallMarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(ret Review.ReturnMarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(para[0] Review.ParameterMarkAttribute)", MatchStatus.Matched)]
    [InlineData("attr(type Review.MissingAttribute)", MatchStatus.NotMatched)]
    [InlineData("attr(call Review.TypeMarkAttribute)", MatchStatus.NotMatched)]
    [InlineData("attr(type Review.ParameterMarkAttribute)", MatchStatus.NotMatched)]
    public void Match_RealAttributeMetadata_AllFourViewsAgree(string expression, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE);
        var compilation = new AspectNEngine().CompileMemberExpression(expression);
        Assert.True(compilation.TryGetMemberPattern(out var pattern), string.Join(";", compilation.Diagnostics.Select(d => d.Message)));
        var methods = fixture.Methods("M").ToArray();
        var snapshot = SignatureSnapshot.Callable(methods[0].Method);
        foreach (var (backend, system, method) in methods)
        {
            Assert.Equal(snapshot, SignatureSnapshot.Callable(method));
            var attribute = Assert.Single(method.TypeAttributes);
            Assert.Equal(AttributeTargetKind.Type, attribute.Target);
            Assert.Null(attribute.ParameterIndex);
            Assert.Equal("Review.TypeMarkAttribute", Assert.IsType<NamedTypeSignature>(attribute.AttributeType).Identity.FullMetadataName);
            Assert.True(pattern!.Match(method, system).Status == expected, backend + ": " + expression);
        }
    }
}
