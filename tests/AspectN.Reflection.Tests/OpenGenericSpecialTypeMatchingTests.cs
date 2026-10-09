using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class OpenGenericSpecialTypeMatchingTests
{
    [Theory]
    [InlineData(typeof(Task<>), "tasklike<*>", "System.Threading.Tasks.Task<*>")]
    [InlineData(typeof(ValueTask<>), "tasklike<*>", "System.Threading.Tasks.ValueTask<*>")]
    [InlineData(typeof(ValueTuple<,>), "(*,*)", "System.ValueTuple<*,*>")]
    public void Match_OpenGenericDefinition_SpecialAndNamedPatternsAgree(Type type, string special, string named)
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = Assert.IsType<NamedTypeSignature>(typeSystem.Mapper.MapType(type));
        Assert.Empty(candidate.TypeArguments);

        foreach (var source in new[] { special, named })
        {
            var compilation = new AspectNEngine().CompileTypePattern(source);
            Assert.True(compilation.TryGetTypePattern(out var pattern));
            Assert.Equal(MatchStatus.Matched, pattern!.Match(candidate, typeSystem).Status);
        }
    }
}
