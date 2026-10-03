using System.Reflection;
using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class AssignablePatternMatchingTests
{
    [Fact]
    public void MemberPattern_StandardNullableReferenceArgumentInAssignableTarget_MatchesUnderlyingType()
    {
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(IEnumerable<string?>+ ReflectionAssignableFixture.Items())",
                nameof(ReflectionAssignableFixture.Items)));
    }

    [Fact]
    public void MemberPattern_StandardNullableValueArgumentInAssignableTarget_RejectsMismatchedArgument()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(IEnumerable<int?>+ ReflectionAssignableFixture.Items())",
                nameof(ReflectionAssignableFixture.Items)));
    }

    [Fact]
    public void MemberPattern_StandardNonNullableArgument_MatchesAsBefore()
    {
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(IEnumerable<string>+ ReflectionAssignableFixture.Items())",
                nameof(ReflectionAssignableFixture.Items)));
    }

    [Fact]
    public void MemberPattern_OmittedGenericClause_RejectsUnrelatedType()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(Dictionary+ ReflectionAssignableFixture.Items())",
                nameof(ReflectionAssignableFixture.Items)));
    }

    [Fact]
    public void MemberPattern_StandardOmittedGenericClause_RequiresArityZero()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(Dictionary+ ReflectionAssignableFixture.GetMap())",
                nameof(ReflectionAssignableFixture.GetMap)));
    }

    [Theory]
    [InlineData("int<>+")]
    [InlineData("int<..>+")]
    [InlineData("System.Int32<>+")]
    [InlineData("System.Int32<..>+")]
    public void MemberPattern_ExplicitArityClauseOnAliasAssignableTarget_IsEnforced(
        string returnType)
    {
        // 审计 #4 回归：别名具体化不得丢弃显式泛型约束——
        // int<>+ / int<..>+ 曾错误命中 int，修复后与 System.Int32<>+ 一致拒绝。
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                $"method({returnType} ReflectionAssignableFixture.Count())",
                nameof(ReflectionAssignableFixture.Count)));
    }

    [Theory]
    [InlineData("int+")]
    [InlineData("int<!>+")]
    [InlineData("int<?>+")]
    public void MemberPattern_ZeroArityClauseOnAliasAssignableTarget_StillMatches(
        string returnType)
    {
        // 省略 / <!> / <?>（任意元数含零）与"别名只存在于零元数"相容，保持命中。
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                $"method({returnType} ReflectionAssignableFixture.Count())",
                nameof(ReflectionAssignableFixture.Count)));
    }

    [Theory]
    [InlineData("int+?")]
    [InlineData("int?+")]
    public void MemberPattern_NullableWrappedAssignable_StillMatchesNullable(
        string returnType)
    {
        // 防过紧对照：int+? 是"nullable 包 assignable 的模式"（不是可赋值目标），合法；
        // int?+ 是单层"命名的 nullable"目标，白名单放行。两者都应命中 int? 返回。
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                $"method({returnType} ReflectionAssignableFixture.Maybe())",
                nameof(ReflectionAssignableFixture.Maybe)));
    }

    [Fact]
    public void MemberPattern_ArrayInsideGenericArguments_IsStillLegalTarget()
    {
        // 防过紧对照：白名单只判定目标节点，命名目标的实参可含数组/nullable。
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(System.Collections.Generic.List<int[]>+ ReflectionAssignableFixture.Jagged())",
                nameof(ReflectionAssignableFixture.Jagged)));
    }

    private static MatchStatus Match(
        string expression,
        string methodName)
    {
        var typeSystem = new ReflectionTypeSystem();
        var method = typeof(ReflectionAssignableFixture).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance) ??
            throw new InvalidOperationException(
                $"Missing fixture method {methodName}.");
        var candidate = typeSystem.Mapper.MapMethod(method);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(expression));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        return pattern!.Match(candidate, typeSystem).Status;
    }
}

internal sealed class ReflectionAssignableFixture
{
    private int _value;

    public List<string> Items()
    {
        _value++;
        return new List<string>();
    }

    public Dictionary<string, int> GetMap()
    {
        _value++;
        return new Dictionary<string, int>();
    }

    public int Count()
    {
        _value++;
        return _value;
    }

    public int[] Digits()
    {
        _value++;
        return new[] { _value };
    }

    public int? Maybe()
    {
        _value++;
        return _value;
    }

    public List<int[]> Jagged()
    {
        _value++;
        return new List<int[]>();
    }
}
