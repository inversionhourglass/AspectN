using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class CallableSelectorTests
{
    public static TheoryData<MethodKind, MatchStatus> MethodKinds => new()
    {
        { MethodKind.OrdinaryMethod, MatchStatus.Matched },
        { MethodKind.PropertyGetter, MatchStatus.Matched },
        { MethodKind.PropertySetter, MatchStatus.Matched },
        { MethodKind.EventAdder, MatchStatus.Matched },
        { MethodKind.EventRemover, MatchStatus.Matched },
        { MethodKind.EventRaiser, MatchStatus.Matched },
        { MethodKind.Operator, MatchStatus.Matched },
        { MethodKind.Conversion, MatchStatus.Matched },
        { MethodKind.InstanceConstructor, MatchStatus.NotMatched },
        { MethodKind.StaticConstructor, MatchStatus.NotMatched }
    };

    [Theory]
    [MemberData(nameof(MethodKinds))]
    public void Match_Callable_UsesCallableDomain(
        MethodKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "callable(* Service.Run(..))",
            Callable("Run", kind));

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(MethodKind.OrdinaryMethod, MatchStatus.Matched)]
    [InlineData(MethodKind.PropertyGetter, MatchStatus.NotMatched)]
    [InlineData(MethodKind.Operator, MatchStatus.NotMatched)]
    [InlineData(MethodKind.InstanceConstructor, MatchStatus.NotMatched)]
    public void Match_Method_AcceptsOrdinaryMethodsOnly(
        MethodKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "method(* Service.Run(..))",
            Callable("Run", kind));

        Assert.Equal(expected, result.Status);
    }

    public static TheoryData<MethodKind, MatchStatus> ExecutionKinds => new()
    {
        { MethodKind.OrdinaryMethod, MatchStatus.Matched },
        { MethodKind.PropertyGetter, MatchStatus.Matched },
        { MethodKind.PropertySetter, MatchStatus.Matched },
        { MethodKind.EventAdder, MatchStatus.Matched },
        { MethodKind.EventRemover, MatchStatus.Matched },
        { MethodKind.EventRaiser, MatchStatus.Matched },
        { MethodKind.Operator, MatchStatus.Matched },
        { MethodKind.Conversion, MatchStatus.Matched },
        { MethodKind.InstanceConstructor, MatchStatus.Matched },
        { MethodKind.StaticConstructor, MatchStatus.Matched }
    };

    [Theory]
    [MemberData(nameof(ExecutionKinds))]
    public void Match_Execution_UsesAllDomain(
        MethodKind kind,
        MatchStatus expected)
    {
        var result = Match(
            "execution(* *.*(..))",
            Callable(ConstructorName(kind), kind));

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(MethodKind.OrdinaryMethod, MatchStatus.Matched)]
    [InlineData(MethodKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData(MethodKind.StaticConstructor, MatchStatus.NotMatched)]
    public void Match_Execution_AppliesMethodSignatureToConstructors(
        MethodKind kind,
        MatchStatus expected)
    {
        var returnType = kind == MethodKind.OrdinaryMethod
            ? Named("System", "String")
            : null;

        var result = Match(
            "execution(string Service.*(..))",
            Callable(ConstructorName(kind), kind, returnType: returnType));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_ExecutionVoidReturn_MatchesConstructorsPositively()
    {
        // 构造函数按元数据名（.ctor/.cctor）与 void 返回匹配：execution(void ...) 正向命中。
        var result = Match(
            "execution(void Service.*(..))",
            Callable(
                ConstructorName(MethodKind.InstanceConstructor),
                MethodKind.InstanceConstructor,
                returnType: Named("System", "Void")));

        Assert.Equal(MatchStatus.Matched, result.Status);
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
            MethodKind.OrdinaryMethod,
            returnType: originalType,
            parameters: new[] { Parameter(originalType, 0) },
            genericParameters: new[] { genericParameter });
        var constructed = Callable(
            "Map",
            MethodKind.OrdinaryMethod,
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

    [Theory]
    [InlineData("method($T *<,$T>(..))", 1)]
    [InlineData("method($T *<$T,>(..))", 0)]
    public void Match_EmptyGenericSlot_PreservesVariablePosition(string source, int position)
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var first = new GenericParameterSignature("First", GenericParameterOwnerKind.Method, "Run", assembly, 0);
        var second = new GenericParameterSignature("Second", GenericParameterOwnerKind.Method, "Run", assembly, 1);
        var firstType = new GenericParameterTypeSignature(first);
        var secondType = new GenericParameterTypeSignature(second);
        var matching = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: position == 0 ? firstType : secondType,
            genericParameters: [first, second]);
        var other = Callable("Run", MethodKind.OrdinaryMethod,
            returnType: position == 0 ? secondType : firstType,
            genericParameters: [first, second]);

        Assert.Equal(MatchStatus.Matched, Match(source, matching).Status);
        Assert.Equal(MatchStatus.NotMatched, Match(source, other).Status);
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
            MethodKind.OrdinaryMethod,
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
            MethodKind.OrdinaryMethod,
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
            MethodKind.OrdinaryMethod,
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
            MethodKind.OrdinaryMethod,
            declaringType: nestedType);

        var result = Match(
            "method(* Example..Outer/Inner.Run(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_BooleanExpression_CombinesSelectorResults()
    {
        var candidate = Callable(".ctor", MethodKind.InstanceConstructor);

        var result = Match(
            "method(* Service.Run(..)) || ctor(Service(..))",
            candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }
    private static MatchResult Match(
        string source,
        MethodSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static MethodSignature Callable(
        string name,
        MethodKind kind,
        bool isAsync = false,
        TypeSignature? returnType = null,
        IEnumerable<ParameterSignature>? parameters = null,
        IEnumerable<GenericParameterSignature>? genericParameters = null,
        IEnumerable<TypeSignature>? genericArguments = null,
        MethodSignature? originalDefinition = null,
        TypeSignature? declaringType = null)
    {
        return new MethodSignature(
            name,
            kind,
            Accessibility.Public,
            isStatic: kind == MethodKind.StaticConstructor,
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
            typeAttributes: null,
            originalDefinition);
    }

    private static string ConstructorName(MethodKind kind)
    {
        return kind == MethodKind.InstanceConstructor ? ".ctor"
            : kind == MethodKind.StaticConstructor ? ".cctor"
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
