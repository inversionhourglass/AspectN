using System.Reflection;
using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class AssignablePatternMatchingTests
{
    private static readonly AspectNParseOptions _Legacy =
        new(AspectNLanguageVersion.Legacy);

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
    public void MemberPattern_LegacyOmittedGenericClauseInAssignableTarget_RequiresNonGenericType()
    {
        // 用户批准 2026-10-01：Legacy genericIn 裸单段名省略子句表示零泛型
        // （对齐 Shared 的 SingleNonGenericNamePattern），Dictionary+ 不再匹配
        // 构造类型 Dictionary<,>；Standard 语义与此一致。
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(Dictionary+ ReflectionAssignableFixture.GetMap())",
                nameof(ReflectionAssignableFixture.GetMap),
                _Legacy));
    }

    [Fact]
    public void MemberPattern_LegacyOmittedGenericClause_RejectsUnrelatedType()
    {
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(Dictionary+ ReflectionAssignableFixture.Items())",
                nameof(ReflectionAssignableFixture.Items),
                _Legacy));
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
    [InlineData("int<>+", AspectNLanguageVersion.Standard)]
    [InlineData("int<>+", AspectNLanguageVersion.Legacy)]
    [InlineData("int<..>+", AspectNLanguageVersion.Standard)]
    [InlineData("int<..>+", AspectNLanguageVersion.Legacy)]
    [InlineData("System.Int32<>+", AspectNLanguageVersion.Standard)]
    [InlineData("System.Int32<..>+", AspectNLanguageVersion.Legacy)]
    public void MemberPattern_ExplicitArityClauseOnAliasAssignableTarget_IsEnforced(
        string returnType,
        AspectNLanguageVersion version)
    {
        // 审计 #4 回归：别名具体化不得丢弃显式泛型约束——
        // int<>+ / int<..>+ 曾错误命中 int，修复后与 System.Int32<>+ 一致拒绝。
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                $"method({returnType} ReflectionAssignableFixture.Count())",
                nameof(ReflectionAssignableFixture.Count),
                new AspectNParseOptions(version)));
    }

    [Theory]
    [InlineData("int+", AspectNLanguageVersion.Standard)]
    [InlineData("int+", AspectNLanguageVersion.Legacy)]
    [InlineData("int<!>+", AspectNLanguageVersion.Standard)]
    [InlineData("int<!>+", AspectNLanguageVersion.Legacy)]
    [InlineData("int<?>+", AspectNLanguageVersion.Standard)]
    public void MemberPattern_ZeroArityClauseOnAliasAssignableTarget_StillMatches(
        string returnType,
        AspectNLanguageVersion version)
    {
        // 省略 / <!> / <?>（任意元数含零）与"别名只存在于零元数"相容，保持命中。
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                $"method({returnType} ReflectionAssignableFixture.Count())",
                nameof(ReflectionAssignableFixture.Count),
                new AspectNParseOptions(version)));
    }

    private static MatchStatus Match(
        string expression,
        string methodName,
        AspectNParseOptions? options = null)
    {
        var typeSystem = new ReflectionTypeSystem();
        var method = typeof(ReflectionAssignableFixture).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance) ??
            throw new InvalidOperationException(
                $"Missing fixture method {methodName}.");
        var candidate = typeSystem.Mapper.MapMethod(method);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(expression, options));
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
}
