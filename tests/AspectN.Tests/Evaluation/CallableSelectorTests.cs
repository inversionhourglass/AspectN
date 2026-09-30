using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class CallableSelectorTests
{
    public static TheoryData<CallableKind, MatchStatus> CallableKinds => new()
    {
        { CallableKind.OrdinaryMethod, MatchStatus.Matched },
        { CallableKind.PropertyGetter, MatchStatus.Matched },
        { CallableKind.PropertySetter, MatchStatus.Matched },
        { CallableKind.EventAdder, MatchStatus.Matched },
        { CallableKind.EventRemover, MatchStatus.Matched },
        { CallableKind.EventRaiser, MatchStatus.Matched },
        { CallableKind.Operator, MatchStatus.Matched },
        { CallableKind.Conversion, MatchStatus.Matched },
        { CallableKind.InstanceConstructor, MatchStatus.NotMatched },
        { CallableKind.StaticConstructor, MatchStatus.NotMatched }
    };

    [Theory]
    [MemberData(nameof(CallableKinds))]
    public void Match_Callable_UsesCallableDomain(
        CallableKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "callable(* Service.Run(..))",
            Callable("Run", kind));

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(CallableKind.OrdinaryMethod, MatchStatus.Matched)]
    [InlineData(CallableKind.PropertyGetter, MatchStatus.NotMatched)]
    [InlineData(CallableKind.Operator, MatchStatus.NotMatched)]
    [InlineData(CallableKind.InstanceConstructor, MatchStatus.NotMatched)]
    public void Match_Method_AcceptsOrdinaryMethodsOnly(
        CallableKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "method(* Service.Run(..))",
            Callable("Run", kind));

        Assert.Equal(expected, result.Status);
    }

    public static TheoryData<CallableKind, MatchStatus> ExecutionKinds => new()
    {
        { CallableKind.OrdinaryMethod, MatchStatus.Matched },
        { CallableKind.PropertyGetter, MatchStatus.Matched },
        { CallableKind.PropertySetter, MatchStatus.Matched },
        { CallableKind.EventAdder, MatchStatus.Matched },
        { CallableKind.EventRemover, MatchStatus.Matched },
        { CallableKind.EventRaiser, MatchStatus.Matched },
        { CallableKind.Operator, MatchStatus.Matched },
        { CallableKind.Conversion, MatchStatus.Matched },
        { CallableKind.InstanceConstructor, MatchStatus.Matched },
        { CallableKind.StaticConstructor, MatchStatus.Matched }
    };

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    public void Match_Execution_UsesAllDomain(
        CallableKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "execution(* *.*(..))",
            Callable(ConstructorName(kind), kind));

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(CallableKind.OrdinaryMethod, MatchStatus.Matched)]
    [InlineData(CallableKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData(CallableKind.StaticConstructor, MatchStatus.NotMatched)]
    public void Match_Execution_AppliesCallableSignatureToConstructors(
        CallableKind kind,
        MatchStatus expected)
    {
        var returnType = kind == CallableKind.OrdinaryMethod
            ? Named("System", "String")
            : null;

        var result = Match(
            "execution(string Service.*(..))",
            Callable(ConstructorName(kind), kind, returnType: returnType));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_ConstructedCallable_UsesSubstitutedSignature()
    {
        var genericParameter = new GenericParameterSignature(
            "T",
            GenericParameterOwnerKind.Method,
            "Example.Service.Map`1",
            new AssemblyIdentity("Example.Assembly"),
            0);
        var originalType = new GenericParameterTypeSignature(genericParameter);
        var original = Callable(
            "Map",
            CallableKind.OrdinaryMethod,
            returnType: originalType,
            parameters: new[] { Parameter(originalType, 0) },
            genericParameters: new[] { genericParameter });
        var constructed = Callable(
            "Map",
            CallableKind.OrdinaryMethod,
            returnType: Named("System", "String"),
            parameters: new[]
            {
                Parameter(Named("System", "Int32", TypeKind.Struct), 0)
            },
            genericArguments: new[] { Named("System", "String") },
            originalDefinition: original);

        var result = Match(
            "method(string Service.Map<string>(int))",
            constructed);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(original, constructed.OriginalDefinition);
    }

    [Fact]
    public void Match_DefinitionGenericClauseVariable_BindsFormalParameter()
    {
        var methodParameter = new GenericParameterSignature(
            "T",
            GenericParameterOwnerKind.Method,
            "Example.Converter.Convert`1",
            new AssemblyIdentity("Example.Assembly"),
            0);
        var parameterType = new GenericParameterTypeSignature(methodParameter);
        var candidate = Callable(
            "Convert",
            CallableKind.OrdinaryMethod,
            returnType: parameterType,
            parameters: new[] { Parameter(parameterType, 0) },
            genericParameters: new[] { methodParameter },
            declaringType: Named("Example", "Converter"));

        var result = Match("method($T Converter.Convert<$T>($T))", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_DefinitionDeclaringTypeVariableArgument_BindsFormalParameter()
    {
        var declaringType = Definition("Example", "Repo", 1);
        var typeParameter = new GenericParameterSignature(
            "T",
            GenericParameterOwnerKind.Type,
            "Example.Repo`1",
            declaringType.Identity.Assembly,
            0);
        var parameterType = new GenericParameterTypeSignature(typeParameter);
        var candidate = Callable(
            "Find",
            CallableKind.OrdinaryMethod,
            returnType: parameterType,
            parameters: new[] { Parameter(parameterType, 0) },
            declaringType: declaringType);

        var result = Match("method($T Repo<$T>.Find($T))", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_DefinitionDeclaringTypeConcreteArgument_ReturnsNotMatched()
    {
        var candidate = Callable(
            "Find",
            CallableKind.OrdinaryMethod,
            returnType: Named("System", "Int32", TypeKind.Struct),
            declaringType: Definition("Example", "Repo", 1));

        var result = Match("method(* Repo<int>.Find(..))", candidate);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void Match_MemberPath_EvaluatesRecursiveNamespaceAndNestedType()
    {
        var outerIdentity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            "Example.Features",
            "Outer",
            0,
            TypeKind.Class);
        var nestedType = new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                string.Empty,
                "Inner",
                0,
                TypeKind.Class,
                outerIdentity));
        var candidate = Callable(
            "Run",
            CallableKind.OrdinaryMethod,
            declaringType: nestedType);

        var result = Match(
            "method(* Example..Outer/Inner.Run(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_BooleanExpression_CombinesSelectorResults()
    {
        var candidate = Callable(".ctor", CallableKind.InstanceConstructor);

        var result = Match(
            "method(* Service.Run(..)) || ctor(Service(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }
    private static MatchResult Match(
        string source,
        CallableSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static CallableSignature Callable(
        string name,
        CallableKind kind,
        bool isAsync = false,
        TypeSignature? returnType = null,
        IEnumerable<ParameterSignature>? parameters = null,
        IEnumerable<GenericParameterSignature>? genericParameters = null,
        IEnumerable<TypeSignature>? genericArguments = null,
        CallableSignature? originalDefinition = null,
        TypeSignature? declaringType = null)
    {
        return new CallableSignature(
            name,
            kind,
            Accessibility.Public,
            isStatic: kind == CallableKind.StaticConstructor,
            isAsync,
            declaringType ?? Named("Example", "Service"),
            new TypeUseSignature(
                returnType ?? Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            parameters ?? Array.Empty<ParameterSignature>(),
            genericParameters ?? Array.Empty<GenericParameterSignature>(),
            genericArguments ?? Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(),
            originalDefinition);
    }

    private static string ConstructorName(CallableKind kind)
    {
        return kind == CallableKind.InstanceConstructor ? ".ctor"
            : kind == CallableKind.StaticConstructor ? ".cctor"
            : "Run";
    }

    private static ParameterSignature Parameter(
        TypeSignature type,
        int position)
    {
        return new ParameterSignature(
            $"value{position}",
            position,
            new TypeUseSignature(type, RefKind.None, TypeUseKind.Parameter));
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

    private static NamedTypeSignature Definition(
        string @namespace,
        string name,
        int arity)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                arity,
                TypeKind.Class));
    }
}
