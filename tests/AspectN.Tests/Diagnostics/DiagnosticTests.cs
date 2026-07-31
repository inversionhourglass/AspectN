using AspectN.Diagnostics;
using AspectN.Text;

namespace AspectN.Tests.Diagnostics;

public sealed class DiagnosticTests
{
    [Fact]
    public void Diagnostic_Stores_Stable_Code_And_Span()
    {
        var diagnostic = new AspectNDiagnostic(
            DiagnosticCodes.UnexpectedToken,
            DiagnosticSeverity.Error,
            "Expected ')'.",
            new TextSpan(12, 0));

        Assert.Equal("AN1001", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Expected ')'.", diagnostic.Message);
        Assert.Equal(new TextSpan(12, 0), diagnostic.Span);
    }
}
