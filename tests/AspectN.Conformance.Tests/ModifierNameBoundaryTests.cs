using AspectN.Evaluation;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ModifierNameBoundaryTests
{
    public static TheoryData<string, int> Cases
    {
        get
        {
            var cases = new TheoryData<string, int>();
            foreach (var keyword in new[] { "static", "public", "private", "protected", "internal" })
                foreach (var mode in new[] { 0, 1, 2 }) cases.Add(keyword, mode);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Match_KeywordPrefixType_SelectsByWholeNamePattern(string keyword, int mode)
    {
        using var fixture = new ReviewFixture($$"""
            namespace Review;
            public class {{keyword}}Widget {}
            public class Fixture
            {
                public {{keyword}}Widget Instance() => null;
                public static string StaticOther() => null;
                public static {{keyword}}Widget StaticPrefix() => null;
            }
            """);
        var typeAndModifiers = mode switch
        {
            1 => "static " + keyword + "*",
            2 => "!" + keyword + "*",
            _ => keyword + "*"
        };
        Assert.True(new AspectNEngine().CompileMemberExpression($"method({typeAndModifiers} *())")
            .TryGetMemberPattern(out var pattern));
        foreach (var name in new[] { "Instance", "StaticOther", "StaticPrefix" })
        {
            var matches = mode switch
            {
                1 => name == "StaticPrefix",
                2 => name == "StaticOther",
                _ => name != "StaticOther"
            };
            foreach (var (backend, system, method) in fixture.Methods(name))
                Assert.True(pattern.Match(method, system).Status == (matches ? MatchStatus.Matched : MatchStatus.NotMatched),
                    $"{backend}: {typeAndModifiers} / {name}");
        }
    }
}
