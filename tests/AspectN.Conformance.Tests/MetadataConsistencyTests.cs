using AspectN.Evaluation;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class MetadataConsistencyTests
{
    private const string SOURCE = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        namespace Review;
        public class Fixture
        {
            public void Dynamic<T>(dynamic value) {}
            public void NestedDynamic<T>(List<dynamic[]> value) {}
            public unsafe void PointerDynamic<T>(delegate*<dynamic,T> value) {}
            public unsafe void Pointer<T>(int* value) {}
            public unsafe void Managed<T>(delegate*<int,void> value) {}
            public unsafe void Cdecl<T>(delegate* unmanaged[Cdecl]<int,void> value) {}
            public unsafe void Stdcall<T>(delegate* unmanaged[Stdcall]<int,void> value) {}
            public unsafe void Suppressed<T>(delegate* unmanaged[Cdecl,SuppressGCTransition]<int,void> value) {}
            public async Task<int> Async() { await Task.Yield(); return 1; }
            public Task<int> NonAsync() => Task.FromResult(1);
            public Dictionary<string,List<int>> NestedGeneric() => new();
            public async IAsyncEnumerable<int> Iterator() { await Task.Yield(); yield return 1; }
            public IEnumerable<int> SyncIterator() { yield return 1; }
            public IEnumerable<int> LocalIterator()
            {
                IEnumerable<int> Inner() { yield return 1; }
                return Inner();
            }
        }
        """;

    [Theory]
    [InlineData("Dynamic")]
    [InlineData("NestedDynamic")]
    [InlineData("PointerDynamic")]
    [InlineData("Pointer")]
    [InlineData("Managed")]
    [InlineData("Cdecl")]
    [InlineData("Stdcall")]
    [InlineData("Suppressed")]
    public void GenericOwnerId_EquivalentCliParameterForms_AgreeAcrossBackends(string methodName)
    {
        using var fixture = new ReviewFixture(SOURCE, allowUnsafe: true);
        var methods = fixture.Methods(methodName).ToArray();
        var expected = methods[0].Method.GenericParameters.Single();
        foreach (var (backend, _, method) in methods.Skip(1))
        {
            var actual = method.GenericParameters.Single();
            Assert.True(expected.OwnerId == actual.OwnerId, backend + ": " + actual.OwnerId);
            Assert.Equal(expected.OwnerAssembly, actual.OwnerAssembly);
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.OwnerKind, actual.OwnerKind);
        }
        Assert.DoesNotContain("dynamic", expected.OwnerId);
    }

    [Theory]
    [InlineData("Async", true, MatchStatus.Matched)]
    [InlineData("NonAsync", false, MatchStatus.Matched)]
    [InlineData("Iterator", true, MatchStatus.NotMatched)]
    public void AsyncFlag_CompilerAsyncMethodsIncludingIterators_IsIndependentOfTasklike(
        string methodName, bool isAsync, MatchStatus tasklike)
    {
        using var fixture = new ReviewFixture(SOURCE, allowUnsafe: true);
        var compilation = new AspectNEngine().CompileMemberExpression($"method(tasklike<int> {methodName}())");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        foreach (var (backend, system, method) in fixture.Methods(methodName))
        {
            Assert.True(method.IsAsync == isAsync, backend);
            Assert.True(pattern!.Match(method, system).Status == tasklike, backend + " tasklike");
        }
    }

    [Theory]
    [InlineData("Async", "AsyncStateMachineAttribute", MatchStatus.Matched)]
    [InlineData("Async", "AsyncIteratorStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("Iterator", "AsyncIteratorStateMachineAttribute", MatchStatus.Matched)]
    [InlineData("Iterator", "AsyncStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("SyncIterator", "IteratorStateMachineAttribute", MatchStatus.Matched)]
    [InlineData("NonAsync", "AsyncStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("LocalIterator", "IteratorStateMachineAttribute", MatchStatus.NotMatched)]
    public void Attribute_CompilerStateMachineMarker_AgreesAcrossBackends(
        string methodName, string attributeName, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, allowUnsafe: true);
        var engine = new AspectNEngine();
        var expression = $"attr(call System.Runtime.CompilerServices.{attributeName})";
        Assert.True(engine.CompileMemberExpression(expression).TryGetMemberPattern(out var pattern));
        Assert.True(engine.CompileMemberExpression("!" + expression).TryGetMemberPattern(out var negated));
        Assert.True(engine.CompileMemberExpression($"attr(call *..*.{attributeName})").TryGetMemberPattern(out var wildcard));

        foreach (var (backend, system, method) in fixture.Methods(methodName))
        {
            Assert.True(pattern!.Match(method, system).Status == expected, backend);
            Assert.True(wildcard!.Match(method, system).Status == expected, backend + " wildcard");
            var complement = expected == MatchStatus.Matched ? MatchStatus.NotMatched : MatchStatus.Matched;
            Assert.True(negated!.Match(method, system).Status == complement, backend + " negated");
        }
    }

    [Theory]
    [InlineData("Async", "AsyncStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("Iterator", "IteratorStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("AsyncIterator", "AsyncIteratorStateMachineAttribute", MatchStatus.NotMatched)]
    [InlineData("MarkedAsync", "AsyncStateMachineAttribute", MatchStatus.Matched)]
    public void Attribute_PartialStateMachineImplementation_AgreesWithEmittedMethod(
        string methodName, string attributeName, MatchStatus expected)
    {
        const string source = """
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            namespace Review;
            public partial class Fixture
            {
                public partial Task<int> Async();
                public async partial Task<int> Async() { await Task.Yield(); return 1; }
                public partial IEnumerable<int> Iterator();
                public partial IEnumerable<int> Iterator() { yield return 1; }
                public partial IAsyncEnumerable<int> AsyncIterator();
                public async partial IAsyncEnumerable<int> AsyncIterator() { await Task.Yield(); yield return 1; }
                [AsyncStateMachine(typeof(Fixture))]
                public partial Task<int> MarkedAsync();
                public async partial Task<int> MarkedAsync() { await Task.Yield(); return 1; }
            }
            """;
        using var fixture = new ReviewFixture(source);
        var methods = fixture.Methods(methodName).ToList();
        var definition = fixture.Source.Compilation.GetTypeByMetadataName("Review.Fixture")!
            .GetMembers(methodName).OfType<IMethodSymbol>().Single();
        var implementation = Assert.IsAssignableFrom<IMethodSymbol>(definition.PartialImplementationPart);
        var syntax = Assert.IsType<MethodDeclarationSyntax>(implementation.DeclaringSyntaxReferences.Single().GetSyntax());
        methods.Add(("Roslyn implementation", fixture.Source,
            fixture.Source.Mapper.MapMethod(syntax, fixture.Source.Compilation)));

        var engine = new AspectNEngine();
        var expression = $"attr(call System.Runtime.CompilerServices.{attributeName})";
        Assert.True(engine.CompileMemberExpression(expression).TryGetMemberPattern(out var pattern));
        Assert.True(engine.CompileMemberExpression("!" + expression).TryGetMemberPattern(out var negated));
        foreach (var (backend, system, method) in methods)
        {
            Assert.True(pattern!.Match(method, system).Status == expected, backend);
            var complement = expected == MatchStatus.Matched ? MatchStatus.NotMatched : MatchStatus.Matched;
            Assert.True(negated!.Match(method, system).Status == complement, backend + " negated");
        }
    }

    [Fact]
    public void UnsupportedPointer_DisplayName_UsesTheSameCliForm()
    {
        using var fixture = new ReviewFixture(SOURCE, allowUnsafe: true);
        var types = fixture.Methods("Pointer").Select(value => value.Method.Parameters[0].Type.Type).ToArray();
        foreach (var type in types)
        {
            var unsupported = Assert.IsType<AspectN.Metadata.UnsupportedTypeSignature>(type);
            Assert.Equal("System.Int32*", unsupported.DisplayName);
            Assert.Equal(MatchStatus.Unknown, StructuralTypeComparer.AreEqual(type, types[0]).Status);
        }
    }

    [Theory]
    [InlineData("System.Collections.Generic.Dictionary<string, List>+", MatchStatus.NotMatched)]
    [InlineData("System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>+", MatchStatus.Matched)]
    public void AssignableTarget_OmittedNestedGenericClause_RequiresArityZero(string expression, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, allowUnsafe: true);
        var compilation = new AspectNEngine().CompileTypePattern(expression);
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        foreach (var (backend, system, method) in fixture.Methods("NestedGeneric"))
            Assert.True(pattern!.Match(method.ReturnType.Type, system).Status == expected, backend);
    }
}
