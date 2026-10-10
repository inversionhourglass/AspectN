using AspectN.Diagnostics;
using AspectN.Evaluation;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class SignatureLimitConformanceTests
{
    [Theory]
    [InlineData(64, false)]
    [InlineData(65, false)]
    [InlineData(64, true)]
    [InlineData(65, true)]
    public void MapMethod_GenericArityLimit_AgreesAcrossBackends(int arity, bool methodParameters)
    {
        var slots = string.Join(",", Enumerable.Range(0, arity).Select(index => "T" + index));
        var arguments = string.Join(",", Enumerable.Repeat("int", arity));
        var source = methodParameters
            ? $"namespace Review; public class Fixture {{ public void Run<{slots}>() {{}} }}"
            : $"namespace Review; public class Many<{slots}> {{}} public class Fixture {{ public Many<{arguments}> Run() => null; }}";
        using var fixture = new ReviewFixture(source);
        Assert.True(new AspectNEngine().CompileMemberExpression("method(* Run<?>(..))").TryGetMemberPattern(out var pattern));

        foreach (var (backend, system, map) in fixture.MethodMappings("Run"))
        {
            if (arity == 65)
            {
                var exception = Assert.Throws<AspectNLimitException>(map);
                Assert.Equal(DiagnosticCodes.MetadataGenericArityExceeded, exception.Diagnostic.Code);
                continue;
            }

            Assert.True(pattern!.Match(map(), system).Status == MatchStatus.Matched, backend);
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    public void MapMethod_NestedTypeArityLimit_IncludesOuterParameters(int innerArity)
    {
        var outer = string.Join(",", Enumerable.Range(0, 32).Select(index => "T" + index));
        var inner = string.Join(",", Enumerable.Range(0, innerArity).Select(index => "U" + index));
        var outerArguments = string.Join(",", Enumerable.Repeat("int", 32));
        var innerArguments = string.Join(",", Enumerable.Repeat("int", innerArity));
        var source = $"namespace Review; public class Outer<{outer}> {{ public class Inner<{inner}> {{}} }} " +
            $"public class Fixture {{ public Outer<{outerArguments}>.Inner<{innerArguments}> Run() => null; }}";
        using var fixture = new ReviewFixture(source);

        foreach (var (_, _, map) in fixture.MethodMappings("Run"))
        {
            if (innerArity == 33)
            {
                var exception = Assert.Throws<AspectNLimitException>(map);
                Assert.Equal(DiagnosticCodes.MetadataGenericArityExceeded, exception.Diagnostic.Code);
            }
            else Assert.NotNull(map());
        }
    }

    [Theory]
    [InlineData(61)]
    [InlineData(62)]
    [InlineData(63)]
    public void MapMethod_NestedConstructedTypeDepth_CountsFlattenedArgumentsOnce(int arrayLayers)
    {
        var argument = "int" + string.Concat(Enumerable.Repeat("[]", arrayLayers));
        using var fixture = new ReviewFixture($"namespace Review; public class Outer<T> {{ public class Inner {{}} }} " +
            $"public class Fixture {{ public Outer<{argument}>.Inner Run() => null; }}");
        foreach (var (backend, _, map) in fixture.MethodMappings("Run"))
        {
            if (arrayLayers == 63)
            {
                var exception = Assert.Throws<AspectNLimitException>(map);
                Assert.Equal(DiagnosticCodes.MetadataDepthExceeded, exception.Diagnostic.Code);
            }
            else Assert.True(map().ReturnType.Type is not null, backend);
        }
    }

    [Theory]
    [InlineData("Review.Known+", MatchStatus.Matched)]
    [InlineData("Review.Absent+", MatchStatus.Unknown)]
    [InlineData("Review.Kn*+", MatchStatus.Matched)]
    [InlineData("Review.Abs*+", MatchStatus.Unknown)]
    public void Match_OverLimitAncestor_PreservesOtherBranchesAndDiagnostic(string text, MatchStatus expected)
    {
        var slots = string.Join(",", Enumerable.Range(0, 65).Select(index => "T" + index));
        var arguments = string.Join(",", Enumerable.Repeat("int", 65));
        using var fixture = new ReviewFixture($"namespace Review; public interface TooMany<{slots}> {{}} " +
            $"public interface Known {{}} public interface Absent {{}} " +
            $"public class Fixture : TooMany<{arguments}>, Known {{ public void Run() {{}} }}");
        Assert.True(new AspectNEngine().CompileTypePattern(text).TryGetTypePattern(out var pattern));
        foreach (var (backend, system, map) in fixture.MethodMappings("Run"))
        {
            var result = pattern!.Match(map().DeclaringType, system);
            Assert.True(result.Status == expected, backend);
            if (expected == MatchStatus.Unknown)
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.MetadataGenericArityExceeded);
        }
    }
}
