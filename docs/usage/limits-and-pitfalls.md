# 限制与常见坑

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": ["GEN-IDENTITY-001", "GEN-BINDING-001", "USE-REF-001", "USE-ARRAY-001", "ATTR-TARGETS-001", "SEM-REGEX-001", "CSH-METADATA-001", "CROSS-AVAILABILITY-001", "CROSS-LOGIC-001"],
  "inventory_refs": ["INV-0006", "INV-0007", "INV-0008", "INV-0009", "INV-0010", "INV-0012", "INV-0013", "INV-0015"]
}
-->

本章区分表达式无法表示的条件、输入缺少所需事实、超出实现资源上限，以及已修复问题的历史记录。编译失败、匹配返回 Unknown 和映射抛异常发生在不同阶段，处理方式也不同。

下面的片段使用 `AspectN`、`AspectN.Metadata`、`AspectN.Reflection`、`AspectN.Evaluation` 命名空间，以及同一个引擎和类型系统：

```csharp
var engine = new AspectNEngine();
var typeSystem = new ReflectionTypeSystem();
```

可直接运行的[完整示例](examples/MatchingBoundaries/Program.cs)包含候选方法定义、类型/方法筛选和结果断言：

```powershell
dotnet run --project docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj -c Release
```

## 1. 数组维数相同，形状和身份仍可能不同

```csharp
var vector = typeSystem.Mapper.MapType(typeof(int[]));
var general = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));
var compilation = engine.CompileTypePattern("int[]");
if (!compilation.TryGetTypePattern(out var pattern))
    throw new InvalidOperationException("类型模式编译失败。");

Console.WriteLine(pattern.Match(vector, typeSystem).Status);  // Matched
Console.WriteLine(pattern.Match(general, typeSystem).Status); // Matched
Console.WriteLine(typeSystem.AreEqual(vector, general).Status); // NotMatched
Console.WriteLine(typeSystem.IsAssignableTo(vector, general).Status); // Matched
Console.WriteLine(typeSystem.IsAssignableTo(general, vector).Status); // NotMatched
```

`int[]` 的 DSL 模式按维数匹配两种一维数组；模型分别保存 Vector/General，因此不能由模式匹配成功推出身份相等。两个方向的名义关系也不同。

```csharp
var unsupported = engine.CompileTypePattern("int[*]");
Console.WriteLine(unsupported.TryGetTypePattern(out _)); // False，有编译诊断
```

`int[*]` 是说明 General 一维数组的元数据记号，DSL 没有该语法。需要区分形状时读取模型，不用正则签名文本恢复额外条件。[详细依据](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)。

## 2. 裸引用 Type 无法区分 ref、out、in

```csharp
var bare = typeSystem.Mapper.MapTypeUse(
    typeof(int).MakeByRefType(), TypeUseKind.Parameter);

Console.WriteLine(bare.Availability.IsKnown(MetadataFacts.ByReference)); // True
Console.WriteLine(bare.IsByReference);                                  // True
Console.WriteLine(bare.Availability.IsKnown(MetadataFacts.RefKind));     // False
```

不能仅凭存储的 `bare.RefKind` 推断精确分类；可用性为 False 时它只是占位值。区别来自参数或返回值的成员上下文。

```csharp
// 示例候选：public static void TryGet(out int value) => value = 42;
var method = typeof(BoundaryExamples.CandidateApi).GetMethod("TryGet")!;
var parameter = typeSystem.Mapper.MapMethod(method).Parameters[0].Type;
Console.WriteLine(parameter.Availability.IsKnown(MetadataFacts.RefKind)); // True
Console.WriteLine(parameter.RefKind);                                    // Out
```

从已有成员映射参数或返回值，可保留精确分类。[详细依据](../knowledge/capabilities/type-uses/USE-REF-001.md)。

## 3. 省略 ref 注解不等于要求按值传递

对上述 `TryGet(out int)`，表达式结果如下：

