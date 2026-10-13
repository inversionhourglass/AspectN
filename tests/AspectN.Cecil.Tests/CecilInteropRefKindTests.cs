using AspectN.Cecil.Tests.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilInteropRefKindTests
{
    [Theory]
    [InlineData("InFlag", false, "int", MatchStatus.Matched)]
    [InlineData("InFlag", false, "ref int", MatchStatus.Matched)]
    [InlineData("InFlag", false, "out int", MatchStatus.NotMatched)]
    [InlineData("InFlag", false, "in int", MatchStatus.NotMatched)]
    [InlineData("InFlag", false, "ref readonly int", MatchStatus.NotMatched)]
    [InlineData("InFlag", false, "!ref int", MatchStatus.NotMatched)]
    [InlineData("InOutFlag", true, "int", MatchStatus.Matched)]
    [InlineData("InOutFlag", true, "ref int", MatchStatus.Matched)]
    [InlineData("InOutFlag", true, "out int", MatchStatus.NotMatched)]
    [InlineData("InOutFlag", true, "in int", MatchStatus.NotMatched)]
    [InlineData("InOutFlag", true, "ref readonly int", MatchStatus.NotMatched)]
    [InlineData("InOutFlag", true, "!ref int", MatchStatus.NotMatched)]
    public void Match_InteropDirectionFlags_PreservesRefKind(
        string methodName, bool isOut, string parameterPattern, MatchStatus expected)
    {
        using var system = EmittedFixture.Open();
        var type = EmittedFixture.FindType(system, nameof(InteropRefKindFixture));
        var native = Assert.Single(type.Methods, method => method.Name == methodName);
        var parameter = Assert.Single(native.Parameters);
        Assert.IsType<ByReferenceType>(parameter.ParameterType);
        Assert.True(parameter.IsIn);
        Assert.Equal(isOut, parameter.IsOut);

        var signature = system.Mapper.MapMethod(native);
        Assert.Equal(RefKind.Ref, Assert.Single(signature.Parameters).Type.RefKind);
        var compilation = new AspectNEngine().CompileMemberExpression(
            $"method(* {methodName}({parameterPattern}))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern.Match(signature, system);

        Assert.Equal(expected, result.Status);
        Assert.Empty(result.Diagnostics);
    }
}
