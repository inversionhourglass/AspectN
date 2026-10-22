using AspectN.Diagnostics;
using AspectN.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspectN.Roslyn.Tests;

public class ResourceBudgetTests
{
    [Fact]
    public void MapType_ErrorSymbol_EnforcesGeneratedTextLimit()
    {
        var compilation = CSharpCompilation.Create("ResourceText");
        var mapper = new RoslynSignatureMapper(compilation, new AspectNMappingOptions(
            maximumGeneratedTextLength: ResourceLimit.Finite(128)));
        var error = compilation.CreateErrorTypeSymbol(null, new string('E', 129), 0);

        var exception = Assert.Throws<AspectNLimitException>(() => mapper.MapType(error));

        Assert.Equal(DiagnosticCodes.ResourceLimitExceeded, exception.Diagnostic.Code);
        Assert.Contains("MaximumGeneratedTextLength", exception.Diagnostic.Message);
    }

    [Fact]
    public void MapMethod_SourceBodyScan_ConsumesMappingWork()
    {
        var compilation = CSharpCompilation.Create("ResourceBody",
            [CSharpSyntaxTree.ParseText("class C { public void M() { " + new string(';', 100) + " } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var method = (IMethodSymbol)compilation.GetTypeByMetadataName("C")!.GetMembers("M").Single();
        var mapper = new RoslynSignatureMapper(compilation, new AspectNMappingOptions(
            maximumWork: ResourceLimit.Finite(80)));

        var exception = Assert.Throws<AspectNLimitException>(() => mapper.MapMethod(method));

        Assert.Equal(DiagnosticCodes.ResourceLimitExceeded, exception.Diagnostic.Code);
        Assert.Contains("MaximumWork", exception.Diagnostic.Message);
    }
}
