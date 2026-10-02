using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class AttributePatternTests
{
    [Theory]
    [InlineData("attr(type Example.RequiredAttribute)", AttributeTargetKind.Type)]
    [InlineData("attr(call Example.RequiredAttribute)", AttributeTargetKind.Method)]
    [InlineData("attr(prop Example.RequiredAttribute)", AttributeTargetKind.Property)]
    [InlineData("attr(para[1] Example.RequiredAttribute)", AttributeTargetKind.Parameter)]
    [InlineData("attr(para[*] Example.RequiredAttribute)", AttributeTargetKind.Parameter)]
    [InlineData("attr(ret Example.RequiredAttribute)", AttributeTargetKind.Return)]
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
            new AttributeSignature(first, AttributeTargetKind.Method),
            new AttributeSignature(second, AttributeTargetKind.Method)
        };
        var typeSystem = new AttributeTypeSystem(_ => false);

        var matched = Match(
            "attr(call $A) && callable($A Service.get_Name(..))",
            Candidate(returnType: second, attributes: attributes),
            typeSystem);
        var notMatched = Match(
            "attr(call $A) && callable($A Service.get_Name(..))",
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
                    new AttributeSignature(first, AttributeTargetKind.Method)
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
            "attr(para[1] Example.RequiredAttribute)",
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
    public void Match_ExecPosition_IncludesAssociatedPropertyAttributes()
    {
        // exec 与 Legacy 语义一致：访问器候选同时查询 callable 与所属 property。
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Property);

        var result = Match(
            "attr(exec Example.RequiredAttribute)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Equal(
            new[]
            {
                AttributeTargetKind.Method,
                AttributeTargetKind.Property
            },
            typeSystem.RequestedTargets.Select(target => target.Kind).ToArray());
    }

    [Fact]
    public void Match_LegacyExecPosition_IncludesAssociatedPropertyAttributes()
    {
        // Legacy 的 exec 同样并入所属 property 的直接特性（Rougamo 规则），
        // 钉住 exec 目标展开在两个语言版本语义一致。
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Property);

        var result = Match(
            "attr(exec Example.RequiredAttribute)",
            Candidate(),
            typeSystem,
            AspectNLanguageVersion.Legacy);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Equal(
            new[]
            {
                AttributeTargetKind.Method,
                AttributeTargetKind.Property
            },
            typeSystem.RequestedTargets.Select(target => target.Kind).ToArray());
    }

    [Fact]
    public void Match_CallPosition_ExcludesAssociatedPropertyAttributes()
    {
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Property);

        var result = Match(
            "attr(call Example.RequiredAttribute)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
        Assert.Equal(
            AttributeTargetKind.Method,
            Assert.Single(typeSystem.RequestedTargets).Kind);
    }

    [Theory]
    [InlineData("attr(para 1 Example.RequiredAttribute)")]
    [InlineData("attr(para[1] Example.RequiredAttribute)")]
    public void Match_ParaIndexCompatForms_AreEquivalent(string source)
    {
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Parameter &&
            target.Parameter!.Position == 1);

        var result = Match(source, Candidate(), typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_StarPosition_MatchesAnyLocation()
    {
        var typeSystem = new AttributeTypeSystem(target =>
            target.Kind == AttributeTargetKind.Return);

        var result = Match(
            "attr(* Example.RequiredAttribute)",
            Candidate(),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_ConcreteGenericAttribute_DirectLookupReceivesTypeArguments()
    {
        var typeSystem = new AttributeTypeSystem(_ => true);

        var result = Match(
            "attr(call Example.ReviewTagAttribute<int>)",
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
    [InlineData("attr(call Example.ReviewTagAttribute<>)")]
    [InlineData("attr(call Example.ReviewTagAttribute<..>)")]
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
                    AttributeTargetKind.Method)
            ]);

        var result = Match(source, candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Theory]
    [InlineData("attr(call ReviewTag*<int>)", MatchStatus.Matched)]
    [InlineData("attr(call ReviewTag*<string>)", MatchStatus.NotMatched)]
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
                    AttributeTargetKind.Method)
            ]);

        var result = Match(source, candidate, typeSystem);

        Assert.Equal(expected, result.Status);
        Assert.Empty(typeSystem.RequestedAttributeTypes);
    }

    [Fact]
    public void Match_QuestionArityGenericAttribute_UsesEmbeddedEvaluation()
    {
        // <?> 是通配元数（含零）：直接特性查询要求完全具体化，必须回退到
        // 嵌入特性求值，否则会被错构为零泛型类型而漏匹配（审计修正回归）。
        var typeSystem = new AttributeTypeSystem(_ => false);
        var candidate = Candidate(
            attributes:
            [
                new AttributeSignature(
                    Generic(
                        "Example",
                        "ReviewTagAttribute",
                        Named("System", "Int32")),
                    AttributeTargetKind.Method)
            ]);

        var result = Match(
            "attr(call ReviewTagAttribute<?>)",
            candidate,
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
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
                    AttributeTargetKind.Method)
            ]);

        var result = Match(
            "attr(call Example.ReviewTagAttribute<System.Collections.Generic.List<>>)",
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
                    AttributeTargetKind.Method)
            ]);

        var result = Match(
            "attr(call Example.ReviewTagAttribute<string?>)",
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
            "attr(call Example.ReviewTagAttribute<System.Collections.Generic.List<int>>)",
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
                new AttributeSignature(alpha, AttributeTargetKind.Method),
                new AttributeSignature(beta, AttributeTargetKind.Method)
            ],
            returnAttributes:
            [
                new AttributeSignature(beta, AttributeTargetKind.Return)
            ]);
        var matched = Candidate(
            returnType: alpha,
            attributes:
            [
                new AttributeSignature(alpha, AttributeTargetKind.Method),
                new AttributeSignature(beta, AttributeTargetKind.Method)
            ],
            returnAttributes:
            [
                new AttributeSignature(alpha, AttributeTargetKind.Return)
            ]);

        // 成员 OR 与类型 OR 两种形式必须对同一约束给出一致结果。
        var memberOr =
            "attr(call $A) && " +
            "(callable($A Service.get_Name(..)) || " +
            "callable($A Service.Missing(..))) && " +
            "attr(ret $A)";
        var typeOr =
            "attr(call $A) && " +
            "callable(($A || Missing) Service.get_Name(..)) && " +
            "attr(ret $A)";

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
                "attr(para[2147483648] Example.RequiredAttribute)"));

        // 越界索引必须产生诊断，且不得退化为 parameter[*] 的通配语义。
        Assert.Contains(
            compilation.Diagnostics,
            diagnostic => diagnostic.Code ==
                DiagnosticCodes.InvalidParameterIndex);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    private static MatchResult Match(
        string source,
        MethodSignature candidate,
        ITypeSystem typeSystem,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Standard)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                source,
                new AspectNParseOptions(languageVersion)));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, typeSystem);
    }

    private static MethodSignature Candidate(
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
        return new MethodSignature(
            "get_Name",
            MethodKind.PropertyGetter,
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

    [Fact]
    public void Match_PropertyTarget_OnPlainMethod_IsNotMatched()
    {
        // 普通方法确定没有关联属性：attr(prop ...) 直接 NotMatched，不再占用 Unknown 通道。
        var plain = new MethodSignature(
            "Run",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                Named("System", "String"),
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());

        var result = Match(
            "attr(prop Example.RequiredAttribute)",
            plain,
            new AttributeTypeSystem(_ => true));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void Match_PropertyTarget_OnAccessorWithoutPropertyLink_IsUnknown()
    {
        // 访问器候选但后端未填 AssociatedProperty 链接：能力守卫，返回 Unknown。
        var getterWithoutLink = Candidate();
        var detached = new MethodSignature(
            getterWithoutLink.Name,
            getterWithoutLink.Kind,
            getterWithoutLink.Accessibility,
            getterWithoutLink.IsStatic,
            getterWithoutLink.IsAsync,
            getterWithoutLink.DeclaringType,
            getterWithoutLink.ReturnType,
            getterWithoutLink.Parameters,
            getterWithoutLink.GenericParameters,
            getterWithoutLink.GenericArguments,
            getterWithoutLink.Attributes,
            getterWithoutLink.ReturnAttributes);

        var result = Match(
            "attr(prop Example.RequiredAttribute)",
            detached,
            new AttributeTypeSystem(_ => true));

        Assert.Equal(MatchStatus.Unknown, result.Status);
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
