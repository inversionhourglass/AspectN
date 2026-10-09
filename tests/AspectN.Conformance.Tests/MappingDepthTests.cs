using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class MappingDepthTests
{
    [Theory]
    [InlineData("Cecil", "array", 1024)]
    [InlineData("Reflection", "array", 1024)]
    [InlineData("Roslyn", "array", 1024)]
    [InlineData("Cecil", "generic", 256)]
    [InlineData("Reflection", "generic", 128)]
    [InlineData("Roslyn", "generic", 256)]
    [InlineData("Cecil", "pointer", 1024)]
    [InlineData("Reflection", "pointer", 1024)]
    [InlineData("Roslyn", "pointer", 1024)]
    [InlineData("Cecil", "containing", 512)]
    [InlineData("Reflection", "containing", 512)]
    [InlineData("Roslyn", "containing", 512)]
    [InlineData("Reflection", "hidden-pointer", 1024)]
    public async Task MapType_DeepNativeShapeOnSmallStack_CompletesInIsolatedProcess(string backend, string shape, int depth)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = directory.Parent!.Name;
        var tests = directory.Parent.Parent!.Parent!.Parent!.FullName;
        var probe = Path.Combine(tests, "AspectN.MappingDepthProbe", "bin", configuration, "net10.0", "AspectN.MappingDepthProbe.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(probe);
        start.ArgumentList.Add(backend);
        start.ArgumentList.Add(shape);
        start.ArgumentList.Add(depth.ToString(CultureInfo.InvariantCulture));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        Assert.True(process.ExitCode == 0, $"{backend}/{shape}: {await output}\n{await error}");
        Assert.Contains($"mapped:{backend}:{shape}:{depth}", await output);
    }
}
