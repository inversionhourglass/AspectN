using AspectN.Evaluation;
using AspectN.Evaluation.Legacy;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Legacy;

public sealed class LegacySemanticTests
{
    private static readonly TypeSignature _Void =
        Named("System", "Void", TypeKind.Struct);

    [Theory]
    [InlineData("T")]
    [InlineData("T1")]
    [InlineData("T42")]
    public void Match_UndeclaredGenericLookingName_IsLiteral(string name)
    {
        var integer = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: Named("System", "Int32", TypeKind.Struct));
        var literal = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: Named("Example", name));

        Assert.Equal(MatchStatus.NotMatched, Match($"method({name} *(..))", integer).Status);
        Assert.Equal(MatchStatus.Matched, Match($"method({name} *(..))", literal).Status);
    }

    [Theory]
    [InlineData("method(* *..*(..))")]
    [InlineData("method(* *..StaticMethod(..))")]
    [InlineData("method(* *.StaticMethod(..))")]
    public void Match_WildcardDeclaringPath_MatchesMethod(string source)
    {
        var candidate = Callable("StaticMethod", MethodKind.OrdinaryMethod, isStatic: true);

        Assert.Equal(MatchStatus.Matched, Match(source, candidate).Status);
        if (source.Contains("StaticMethod", StringComparison.Ordinal))
        {
            Assert.Equal(MatchStatus.NotMatched,
                Match(source.Replace("StaticMethod", "Missing", StringComparison.Ordinal), candidate).Status);
        }
    }

    [Fact]
    public void Match_NegatedReturnType_ExcludesOnlySpecifiedType()
    {
        var integer = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: Named("System", "Int32", TypeKind.Struct));
        var text = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: Named("System", "String"));

        Assert.Equal(MatchStatus.NotMatched, Match("method(!int *(..))", integer).Status);
        Assert.Equal(MatchStatus.Matched, Match("method(!int *(..))", text).Status);
    }

    [Fact]
    public void Match_LegacyBareType_PreservesSharedArrayElementMatching()
    {
        var integer = Named("System", "Int32", TypeKind.Struct);
        var array = new ArrayTypeSignature(new ArrayTypeSignature(integer, 1), 2);
        var candidate = Callable("Run", MethodKind.OrdinaryMethod, returnType: array);

        Assert.Equal(MatchStatus.Matched, Match("method(int *(..))", candidate).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("method(!int *(..))", candidate).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("method(int[,] *(..))", candidate).Status);
        Assert.Equal(MatchStatus.Matched, Match("method(int[,][] *(..))", candidate).Status);
    }

    [Fact]
    public void Match_LegacyAsyncVoid_PreservesReturnShapeWithoutAsyncFlag()
    {
        var candidate = Callable("Run", MethodKind.OrdinaryMethod, isAsync: false);

        Assert.Equal(MatchStatus.Matched, Match("method(async void *(..))", candidate).Status);
    }

    [Theory]
    [InlineData("method(* *..*(..))")]
    [InlineData("method(* *..*.*(..))")]
    [InlineData("method(* *.Run(..))")]
    public void Match_UniversalDeclaringType_IncludesNestedTypes(string source)
    {
        var outer = Named("Example", "Outer");
        var identity = new TypeIdentity(new AssemblyIdentity("Example.Assembly"),
            "Example", "Inner", 0, TypeKind.Class, outer.Identity);
        var nested = new NamedTypeSignature(identity);
        var candidate = Callable("Run", MethodKind.OrdinaryMethod, declaringType: nested);

        Assert.Equal(MatchStatus.Matched, Match(source, candidate).Status);
    }

    [Theory]
    [InlineData("method(T *<,T>(..))", 1)]
    [InlineData("method(T *<T,>(..))", 0)]
    public void Match_OmittedGenericSlot_PreservesDeclaredVariablePosition(string source, int position)
    {
        var first = GenericParameter("First", GenericParameterOwnerKind.Method, "Run", 0);
        var second = GenericParameter("Second", GenericParameterOwnerKind.Method, "Run", 1);
        var matching = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: position == 0 ? first : second,
            genericParameters: [first.Parameter, second.Parameter]);
        var other = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: position == 0 ? second : first,
            genericParameters: [first.Parameter, second.Parameter]);

        Assert.Equal(MatchStatus.Matched, Match(source, matching).Status);
        Assert.Equal(MatchStatus.NotMatched, Match(source, other).Status);
    }

    [Theory]
    [InlineData("execution(* Service.Run(..))", MethodKind.OrdinaryMethod, true)]
    [InlineData("execution(* Service.*(..))", MethodKind.PropertyGetter, true)]
    [InlineData("execution(* Service.*(..))", MethodKind.InstanceConstructor, true)]
    [InlineData("execution(* Service.*(..))", MethodKind.StaticConstructor, true)]
    [InlineData("execution(* Service.Run(..))", MethodKind.InstanceConstructor, false)]
    [InlineData("method(* Service.Run(..))", MethodKind.PropertyGetter, false)]
    [InlineData("property(string Service.Name)", MethodKind.PropertyGetter, true)]
    [InlineData("property(string Service.Name)", MethodKind.PropertySetter, true)]
    [InlineData("ctor(Service(..))", MethodKind.InstanceConstructor, true)]
    [InlineData("cctor(Service)", MethodKind.StaticConstructor, true)]
    public void Match_LegacySelector_UsesHistoricalDomain(
        string source,
        MethodKind kind,
        bool expected)
    {
        var candidate = kind is MethodKind.PropertyGetter or
            MethodKind.PropertySetter
            ? PropertyAccessor(kind)
            : Callable(
                kind is MethodKind.InstanceConstructor
                    ? ".ctor"
                    : kind == MethodKind.StaticConstructor
                        ? ".cctor"
                        : "Run",
                kind);

        var result = Match(source, candidate);

        Assert.Equal(
            expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            result.Status);
    }

    [Fact]
    public void Match_LegacyMemberNameStartingWithAsync_IsNotAsyncModifier()
    {
        var candidate = Callable(
            "AsyncTask",
            MethodKind.OrdinaryMethod,
            isAsync: false,
            returnType: Named("System.Threading.Tasks", "Task"),
            declaringType: Named("ConfiguredMoUsage", "InstanceCls"));

        var result = Match("method(* *.Async*(..))", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_LegacyWildcardParameterSlots_PreserveArityAndPositions()
    {
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            parameters:
            [
                Parameter(Named("System", "Int32", TypeKind.Struct), 0, RefKind.Out),
                Parameter(Named("System", "Double", TypeKind.Struct), 1),
                Parameter(Named("System", "String"), 2, RefKind.In)
            ]);

        Assert.Equal(
            MatchStatus.Matched,
            Match("method(* Service.Run(,double,))", candidate).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match("method(* Service.Run(,))", candidate).Status);
    }

    [Fact]
    public void Match_LegacyGenericVirtualPositions_IgnoreMetadataNames()
    {
        var typeFirst = GenericParameter(
            "TLeft",
            GenericParameterOwnerKind.Type,
            "Example.Container`2",
            0);
        var typeSecond = GenericParameter(
            "TRight",
            GenericParameterOwnerKind.Type,
            "Example.Container`2",
            1);
        var methodFirst = GenericParameter(
            "TInput",
            GenericParameterOwnerKind.Method,
            "Example.Container`2.Run`2",
            0);
        var methodSecond = GenericParameter(
            "TOutput",
            GenericParameterOwnerKind.Method,
            "Example.Container`2.Run`2",
            1);
        var declaringType = Constructed(
            "Example",
            "Container",
            TypeKind.Class,
            typeFirst,
            typeSecond);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            declaringType: declaringType,
            parameters:
            [
                Parameter(typeFirst, 0),
                Parameter(typeSecond, 1),
                Parameter(methodFirst, 2),
                Parameter(methodSecond, 3)
            ],
            genericParameters:
            [
                methodFirst.Parameter,
                methodSecond.Parameter
            ]);

        var matched = Match(
            "method(* *<TA,TB>.Run<TC,TD>(TA,TB,TC,TD))",
            candidate);
        var reversed = Match(
            "method(* *<TA,TB>.Run<TC,TD>(TB,TA,TC,TD))",
            candidate);

        Assert.Equal(MatchStatus.Matched, matched.Status);
        Assert.Equal(MatchStatus.NotMatched, reversed.Status);
    }

    [Fact]
    public void Match_LegacyNullable_RequiresSystemNullableShape()
    {
        var nullableInt = Constructed(
            "System",
            "Nullable",
            TypeKind.Struct,
            Named("System", "Int32", TypeKind.Struct));

        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(string? Service.Run(..))",
                Callable(
                    "Run",
                    MethodKind.OrdinaryMethod,
                    returnType: Named("System", "String"))).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(int? Service.Run(..))",
                Callable(
                    "Run",
                    MethodKind.OrdinaryMethod,
                    returnType: nullableInt)).Status);
    }

    [Fact]
    public void Match_LegacyAsyncSugar_UsesTaskShapeNotAsyncFlag()
    {
        var taskOfString = Constructed(
            "System.Threading.Tasks",
            "Task",
            TypeKind.Class,
            Named("System", "String"));
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            isAsync: false,
            returnType: taskOfString);

        var result = Match(
            "method(async string Service.Run(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Theory]
    [InlineData("Task", TypeKind.Class)]
    [InlineData("ValueTask", TypeKind.Struct)]
    public void Match_LegacyAsyncNull_MatchesNonGenericTaskTypes(
        string metadataName,
        TypeKind kind)
    {
        // Legacy 的 async null 对应 Standard 的裸 tasklike：仅匹配非泛型 Task/ValueTask。
        var candidate = Callable(
            "Flush",
            MethodKind.OrdinaryMethod,
            isAsync: true,
            returnType: Named("System.Threading.Tasks", metadataName, kind));

        var result = Match(
            "method(async null Service.Flush(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_AsyncNullAndBareTaskLike_AgreeAcrossVersions()
    {
        // async null ↔ 裸 tasklike、async T ↔ tasklike<T>：同一候选在两个版本给出一致结果。
        var nonGeneric = Callable(
            "Flush",
            MethodKind.OrdinaryMethod,
            isAsync: true,
            returnType: Named("System.Threading.Tasks", "Task", TypeKind.Class));
        var generic = Callable(
            "Flush",
            MethodKind.OrdinaryMethod,
            isAsync: true,
            returnType: Constructed(
                "System.Threading.Tasks",
                "Task",
                TypeKind.Class,
                Named("System", "String")));

        Assert.Equal(
            MatchStatus.Matched,
            Match("method(async null Service.Flush(..))", nonGeneric).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match("method(async null Service.Flush(..))", generic).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(tasklike Service.Flush(..))",
                nonGeneric,
                languageVersion: AspectNLanguageVersion.Standard).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            Match(
                "method(tasklike Service.Flush(..))",
                generic,
                languageVersion: AspectNLanguageVersion.Standard).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(tasklike<string> Service.Flush(..))",
                generic,
                languageVersion: AspectNLanguageVersion.Standard).Status);
    }

    [Fact]
    public void Match_LegacyMixedArrayRanks_PreservesOuterToInnerOrder()
    {
        var returnType = new ArrayTypeSignature(
            new ArrayTypeSignature(
                new ArrayTypeSignature(
                    Named("System", "Int32", TypeKind.Struct),
                    3),
                1),
            2);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            returnType: returnType);

        var result = Match(
            "method(int[,][][,,] Service.Run(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_LegacyExecAttribute_ComposesAccessorPropertyAttributes()
    {
        var marker = Named("Example", "MarkerAttribute");
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
            [
                new AttributeSignature(
                    marker,
                    AttributeTargetKind.Property)
            ]);
        var candidate = Callable(
            "get_Name",
            MethodKind.PropertyGetter,
            returnType: Named("System", "String"),
            declaringType: declaringType,
            associatedProperty: property);

        var result = Match(
            "attr(exec Example.MarkerAttribute)",
            candidate,
            new SignatureAttributeTypeSystem());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_WildcardExecutionAttribute_MatchesCallableAttribute()
    {
        var marker = Named(
            "PatternUsage.Attributes.Attributes",
            "Label1Attribute");
        var candidate = Callable(
            "set_Prop",
            MethodKind.PropertySetter,
            attributes:
            [
                new AttributeSignature(
                    marker,
                    AttributeTargetKind.Method)
            ]);

        var result = Match(
            "attr(exec PatternUsage..Label1Attribute)",
            candidate,
            new SignatureAttributeTypeSystem());

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Format_LegacySignature_ReproducesOracleShape()
    {
        var methodParameter = new GenericParameterSignature(
            "TResult",
            GenericParameterOwnerKind.Method,
            "Example.Container`2.Run`1",
            new AssemblyIdentity("Example.Assembly"),
            0);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            accessibility: Accessibility.PrivateProtected,
            isStatic: true,
            isAsync: true,
            returnType: Named("System", "String"),
            returnRefKind: RefKind.RefReadOnly,
            declaringType: Constructed(
                "Example",
                "Container",
                TypeKind.Class,
                Named("System", "Int32", TypeKind.Struct),
                Named("System", "String")),
            parameters:
            [
                Parameter(
                    Named("System", "Int32", TypeKind.Struct),
                    0,
                    RefKind.Ref),
                Parameter(Named("System", "String"), 1, RefKind.Out)
            ],
            genericParameters: [methodParameter]);

        var formatted = LegacyCanonicalSignatureFormatter.Format(candidate);

        Assert.Equal(
            "privateprotected static System.String& " +
            "Example.Container<System.Int32,System.String>.Run<T>" +
            "(System.Int32&,System.String&)",
            formatted);
    }

    [Fact]
    public void Format_OpenGenericDefinition_UsesVirtualTypeParameterName()
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            "Example",
            "Container",
            1,
            TypeKind.Class);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            declaringType: new NamedTypeSignature(identity));

        var formatted = LegacyCanonicalSignatureFormatter.Format(candidate);

        Assert.Equal(
            "public System.Void Example.Container<T>.Run()",
            formatted);
    }

    [Fact]
    public void Format_NestedOpenGenericDefinition_UsesVirtualTypeParameterNames()
    {
        var outer = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            "Example",
            "Outer",
            1,
            TypeKind.Class);
        var inner = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            string.Empty,
            "Inner",
            2,
            TypeKind.Class,
            outer);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            declaringType: new NamedTypeSignature(inner));

        var formatted = LegacyCanonicalSignatureFormatter.Format(candidate);

        Assert.Equal(
            "public System.Void Example.Outer<T1>/Inner<T2,T3>.Run()",
            formatted);
    }

    [Fact]
    public void Match_AssignableDeclaringType_UsesTypeSystemRelation()
    {
        var declaringType = Named("PatternUsage", "Implementation");
        var interfaceType = Named(
            "PatternUsage",
            "InterfaceAB",
            TypeKind.Interface);
        var candidate = Callable(
            "Run",
            MethodKind.OrdinaryMethod,
            declaringType: declaringType);
        var typeSystem = new StubTypeSystem
        {
            // Legacy 省略泛型子句的 + 目标不再具体化，必须沿继承闭包求值。
            Assignability = (_, _, _) =>
                throw new InvalidOperationException(
                    "Legacy omitted generic clauses must not concretize."),
            Closure = (actual, _) =>
            {
                Assert.Same(declaringType, actual);
                return new InheritanceClosure(
                    new TypeSignature[] { interfaceType },
                    null);
            }
        };

        var result = Match(
            "execution(* PatternUsage.InterfaceAB+.*(..))",
            candidate,
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    private static MatchResult Match(
        string source,
        MethodSignature candidate,
        ITypeSystem? typeSystem = null,
        AspectNLanguageVersion languageVersion = AspectNLanguageVersion.Legacy)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                source,
                new AspectNParseOptions(languageVersion)));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(
            candidate,
            typeSystem ?? new StubTypeSystem());
    }

    private static MethodSignature PropertyAccessor(MethodKind kind)
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
        return Callable(
            kind == MethodKind.PropertyGetter ? "get_Name" : "set_Name",
            kind,
            returnType: kind == MethodKind.PropertyGetter
                ? Named("System", "String")
                : _Void,
            declaringType: declaringType,
            associatedProperty: property);
    }

    private static MethodSignature Callable(
        string name,
        MethodKind kind,
        Accessibility accessibility = Accessibility.Public,
        bool isStatic = false,
        bool isAsync = false,
        TypeSignature? returnType = null,
        RefKind returnRefKind = RefKind.None,
        TypeSignature? declaringType = null,
        IEnumerable<ParameterSignature>? parameters = null,
        IEnumerable<GenericParameterSignature>? genericParameters = null,
        IEnumerable<AttributeSignature>? attributes = null,
        PropertySignature? associatedProperty = null)
    {
        return new MethodSignature(
            name,
            kind,
            accessibility,
            isStatic || kind == MethodKind.StaticConstructor,
            isAsync,
            declaringType ?? Named("Example", "Service"),
            new TypeUseSignature(
                returnType ?? _Void,
                returnRefKind,
                TypeUseKind.Return),
            parameters ?? Array.Empty<ParameterSignature>(),
            genericParameters ?? Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            attributes ?? Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(),
            associatedProperty: associatedProperty);
    }

    private static ParameterSignature Parameter(
        TypeSignature type,
        int position,
        RefKind refKind = RefKind.None)
    {
        return new ParameterSignature(
            $"value{position}",
            position,
            new TypeUseSignature(type, refKind, TypeUseKind.Parameter));
    }

    private static GenericParameterTypeSignature GenericParameter(
        string name,
        GenericParameterOwnerKind ownerKind,
        string ownerId,
        int position)
    {
        return new GenericParameterTypeSignature(
            new GenericParameterSignature(
                name,
                ownerKind,
                ownerId,
                new AssemblyIdentity("Example.Assembly"),
                position));
    }

    private static NamedTypeSignature Constructed(
        string @namespace,
        string name,
        TypeKind kind,
        params TypeSignature[] arguments)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            arguments.Length,
            kind);
        var definition = new NamedTypeSignature(identity);
        return new NamedTypeSignature(identity, arguments, definition);
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                kind));
    }

    private sealed class SignatureAttributeTypeSystem : ITypeSystem
    {
        public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

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
            var attributes = target.Kind switch
            {
                AttributeTargetKind.Method => target.Method!.Attributes,
                AttributeTargetKind.Property => target.Property!.Attributes,
                AttributeTargetKind.Parameter => target.Parameter!.Attributes,
                AttributeTargetKind.Return => target.Method!.ReturnAttributes,
                _ => Array.Empty<AttributeSignature>()
            };
            return attributes.Any(attribute =>
                attribute.AttributeType is NamedTypeSignature actual &&
                attributeType is NamedTypeSignature expected &&
                StringComparer.Ordinal.Equals(
                    actual.Identity.FullMetadataName,
                    expected.Identity.FullMetadataName))
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
