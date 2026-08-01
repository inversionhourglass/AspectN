using AspectN.Evaluation;

namespace AspectN.Tests.Evaluation;

public sealed class NamePatternMatcherTests
{
    [Theory]
    [InlineData("*", "Service", true)]
    [InlineData("*Service", "OrderService", true)]
    [InlineData("Service*", "ServiceFactory", true)]
    [InlineData("S*vi*e", "Service", true)]
    [InlineData("*Service", "ServiceFactory", false)]
    [InlineData("Service", "service", false)]
    [InlineData("", "", true)]
    [InlineData("", "Service", false)]
    public void IsMatch_WildcardPattern_ReturnsExpected(
        string pattern,
        string candidate,
        bool expected)
    {
        Assert.Equal(expected, NamePatternMatcher.IsMatch(pattern, candidate));
    }
}
