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
    [InlineData("Cecil", "owner", 1024)]
    [InlineData("Cecil", "owner-reference", 8192)]
    [InlineData("Cecil", "type-use", 8192)]
    [InlineData("Cecil", "type-use", 64)]
    [InlineData("Cecil", "array", 63)]
    [InlineData("Cecil", "array", 64)]
    [InlineData("Reflection", "array", 63)]
    [InlineData("Reflection", "array", 64)]
    [InlineData("Roslyn", "array", 63)]
    [InlineData("Roslyn", "array", 64)]
    [InlineData("Cecil", "containing", 64)]
    [InlineData("Cecil", "containing", 65)]
    [InlineData("Reflection", "containing", 64)]
    [InlineData("Reflection", "containing", 65)]
    [InlineData("Roslyn", "containing", 64)]
    [InlineData("Roslyn", "containing", 65)]
    [InlineData("Cecil", "generic", 63)]
    [InlineData("Cecil", "generic", 64)]
    [InlineData("Reflection", "generic", 63)]
    [InlineData("Reflection", "generic", 64)]
    [InlineData("Roslyn", "generic", 63)]
    [InlineData("Roslyn", "generic", 64)]
    [InlineData("Cecil", "pointer", 62)]
    [InlineData("Cecil", "pointer", 63)]
    [InlineData("Reflection", "pointer", 62)]
    [InlineData("Reflection", "pointer", 63)]
    [InlineData("Roslyn", "pointer", 62)]
    [InlineData("Roslyn", "pointer", 63)]
    [InlineData("Cecil", "owner", 64)]
    [InlineData("Cecil", "owner", 65)]
    [InlineData("Cecil", "by-reference", 62)]
    [InlineData("Cecil", "by-reference", 63)]
    [InlineData("Reflection", "by-reference", 62)]
    [InlineData("Reflection", "by-reference", 63)]
    public async Task MapType_NativeShapeOnSmallStack_EnforcesLimitsInIsolatedProcess(string backend, string shape, int depth)
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
        var limited = shape != "hidden-pointer" && (shape is "containing" or "owner" or "owner-reference" ? depth > 64 :
            shape is "pointer" or "by-reference" ? depth > 62 : depth > 63);
        Assert.Contains($"{(limited ? "limited" : "mapped")}:{backend}:{shape}:{depth}", await output);
    }
}
