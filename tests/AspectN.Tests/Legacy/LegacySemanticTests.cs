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
    [InlineData("execution(* Service.Run(..))", CallableKind.OrdinaryMethod, true)]
    [InlineData("execution(* Service.*(..))", CallableKind.PropertyGetter, true)]
    [InlineData("execution(* Service.Run(..))", CallableKind.InstanceConstructor, false)]
    [InlineData("method(* Service.Run(..))", CallableKind.PropertyGetter, false)]
    [InlineData("property(string Service.Name)", CallableKind.PropertyGetter, true)]
    [InlineData("property(string Service.Name)", CallableKind.PropertySetter, true)]
    [InlineData("ctor(Service(..))", CallableKind.InstanceConstructor, true)]
    [InlineData("cctor(Service)", CallableKind.StaticConstructor, true)]
    public void Match_LegacySelector_UsesHistoricalDomain(
        string source,
        CallableKind kind,
        bool expected)
    {
        var candidate = kind is CallableKind.PropertyGetter or
            CallableKind.PropertySetter
            ? PropertyAccessor(kind)
            : Callable(
                kind is CallableKind.InstanceConstructor
                    ? ".ctor"
                    : kind == CallableKind.StaticConstructor
                        ? ".cctor"
                        : "Run",
                kind);

        var result = Match(source, candidate);

        Assert.Equal(
            expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            result.Status);
    }

    [Fact]
    public void Match_LegacyWildcardParameterSlots_PreserveArityAndPositions()
    {
        var candidate = Callable(
            "Run",
            CallableKind.OrdinaryMethod,
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
            CallableKind.OrdinaryMethod,
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
                    CallableKind.OrdinaryMethod,
                    returnType: Named("System", "String"))).Status);
        Assert.Equal(
            MatchStatus.Matched,
            Match(
                "method(int? Service.Run(..))",
                Callable(
                    "Run",
                    CallableKind.OrdinaryMethod,
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
            CallableKind.OrdinaryMethod,
            isAsync: false,
            returnType: taskOfString);

        var result = Match(
            "method(async string Service.Run(..))",
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
            CallableKind.PropertyGetter,
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
    public void Format_LegacySignature_ReproducesOracleShape()
    {
        var methodParameter = new GenericParameterSignature(
            "TResult",
            GenericParameterOwnerKind.Method,
            "Example.Container`2.Run`1",
            0);
        var candidate = Callable(
            "Run",
            CallableKind.OrdinaryMethod,
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
            CallableKind.OrdinaryMethod,
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
            CallableKind.OrdinaryMethod,
            declaringType: new NamedTypeSignature(inner));

        var formatted = LegacyCanonicalSignatureFormatter.Format(candidate);

        Assert.Equal(
            "public System.Void Example.Outer<T1>/Inner<T2,T3>.Run()",
            formatted);
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate,
        ITypeSystem? typeSystem = null)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(
                source,
                new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(
            candidate,
            typeSystem ?? new StubTypeSystem());
    }

    private static CallableSignature PropertyAccessor(CallableKind kind)
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
            kind == CallableKind.PropertyGetter ? "get_Name" : "set_Name",
            kind,
            returnType: kind == CallableKind.PropertyGetter
                ? Named("System", "String")
                : _Void,
            declaringType: declaringType,
            associatedProperty: property);
    }

    private static CallableSignature Callable(
        string name,
        CallableKind kind,
        Accessibility accessibility = Accessibility.Public,
        bool isStatic = false,
        bool isAsync = false,
        TypeSignature? returnType = null,
        RefKind returnRefKind = RefKind.None,
        TypeSignature? declaringType = null,
        IEnumerable<ParameterSignature>? parameters = null,
        IEnumerable<GenericParameterSignature>? genericParameters = null,
        PropertySignature? associatedProperty = null)
    {
        return new CallableSignature(
            name,
            kind,
            accessibility,
            isStatic || kind == CallableKind.StaticConstructor,
            isAsync,
            declaringType ?? Named("Example", "Service"),
            new TypeUseSignature(
                returnType ?? _Void,
                returnRefKind,
                TypeUseKind.Return),
            parameters ?? Array.Empty<ParameterSignature>(),
            genericParameters ?? Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
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
                AttributeTargetKind.Callable => target.Callable!.Attributes,
                AttributeTargetKind.Property => target.Property!.Attributes,
                AttributeTargetKind.Parameter => target.Parameter!.Attributes,
                AttributeTargetKind.Return => target.Callable!.ReturnAttributes,
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
    }
}
