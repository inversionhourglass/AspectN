using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class GenericSignatureConstructionTests
{
    private const string SOURCE = """
        namespace Review;
        public class Box<TLeft, TRight> {}
        public class Outer<T> { public class Inner<U> {} }
        public class Fixture
        {
            public Box<int, string> Closed() => null;
            public Box<int, T> Open<T>() => null;
            public Outer<int>.Inner<string> Nested() => null;
        }
        """;

    [Theory]
    [InlineData("Closed", "method(Review.Box<int,string> Closed())")]
    [InlineData("Open", "method(Review.Box<int,*> Open<..>())")]
    [InlineData("Nested", "method(Review.Outer<int>/Inner<string> Nested())")]
    public void MapMethod_LegalGenericConstruction_PreservesCompleteSlots(string name, string expression)
    {
        using var fixture = new ReviewFixture(SOURCE);
        Assert.True(new AspectNEngine().CompileMemberExpression(expression).TryGetMemberPattern(out var structural));
        Assert.True(new AspectNEngine().CompileMemberExpression("regex(.*)").TryGetMemberPattern(out var regex));
        foreach (var (backend, system, method) in fixture.Methods(name))
        {
            var result = Assert.IsType<NamedTypeSignature>(method.ReturnType.Type);
            Assert.Equal(2, result.TypeArguments.Count);
            Assert.NotNull(result.OriginalDefinition);
            Assert.Empty(result.OriginalDefinition.TypeArguments);
            if (name == "Open") Assert.IsType<GenericParameterTypeSignature>(result.TypeArguments[1]);
            Assert.True(structural.Match(method, system).Status == MatchStatus.Matched, backend);
            Assert.True(regex.Match(method, system).Status == MatchStatus.Matched, backend);
        }
    }
}
