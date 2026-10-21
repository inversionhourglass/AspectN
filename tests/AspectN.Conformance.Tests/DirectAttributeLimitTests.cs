using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class DirectAttributeLimitTests
{
    [Theory]
    [InlineData(1, 62, false)]
    [InlineData(1, 63, true)]
    [InlineData(1, 65, true)]
    [InlineData(64, 0, false)]
    [InlineData(65, 0, true)]
    public void HasDirectAttribute_NativeAttributeLimit_AgreesAcrossBackends(int arity, int arrayLayers, bool exceedsLimit)
    {
        var slots = string.Join(",", Enumerable.Range(0, arity).Select(index => "T" + index));
        var argument = "int" + string.Concat(Enumerable.Repeat("[]", arrayLayers));
        var arguments = string.Join(",", Enumerable.Repeat(argument, arity));
        using var fixture = new ReviewFixture($$"""
            namespace Review;
            public class Marker<{{slots}}> : System.Attribute { }
            [Marker<{{arguments}}>] public class Marked { }
            public class Empty { }
            public class Fixture
            {
                public Marked Target() => null;
                public Empty Unmarked() => null;
                public Marker<{{arguments}}> AttributeType() => null;
            }
            """);
        var targets = fixture.Methods("Target").ToDictionary(item => item.Backend, item => item.Method.ReturnType.Type);
        var empty = fixture.Methods("Unmarked").ToDictionary(item => item.Backend, item => item.Method.ReturnType.Type);
        var code = arrayLayers > 0 ? DiagnosticCodes.MetadataDepthExceeded : DiagnosticCodes.MetadataGenericArityExceeded;

        foreach (var (backend, system, mapAttributeType) in fixture.MethodMappings("AttributeType"))
        {
            TypeSignature query = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity(""), "Review", "Unrelated", 0, TypeKind.Unknown));
            if (exceedsLimit)
            {
                // 同一原生类型由显式 Mapper 消费时仍通过异常拒绝。
                var exception = Assert.Throws<AspectNLimitException>(mapAttributeType);
                Assert.Equal(code, exception.Diagnostic.Code);
            }
            else query = mapAttributeType().ReturnType.Type;

            var result = system.HasDirectAttribute(AttributeTarget.ForType(targets[backend]), query);
            Assert.True(result.Status == (exceedsLimit ? MatchStatus.Unknown : MatchStatus.Matched), backend);
            if (exceedsLimit) Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
            else Assert.Empty(result.Diagnostics);
            var absent = system.HasDirectAttribute(AttributeTarget.ForType(empty[backend]), query);
            Assert.True(absent.Status == MatchStatus.NotMatched, backend);
            Assert.Empty(absent.Diagnostics);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => system.HasDirectAttribute(
                AttributeTarget.ForType(targets[backend]), query, cancellation.Token));
        }
    }
}
