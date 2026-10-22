namespace AspectN.Tests.Caching;

public sealed class SingleLanguageCompilationTests
{
    [Fact]
    public void CompileTypePattern_CompilationOptionsInSecondPosition_UsesOptionsAndCache()
    {
        var engine = new AspectNEngine();
        var options = new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(regexTimeout: TimeLimit.Finite(TimeSpan.FromMilliseconds(10))));
        var compilation = engine.CompileTypePattern("Name", compilationOptions: options);

        Assert.True(compilation.TryGetTypePattern(out var pattern));
        Assert.Same(options, pattern!.Options);
        Assert.Same(compilation, engine.CompileTypePattern("Name", compilationOptions: options));
    }
}
