using System.Reflection;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ResourceBudgetTests
{
    [Fact]
    public void MapType_TinyWork_StopsBeforeReadingWholeDeclaringChain()
    {
        var reads = new Counter();
        Type current = typeof(object);
        for (var index = 0; index < 10_000; index++) current = new CountingType(current, reads);
        var mapper = new ReflectionSignatureMapper(new AspectNMappingOptions(maximumTypeDepth: ResourceLimit.Unlimited,
            maximumWork: ResourceLimit.Finite(2)));
        Assert.Equal("AN3008", Assert.Throws<AspectNLimitException>(() => mapper.MapType(current)).Diagnostic.Code);
        Assert.True(reads.Value <= 2, $"Read {reads.Value} declaring links before stopping.");
    }

    private sealed class Counter { public int Value; }
    private sealed class CountingType(Type declaring, Counter counter) : TypeDelegator(typeof(object))
    {
        public override Type? DeclaringType { get { counter.Value++; return declaring == typeof(object) ? null : declaring; } }
        public override string Name => "Level";
    }
}
