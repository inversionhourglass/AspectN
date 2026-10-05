using AspectN.Evaluation;
using AspectN.Metadata;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class GenericConstraintConformanceTests
{
    private const string SOURCE = """
        namespace Review;
        public class Base : System.IDisposable { public void Dispose() {} }
        public class Fixture
        {
            public void Unconstrained<T>(T value) {}
            public void Interface<T>(T value) where T : System.IDisposable {}
            public void Class<T>(T value) where T : Base {}
            public void Value<T>(T value) where T : struct {}
            public void Transitive<T, U>(T value) where T : U where U : Base {}
            public void Multiple<T>(T value) where T : System.IDisposable, System.IComparable {}
        }
        """;

    [Theory]
    [InlineData("Unconstrained", "System.Object", true)]
    [InlineData("Unconstrained", "System.IDisposable", false)]
    [InlineData("Interface", "System.Object", true)]
    [InlineData("Interface", "System.IDisposable", true)]
    [InlineData("Class", "Review.Base", true)]
    [InlineData("Class", "System.IDisposable", true)]
    [InlineData("Value", "System.ValueType", true)]
    [InlineData("Value", "System.Object", true)]
    [InlineData("Transitive", "System.IDisposable", true)]
    [InlineData("Multiple", "System.IComparable", true)]
    public void GenericParameter_DeclaredConstraints_AgreeAcrossBackends(string methodName, string targetName, bool matches)
    {
        using var fixture = new ReviewFixture(SOURCE);
        var split = targetName.LastIndexOf('.');
        var target = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity(""), targetName[..split],
            targetName[(split + 1)..], 0, TypeKind.Unknown));
        foreach (var (backend, system, method) in fixture.Methods(methodName))
        {
            var candidate = method.Parameters[0].Type.Type;
            var expected = matches ? MatchStatus.Matched : MatchStatus.NotMatched;
            Assert.True(system.IsAssignableTo(candidate, target).Status == expected, backend);
            var compilation = new AspectNEngine().CompileMemberExpression($"method(* {methodName}<..>( {targetName}+ ))");
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.True(pattern!.Match(method, system).Status == expected, backend + " concrete pattern");
            var closure = system.GetInheritanceClosure(candidate);
            Assert.Null(closure.Failure);
            Assert.Equal(matches, closure.Ancestors.Any(ancestor => system.IsAssignableTo(ancestor, target).Status == MatchStatus.Matched));
        }
    }
}
