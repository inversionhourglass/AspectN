using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Text;
using AspectN.TypeSystem;

namespace AspectN.Tests.Metadata;

public sealed class MetadataContractTests
{
    private static NamedTypeSignature Type() => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Item", 0, TypeKind.Class));

    [Theory]
    [InlineData(-1)]
    [InlineData(null)]
    public void AttributeSignature_ParameterWithoutValidIndex_Rejects(int? index)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AttributeSignature(Type(), AttributeTargetKind.Parameter, index));
    }

    [Fact]
    public void AttributeSignature_NonParameterWithIndex_Rejects()
    {
        Assert.Throws<ArgumentException>(() => new AttributeSignature(Type(), AttributeTargetKind.Method, 0));
    }

    [Fact]
    public void InheritanceClosure_CopiesInputAndRequiresUnknownFailure()
    {
        var ancestors = new List<TypeSignature> { Type() };
        var closure = new InheritanceClosure(ancestors, null);
        ancestors.Clear();
        Assert.Single(closure.Ancestors);
        Assert.Throws<ArgumentException>(() => new InheritanceClosure([], TypeRelationResult.Matched));
        Assert.Throws<ArgumentException>(() => new InheritanceClosure([], TypeRelationResult.NotMatched));
    }

    [Theory]
    [InlineData("parameters")]
    [InlineData("genericParameters")]
    [InlineData("genericArguments")]
    [InlineData("attributes")]
    [InlineData("returnAttributes")]
    public void MethodSignature_NullCollection_ReportsPublicParameterName(string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MethodSignature(
            "M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            Type(), new TypeUseSignature(Type(), RefKind.None, TypeUseKind.Return),
            parameter == "parameters" ? null! : [],
            parameter == "genericParameters" ? null! : [],
            parameter == "genericArguments" ? null! : [],
            parameter == "attributes" ? null! : [],
            parameter == "returnAttributes" ? null! : []));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory]
    [InlineData("parameters")]
    [InlineData("attributes")]
    public void PropertySignature_NullCollection_ReportsPublicParameterName(string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PropertySignature(
            "P", Accessibility.Public, false, Type(), new TypeUseSignature(Type(), RefKind.None, TypeUseKind.Property),
            parameter == "parameters" ? null! : [],
            parameter == "attributes" ? null! : []));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void Results_DiagnosticsAreSortedAndOnlyExactDuplicatesAreRemoved()
    {
        var first = new AspectNDiagnostic("B", DiagnosticSeverity.Warning, "first", new TextSpan(0, 1));
        var duplicate = new AspectNDiagnostic("B", DiagnosticSeverity.Warning, "first", new TextSpan(0, 1));
        var different = new AspectNDiagnostic("B", DiagnosticSeverity.Warning, "different", new TextSpan(0, 1));
        var later = new AspectNDiagnostic("A", DiagnosticSeverity.Warning, "later", new TextSpan(2, 1));
        var unlocated = new AspectNDiagnostic("A", DiagnosticSeverity.Warning, "unlocated");
        var input = new[] { unlocated, later, first, duplicate, different };
        var expected = new[] { different, first, later, unlocated };
        Assert.Equal(expected, new MatchResult(MatchStatus.Unknown, input).Diagnostics);
        Assert.Equal(expected, new TypeRelationResult(MatchStatus.Unknown, input).Diagnostics);
        Assert.Equal(expected, new MetadataAvailability(MetadataFacts.Name, input).Diagnostics);
    }
}
