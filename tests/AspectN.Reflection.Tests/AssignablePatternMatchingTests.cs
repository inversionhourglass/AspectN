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
        var candidate = typeSystem.Mapper.MapCallable(method);
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
}
