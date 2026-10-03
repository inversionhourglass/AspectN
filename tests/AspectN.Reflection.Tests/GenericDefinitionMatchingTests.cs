using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class GenericDefinitionMatchingTests
{
    [Fact]
    public void MemberPattern_NestedDefinitionGenericClauseVariable_BindsFormalParameter()
    {
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(* GenericDefinitionOuter<>/GenericDefinitionInner<$T>.Get($T))",
                nameof(GenericDefinitionOuter<>.GenericDefinitionInner<>.Get)));
    }

    [Fact]
    public void MemberPattern_NestedDefinitionConcreteArgument_DoesNotMatchFormalParameter()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(* GenericDefinitionOuter<>/GenericDefinitionInner<int>.Get(..))",
                nameof(GenericDefinitionOuter<>.GenericDefinitionInner<>.Get)));
    }

    [Fact]
    public void MemberPattern_RegexSeesSingleConsistentGenericName()
    {
        // 同一形参（扁平槽位 1）在规范签名的声明类型与参数处必须渲染
        // 同一个名字 T2；合成/真实两种形态 Position 不一致时会出现
        // Inner<T2>.Get(TInner) 的双名渲染。
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "regex(Inner<T2>\\.Get\\(T2\\))",
                nameof(GenericDefinitionOuter<>.GenericDefinitionInner<>.Get)));
    }

    [Fact]
    public void MemberPattern_DeclaringTypeGenericClauseAssignableSuffix_MatchesExactType()
    {
        Assert.Equal(
            MatchStatus.Matched,
            MatchConstructed(
                "method(* GenericRepo<int>+.Find(..))",
                nameof(GenericRepo<int>.Find)));
    }

    [Fact]
    public void MemberPattern_DeclaringTypeGenericClauseAssignableSuffix_RejectsOtherArguments()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            MatchConstructed(
                "method(* GenericRepo<string>+.Find(..))",
                nameof(GenericRepo<int>.Find)));
    }

    private static MatchStatus Match(
        string expression,
        string methodName)
    {
        var typeSystem = new ReflectionTypeSystem();
        var method = typeof(GenericDefinitionOuter<>.GenericDefinitionInner<>)
            .GetMethod(methodName) ??
            throw new InvalidOperationException(
                $"Missing fixture method {methodName}.");
        var candidate = typeSystem.Mapper.MapMethod(method);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(expression));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        return pattern!.Match(candidate, typeSystem).Status;
    }

    private static MatchStatus MatchConstructed(
        string expression,
        string methodName)
    {
        var typeSystem = new ReflectionTypeSystem();
        var method = typeof(GenericRepo<int>).GetMethod(methodName) ??
            throw new InvalidOperationException(
                $"Missing fixture method {methodName}.");
        var candidate = typeSystem.Mapper.MapMethod(method);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(expression));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        return pattern!.Match(candidate, typeSystem).Status;
    }
}

internal sealed class GenericDefinitionOuter<TOuter>
{
    internal sealed class GenericDefinitionInner<TInner>
    {
        public static TInner Get(TInner value)
        {
            return value;
        }
    }
}

internal sealed class GenericRepo<T>
{
    public static T Find(T key)
    {
        return key;
    }
}
