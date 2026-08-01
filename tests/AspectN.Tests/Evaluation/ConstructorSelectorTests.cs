using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class ConstructorSelectorTests
{
    [Theory]
    [InlineData("ctor(Service(..))", CallableKind.InstanceConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", CallableKind.StaticConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", CallableKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", CallableKind.StaticConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", CallableKind.OrdinaryMethod, MatchStatus.NotMatched)]
    public void Match_Constructors_UseDisjointDomains(
        string source,
        CallableKind kind,
        MatchStatus expected)
    {
        var result = Match(source, Callable(kind));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_InstanceConstructor_MatchesParametersAndRefKinds()
    {
        var candidate = Callable(
            CallableKind.InstanceConstructor,
            new ParameterSignature(
                "name",
                0,
                new TypeUseSignature(
                    Named("System", "String"),
                    RefKind.None,
                    TypeUseKind.Parameter)),
            new ParameterSignature(
                "value",
                1,
                new TypeUseSignature(
                    Named("System", "Int32", TypeKind.Struct),
                    RefKind.Ref,
                    TypeUseKind.Parameter)));

        var result = Match("ctor(Service(string, ref int))", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static CallableSignature Callable(
        CallableKind kind,
        params ParameterSignature[] parameters)
    {
        return new CallableSignature(
            kind == CallableKind.StaticConstructor ? ".cctor" : ".ctor",
            kind,
            Accessibility.Public,
            isStatic: kind == CallableKind.StaticConstructor,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            parameters,
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                kind));
    }
}
