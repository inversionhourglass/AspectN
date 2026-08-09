using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class AssignableRelationshipTargetTests
{
    [Fact]
    public void Match_ConcreteGenericTarget_PreservesTypeArguments()
    {
        TypeSignature? observedTarget = null;
        var typeSystem = new StubTypeSystem
        {
            Assignability = (_, target, _) =>
            {
                observedTarget = target;
                return TypeRelationResult.Matched;
            }
        };
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(
                "System.Collections.Generic.IDictionary<string,string>+",
                new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var result = pattern!.Match(Candidate(), typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(observedTarget);
        Assert.Equal(
            "System.Collections.Generic.IDictionary`2",
            namedTarget.Identity.FullMetadataName);
        Assert.Collection(
            namedTarget.TypeArguments,
            argument => Assert.Equal("System.String", Name(argument)),
            argument => Assert.Equal("System.String", Name(argument)));
    }

    private static NamedTypeSignature Candidate()
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "Example",
                "Dictionary",
                0,
                TypeKind.Class));
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }
}
