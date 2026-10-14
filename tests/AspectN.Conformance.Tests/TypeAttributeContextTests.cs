using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class TypeAttributeContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_KnownTypeAttributes_IndependentOfContext(bool marked)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var candidate = Map(fixture, marked);
        Assert.True(candidate.Availability.IsKnown(MetadataFacts.TypeAttributes));
        Assert.Equal(marked ? 1 : 0, candidate.TypeAttributes.Count);

        foreach (var system in new[] { fixture.Reflection, new ReflectionTypeSystem() })
        {
            foreach (var expression in Expressions())
                AssertResult(expression, candidate, system, marked ? MatchStatus.Matched : MatchStatus.NotMatched);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Match_UnavailableTypeAttributes_AttemptsConcreteNativeQuery(bool marked, bool associated)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var candidate = Unavailable(Map(fixture, marked));
        var system = associated ? fixture.Reflection : new ReflectionTypeSystem();

        AssertResult("attr(type Example.MarkAttribute)", candidate, system,
            associated ? (marked ? MatchStatus.Matched : MatchStatus.NotMatched) : MatchStatus.Unknown);
    }

    [Theory]
    [InlineData("attr(type Example.Mark*)")]
    [InlineData("attr(type (Example.MarkAttribute || Example.MissingAttribute))")]
    public void Match_UnavailableTypeAttributes_NonConcreteNeedsList(string expression)
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);

        AssertResult(expression, Unavailable(Map(fixture, true)), fixture.Reflection, MatchStatus.Unknown);
    }

    [Fact]
    public void Match_UnavailableTypeAttributes_PreservesReadDiagnostic()
    {
        using var fixture = new CompiledAdapterFixture(SOURCE);
        var mapped = Map(fixture, true);
        var candidate = Unavailable(mapped, new AspectNDiagnostic(
            "AN3004", DiagnosticSeverity.Warning, "Original attribute read failed."));
        Assert.True(new AspectNEngine().CompileMemberExpression("attr(type Example.MarkAttribute)")
            .TryGetMemberPattern(out var pattern));

        var result = pattern.Match(candidate, new ReflectionTypeSystem());

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3004");
    }

    private static void AssertResult(string expression, MethodSignature candidate, ReflectionTypeSystem system,
        MatchStatus expected)
    {
        Assert.True(new AspectNEngine().CompileMemberExpression(expression).TryGetMemberPattern(out var pattern));
        var result = pattern.Match(candidate, system);
        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown)
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code is "AN3001" or "AN3004");
        else
            Assert.Empty(result.Diagnostics);
    }

    private static MethodSignature Map(CompiledAdapterFixture fixture, bool marked) => fixture.Reflection.Mapper.MapMethod(
        fixture.Assembly.GetType(marked ? "Example.Marked" : "Example.Plain")!.GetMethod("M")!);

    private static MethodSignature Unavailable(MethodSignature source, AspectNDiagnostic? diagnostic = null) => new(
        source.Name, source.Kind, source.Accessibility, source.IsStatic, source.IsAsync, source.DeclaringType,
        source.ReturnType, source.Parameters, source.GenericParameters, source.GenericArguments, source.Attributes,
        source.ReturnAttributes, source.TypeAttributes, source.OriginalDefinition, source.AssociatedProperty,
        new MetadataAvailability(MetadataFacts.TypeAttributes, diagnostic is null ? [] : [diagnostic]));

    private static string[] Expressions() =>
    [
        "attr(type Example.MarkAttribute)",
        "attr(type Example.Mark*)",
        "attr(type (Example.MarkAttribute || Example.MissingAttribute))"
    ];

    private const string SOURCE = """
        using System;
        namespace Example;
        [AttributeUsage(AttributeTargets.Class)]
        public sealed class MarkAttribute : Attribute {}
        [Mark] public class Marked { public void M() {} }
        public class Plain { public void M() {} }
        """;
}
