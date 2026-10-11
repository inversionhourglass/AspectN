using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ReflectionQueryRegressionTests
{
    [Fact]
    public void IsAssignableTo_DetachedUnsupportedSignature_PreservesStructuralDiagnostic()
    {
        var system = new ReflectionTypeSystem();
        var candidate = new UnsupportedTypeSignature("bad-shape", "Unavailable shape identity.");
        var target = system.Mapper.MapType(typeof(int));
        var structural = system.AreEqual(candidate, target);

        var relation = system.IsAssignableTo(candidate, target);

        Assert.Equal(MatchStatus.Unknown, relation.Status);
        Assert.Equal(structural.Diagnostics.Select(diagnostic => diagnostic.Message),
            relation.Diagnostics.Select(diagnostic => diagnostic.Message));
    }
}