```text
method(* CandidateApi.TryGet(int))       // Matched：没有约束 ref 维度
method(* CandidateApi.TryGet(out int))   // Matched：精确 Out
method(* CandidateApi.TryGet(ref int))   // NotMatched：Ref 与 Out 不同
method(* CandidateApi.TryGet(!ref int))  // NotMatched：要求非引用
method(* CandidateApi.TryGet(! ref int)) // 编译失败：AN2005
```

`!ref` 必须邻接；返回槽不能使用 `out` 或 `in`。这些注解属于成员的参数/返回位置，不是纯类型表达式。

## 4. 可空引用注解不能用来区分 CLR 类型身份

```csharp
// 候选定义
public static string Echo(string value) => value;
public static string? MaybeNull(string? value) => value;
```

两个方法的返回类型均映射为同一 `System.String` 身份，`CompileTypePattern("string?")` 也能匹配 `typeof(string)`。不能用它证明源代码是否声明了 `?`，更不能据此推断返回值运行时一定为 null 或非 null。

值类型不同：`int?` 表示 `Nullable<int>`。引用可空性是源代码注解，CLR 类型身份不保留这一独立区别；AspectN 类型模式不是静态可空性分析器。

## 5. 名义关系不覆盖完整 CLR 协变与逆变

```csharp
Console.WriteLine(typeof(object[,]).IsAssignableFrom(typeof(string[,]))); // True

var candidate = typeSystem.Mapper.MapType(typeof(string[,]));
var target = typeSystem.Mapper.MapType(typeof(object[,]));
Console.WriteLine(typeSystem.IsAssignableTo(candidate, target).Status); // NotMatched
```

CLR 允许该数组元素协变，AspectN 的数组关系要求元素身份一致。`+` 使用已批准的名义关系闭包和结构匹配，不能将运行时 `IsAssignableFrom` 作为全部表达式语义的判断标准；泛型协变、逆变也没有因此成为支持承诺。

此外，关系目标有结构白名单，`int[]+` 不能表示数组关系目标。数组之间的上述关系用明确模型调用 `IsAssignableTo`。

## 6. 具体名称省略泛型子句，要求元数为零

```text
System.Collections.Generic.List       // 不匹配 List<int>
System.Collections.Generic.List<?>    // 匹配 List<int>，不限元数
```

泛型元数就是声明的泛型形参数量。省略子句不是“任何泛型”；裸 `*` 的完整通配规则另有定义。模式变量 `$T` 也不是 CLR 声明形参名称，不能把同名形参当作同一声明。[泛型身份与上下文](../knowledge/capabilities/generics/GEN-IDENTITY-001.md)给出具体范围。

## 7. C# 语法、元数据表示与 DSL 不是同一层

### params 和 scoped

```csharp
public static int Sum(params int[] values) => values.Sum();
public static int Read(scoped ref int value) => value;
```

```text
method(* CandidateApi.Sum(int[]))         // 可按数组参数匹配
method(* CandidateApi.Read(ref int))      // 可按引用参数匹配
method(* CandidateApi.Sum(params int[]))  // 编译失败
method(* CandidateApi.Read(scoped ref int)) // 编译失败
```

方法可以参与匹配，但 DSL 不提供这两个 C# 修饰符的独立筛选条件。需要筛选该源代码事实时，结合 Roslyn 或调用方自身的分析。

### 字段、事件声明及指针

```text
field(int CandidateApi.Value)     // 没有字段声明选择器，编译失败
event(* CandidateApi.Changed)     // 没有事件声明选择器，编译失败
```

事件访问器属于 `callable` 范围，不能由此推出支持独立事件声明选择器。

```csharp
var pointer = typeSystem.Mapper.MapType(typeof(int).MakePointerType());
Console.WriteLine(pointer is UnsupportedTypeSignature); // True
```

模型保留指针/函数指针的显示形式，不代表 DSL 支持结构化匹配它们。`int*` 在模式中可能按名称通配符解释，不能当作 C# 指针语法；规范正则文本可用于已承诺的文本匹配，也不会新增结构语法。

### 正则表达式不是完整 .NET 正则语法

