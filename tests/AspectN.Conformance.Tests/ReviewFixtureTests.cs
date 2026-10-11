using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ReviewFixtureTests
{
    private static readonly string[] _Backends = ["Roslyn source", "Roslyn PE", "Cecil", "Reflection"];

    [Theory]
    [InlineData("PrivateInstance", Accessibility.Private, false)]
    [InlineData("PrivateStatic", Accessibility.Private, true)]
    [InlineData("InternalInstance", Accessibility.Internal, false)]
    [InlineData("InternalStatic", Accessibility.Internal, true)]
    [InlineData("Public", Accessibility.Public, false)]
    public void Methods_AllVisibilities_MapsEveryBackend(string name, Accessibility accessibility, bool isStatic)
    {
        using var fixture = new ReviewFixture("""
            namespace Review;
            public class Fixture
            {
                private void PrivateInstance() { }
                private static void PrivateStatic() { }
                internal void InternalInstance() { }
                internal static void InternalStatic() { }
                public void Public() { }
            }
            """);
        var methods = fixture.Methods(name).ToArray();

        Assert.Equal(_Backends, methods.Select(value => value.Backend));
        Assert.All(methods, value =>
        {
            Assert.Equal(accessibility, value.Method.Accessibility);
            Assert.Equal(isStatic, value.Method.IsStatic);
            var compilation = new AspectNEngine().CompileMemberExpression($"method(* {name}())");
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.Equal(MatchStatus.Matched, pattern.Match(value.Method, value.System).Status);
        });
    }
}
