using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Cecil;
using Mono.Cecil;
using Xunit;
using SharedParser = Cecil.AspectN.PatternParser;
using SharedTypeParser = Cecil.AspectN.TyppePatternParser;
using SharedSignatureParser = Cecil.AspectN.SignatureParser;

namespace AspectN.LegacyOracle.Tests;

public sealed class LegacyGrammarCompatibilityTests
{
    [Theory]
    [InlineData("method(!int *(..))")]
    [InlineData("method(* *..*(..))")]
    [InlineData("method(* *..StaticMethod(..))")]
    [InlineData("method(* *.StaticMethod(..))")]
    [InlineData("method(* *+.*(..))")]
    [InlineData("method(* *..*+.*(..))")]
    [InlineData("method(* *..+.*(..))")]
    [InlineData("method(*+ *(..))")]
    [InlineData("method(* *<,T>(..))")]
    [InlineData("method(* *<T,>(..))")]
    [InlineData("method(* 123(..))")]
    [InlineData("method(123 Type.Foo(..))")]
    [InlineData("method(async string *(..))")]
    [InlineData("method(async null *(..))")]
    public void Compile_SupportedSharedGrammar_BothAccept(string source)
    {
        Assert.Null(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method((int) *(..))")]
    [InlineData("method(* .StaticMethod(..))")]
    [InlineData("method(* ..StaticMethod(..))")]
    [InlineData("attr(ret * extra)")]
    [InlineData("method(* Type.Foo(int bool))")]
    public void Compile_ApprovedSharedCorrection_LegacyReportsError(string source)
    {
        // Approved by the user on 2026-10-01; see docs/language/legacy-compatibility.md.
        Assert.Null(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method(* Service <T>.Run())")]
    [InlineData("method(* Service.Run <T>())")]
    public void Compile_NonAdjacentGeneric_BothReject(string source)
    {
        Assert.NotNull(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("List<int,>")]
    [InlineData("List<,string>")]
    [InlineData("*..")]
    [InlineData("Example..")]
    [InlineData("*+")]
    [InlineData("123")]
    public void Compile_StandaloneSharedGrammar_BothAccept(string source)
    {
        Assert.Null(Record.Exception(() => SharedTypeParser.Parse(source)));
        var compilation = CompileType(source);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("()")]
    [InlineData("(int)")]
    public void Compile_InvalidTupleArity_LegacyReportsError(string source)
    {
        Assert.Null(Record.Exception(() => SharedTypeParser.Parse(source)));
        var compilation = CompileType(source);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("!int || string")]
    [InlineData("int || !string && bool")]
    public void Compile_StandaloneBoolean_UsesApprovedOperatorPrecedence(string source)
    {
        Assert.NotNull(Record.Exception(() => SharedTypeParser.Parse(source)));
        var compilation = CompileType(source);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("method(T *(..))", "StaticMethod")]
    [InlineData("method(int *(..))", "ArrayResult")]
    [InlineData("method(!int *(..))", "ArrayResult")]
    [InlineData("method(async void *(..))", "ContractMethod")]
    [InlineData("method(async void<> *(..))", "ContractMethod")]
    [InlineData("method(async void<..> *(..))", "ContractMethod")]
    [InlineData("method(async void<!> *(..))", "ContractMethod")]
    [InlineData("method(!int *(..))", "StaticMethod")]
    [InlineData("method(* *..*(..))", "StaticMethod")]
    [InlineData("method(* *..StaticMethod(..))", "StaticMethod")]
    [InlineData("method(* *.StaticMethod(..))", "StaticMethod")]
    [InlineData("method(* *<,T>(..))", "GenericMethod")]
    [InlineData("method(* *<T,>(..))", "GenericMethod")]
    [InlineData("method(* *+.*(..))", "StaticMethod")]
    [InlineData("method(* *..*+.*(..))", "StaticMethod")]
    [InlineData("method(* *..+.*(..))", "StaticMethod")]
    [InlineData("method(*+ *(..))", "StaticMethod")]
    public void Match_RepairedGrammar_AgreesWithShared(string source, string methodName)
    {
        var corpus = LegacyCorpusLoader.Load<LegacyCorpus>("legacy-patterns.json");
        using var context = new LegacyOracleContext(false,
            corpus.Assemblies.Where(assembly => assembly.Name == "AspectN.TestAssembly").ToArray());
        var candidate = Assert.Single(context.Candidates.Values, candidate =>
            candidate.Method.Name == methodName &&
            candidate.Method.DeclaringType.Name.StartsWith("LegacyFixture", StringComparison.Ordinal));
        var shared = SharedParser.Parse(source);
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var current));

        var oldResult = shared.IsMatch(SharedSignatureParser.ParseMethod(candidate.Method, false));
        var newResult = current!.Match(candidate.TypeSystem.Mapper.MapCallable(candidate.Method), candidate.TypeSystem);

        Assert.NotEqual(MatchStatus.Unknown, newResult.Status);
        Assert.Equal(oldResult, newResult.Status == MatchStatus.Matched);
    }

    [Theory]
    [InlineData("method(*.. *(..))", false, false)]
    [InlineData("method(*.. *(..))", true, false)]
    [InlineData("method(* *(*..))", false, false)]
    [InlineData("method(* *(*..))", true, false)]
    [InlineData("method(*..* *(..))", false, true)]
    [InlineData("method(*..* *(..))", true, false)]
    [InlineData("method(* *(*..*))", false, true)]
    [InlineData("method(* *(*..*))", true, false)]
    public void Match_GenericInWildcard_PreservesSharedTypePositionBehavior(
        string source, bool nestedType, bool expected)
    {
        using var module = ModuleDefinition.CreateModule("CompatibilityFixture", ModuleKind.Dll);
        var outer = new TypeDefinition("Example", "Outer", TypeAttributes.Public, module.TypeSystem.Object);
        var inner = new TypeDefinition(string.Empty, "Inner", TypeAttributes.NestedPublic, module.TypeSystem.Object);
        module.Types.Add(outer);
        outer.NestedTypes.Add(inner);
        TypeReference type = nestedType ? inner : module.TypeSystem.Int32;
        var method = new MethodDefinition("Run", MethodAttributes.Public, type);
        method.Parameters.Add(new ParameterDefinition(type));
        outer.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
        Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
    }

    [Theory]
    [InlineData("method((int,string) *(..))", "tuple", 1, true)]
    [InlineData("method((int,string) *(..))", "tuple", 2, true)]
    [InlineData("method(int? *(..))", "nullable", 1, true)]
    [InlineData("method(int?[] *(..))", "nullable", 1, false)]
    [InlineData("method(int?[] *(..))", "nullable-name", 1, true)]
    [InlineData("method(int+ *(..))", "int", 1, true)]
    [InlineData("method(async string *(..))", "task", 1, false)]
    [InlineData("method(*+ *(..))", "int", 1, true)]
    public void Match_ArrayShape_PreservesSharedStructuralPatternBehavior(
        string source, string elementKind, int rank, bool expected)
    {
        using var module = ModuleDefinition.CreateModule("ArrayCompatibilityFixture", ModuleKind.Dll);
        TypeReference element = module.TypeSystem.Int32;
        if (elementKind == "nullable-name")
        {
            element = new TypeReference("Example", "int?", module, module.TypeSystem.CoreLibrary);
        }
        else if (elementKind != "int")
        {
            var definition = elementKind switch
            {
                "tuple" => new TypeReference("System", "ValueTuple`2", module, module.TypeSystem.CoreLibrary, true),
                "nullable" => new TypeReference("System", "Nullable`1", module, module.TypeSystem.CoreLibrary, true),
                _ => new TypeReference("System.Threading.Tasks", "Task`1", module, module.TypeSystem.CoreLibrary)
            };
            var generic = new GenericInstanceType(definition);
            generic.GenericArguments.Add(elementKind == "task" ? module.TypeSystem.String : module.TypeSystem.Int32);
            if (elementKind == "tuple") generic.GenericArguments.Add(module.TypeSystem.String);
            element = generic;
        }

        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run", MethodAttributes.Public, new ArrayType(element, rank));
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
        Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
    }

    [Fact]
    public void Match_AssignableUniversalDeclaringType_IncludesGlobalInterface()
    {
        using var module = ModuleDefinition.CreateModule("GlobalCompatibilityFixture", ModuleKind.Dll);
        var declaring = new TypeDefinition(string.Empty, "IGlobal",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run",
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            module.TypeSystem.Void);
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        const string SOURCE = "method(* *..*+.*(..))";
        Assert.True(SharedParser.Parse(SOURCE).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
        Assert.True(CompileMember(SOURCE).TryGetMemberPattern(out var pattern));
        Assert.Equal(MatchStatus.Matched,
            pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
    }

    private static AspectNCompilation CompileMember(string source)
    {
        return AspectNCompilation.Create(AspectNParser.ParseMemberExpression(
            source, new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
    }

    private static AspectNCompilation CompileType(string source)
    {
        return AspectNCompilation.Create(AspectNParser.ParseTypePattern(
            source, new AspectNParseOptions(AspectNLanguageVersion.Legacy)));
    }
}
