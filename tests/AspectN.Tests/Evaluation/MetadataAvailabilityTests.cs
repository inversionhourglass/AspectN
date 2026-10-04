using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class MetadataAvailabilityTests
{
    [Theory]
    [InlineData("method(public * Service.Run(..))", MetadataFacts.Accessibility, MatchStatus.Unknown)]
    [InlineData("method(!public * Service.Run(..))", MetadataFacts.Accessibility, MatchStatus.Unknown)]
    [InlineData("method(public string Service.Run(..))", MetadataFacts.Accessibility, MatchStatus.NotMatched)]
    [InlineData("method(static * Service.Run(..))", MetadataFacts.IsStatic, MatchStatus.Unknown)]
    [InlineData("method(!static * Service.Run(..))", MetadataFacts.IsStatic, MatchStatus.Unknown)]
    [InlineData("method(* Service.Run(..))", MetadataFacts.IsAsync | MetadataFacts.Attributes, MatchStatus.Matched)]
    [InlineData("method(* Service.Run(..))", MetadataFacts.Kind, MatchStatus.Unknown)]
    [InlineData("execution(* Service.Run(..))", MetadataFacts.Kind, MatchStatus.Matched)]
    [InlineData("method(* Other.Run(..))", MetadataFacts.Kind, MatchStatus.NotMatched)]
    [InlineData("method(* Service.Run(..))", MetadataFacts.Name, MatchStatus.Unknown)]
    [InlineData("method(* Service.*(..))", MetadataFacts.Name, MatchStatus.Matched)]
    [InlineData("attr(call *)", MetadataFacts.Attributes, MatchStatus.Unknown)]
    [InlineData("!attr(call *)", MetadataFacts.Attributes, MatchStatus.Unknown)]
    [InlineData("attr(ret *)", MetadataFacts.Attributes, MatchStatus.Matched)]
    [InlineData("attr(any *)", MetadataFacts.Attributes | MetadataFacts.TypeAttributes, MatchStatus.Matched)]
    [InlineData("regex(Run)", MetadataFacts.IsAsync | MetadataFacts.Attributes, MatchStatus.Matched)]
    [InlineData("regex(Run)", MetadataFacts.Name, MatchStatus.Unknown)]
    public void Match_OneUnavailableFact_DoesNotDisableIndependentFacts(string source, MetadataFacts facts, MatchStatus expected)
    {
        var type = Named("Int32");
        var signature = new MethodSignature("Run", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Named("Service"), new TypeUseSignature(type, RefKind.None, TypeUseKind.Return), Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(), Array.Empty<TypeSignature>(), Array.Empty<AttributeSignature>(),
            new[] { new AttributeSignature(Named("MarkerAttribute"), AttributeTargetKind.Return) },
            availability: new MetadataAvailability(facts));
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, pattern!.Match(signature, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Availability_Diagnostics_AreAnImmutableSnapshot()
    {
        var diagnostics = new List<AspectNDiagnostic>
        {
            new(DiagnosticCodes.ResolutionFailure, DiagnosticSeverity.Warning, "Missing definition.")
        };
        var availability = new MetadataAvailability(MetadataFacts.Attributes, diagnostics);
        diagnostics.Clear();

        Assert.Single(availability.Diagnostics);
        Assert.False(availability.IsKnown(MetadataFacts.Attributes));
        Assert.True(availability.IsKnown(MetadataFacts.IsStatic));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetadataAvailability((MetadataFacts)(1 << 20)));
    }

    private static NamedTypeSignature Named(string name) => new(new TypeIdentity(
        new AssemblyIdentity("Example"), name == "Int32" ? "System" : "Example", name, 0, TypeKind.Class));
}
