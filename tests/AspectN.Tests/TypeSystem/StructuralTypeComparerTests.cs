using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.TypeSystem;

public sealed class StructuralTypeComparerTests
{
    [Fact]
    public void AreEqual_EquivalentNamedDefinitions_ReturnsMatched()
    {
        var left = Named("Example", "Widget");
        var right = Named("Example", "Widget");

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void AreEqual_DifferentNamedDefinitions_ReturnsNotMatched()
    {
        var left = Named("Example", "First");
        var right = Named("Example", "Second");

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void AreEqual_EquivalentConstructedGenerics_ReturnsMatched()
    {
        var definition = Named("System.Collections.Generic", "List", 1);
        var left = new NamedTypeSignature(
            definition.Identity,
            [Named("System", "String")],
            definition);
        var right = new NamedTypeSignature(
            definition.Identity,
            [Named("System", "String")],
            definition);

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void AreEqual_DifferentConstructedGenericArguments_ReturnsNotMatched()
    {
        var definition = Named("System.Collections.Generic", "List", 1);
        var left = new NamedTypeSignature(
            definition.Identity,
            [Named("System", "String")],
            definition);
        var right = new NamedTypeSignature(
            definition.Identity,
            [Named("System", "Int32", kind: TypeKind.Struct)],
            definition);

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void AreEqual_EquivalentNestedGenericDefinitions_ReturnsMatched()
    {
        var leftOuter = Identity("Example", "Outer", 1);
        var rightOuter = Identity("Example", "Outer", 1);
        var left = new NamedTypeSignature(
            Identity(string.Empty, "Inner", 2, leftOuter));
        var right = new NamedTypeSignature(
            Identity(string.Empty, "Inner", 2, rightOuter));

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void AreEqual_ArrayShapeAndElementMatch_ReturnsMatched()
    {
        var left = new ArrayTypeSignature(Named("System", "String"), 2);
        var right = new ArrayTypeSignature(Named("System", "String"), 2);

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void AreEqual_DifferentArrayRanks_ReturnsNotMatched()
    {
        var left = new ArrayTypeSignature(Named("System", "String"), 1);
        var right = new ArrayTypeSignature(Named("System", "String"), 2);

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void AreEqual_GenericParametersUseOwnerAndPosition_ReturnsMatched()
    {
        var left = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "TSource",
                GenericParameterOwnerKind.Method,
                "Example.Mapper.Map`1",
                0));
        var right = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "TResult",
                GenericParameterOwnerKind.Method,
                "Example.Mapper.Map`1",
                0));

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void AreEqual_DifferentGenericParameterOwners_ReturnsNotMatched()
    {
        var left = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "T",
                GenericParameterOwnerKind.Type,
                "Example.First`1",
                0));
        var right = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "T",
                GenericParameterOwnerKind.Type,
                "Example.Second`1",
                0));

        var result = StructuralTypeComparer.AreEqual(left, right);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void AreEqual_UnsupportedType_ReturnsUnknownWithDiagnostic()
    {
        var unsupported = new UnsupportedTypeSignature(
            "delegate*<void>",
            "Function pointers are not available in this metadata source.");

        var result = StructuralTypeComparer.AreEqual(unsupported, unsupported);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void AreEqual_MissingStructuralIdentity_ReturnsUnknownWithDiagnostic()
    {
        var missing = new MissingTypeSignature();

        var result = StructuralTypeComparer.AreEqual(missing, missing);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Capabilities_FlagsCanBeCombined()
    {
        var capabilities = TypeSystemCapabilities.TypeEquality |
                           TypeSystemCapabilities.Assignability;

        Assert.True(capabilities.HasFlag(TypeSystemCapabilities.TypeEquality));
        Assert.True(capabilities.HasFlag(TypeSystemCapabilities.Assignability));
        Assert.False(
            capabilities.HasFlag(TypeSystemCapabilities.DirectAttributeLookup));
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string metadataName,
        int genericArity = 0,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            Identity(@namespace, metadataName, genericArity, kind: kind));
    }

    private static TypeIdentity Identity(
        string @namespace,
        string metadataName,
        int genericArity = 0,
        TypeIdentity? containingType = null,
        TypeKind kind = TypeKind.Class)
    {
        return new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            metadataName,
            genericArity,
            kind,
            containingType);
    }

    private sealed class MissingTypeSignature : TypeSignature
    {
        public MissingTypeSignature()
            : base(TypeSignatureKind.Unsupported)
        {
        }
    }
}
