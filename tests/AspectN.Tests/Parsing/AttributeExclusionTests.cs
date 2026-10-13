using AspectN.Diagnostics;

namespace AspectN.Tests.Parsing;

public sealed class AttributeExclusionTests
{
    [Theory]
    [InlineData("callable", "call")]
    [InlineData("property", "prop")]
    [InlineData("parameter[0]", "para[0]")]
    [InlineData("parameter 0", "para 0")]
    [InlineData("parameter[*]", "para[*]")]
    [InlineData("parameter *", "para *")]
    [InlineData("return", "ret")]
    public void CompileMemberExpression_LongAttributePosition_RejectsWithMigration(
        string oldPosition, string shortPosition)
    {
        var engine = new AspectNEngine();
        var rejected = engine.CompileMemberExpression($"attr({oldPosition} Review.MarkerAttribute)");

        Assert.False(rejected.TryGetMemberPattern(out _));
        var diagnostic = Assert.Single(rejected.Diagnostics,
            value => value.Code == DiagnosticCodes.AttributePositionMigration);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        var shortName = shortPosition.Split('[', ' ')[0];
        Assert.Contains($"'{shortName}'", diagnostic.Message, StringComparison.Ordinal);

        var accepted = engine.CompileMemberExpression($"attr({shortPosition} Review.MarkerAttribute)");

        Assert.True(accepted.TryGetMemberPattern(out _));
        Assert.Empty(accepted.Diagnostics);
    }

    [Theory]
    [InlineData("get", "getter")]
    [InlineData("set", "setter")]
    public void CompileMemberExpression_AccessorAttributePosition_RequiresSelectorComposition(
        string rejectedPosition, string selector)
    {
        var engine = new AspectNEngine();
        var rejected = engine.CompileMemberExpression($"attr({rejectedPosition} Review.MarkerAttribute)");

        Assert.False(rejected.TryGetMemberPattern(out _));
        Assert.Contains(rejected.Diagnostics, value =>
            value.Code == DiagnosticCodes.UnexpectedToken && value.Severity == DiagnosticSeverity.Error);

        var accepted = engine.CompileMemberExpression(
            $"{selector}(int Value) && attr(call Review.MarkerAttribute)");

        Assert.True(accepted.TryGetMemberPattern(out _));
        Assert.Empty(accepted.Diagnostics);
    }
}
