using System.Diagnostics.CodeAnalysis;

namespace AspectN.Tests;

public sealed class CompilationFlowTests
{
    [Theory]
    [InlineData(nameof(AspectNCompilation.TryGetMemberPattern))]
    [InlineData(nameof(AspectNCompilation.TryGetTypePattern))]
    public void TryGetPattern_SuccessContract_DeclaresNonNullOutput(string methodName)
    {
        var parameter = Assert.Single(typeof(AspectNCompilation).GetMethod(methodName)!.GetParameters());
        var attribute = Assert.Single(parameter.GetCustomAttributes(typeof(NotNullWhenAttribute), false));

        Assert.True(Assert.IsType<NotNullWhenAttribute>(attribute).ReturnValue);
    }
}
