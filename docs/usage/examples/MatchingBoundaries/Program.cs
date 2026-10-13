using AspectN;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using BoundaryExamples;

var engine = new AspectNEngine();
var typeSystem = new ReflectionTypeSystem();
var checks = 0;

// 日常类型和方法筛选，不需要 AOP 或织入框架。
var typeCandidates = new[] { typeof(string), typeof(int), typeof(string[]) };
var selectedTypes = typeCandidates.Where(type => MatchType("string", type) == MatchStatus.Matched).ToArray();
Check(selectedTypes.SequenceEqual(new[] { typeof(string) }), "按表达式查找类型");
var memberCompilation = engine.CompileMemberExpression("method(public string *(..))");
Check(memberCompilation.TryGetMemberPattern(out var memberPattern), "编译方法筛选表达式");
var selectedMethods = typeof(string).GetMethods()
    .Where(method => memberPattern!.Match(typeSystem.Mapper.MapMethod(method), typeSystem).Status == MatchStatus.Matched)
    .ToArray();
Check(selectedMethods.Any(method => method.Name == nameof(string.ToLowerInvariant)), "按签名查找方法");

// DSL 按数组维数匹配，身份查询区分形状。
var vector = typeSystem.Mapper.MapType(typeof(int[]));
var general = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));
Check(MatchSignature("int[]", vector) == MatchStatus.Matched, "一维 Vector 的 DSL 匹配");
Check(MatchSignature("int[]", general) == MatchStatus.Matched, "一维 General 的 DSL 匹配");
Check(typeSystem.AreEqual(vector, general).Status == MatchStatus.NotMatched, "形状不同的数组身份不等");
Check(typeSystem.IsAssignableTo(vector, general).Status == MatchStatus.Matched, "Vector 到 General 的方向");
Check(typeSystem.IsAssignableTo(general, vector).Status == MatchStatus.NotMatched, "General 到 Vector 的反方向");
CompileFailure("int[*]", false);
CompileFailure("int[]+", false, "AN2010");

// 裸 Type 只有引用形状；不要读取不可用的 RefKind 占位值。
var bare = typeSystem.Mapper.MapTypeUse(typeof(int).MakeByRefType(), TypeUseKind.Parameter);
Check(bare.Availability.IsKnown(MetadataFacts.ByReference), "裸 Type 的引用形状已知");
Check(bare.IsByReference, "裸 Type 保留引用标记");
Check(!bare.Availability.IsKnown(MetadataFacts.RefKind), "裸 Type 的精确分类不可用");
var parameter = typeSystem.Mapper.MapMethod(typeof(CandidateApi).GetMethod(nameof(CandidateApi.TryGet))!).Parameters[0].Type;
Check(parameter.Availability.IsKnown(MetadataFacts.RefKind) && parameter.RefKind == RefKind.Out, "成员上下文保留 out 分类");

// 省略 ref 注解不约束该维度，!ref 则要求按值传递。
Check(MatchMethod("method(* CandidateApi.TryGet(int))", nameof(CandidateApi.TryGet)) == MatchStatus.Matched, "省略 ref 注解");
Check(MatchMethod("method(* CandidateApi.TryGet(out int))", nameof(CandidateApi.TryGet)) == MatchStatus.Matched, "精确 out 注解");
Check(MatchMethod("method(* CandidateApi.TryGet(ref int))", nameof(CandidateApi.TryGet)) == MatchStatus.NotMatched, "ref 不等于 out");
Check(MatchMethod("method(* CandidateApi.TryGet(!ref int))", nameof(CandidateApi.TryGet)) == MatchStatus.NotMatched, "!ref 排除引用参数");
CompileFailure("method(* CandidateApi.TryGet(! ref int))", true, "AN2005");

// C# 引用类型可空注解不形成另一种 CLR 类型身份。
Check(MatchType("string?", typeof(string)) == MatchStatus.Matched, "string? 的运行时类型模式");
var firstReturn = typeSystem.Mapper.MapMethod(typeof(CandidateApi).GetMethod(nameof(CandidateApi.Echo))!).ReturnType.Type;
var nullableReturn = typeSystem.Mapper.MapMethod(typeof(CandidateApi).GetMethod(nameof(CandidateApi.MaybeNull))!).ReturnType.Type;
Check(typeSystem.AreEqual(firstReturn, nullableReturn).Status == MatchStatus.Matched, "string 与 string? 的声明返回类型身份");

// 名义关系不是完整 CLR 数组元素协变或泛型变体。
Check(typeof(object[,]).IsAssignableFrom(typeof(string[,])), "CLR 允许此数组元素协变");
Check(typeSystem.IsAssignableTo(typeSystem.Mapper.MapType(typeof(string[,])),
    typeSystem.Mapper.MapType(typeof(object[,]))).Status == MatchStatus.NotMatched, "AspectN 数组关系要求元素身份一致");
Check(MatchType("System.Collections.Generic.List", typeof(List<int>)) == MatchStatus.NotMatched, "省略泛型子句要求零元数");
Check(MatchType("System.Collections.Generic.List<?>", typeof(List<int>)) == MatchStatus.Matched, "任意泛型元数");