```csharp
var compilation = engine.CompileMemberExpression("regex((?=Run))");
Console.WriteLine(compilation.TryGetMemberPattern(out _)); // False
// (?=...) 是环视：编译诊断 AN2012。
```

受限子集也排除内联选项、反向引用等形式。无效正则语法使用 AN3002，匹配超时使用 Unknown/AN3003。原因和完整列表见[规范第 15 节](../superpowers/specs/2026-07-31-aspectn-language-spec.md)。

## 8. 资源超限分阶段处理

类型结构最多 64 层，包含根；包含外层类型的有效类型泛型元数和方法自身泛型元数分别最多 64。

```csharp
var native = typeof(int);
TypeSignature neutral = typeSystem.Mapper.MapType(native);
for (var index = 0; index < 64; index++)
{
    native = native.MakeArrayType();
    neutral = new ArrayTypeSignature(neutral, 1);
}
// int 根加 64 层数组，共 65 层。
try
{
    _ = typeSystem.Mapper.MapType(native);
}
catch (AspectNLimitException exception)
{
    Console.WriteLine(exception.Diagnostic.Code); // AN3005
}

var compilation = engine.CompileTypePattern("*");
if (!compilation.TryGetTypePattern(out var pattern))
    throw new InvalidOperationException("通配符编译失败。");
var result = pattern.Match(neutral, typeSystem);
Console.WriteLine(result.Status); // Unknown，附 AN3005
```

原生映射深度超限抛异常；已构造中立签名的匹配返回 Unknown。元数超限对应 AN3006。不要把超限当作普通元数据缺失，也不要自设解析深度、正则超时或规范文本阈值；完整约定见[规范第 21 节](../superpowers/specs/2026-07-31-aspectn-language-spec.md)。

## 9. 空集合、未知事实与人工损坏输入

已知空特性集合表示确实没有特性；不可用集合的空值仅是占位值：

```csharp
var typeUse = new TypeUseSignature(
    typeSystem.Mapper.MapType(typeof(int)), RefKind.None, TypeUseKind.Parameter);
var known = new ParameterSignature("value", 0, typeUse);
var unavailable = new ParameterSignature("value", 0, typeUse,
    availability: new MetadataAvailability(MetadataFacts.Attributes));
Console.WriteLine(known.Attributes.Count);                              // 0
Console.WriteLine(known.Availability.IsKnown(MetadataFacts.Attributes)); // True
Console.WriteLine(unavailable.Attributes.Count);                        // 0
Console.WriteLine(unavailable.Availability.IsKnown(MetadataFacts.Attributes)); // False
```

手工构造参数集合时，`Position` 必须按声明序从零连续递增，不会自动重排：

```csharp
var mapped = typeSystem.Mapper.MapMethod(
    typeof(BoundaryExamples.CandidateApi).GetMethod("Update")!);
var wrongPosition = new ParameterSignature("value", 1, mapped.Parameters[0].Type);
// 将 Position=1 的参数放到列表第零项：构造时抛 ArgumentException。
_ = new MethodSignature(mapped.Name, mapped.Kind, mapped.Accessibility,
    mapped.IsStatic, mapped.IsAsync, mapped.DeclaringType, mapped.ReturnType,
    [wrongPosition], mapped.GenericParameters, mapped.GenericArguments,
    mapped.Attributes, mapped.ReturnAttributes);
```

完整示例捕获并断言这个异常。缺失事实在何处影响匹配，见[结果与诊断](results-and-diagnostics.md)。

H1 的人工损坏 Cecil 对象（缺 ReturnType/ElementType）仍保留异常，维护者决定不增加容错或改成 Unknown；尚未证明真实程序集可达。[历史处置](../reviews/findings/FND-20261011-001.md)保留条件和证据缺口。完整可运行示例不包含 H1，不把其他正常输入的验证当作 H1 已修复。

## 10. 未知、未建档不等于不支持

