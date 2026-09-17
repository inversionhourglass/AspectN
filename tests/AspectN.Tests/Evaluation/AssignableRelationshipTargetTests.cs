using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class AssignableRelationshipTargetTests
{
    [Fact]
    public void Match_ConcreteGenericTarget_PreservesTypeArguments()
    {
        TypeSignature? observedTarget = null;
        var typeSystem = new StubTypeSystem
        {
            Assignability = (_, target, _) =>
            {
                observedTarget = target;
                return TypeRelationResult.Matched;
            }
        };
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(
                "System.Collections.Generic.IDictionary<string,string>+",
                new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var result = pattern!.Match(Candidate(), typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(observedTarget);
        Assert.Equal(
            "System.Collections.Generic.IDictionary`2",
            namedTarget.Identity.FullMetadataName);
        Assert.Collection(
            namedTarget.TypeArguments,
            argument => Assert.Equal("System.String", Name(argument)),
            argument => Assert.Equal("System.String", Name(argument)));
    }

    [Fact]
    public void Match_NullableReferenceAliasTarget_DropsNullableAnnotation()
    {
        var (result, target) = MatchWithObservedTarget(
            "string?+",
            Named("Example", "Resource", 0),
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal("System.String", namedTarget.Identity.FullMetadataName);
    }

    [Fact]
    public void Match_NullableValueAliasTarget_WrapsSystemNullable()
    {
        var (result, target) = MatchWithObservedTarget(
            "int?+",
            Named("Example", "Resource", 0),
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal("System.Nullable`1", namedTarget.Identity.FullMetadataName);
        var argument = Assert.Single(namedTarget.TypeArguments);
        Assert.Equal("System.Int32", Name(argument));
    }

    [Fact]
    public void Match_NullableReferenceAliasArgument_ConcretizesToUnderlying()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.Collections.Generic.IEnumerable<string?>+",
            Named("Example", "Resource", 0),
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal(
            "System.Collections.Generic.IEnumerable`1",
            namedTarget.Identity.FullMetadataName);
        var argument = Assert.Single(namedTarget.TypeArguments);
        Assert.Equal("System.String", Name(argument));
    }

    [Fact]
    public void Match_NullableReferenceAliasArrayElement_ConcretizesToUnderlying()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.Collections.Generic.List<string?[]>+",
            Named("Example", "Resource", 0),
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        var argument = Assert.IsType<ArrayTypeSignature>(
            Assert.Single(namedTarget.TypeArguments));
        Assert.Equal(1, argument.Rank);
        Assert.Equal("System.String", Name(argument.ElementType));
    }

    [Fact]
    public void Match_NullableUnknownNamedArgument_FallsBackToPatternWalk()
    {
        var foo = Named("Example", "Foo", 0);
        var enumerableOfFoo = Named(
            "System.Collections.Generic",
            "IEnumerable",
            1,
            TypeKind.Interface,
            foo);

        var (result, target) = MatchWithObservedTarget(
            "System.Collections.Generic.IEnumerable<Example.Foo?>+",
            Named("Example", "FooList", 0),
            closure: new InheritanceClosure(
                new TypeSignature[] { enumerableOfFoo },
                null));

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Null(target);
    }

    [Fact]
    public void Match_NullableQualifiedReferenceTarget_DropsNullableAnnotation()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.String?+",
            Named("Example", "Resource", 0),
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal("System.String", namedTarget.Identity.FullMetadataName);
    }

    [Fact]
    public void Match_NullableNonBclReferenceTarget_MatchesDerivedViaPatternWalk()
    {
        var (result, target) = MatchWithObservedTarget(
            "Fixtures.Base?+",
            Named("Fixtures", "Derived", 0),
            closure: new InheritanceClosure(
                new TypeSignature[] { Named("Fixtures", "Base", 0) },
                null));

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Null(target);
    }

    [Fact]
    public void Match_NullableNonBclReferenceTarget_WithoutMatchingAncestor_ReturnsNotMatched()
    {
        var (result, target) = MatchWithObservedTarget(
            "Fixtures.Base?+",
            Named("Fixtures", "Unrelated", 0),
            closure: new InheritanceClosure(
                new TypeSignature[] { Named("Fixtures", "Other", 0) },
                null));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Null(target);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Match_NullableNonBclValueTarget_MatchesConstructedNullableViaWalk()
    {
        var nullableGuid = Named(
            "System",
            "Nullable",
            1,
            TypeKind.Struct,
            Named("System", "Guid", 0, TypeKind.Struct));

        var (result, target) = MatchWithObservedTarget(
            "System.Guid?+",
            nullableGuid);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Null(target);
    }

    [Fact]
    public void Match_NullableNonBclValueTarget_AgainstPlainStruct_ReturnsNotMatched()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.Guid?+",
            Named("System", "Guid", 0, TypeKind.Struct));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Null(target);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Match_LegacyNullableValueTarget_PreservesNullableWrapper()
    {
        var (result, target) = MatchWithObservedTarget(
            "int?+",
            Named("Example", "Resource", 0),
            version: AspectNLanguageVersion.Legacy,
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal("System.Nullable`1", namedTarget.Identity.FullMetadataName);
    }

    [Fact]
    public void Match_LegacyNullableReferenceAliasArgument_PreservesNullableWrapper()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.Collections.Generic.IEnumerable<string?>+",
            Named("Example", "Resource", 0),
            version: AspectNLanguageVersion.Legacy,
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        var argument = Assert.Single(namedTarget.TypeArguments);
        Assert.Equal("System.Nullable`1", Name(argument));
    }

    [Fact]
    public void Match_LegacyNullableNamedTarget_KeepsExistingUnknownBehavior()
    {
        var (result, target) = MatchWithObservedTarget(
            "Fixtures.Base?+",
            Named("Example", "Resource", 0),
            version: AspectNLanguageVersion.Legacy,
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Null(target);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Match_LegacyOmittedGenericClause_MatchesConstructedSelfViaWalk()
    {
        var candidate = Named(
            "Example",
            "Dictionary",
            2,
            TypeKind.Class,
            Named("System", "String", 0),
            Named("System", "Int32", 0, TypeKind.Struct));

        var (result, target) = MatchWithObservedTarget(
            "Dictionary+",
            candidate,
            version: AspectNLanguageVersion.Legacy);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Null(target);
    }

    [Fact]
    public void Match_LegacyOmittedNestedGenericArgument_MatchesConstructedSelfViaWalk()
    {
        var candidate = Named(
            "System.Collections.Generic",
            "Dictionary",
            2,
            TypeKind.Class,
            Named("System", "String", 0),
            Named(
                "System.Collections.Generic",
                "List",
                1,
                TypeKind.Class,
                Named("System", "Int32", 0, TypeKind.Struct)));

        var (result, target) = MatchWithObservedTarget(
            "System.Collections.Generic.Dictionary<string, List>+",
            candidate,
            version: AspectNLanguageVersion.Legacy);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Null(target);
    }

    [Fact]
    public void Match_LegacyOmittedGenericClause_StillMatchesNonGenericSelf()
    {
        var (result, target) = MatchWithObservedTarget(
            "System.Action+",
            Named("System", "Action", 0, TypeKind.Delegate),
            version: AspectNLanguageVersion.Legacy,
            relation: TypeRelationResult.Matched);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_StandardOmittedGenericClause_KeepsArityZeroConcreteTarget()
    {
        var candidate = Named(
            "Example",
            "Dictionary",
            2,
            TypeKind.Class,
            Named("System", "String", 0),
            Named("System", "Int32", 0, TypeKind.Struct));

        var (result, target) = MatchWithObservedTarget(
            "Dictionary+",
            candidate);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        var namedTarget = Assert.IsType<NamedTypeSignature>(target);
        Assert.Equal("Dictionary", namedTarget.Identity.FullMetadataName);
    }

    private static (MatchResult Result, TypeSignature? Target) MatchWithObservedTarget(
        string source,
        TypeSignature candidate,
        AspectNLanguageVersion version = AspectNLanguageVersion.Standard,
        InheritanceClosure? closure = null,
        TypeRelationResult? relation = null)
    {
        TypeSignature? observedTarget = null;
        var typeSystem = new StubTypeSystem
        {
            Assignability = (_, assignabilityTarget, _) =>
            {
                observedTarget = assignabilityTarget;
                return relation ?? TypeRelationResult.NotMatched;
            },
            Closure = closure is null
                ? null
                : (_, _) => closure
        };
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(
                source,
                new AspectNParseOptions(version)));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var result = pattern!.Match(candidate, typeSystem);
        return (result, observedTarget);
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string metadataName,
        int arity,
        TypeKind kind = TypeKind.Class,
        params TypeSignature[] arguments)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            metadataName,
            arity,
            kind);
        var definition = new NamedTypeSignature(identity);
        return arguments.Length == 0
            ? definition
            : new NamedTypeSignature(identity, arguments, definition);
    }

    private static NamedTypeSignature Candidate()
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                "Example",
                "Dictionary",
                0,
                TypeKind.Class));
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }
}
