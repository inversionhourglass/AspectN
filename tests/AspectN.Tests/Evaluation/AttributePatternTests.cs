using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class AttributePatternTests
{
    [Theory]
    [InlineData("attr(type Example.RequiredAttribute)", AttributeTargetKind.Type)]
    [InlineData("attr(callable Example.RequiredAttribute)", AttributeTargetKind.Callable)]
    [InlineData("attr(property Example.RequiredAttribute)", AttributeTargetKind.Property)]
    [InlineData("attr(parameter[1] Example.RequiredAttribute)", AttributeTargetKind.Parameter)]
    [InlineData("attr(parameter[*] Example.RequiredAttribute)", AttributeTargetKind.Parameter)]
    [InlineData("attr(return Example.RequiredAttribute)", AttributeTargetKind.Return)]
    [InlineData("attr(any Example.RequiredAttribute)", AttributeTargetKind.Return)]
    public void Match_AttributePosition_QueriesDirectLocationOnly(
        string source,
        AttributeTargetKind matchingTarget)
    {
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == matchingTarget &&
            (target.Kind != AttributeTargetKind.Parameter ||
             target.Parameter!.Position == 1));

        var result = Match(source, Candidate(), typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.All(
            typeSystem.RequestedAttributeTypes,
            type => Assert.Equal(
                "Example.RequiredAttribute",
                Assert.IsType<NamedTypeSignature>(type)
                    .Identity.FullMetadataName));
    }

    [Fact]
    public void Match_IndexedParameter_DoesNotInspectOtherParameters()
    {
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Parameter &&
            target.Parameter!.Position == 0);

        var result = Match(
            "attr(parameter[1] Example.RequiredAttribute)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        var target = Assert.Single(typeSystem.RequestedTargets);
        Assert.Equal(1, target.Parameter!.Position);
    }

    [Fact]
    public void Match_TypeAttribute_UsesDirectLookupWithoutInheritance()
    {
        var typeSystem = new AttributeTypeSystem(_ => false);

        var result = Match(
            "attr(type Example.InheritedAttribute)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Equal(
            AttributeTargetKind.Type,
            Assert.Single(typeSystem.RequestedTargets).Kind);
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate,
        ITypeSystem typeSystem)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, typeSystem);
    }

    private static CallableSignature Candidate()
    {
        var declaringType = Named("Example", "Service");
        var property = new PropertySignature(
            "Name",
            Accessibility.Public,
            isStatic: false,
            declaringType,
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.None,
                TypeUseKind.Property),
            Array.Empty<ParameterSignature>(),
            Array.Empty<AttributeSignature>());
        return new CallableSignature(
            "get_Name",
            CallableKind.PropertyGetter,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            declaringType,
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                Parameter("first", 0),
                Parameter("second", 1)
            },
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(),
            associatedProperty: property);
    }

    private static ParameterSignature Parameter(string name, int position)
    {
        return new ParameterSignature(
            name,
            position,
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.None,
                TypeUseKind.Parameter));
    }

    private static NamedTypeSignature Named(string @namespace, string name)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                TypeKind.Class));
    }

    private sealed class AttributeTypeSystem : ITypeSystem
    {
        private readonly Func<AttributeTarget, bool> _isMatch;

        public AttributeTypeSystem(Func<AttributeTarget, bool> isMatch)
        {
            _isMatch = isMatch;
        }

        public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

        public List<AttributeTarget> RequestedTargets { get; } = new();

        public List<TypeSignature> RequestedAttributeTypes { get; } = new();

        public TypeRelationResult AreEqual(
            TypeSignature left,
            TypeSignature right)
        {
            return StructuralTypeComparer.AreEqual(left, right);
        }

        public TypeRelationResult IsAssignableTo(
            TypeSignature candidate,
            TypeSignature target,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return StructuralTypeComparer.AreEqual(candidate, target);
        }

        public TypeRelationResult HasDirectAttribute(
            AttributeTarget target,
            TypeSignature attributeType,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedTargets.Add(target);
            RequestedAttributeTypes.Add(attributeType);
            return _isMatch(target)
                ? TypeRelationResult.Matched
                : TypeRelationResult.NotMatched;
        }
    }
}
