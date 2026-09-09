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
    public void Match_VariableAttributeAlternatives_PreserveSatisfyingBinding()
    {
        var first = Named("Example.Markers", "FirstAttribute");
        var second = Named("Example.Markers", "SecondAttribute");
        var attributes = new[]
        {
            new AttributeSignature(first, AttributeTargetKind.Callable),
            new AttributeSignature(second, AttributeTargetKind.Callable)
        };
        var typeSystem = new AttributeTypeSystem(_ => false);

        var matched = Match(
            "attr(callable $A) && callable($A Service.get_Name(..))",
            Candidate(returnType: second, attributes: attributes),
            typeSystem);
        var notMatched = Match(
            "attr(callable $A) && callable($A Service.get_Name(..))",
            Candidate(returnType: second, attributes: attributes.Take(1)),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, matched.Status);
        Assert.Equal(MatchStatus.NotMatched, notMatched.Status);
    }

    [Fact]
    public void Match_VariableAttributeTargets_PreserveSatisfyingBinding()
    {
        var first = Named("Example.Markers", "FirstAttribute");
        var second = Named("Example.Markers", "SecondAttribute");
        var typeSystem = new AttributeTypeSystem(_ => false);

        var result = Match(
            "attr(any $A) && callable($A Service.get_Name(..))",
            Candidate(
                returnType: second,
                attributes:
                [
                    new AttributeSignature(first, AttributeTargetKind.Callable)
                ],
                propertyAttributes:
                [
                    new AttributeSignature(second, AttributeTargetKind.Property)
                ]),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
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

    [Fact]
    public void Match_ConcreteGenericAttribute_DirectLookupReceivesTypeArguments()
    {
        var typeSystem = new AttributeTypeSystem(_ => true);

        var result = Match(
            "attr(callable Example.ReviewTagAttribute<int>)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var requested = Assert.IsType<NamedTypeSignature>(
            Assert.Single(typeSystem.RequestedAttributeTypes));
        Assert.Equal(1, requested.Identity.GenericArity);
        var argument = Assert.IsType<NamedTypeSignature>(
            Assert.Single(requested.TypeArguments));
        Assert.Equal("System.Int32", argument.Identity.FullMetadataName);
    }

    [Theory]
    [InlineData("attr(callable Example.ReviewTagAttribute<>)")]
    [InlineData("attr(callable Example.ReviewTagAttribute<..>)")]
    public void Match_GenericWildcardAttribute_UsesEmbeddedAttributes(
        string source)
    {
        var typeSystem = new AttributeTypeSystem(_ => false);
        var candidate = Candidate(
            attributes:
            [
                new AttributeSignature(
                    Generic(
                        "Example",
                        "ReviewTagAttribute",
                        Named("System", "Int32")),
                    AttributeTargetKind.Callable)
            ]);

        var result = Match(source, candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Theory]
    [InlineData("attr(callable ReviewTag*<int>)", MatchStatus.Matched)]
    [InlineData("attr(callable ReviewTag*<string>)", MatchStatus.NotMatched)]
    public void Match_WildcardNameGenericAttribute_ComparesEmbeddedArguments(
        string source,
        MatchStatus expected)
    {
        var typeSystem = new AttributeTypeSystem(_ => false);
        var candidate = Candidate(
            attributes:
            [
                new AttributeSignature(
                    Generic(
                        "Example",
                        "ReviewTagAttribute",
                        Named("System", "Int32")),
                    AttributeTargetKind.Callable)
            ]);

        var result = Match(source, candidate, typeSystem);

        Assert.Equal(expected, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Fact]
    public void Match_NestedOpenGenericArgument_UsesEmbeddedAttributes()
    {
        var typeSystem = new AttributeTypeSystem(_ => true);
        var candidate = Candidate(
            attributes:
            [
                new AttributeSignature(
                    Generic(
                        "Example",
                        "ReviewTagAttribute",
                        Generic(
                            "System.Collections.Generic",
                            "List",
                            Named("System", "Int32"))),
                    AttributeTargetKind.Callable)
            ]);

        var result = Match(
            "attr(callable Example.ReviewTagAttribute<System.Collections.Generic.List<>>)",
            candidate,
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Fact]
    public void Match_NullableReferenceArgument_UsesEmbeddedAttributes()
    {
        var typeSystem = new AttributeTypeSystem(_ => true);
        var candidate = Candidate(
            attributes:
            [
                new AttributeSignature(
                    Generic(
                        "Example",
                        "ReviewTagAttribute",
                        Named("System", "String")),
                    AttributeTargetKind.Callable)
            ]);

        var result = Match(
            "attr(callable Example.ReviewTagAttribute<string?>)",
            candidate,
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Fact]
    public void Match_FullyConcreteNestedArguments_RemainDirectLookup()
    {
        var typeSystem = new AttributeTypeSystem(_ => true);
        var candidate = Candidate();

        var result = Match(
            "attr(callable Example.ReviewTagAttribute<System.Collections.Generic.List<int>>)",
            candidate,
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        var requested = Assert.IsType<NamedTypeSignature>(
            Assert.Single(typeSystem.RequestedAttributeTypes));
        var argument = Assert.IsType<NamedTypeSignature>(
            Assert.Single(requested.TypeArguments));
        Assert.Equal("System.Collections.Generic.List`1", argument.Identity.FullMetadataName);
    }

    [Fact]
    public void Match_OrBranch_ConstrainsExistingVariableForLaterConjunction()
    {
        var alpha = Named("Example", "AlphaAttribute");
        var beta = Named("Example", "BetaAttribute");
        var typeSystem = new AttributeTypeSystem(_ => false);

        var notMatched = Candidate(
            returnType: alpha,
            attributes:
            [
                new AttributeSignature(alpha, AttributeTargetKind.Callable),
                new AttributeSignature(beta, AttributeTargetKind.Callable)
            ],
            returnAttributes:
            [
                new AttributeSignature(beta, AttributeTargetKind.Return)
            ]);
        var matched = Candidate(
            returnType: alpha,
            attributes:
            [
                new AttributeSignature(alpha, AttributeTargetKind.Callable),
                new AttributeSignature(beta, AttributeTargetKind.Callable)
            ],
            returnAttributes:
            [
                new AttributeSignature(alpha, AttributeTargetKind.Return)
            ]);

        // 成员 OR 与类型 OR 两种形式必须对同一约束给出一致结果。
        var memberOr =
            "attr(callable $A) && " +
            "(callable($A Service.get_Name(..)) || " +
            "callable($A Service.Missing(..))) && " +
            "attr(return $A)";
        var typeOr =
            "attr(callable $A) && " +
            "callable(($A || Missing) Service.get_Name(..)) && " +
            "attr(return $A)";

        Assert.Equal(
            MatchStatus.NotMatched,
            Match(memberOr, notMatched, typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(memberOr, matched, typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(typeOr, notMatched, typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(typeOr, matched, typeSystem).Status);
    }

    [Fact]
    public void Match_OutOfRangeParameterIndex_ReportsDiagnosticAndRejectsPattern()
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                "attr(parameter[2147483648] Example.RequiredAttribute)"));

        // 越界索引必须产生诊断，且不得退化为 parameter[*] 的通配语义。
        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code ==
                DiagnosticCodes.InvalidParameterIndex);
        Assert.False(compilation.TryGetMemberPattern(out _));
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

    private static CallableSignature Candidate(
        TypeSignature? returnType = null,
        IEnumerable<AttributeSignature>? attributes = null,
        IEnumerable<AttributeSignature>? propertyAttributes = null,
        IEnumerable<AttributeSignature>? returnAttributes = null)
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
            propertyAttributes ?? Array.Empty<AttributeSignature>());
        return new CallableSignature(
            "get_Name",
            CallableKind.PropertyGetter,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            declaringType,
            new TypeUseSignature(
                returnType ?? Named("System", "String"),
                RefKind.None,
                TypeUseKind.Return),
            new[]
            {
                Parameter("first", 0),
                Parameter("second", 1)
            },
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            attributes ?? Array.Empty<AttributeSignature>(),
            returnAttributes ?? Array.Empty<AttributeSignature>(),
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

    private static NamedTypeSignature Generic(
        string @namespace,
        string name,
        params TypeSignature[] arguments)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            arguments.Length,
            TypeKind.Class);
        return new NamedTypeSignature(
            identity,
            arguments,
            new NamedTypeSignature(identity));
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

        public InheritanceClosure GetInheritanceClosure(
            TypeSignature candidate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InheritanceClosure.Empty;
        }
    }
}
