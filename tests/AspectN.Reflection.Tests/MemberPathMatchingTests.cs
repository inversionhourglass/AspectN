using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class MemberPathMatchingTests
{
    [Theory]
    [InlineData("method(* int[,].Get(..))", MatchStatus.Matched)]
    [InlineData("method(* *.Get(..))", MatchStatus.Matched)]
    [InlineData("method(* (int[,] || string[,]).Get(..))", MatchStatus.Matched)]
    [InlineData("method(* !int[,].Get(..))", MatchStatus.NotMatched)]
    [InlineData("method(* int[].Get(..))", MatchStatus.NotMatched)]
    public void Match_RuntimeArrayMethod_UsesArrayDeclaringType(string source, MatchStatus expected)
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapMethod(typeof(int[,]).GetMethod("Get")!);
        Assert.Equal(expected, Compile(source).Match(candidate, typeSystem).Status);
    }

    [Theory]
    [InlineData("method(* int?.GetValueOrDefault(..))", MatchStatus.Matched)]
    [InlineData("method(* System.Nullable<int>.GetValueOrDefault(..))", MatchStatus.Matched)]
    [InlineData("method(* (int? || long?).(GetValueOrDefault || ToString)(..))", MatchStatus.Matched)]
    [InlineData("method(* !int?.GetValueOrDefault(..))", MatchStatus.NotMatched)]
    public void Match_RuntimeNullableMethod_UsesNullableDeclaringType(string source, MatchStatus expected)
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapMethod(typeof(int?).GetMethod("GetValueOrDefault", Type.EmptyTypes)!);
        Assert.Equal(expected, Compile(source).Match(candidate, typeSystem).Status);
    }

    private static CompiledMemberPattern Compile(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!;
    }
}