// 元数据模型或 C# 标记不自动成为 DSL 关键字。
Check(MatchMethod("method(* CandidateApi.Sum(int[]))", nameof(CandidateApi.Sum)) == MatchStatus.Matched, "params 方法可按数组参数匹配");
Check(MatchMethod("method(* CandidateApi.Read(ref int))", nameof(CandidateApi.Read)) == MatchStatus.Matched, "scoped ref 方法可按 ref 类型匹配");
CompileFailure("method(* CandidateApi.Sum(params int[]))", true);
CompileFailure("method(* CandidateApi.Read(scoped ref int))", true);
CompileFailure("field(int CandidateApi.Value)", true);
CompileFailure("event(* CandidateApi.Changed)", true);
var pointer = typeSystem.Mapper.MapType(typeof(int).MakePointerType());
Check(pointer is UnsupportedTypeSignature, "指针保留不支持的类型表示");
CompileFailure("regex((?=Run))", true, "AN2012");

// 同样的空列表，事实可能已知，也可能不可用；参数序号不能自动重排。
var typeUse = new TypeUseSignature(typeSystem.Mapper.MapType(typeof(int)), RefKind.None, TypeUseKind.Parameter);
var known = new ParameterSignature("value", 0, typeUse);
var unavailable = new ParameterSignature("value", 0, typeUse,
    availability: new MetadataAvailability(MetadataFacts.Attributes));
Check(known.Attributes.Count == 0 && known.Availability.IsKnown(MetadataFacts.Attributes), "已知空特性集合");
Check(unavailable.Attributes.Count == 0 && !unavailable.Availability.IsKnown(MetadataFacts.Attributes), "不可用的空集合占位值");
var mapped = typeSystem.Mapper.MapMethod(typeof(CandidateApi).GetMethod(nameof(CandidateApi.Update))!);
try
{
    _ = new MethodSignature(mapped.Name, mapped.Kind, mapped.Accessibility,
        mapped.IsStatic, mapped.IsAsync, mapped.DeclaringType, mapped.ReturnType,
        [new ParameterSignature("value", 1, mapped.Parameters[0].Type)],
        mapped.GenericParameters, mapped.GenericArguments, mapped.Attributes, mapped.ReturnAttributes);
    throw new InvalidOperationException("位置不连续的参数列表没有被拒绝。");
}
catch (ArgumentException)
{
    Check(true, "参数列表拒绝不连续位置");
}

// 64 层含根。原生映射抛异常；已构造中立签名匹配返回 Unknown。
var native = typeof(int);
TypeSignature neutral = typeSystem.Mapper.MapType(native);
for (var index = 0; index < 64; index++)
{
    native = native.MakeArrayType();
    neutral = new ArrayTypeSignature(neutral, 1);
}
try
{
    _ = typeSystem.Mapper.MapType(native);
    throw new InvalidOperationException("超限原生类型没有被拒绝。");
}
catch (AspectNLimitException exception)
{
    Check(exception.Diagnostic.Code == "AN3005", "原生深度超限诊断");
}
var wildcard = engine.CompileTypePattern("*");
Check(wildcard.TryGetTypePattern(out var wildcardPattern), "编译全类型通配符");
var depthResult = wildcardPattern!.Match(neutral, typeSystem);
Check(depthResult.Status == MatchStatus.Unknown && depthResult.Diagnostics.Any(diagnostic => diagnostic.Code == "AN3005"),
    "中立签名深度超限返回 Unknown");

Console.WriteLine($"边界示例验证通过：{checks} 项检查。");

MatchStatus MatchType(string source, Type type) => MatchSignature(source, typeSystem.Mapper.MapType(type));

MatchStatus MatchSignature(string source, TypeSignature signature)
{
    var compilation = engine.CompileTypePattern(source);
    Check(compilation.TryGetTypePattern(out var pattern), $"编译类型模式：{source}");
    return pattern!.Match(signature, typeSystem).Status;
}

MatchStatus MatchMethod(string source, string name)
{
    var compilation = engine.CompileMemberExpression(source);
    Check(compilation.TryGetMemberPattern(out var pattern), $"编译成员表达式：{source}");
    return pattern!.Match(typeSystem.Mapper.MapMethod(typeof(CandidateApi).GetMethod(name)!), typeSystem).Status;
}

void CompileFailure(string source, bool member, string? code = null)
{
    var compilation = member ? engine.CompileMemberExpression(source) : engine.CompileTypePattern(source);
    var succeeded = member ? compilation.TryGetMemberPattern(out _) : compilation.TryGetTypePattern(out _);
    Check(!succeeded && compilation.Diagnostics.Count > 0, $"拒绝表达式：{source}");
    if (code is not null) Check(compilation.Diagnostics.Any(diagnostic => diagnostic.Code == code), $"诊断码：{code}");
}

void Check(bool condition, string subject)
{
    if (!condition) throw new InvalidOperationException($"边界示例不符合预期：{subject}");
    checks++;
}