这些状态描述证据和盘点缺口，不据此新增排除承诺。不支持原因依[正式规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[架构范围](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)；九分类的详细证据可从[知识库](../knowledge/index.md)按主题读取。

## 11. Inherited=true 不会给派生声明新增直接记录

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class MarkerAttribute : Attribute { }

[Marker]
public class BaseApi
{
    [Marker] public virtual void Run() { }
}

public class DerivedApi : BaseApi
{
    public override void Run() { }
}
```

运行时 Attribute.IsDefined(typeof(DerivedApi), typeof(MarkerAttribute), inherit: true) 为 true；使用 inherit: false 为 false。对 DerivedApi.Run 的方法查询也是如此。AspectN 的 attr(type MarkerAttribute) 与 attr(call MarkerAttribute) 查询直接记录，因此派生 override 候选得到 NotMatched；基类候选或显式补上 [Marker] 的派生声明得到 Matched。

这属于已批准的查询范围，不是元数据丢失。需按 C# 特性继承规则查找时，由调用方执行原生继承查询；不要通过 + 类型关系语法假设 attr 也继承。具体[特性场景及四视图证据](../knowledge/capabilities/attributes/ATTR-TARGETS-001.md)区分继承排除和语法排除。

## 12. 残缺泛型签名按所需事实求值

```csharp
var partial = new NamedTypeSignature(
    new TypeIdentity(new AssemblyIdentity("Review"), "Review", "Box", 2, AspectN.Metadata.TypeKind.Class),
    [typeSystem.Mapper.MapType(typeof(int))]);
// 声明元数是 2，却只提供 1 个构造实参；公开模型接受这个输入。
```

把 partial 放入方法返回槽后，`regex(.*)` 与需要两个具体实参的结构模式 `Review.Box<int, int>` 都返回 Unknown/AN3001，不能把残缺实参列表格式化成完整签名。仅约束已知元数的模式 `Review.Box<,>` 可得 Matched；已知元数不符可直接得 NotMatched。

同理，方法声明形参有两个、构造实参只有一个时：

| 表达式 | 结果 | 原因 |
| --- | --- | --- |
| `method(* M<int>(..))` | NotMatched | 声明元数 2 与模式元数 1 不符 |
| `method(* M<int,string>(..))` | Unknown/AN3001 | 元数相符，但缺少所需构造实参 |
| `method(* M<,>(..))` | Matched | 仅要求已知的两槽元数，不消费具体实参 |
| `regex(.*)` | Unknown/AN3001 | 无法提供完整规范签名 |

这个[历史误判已修复](../reviews/findings/FND-20261013-002.md)，不是“不支持外部签名”的排除。外部生产者应保留准确声明、完整实参和可用性；有原始声明时也不能凭当前残缺槽位改写已知元数。开放定义的零构造实参与残缺构造输入不同：有完整声明槽位的开放方法可以格式化，缺少可编号形参事实的外部开放类型仍可能 Unknown。[回归测试](../../tests/AspectN.Tests/Evaluation/IncompleteGenericArgumentTests.cs)和[原探针](../reviews/reproductions/2026-10-13-capability-boundaries/README.md)保留完整、开放、残缺输入及原始声明对照。

## 13. CLI 可以包含 C# 无法声明的返回类型重载

```text
int    M<T>(T value) where T : IDisposable
string M<T>(T value) where T : IComparable
```

上例说明 CLI 签名，不能复制为 C# 重载声明。当前实现将原始声明的返回形状纳入方法声明者身份；真实发射、加载并调用的两个 PE 方法，在 Cecil/Fody、Reflection 和 Roslyn PE 中保持不同泛型形参 OwnerId，AreEqual 为 NotMatched。[历史碰撞及修复证据](../reviews/findings/FND-20261013-003.md)保留原始事实。

闭合声明类型也不会改写方法形参的声明身份，返回值与约束查询仍使用各自的闭合上下文。OwnerId 的字符串编码是实现细节，不承诺旧文本永久稳定；使用结构身份比较，避免按显示文本合并声明。已执行的返回形状控制见[跨后端回归](../../tests/AspectN.Conformance.Tests/ReturnShapeOwnerIdentityTests.cs)，不由此推断所有变量绑定或任意 CLI 修饰符组合已验证。
