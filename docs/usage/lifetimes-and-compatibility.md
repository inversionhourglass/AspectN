# 生命周期、取消与兼容资产

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "CROSS-LIFETIME-001",
    "CROSS-RESOURCES-001",
    "CROSS-COMPAT-001",
    "CROSS-INPUT-001",
    "CROSS-AVAILABILITY-001"
  ],
  "inventory_refs": []
}
-->

表达式可以预编译复用。调用方提供候选对象，并保持需要原生查询的后端上下文存活；中立签名、编译计划和原生对象的生存期分别管理。

## 复用引擎与模式

```csharp
var engine = new AspectNEngine();
var compilation = engine.CompileTypePattern("string");
if (!compilation.TryGetTypePattern(out var pattern))
    throw new InvalidOperationException("模式编译失败。");
var system = new AspectN.Reflection.ReflectionTypeSystem();
foreach (var candidate in new[] { typeof(string), typeof(int) })
    Console.WriteLine(pattern.Match(system.Mapper.MapType(candidate), system).Status);
// Matched、NotMatched。
```

引擎缓存属于实例，键包含源码、解析种类、RegexTimeout、变量模式和两个捕获预算。Declaration 的唯一槽位事实缓存、Capture 的动态环境/预算和诊断都属于每次 Match；后续候选与并发匹配互不消耗额度。模式的并发隔离不表示任意第三方 ModuleDefinition 或 Compilation 可以按任意方式并发修改。后台工具可以复用编译模式，在自己的候选遍历逻辑中处理结果。

CecilTypeSystem 释放后，受守卫的入口可能抛 ObjectDisposedException。使用外部 module 时明确选择 LeaveOpen；Reflection 弱关联不永久锁住可收集程序集，但仍需让当前查询所需原生对象存活。不同后端之间传递中立签名，不会自动建立另一后端的原生关联。[生命周期专题](../knowledge/capabilities/cross-cutting/CROSS-LIFETIME-001.md)给出各后端证据和限制。

## 取消与资源上限

```csharp
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
// pattern、signature、typeSystem 是已准备好的模式、候选和后端。
_ = pattern.Match(signature, typeSystem, cancellation.Token);
// 抛 OperationCanceledException，不返回 Unknown。
```

Cecil/Fody 的直接继承闭包入口也遵守取消，包括没有原生关联的有效中立签名：

```csharp
using var cecil = new AspectN.Cecil.CecilTypeSystem(
    Mono.Cecil.ModuleDefinition.CreateModule("Example", Mono.Cecil.ModuleKind.Dll));
var detached = new NamedTypeSignature(
    new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Detached", 0, AspectN.Metadata.TypeKind.Class));
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
_ = cecil.GetInheritanceClosure(detached, cancellation.Token);
// 抛 OperationCanceledException；不把取消转换为 Unknown。
```

未取消、但仍没有原生关联的同一输入返回不完整闭包 Unknown/AN3001。[历史取消缺陷及当前回归](../reviews/findings/FND-20261013-001.md)记录修复范围；当前工作树结论不等于所有已发布包都具备该修复。

结构最多64层含根；有效类型泛型元数最多64含外层，方法自身元数独立最多64。原生映射超限抛 AspectNLimitException，带 AN3005/AN3006；外部中立签名匹配超限则 Unknown 带相同诊断。解析和正则文本另有保护，不能把不同阶段统一为普通不匹配。[资源专题](../knowledge/capabilities/cross-cutting/CROSS-RESOURCES-001.md)和[具体代码例子](limits-and-pitfalls.md)说明阶段差别。

## 选择兼容资产和后端

库项目提供 netstandard2.0 和 net10.0；正式 .NET Framework 使用下限为4.7.2。兼容验证既要检查加载的资产，也要实际执行指定 API。现代宿主通过 net10 资产的测试，不能单独证明 netstandard 资产在旧宿主可运行。

AspectN.Cecil 与 AspectN.Cecil.Fody 面向不同强名称的 Mono.Cecil 程序集。FodyHelpers 项目选后者，普通程序集分析选前者，两者不能同时引用。包版本按实际目标发布版本选择；当前工作树文档不等于所有已发布版本的证明。

本轮实际验证为 Windows 上的 net10.0 和 net472 测试宿主；兼容项目确认核心、Cecil、Roslyn、Reflection 加载 netstandard2.0 资产并执行指定例子。没有由此宣称 Fody 的全部兼容宿主、任意历史 CLR/NuGet 版本或远程 Linux CI 已验证。[兼容专题](../knowledge/capabilities/cross-cutting/CROSS-COMPAT-001.md)和[场景矩阵](../knowledge/generated/support-matrix.md)列出精确范围。

## 候选尚未形成与事实不可用

如果 Reflection 的 GetParameters 已因缺依赖失败，尚无参数 Type 给 Mapper；此时由调用方处理加载器异常。把 Cecil 的中立签名交给 Reflection，只能测试脱离原生关联的情况，不能替代同一加载失败路径。

手工签名默认 Complete 表示已提供事实；Position 必须按声明从零连续排列。人工损坏 Cecil 对象的 H1 沿用既有接受风险，不新增通用容错能力。已有有效候选的未知事实则按所消费信息求值。[输入边界](../knowledge/capabilities/cross-cutting/CROSS-INPUT-001.md)与[可用性](../knowledge/capabilities/cross-cutting/CROSS-AVAILABILITY-001.md)分别说明。

把映射方法交给新的 ReflectionTypeSystem 不会迁移原生关联，但已知完整的 TypeAttributes（包括已知空列表）是独立事实。类型特性精确、通配、复合查询均优先消费该快照，换上下文不改变其结果。

所需 TypeAttributes 不可用时，可具体化的精确模式尝试原生直接查询；仍有原关联时可得到确定结果，无关联时 Unknown。现有接口没有特性列表枚举入口，缺列表的通配、变量或复合模式仍 Unknown。需要这类查询时，在具备依赖的上下文中重新映射并取得完整事实；其他关系查询仍按各自所需原生关联处理。[已批准规则与具体场景](../knowledge/capabilities/attributes/ATTR-TARGETS-001.md)明确区分这几种条件。
