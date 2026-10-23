using AspectN.Diagnostics;
using AspectN.Evaluation;
using Xunit;
using Xunit.Sdk;

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
            WithBackendContext(backend, $"arity={arity}; methodParameters={methodParameters}", () =>
            {
                if (arity == 65)
                {
                    var exception = Assert.Throws<AspectNLimitException>(map);
                    Assert.Equal(DiagnosticCodes.MetadataGenericArityExceeded, exception.Diagnostic.Code);
                    return;
                }

                Assert.Equal(MatchStatus.Matched, pattern!.Match(map(), system).Status);
            });
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

        foreach (var (backend, _, map) in fixture.MethodMappings("Run"))
        {
            WithBackendContext(backend, $"outerArity=32; innerArity={innerArity}", () =>
            {
                if (innerArity == 33)
                {
                    var exception = Assert.Throws<AspectNLimitException>(map);
                    Assert.Equal(DiagnosticCodes.MetadataGenericArityExceeded, exception.Diagnostic.Code);
                }
                else Assert.NotNull(map());
            });
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
            WithBackendContext(backend, $"arrayLayers={arrayLayers}", () =>
            {
                if (arrayLayers == 63)
                {
                    var exception = Assert.Throws<AspectNLimitException>(map);
                    Assert.Equal(DiagnosticCodes.MetadataDepthExceeded, exception.Diagnostic.Code);
                }
                else Assert.True(map().ReturnType.Type is not null, "Expected a non-null return type.");
            });
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
            WithBackendContext(backend, $"text={text}; expected={expected}", () =>
            {
                var result = pattern!.Match(map().DeclaringType, system);
                Assert.Equal(expected, result.Status);
                if (expected == MatchStatus.Unknown)
                    Assert.Contains(DiagnosticCodes.MetadataGenericArityExceeded,
                        result.Diagnostics.Select(diagnostic => diagnostic.Code));
            });
        }
    }

    private static void WithBackendContext(string backend, string input, Action assertion)
    {
        try
        {
            assertion();
        }
        catch (Exception exception)
        {
            // 保留原断言或映射异常及其堆栈，只为测试失败补充当前后端和输入。
            throw new XunitException($"Backend: {backend}; {input}{Environment.NewLine}{exception.Message}", exception);
        }
    }
}
