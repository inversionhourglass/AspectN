using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class QueryTargetContractTests
{
    private const string SOURCE = """
        using System;
        namespace QueryContracts;
        public sealed class MarkerAttribute<T> : Attribute { }
        public class Box<T> { }
        public class Outer<T> { public class Inner<U> { } }
        public class Fixture
        {
            [Marker<int>] public Box<int> M() => null;
            [Marker<int[,]>] public Box<int[,]> Matrix() => null;
            [Marker<int[]>] public Box<int[]> Vector() => null;
            public Outer<int>.Inner<string> Nested() => null;
        }
        """;

    [Theory]
    [InlineData("unqualified", MatchStatus.Matched)]
    [InlineData("namespace", MatchStatus.Matched)]
    [InlineData("assembly", MatchStatus.Matched)]
    [InlineData("kind", MatchStatus.Matched)]
    [InlineData("full", MatchStatus.Matched)]
    [InlineData("argument-unqualified", MatchStatus.Matched)]
    [InlineData("wrong-assembly", MatchStatus.NotMatched)]
    [InlineData("wrong-version", MatchStatus.NotMatched)]
    [InlineData("wrong-namespace", MatchStatus.NotMatched)]
    [InlineData("wrong-kind", MatchStatus.NotMatched)]
    [InlineData("wrong-name", MatchStatus.NotMatched)]
    [InlineData("wrong-arity", MatchStatus.NotMatched)]
    [InlineData("wrong-argument", MatchStatus.NotMatched)]
    [InlineData("wrong-containing", MatchStatus.NotMatched)]
    public void Query_IdentityConditions_AllViewsAgree(string variant, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, "QueryContracts.Fixture");
        foreach (var (backend, system, method) in fixture.Methods("M"))
        {
            var actual = Assert.IsType<NamedTypeSignature>(method.ReturnType.Type);
            var attribute = Assert.IsType<NamedTypeSignature>(Assert.Single(method.Attributes).AttributeType);
            AssertResult(system.IsAssignableTo(actual, Query(actual, variant)), expected, backend);
            AssertResult(system.HasDirectAttribute(AttributeTarget.ForMethod(method), Query(attribute, variant)), expected, backend);
            var equality = variant == "full" ? MatchStatus.Matched : MatchStatus.NotMatched;
            AssertResult(system.AreEqual(actual, Query(actual, variant)), equality, backend);
            AssertResult(system.AreEqual(Query(actual, variant), actual), equality, backend);
        }
    }

    [Theory]
    [InlineData("Matrix", "same", MatchStatus.Matched)]
    [InlineData("Matrix", "rank", MatchStatus.NotMatched)]
    [InlineData("Vector", "same", MatchStatus.Matched)]
    [InlineData("Vector", "rank", MatchStatus.NotMatched)]
    [InlineData("Vector", "shape", MatchStatus.NotMatched)]
    public void Query_ArrayArguments_PreserveRankAndShape(string member, string variant, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, "QueryContracts.Fixture");
        foreach (var (backend, system, method) in fixture.Methods(member))
        {
            var actual = Assert.IsType<NamedTypeSignature>(method.ReturnType.Type);
            var attribute = Assert.IsType<NamedTypeSignature>(Assert.Single(method.Attributes).AttributeType);
            NamedTypeSignature Target(NamedTypeSignature type)
            {
                var query = Query(type, "unqualified");
                var array = Assert.IsType<ArrayTypeSignature>(Assert.Single(query.TypeArguments));
                var argument = variant switch
                {
                    "rank" => new ArrayTypeSignature(array.ElementType, 3),
                    "shape" => new ArrayTypeSignature(array.ElementType, 1, ArrayShape.General),
                    _ => array
                };
                return new NamedTypeSignature(query.Identity, [argument]);
            }
            AssertResult(system.IsAssignableTo(actual, Target(actual)), expected, backend);
            AssertResult(system.HasDirectAttribute(AttributeTarget.ForMethod(method), Target(attribute)), expected, backend);
        }
    }

    [Theory]
    [InlineData(false, MatchStatus.Matched)]
    [InlineData(true, MatchStatus.NotMatched)]
    public void Query_NestedIdentity_PreservesContainingChain(bool wrongOwner, MatchStatus expected)
    {
        using var fixture = new ReviewFixture(SOURCE, "QueryContracts.Fixture");
        foreach (var (backend, system, method) in fixture.Methods("Nested"))
        {
            var actual = Assert.IsType<NamedTypeSignature>(method.ReturnType.Type);
            var owner = actual.Identity.ContainingType!;
            var outer = new TypeIdentity(new AssemblyIdentity(""), "", wrongOwner ? "Missing" : owner.MetadataName,
                owner.GenericArity, TypeKind.Unknown);
            var query = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity(""), "", actual.Identity.MetadataName,
                actual.Identity.GenericArity, TypeKind.Unknown, outer), actual.TypeArguments);
            AssertResult(system.IsAssignableTo(actual, query), expected, backend);
        }
    }

    [Fact]
    public void Match_ExactAndWildcardQueries_AllViewsAgree()
    {
        using var fixture = new ReviewFixture(SOURCE, "QueryContracts.Fixture");
        foreach (var (backend, system, method) in fixture.Methods("M"))
        {
            foreach (var source in new[] { "Box<int>+", "QueryContracts.Box<int>+", "QueryContracts.Bo*<int>+" })
            {
                Assert.True(new AspectNEngine().CompileTypePattern(source).TryGetTypePattern(out var pattern));
                AssertResult(pattern.Match(method.ReturnType.Type, system), MatchStatus.Matched, backend);
            }
            foreach (var source in new[] { "attr(call MarkerAttribute<int>)", "attr(call QueryContracts.MarkerAttribute<int>)", "attr(call QueryContracts.Marker*<int>)" })
            {
                Assert.True(new AspectNEngine().CompileMemberExpression(source).TryGetMemberPattern(out var pattern));
                AssertResult(pattern.Match(method, system), MatchStatus.Matched, backend);
            }
        }
    }

    private static NamedTypeSignature Query(NamedTypeSignature actual, string variant)
    {
        var identity = actual.Identity;
        var assembly = variant is "assembly" or "full" ? identity.Assembly
            : variant == "wrong-assembly" ? new AssemblyIdentity("Other")
            : variant == "wrong-version" ? new AssemblyIdentity(identity.Assembly.Name, new Version(99, 0),
                identity.Assembly.CultureName, identity.Assembly.PublicKeyToken) : new AssemblyIdentity("");
        var space = variant is "namespace" or "full" ? identity.Namespace : variant == "wrong-namespace" ? "Other" : "";
        var kind = variant is "kind" or "full" ? identity.Kind : variant == "wrong-kind" ? TypeKind.Struct : TypeKind.Unknown;
        var name = variant == "wrong-name" ? identity.MetadataName.ToLowerInvariant() : identity.MetadataName;
        var arity = variant == "wrong-arity" ? identity.GenericArity + 1 : identity.GenericArity;
        var owner = variant == "wrong-containing"
            ? new TypeIdentity(new AssemblyIdentity(""), "", "Missing", 0, TypeKind.Unknown) : null;
        var arguments = variant == "wrong-arity" ? [] : actual.TypeArguments.ToArray();
        if (variant is "wrong-argument" or "argument-unqualified")
        {
            var argument = Assert.IsType<NamedTypeSignature>(Assert.Single(actual.TypeArguments));
            arguments = [new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity(""), "",
                variant == "wrong-argument" ? "String" : argument.Identity.MetadataName, 0, TypeKind.Unknown))];
        }
        return new NamedTypeSignature(new TypeIdentity(assembly, space, name, arity, kind, owner), arguments);
    }

    private static void AssertResult(TypeRelationResult result, MatchStatus expected, string backend)
    {
        Assert.True(result.Status == expected, backend);
        Assert.Empty(result.Diagnostics);
    }

    private static void AssertResult(MatchResult result, MatchStatus expected, string backend)
    {
        Assert.True(result.Status == expected, backend);
        Assert.Empty(result.Diagnostics);
    }
}
