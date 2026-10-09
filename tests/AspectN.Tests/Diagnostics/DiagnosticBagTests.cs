using AspectN.Diagnostics;
using AspectN.Text;

namespace AspectN.Tests.Diagnostics;

public sealed class DiagnosticBagTests
{
    [Fact]
    public void ToArray_AfterAdditionalDiagnostic_ReturnsOriginalSnapshot()
    {
        var bag = new DiagnosticBag();
        bag.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            "Expected ')'.",
            new TextSpan(12, 0));

        var snapshot = bag.ToArray();
        bag.Add(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            "Expected ','.",
            new TextSpan(13, 0));

        Assert.Single(snapshot);
        Assert.Equal("Expected ')'.", snapshot[0].Message);
        Assert.Equal(2, bag.ToArray().Length);
    }
}
