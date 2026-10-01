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
    [InlineData("method(any * *(..))")]
    [InlineData("method(none * *(..))")]
    [InlineData("method(!any * *(..))")]
    [InlineData("method(!none * *(..))")]
    public void Compile_ApprovedSharedCorrection_LegacyReportsError(string source)
    {
        // Approved by the user on 2026-10-01; see docs/language/legacy-compatibility.md.
        // any/none 是旧实现 ModifierPattern.Flags 枚举名泄露为修饰符（none 等价 any），
        // 不是文档化访问性关键字，Legacy 拒绝。
        Assert.Null(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method(ASYNC void *(..))")]
    [InlineData("method(Async void *(..))")]
    [InlineData("method(int [] *(..))")]
    [InlineData("method(int ? *(..))")]
    public void Compile_SharedRejects_LegacyAlsoRejects(string source)
    {
        // 用户批准 2026-10-01：async 糖严格小写；[ / ? 与类型模式邻接（对齐 Shared
        // 的类型累积截断）。Shared 本就拒绝这些写法，Legacy 不再额外接受。
        Assert.NotNull(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method((int,string)[] *(..))")]
    [InlineData("method((int,string)? *(..))")]
    [InlineData("method(List<int || string> *(..))")]
    public void Compile_SharedRejects_LegacyStructuredEnhancement_Accepts(string source)
    {
        // 用户批准 2026-10-01：tuple 结构化后缀与泛型实参内 Boolean 是 Legacy 增强；
        // Shared 在返回位置拒绝这些写法（独立入口忽略尾随后缀），Legacy 按结构化语义接受。
        Assert.NotNull(Record.Exception(() => SharedParser.Parse(source)));
        var compilation = CompileMember(source);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("int []")]
    [InlineData("int ?")]
    public void Compile_SharedThrowsOnSpacedSuffix_LegacyAlsoRejects(string source)
    {
        Assert.NotNull(Record.Exception(() => SharedTypeParser.Parse(source)));
        var compilation = CompileType(source);

        Assert.Contains(compilation.Diagnostics, diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.False(compilation.TryGetTypePattern(out _));
    }

    [Theory]
    [InlineData("int[]")]
    [InlineData("(int,string)[]")]
    public void Compile_LegacyStructuredSuffix_StillAccepted(string source)
    {
        // 独立入口的结构化后缀（tuple 后缀等）是 Legacy 增强：独立入口 Shared 忽略
        // 尾随后缀、返回位置 Shared 拒绝；Legacy 统一按结构化语义接受。
        Assert.Null(Record.Exception(() => SharedTypeParser.Parse(source)));
        var compilation = CompileType(source);

        Assert.Empty(compilation.Diagnostics);
        Assert.True(compilation.TryGetTypePattern(out _));
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

    [Theory]
    [InlineData("attr(type *)", true)]
    [InlineData("attr(* *)", true)]
    [InlineData("attr(exec *)", false)]
    public void Match_AttributeTypeTarget_AgreesWithShared(
        string source,
        bool expected)
    {
        // C1 修复（用户批准 2026-10-01）：通配 / 复合特性查询补上 Type 目标，
        // 声明类型特性随候选签名映射（对应旧 Shared 的 AttributeSignatures.Types）。
        using var module = ModuleDefinition.CreateModule("AttrTypeCompatibilityFixture", ModuleKind.Dll);
        var attribute = new TypeDefinition("Example", "MarkerAttribute", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(attribute);
        var markerCtor = new MethodReference(".ctor", module.TypeSystem.Void, attribute) { HasThis = true };
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        declaring.CustomAttributes.Add(new CustomAttribute(markerCtor));
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run", MethodAttributes.Public, module.TypeSystem.Int32);
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
        Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
    }

    [Theory]
    [InlineData("ctor(*..*())", "Nested::.ctor", true)]
    [InlineData("ctor(Example.Service/Nested())", "Nested::.ctor", true)]
    [InlineData("ctor(*..Generic<T1,T2>())", "Generic::.ctor", true)]
    [InlineData("ctor(*..*(*))", "Nested::.ctor", false)]
    public void Match_ConstructorDeclaringType_AgreesWithShared(
        string source,
        string candidateName,
        bool expected)
    {
        // C2 修复（用户批准 2026-10-01）：ctor 声明类型改走 genericOut 规则——
        // 声明泛型变量、* / *..* 视为含嵌套的通配；与 Shared 一致。
        // 注意 IsGetter/IsSetter 等语义标志要经写读回环才可见，这里统一回环。
        using var buffer = new MemoryStream();
        using (var original = ModuleDefinition.CreateModule("CtorCompatibilityFixture", ModuleKind.Dll))
        {
            var outer = new TypeDefinition("Example", "Service", TypeAttributes.Public, original.TypeSystem.Object);
            original.Types.Add(outer);
            var nested = new TypeDefinition(string.Empty, "Nested", TypeAttributes.NestedPublic, original.TypeSystem.Object);
            outer.NestedTypes.Add(nested);
            var nestedCtor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, original.TypeSystem.Void);
            nested.Methods.Add(nestedCtor);
            var generic = new TypeDefinition("Example", "Generic`2", TypeAttributes.Public, original.TypeSystem.Object);
            original.Types.Add(generic);
            generic.GenericParameters.Add(new GenericParameter("TFirst", generic));
            generic.GenericParameters.Add(new GenericParameter("TSecond", generic));
            var genericCtor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, original.TypeSystem.Void);
            generic.Methods.Add(genericCtor);
            original.Write(buffer);
        }

        buffer.Position = 0;
        using var module = ModuleDefinition.ReadModule(buffer);
        var candidate = module.GetType("Example", "Service").NestedTypes.First(t => t.Name == "Nested").Methods.First(m => m.Name == ".ctor");
        if (candidateName == "Generic::.ctor")
        {
            candidate = module.GetType("Example", "Generic`2").Methods.First(m => m.Name == ".ctor");
        }

        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(candidate, false)));
        Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            pattern!.Match(typeSystem.Mapper.MapCallable(candidate), typeSystem).Status);
    }

    [Theory]
    [InlineData("setter(private * *)", "set_Value", true)]
    [InlineData("setter(public * *)", "set_Value", false)]
    [InlineData("property(private * *)", "set_Value", true)]
    [InlineData("getter(public * *)", "get_Value", true)]
    [InlineData("getter(private * *)", "get_Value", false)]
    public void Match_PropertyAccessibility_UsesAccessorModifiers(
        string source,
        string accessorName,
        bool expected)
    {
        // C3 修复（用户批准 2026-10-01）：property/getter/setter 一律按当前 accessor
        // 自身权限求值（public get / private set 时整体权限与 setter 权限不同）。
        // IsGetter/IsSetter 语义标志与 setter 的 value 参数都要经写读回环补全。
        using var buffer = new MemoryStream();
        using (var original = ModuleDefinition.CreateModule("PropertyCompatibilityFixture", ModuleKind.Dll))
        {
            var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, original.TypeSystem.Object);
            original.Types.Add(declaring);
            var property = new PropertyDefinition("Value", PropertyAttributes.None, original.TypeSystem.Int32);
            declaring.Properties.Add(property);
            var getter = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, original.TypeSystem.Int32);
            var setter = new MethodDefinition("set_Value", MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.HideBySig, original.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, original.TypeSystem.Int32));
            getter.SemanticsAttributes = MethodSemanticsAttributes.Getter;
            setter.SemanticsAttributes = MethodSemanticsAttributes.Setter;
            property.GetMethod = getter;
            property.SetMethod = setter;
            declaring.Methods.Add(getter);
            declaring.Methods.Add(setter);
            original.Write(buffer);
        }

        buffer.Position = 0;
        using var module = ModuleDefinition.ReadModule(buffer);
        var declaring2 = module.Types.First(t => t.Name == "Service");
        var candidate = accessorName == "set_Value"
            ? declaring2.Methods.First(m => m.Name == "set_Value")
            : declaring2.Methods.First(m => m.Name == "get_Value");
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(candidate, false)));
        Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
            pattern!.Match(typeSystem.Mapper.MapCallable(candidate), typeSystem).Status);
    }

    [Fact]
    public void Match_WildcardReturn_IncludesRefReturnLikeShared()
    {
        // C7 修复（用户批准 2026-10-01）：通配返回 * 与 Shared 一致，不区分 ref 返回；
        // 具名返回（int）双方都不匹配 ref int 返回。
        using var module = ModuleDefinition.CreateModule("RefReturnCompatibilityFixture", ModuleKind.Dll);
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var field = new FieldDefinition("Storage", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Int32);
        declaring.Fields.Add(field);
        var method = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static, new ByReferenceType(module.TypeSystem.Int32));
        declaring.Methods.Add(method);
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ldsflda, field));
        method.Body.Instructions.Add(Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Ret));
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        foreach (var (source, expected) in new[] { ("method(* *(..))", true), ("method(int *(..))", false) })
        {
            var compilation = CompileMember(source);
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
            Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
                pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
        }
    }

    [Fact]
    public void Match_BareGenericInName_RequiresZeroArityLikeShared()
    {
        // C5 修复（用户批准 2026-10-01）：genericIn 裸单段名省略泛型子句表示零泛型
        // （Shared 的 SingleNonGenericNamePattern）；限定名省略仍匹配任意泛型；
        // *[] 的元素同样要求零泛型。
        using var module = ModuleDefinition.CreateModule("BareNameCompatibilityFixture", ModuleKind.Dll);
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var listOfInt = new GenericInstanceType(new TypeReference("System.Collections.Generic", "List`1", module, module.TypeSystem.CoreLibrary, true));
        listOfInt.GenericArguments.Add(module.TypeSystem.Int32);
        var method = new MethodDefinition("Run", MethodAttributes.Public, listOfInt);
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        foreach (var (source, expected) in new[]
        {
            ("method(List *(..))", false),
            ("method(System.Collections.Generic.List *(..))", true),
            ("method(* *(List))", false),
        })
        {
            var compilation = CompileMember(source);
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
            Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
                pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
        }

        var arrayOfList = new ArrayType(listOfInt);
        foreach (var (source, expected) in new[] { ("*[]", false), ("System.Collections.Generic.List[]", true) })
        {
            var compilation = CompileType(source);
            Assert.True(compilation.TryGetTypePattern(out var pattern));
            Assert.Equal(expected, SharedTypeParser.Parse(source).IsMatch(SharedSignatureParser.ParseType(arrayOfList)));
            Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
                pattern!.Match(typeSystem.Mapper.MapType(arrayOfList), typeSystem).Status);
        }
    }

    [Fact]
    public void Match_GlobalNamespaceType_MatchesStarDotDotPrefixLikeShared()
    {
        // C6 修复（用户批准 2026-10-01）：genericIn 的裸 *.. 前缀匹配任意命名空间
        // （含全局命名空间），与 Shared 的 NamespacePattern 特例一致。
        using var module = ModuleDefinition.CreateModule("GlobalNsCompatibilityFixture", ModuleKind.Dll);
        var global = new TypeDefinition(string.Empty, "GlobalFoo", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(global);
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run", MethodAttributes.Public, global);
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        foreach (var (source, expected) in new[]
        {
            ("method(*..* *(..))", true),
            ("method(*..GlobalFoo *(..))", true),
        })
        {
            var compilation = CompileMember(source);
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.Equal(expected, SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
            Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.NotMatched,
                pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
        }
    }

    [Theory]
    [InlineData("method(* Service.Run(ref int, out string, in double))")]
    [InlineData("method(* Service.Run(ref int))")]
    public void Match_RefKindParameters_ApprovedEnhancementBeyondShared(string source)
    {
        // 用户批准 2026-10-01：ref/out/in 参数按 ref kind 匹配是 Legacy 增强。
        // Shared 把 (ref int) 解析为双槽参数（"ref" 名 + Any），永不匹配单参数方法。
        using var module = ModuleDefinition.CreateModule("RefParamCompatibilityFixture", ModuleKind.Dll);
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run", MethodAttributes.Public, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("first", ParameterAttributes.None, new ByReferenceType(module.TypeSystem.Int32)));
        method.Parameters.Add(new ParameterDefinition("second", ParameterAttributes.Out, new ByReferenceType(module.TypeSystem.String)));
        method.Parameters.Add(new ParameterDefinition("third", ParameterAttributes.In, new ByReferenceType(module.TypeSystem.Double)));
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        if (source.EndsWith("(ref int))", StringComparison.Ordinal))
        {
            // 单独 (ref int) 与三参数方法的元数不符：双方都 NotMatched。
            Assert.False(SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
            Assert.Equal(MatchStatus.NotMatched,
                pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
            return;
        }

        Assert.False(SharedParser.Parse(source).IsMatch(SharedSignatureParser.ParseMethod(method, false)));
        Assert.Equal(MatchStatus.Matched,
            pattern!.Match(typeSystem.Mapper.MapCallable(method), typeSystem).Status);
    }

    [Theory]
    [InlineData("int+[]", "array", false)]
    [InlineData("int+[]", "int-array", true)]
    [InlineData("int+?", "nullable", true)]
    [InlineData("int?+", "nullable", true)]
    [InlineData("Box?+", "int", false)]
    public void Match_MixedSuffix_ApprovedStructuredSemantics(
        string source,
        string candidateKind,
        bool expected)
    {
        // 用户批准 2026-10-01：混合后缀按 AST 逐层组合（int+[] = 元素可赋值的数组、
        // int+? / int?+ = 组合 nullable），是对 Shared 尾部单后缀 + 字面名称拼接的修复；
        // Box?+ 对非 Nullable 候选直接 NotMatched（不再 Unknown）。
        using var module = ModuleDefinition.CreateModule("MixedSuffixCompatibilityFixture", ModuleKind.Dll);
        TypeReference candidate = module.TypeSystem.Int32;
        if (candidateKind == "nullable")
        {
            var nullable = new GenericInstanceType(new TypeReference("System", "Nullable`1", module, module.TypeSystem.CoreLibrary, true));
            nullable.GenericArguments.Add(module.TypeSystem.Int32);
            candidate = nullable;
        }
        else if (candidateKind == "int-array")
        {
            candidate = new ArrayType(module.TypeSystem.Int32);
        }
        else if (candidateKind == "array")
        {
            var listOfInt = new GenericInstanceType(new TypeReference("System.Collections.Generic", "List`1", module, module.TypeSystem.CoreLibrary, true));
            listOfInt.GenericArguments.Add(module.TypeSystem.Int32);
            candidate = new ArrayType(listOfInt);
        }

        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileType(source);
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        if (expected)
        {
            // 保留的结构化增强：Shared 不匹配（或按字面名称匹配），Legacy 命中。
            Assert.False(SharedTypeParser.Parse(source).IsMatch(SharedSignatureParser.ParseType(candidate)));
            Assert.Equal(MatchStatus.Matched,
                pattern!.Match(typeSystem.Mapper.MapType(candidate), typeSystem).Status);
        }
        else
        {
            Assert.Equal(SharedTypeParser.Parse(source).IsMatch(SharedSignatureParser.ParseType(candidate)),
                pattern!.Match(typeSystem.Mapper.MapType(candidate), typeSystem).Status == MatchStatus.Matched);
        }
    }

    [Fact]
    public void Match_NullTypeName_ApprovedEnhancementBeyondShared()
    {
        // C8（用户批准 2026-10-01 保持现状）：裸 null 作为普通类型名参与匹配，
        // 可命中 CLR 名为 null 的类型；Shared 的 NullTypePattern 永不匹配。
        using var module = ModuleDefinition.CreateModule("NullNameCompatibilityFixture", ModuleKind.Dll);
        var nullType = new TypeDefinition("Example", "null", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(nullType);
        var declaring = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(declaring);
        var method = new MethodDefinition("Run", MethodAttributes.Public, nullType);
        declaring.Methods.Add(method);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var compilation = CompileMember("method(null *(..))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.False(SharedParser.Parse("method(null *(..))").IsMatch(SharedSignatureParser.ParseMethod(method, false)));
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
