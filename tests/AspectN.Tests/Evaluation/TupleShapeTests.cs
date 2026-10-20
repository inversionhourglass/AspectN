using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class TupleShapeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_ZeroArityValueTuple_IsKnownNonMatching(bool negated)
    {
        AssertStatus(negated ? "!(int,string)" : "(int,string)", Tuple([]),
            negated ? MatchStatus.Matched : MatchStatus.NotMatched);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_KnownInvalidRest_IsKnownNonMatching(bool negated)
    {
        var elements = Enumerable.Repeat<TypeSignature>(Int(), 8).ToArray();
        AssertStatus(negated ? "!(int,int,int,int,int,int,int,int)" : "(int,int,int,int,int,int,int,int)",
            Tuple(elements), negated ? MatchStatus.Matched : MatchStatus.NotMatched);
    }

    [Fact]
    public void Match_UnavailableRest_RemainsUnknown()
    {
        var elements = Enumerable.Repeat<TypeSignature>(Int(), 7)
            .Append(new UnsupportedTypeSignature("missing rest", "metadata unavailable")).ToArray();
        AssertStatus("!(int,int,int,int,int,int,int,int)", Tuple(elements), MatchStatus.Unknown);
    }

    [Fact]
    public void Match_ZeroArityRest_EndsLongTuple()
    {
        var elements = Enumerable.Repeat<TypeSignature>(Int(), 7).Append(Tuple([])).ToArray();
        AssertStatus("(int,int,int,int,int,int,int)", Tuple(elements), MatchStatus.Matched);
    }

    [Fact]
    public void Construct_ZeroArityRestWithUnexpectedArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => new NamedTypeSignature(
            new TypeIdentity(new AssemblyIdentity("Example"), "System", "ValueTuple", 0, TypeKind.Struct),
            new TypeSignature[] { Int() }));
    }

    private static void AssertStatus(string expression, TypeSignature candidate, MatchStatus status)
    {
        var compilation = new AspectNEngine().CompileTypePattern(expression);
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Equal(status, pattern!.Match(candidate, new StubTypeSystem()).Status);
    }

    private static NamedTypeSignature Tuple(TypeSignature[] arguments) => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "System", "ValueTuple", arguments.Length, TypeKind.Struct), arguments);

    private static NamedTypeSignature Int() => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "System", "Int32", 0, TypeKind.Struct));
}
