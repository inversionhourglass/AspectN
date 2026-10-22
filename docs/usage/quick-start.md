# 快速开始

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "USE-REF-001",
    "USE-ARRAY-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001"
  ],
  "inventory_refs": [
    "INV-0014"
  ]
}
-->

选择 AspectN 和所需适配器；运行时反射使用 AspectN.Reflection。仓库库项目提供 netstandard2.0/net10.0 资产，当前项目的构建/兼容条件见[README](../../README.md)。安装已发布包时指定目标版本，不把工作树文档直接当作发布证明。

下面的 Service 是示例自定义类，其公开 GetName() 返回 string：

```csharp
using AspectN;
using AspectN.Reflection;
using AspectN.Evaluation;

var compilation = new AspectNEngine().CompileMemberExpression(
    "method(public string Example.Service.GetName())");
if (!compilation.TryGetMemberPattern(out var pattern))
    throw new InvalidOperationException(string.Join(Environment.NewLine, compilation.Diagnostics));
var typeSystem = new ReflectionTypeSystem();
var method = typeof(Example.Service).GetMethod("GetName")!;
var result = pattern!.Match(typeSystem.Mapper.MapMethod(method), typeSystem);
Console.WriteLine(result.Status);
```

先判断编译诊断，再处理匹配结果；Unknown 时检查 result.Diagnostics 并明确选择策略。用 CompileTypePattern 编译纯类型模式，不能把 ref 参数注解当作纯类型表达式。

可完整运行的[示例源码](examples/ReflectionQuickStart/Program.cs)包含 Service 和 `Owner<T>` 定义，并断言普通成员匹配、`out` 参数、数组两种形状、泛型声明身份、缺失可见性和三态转换：

```powershell
dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release
```

预期输出 `Documentation examples passed.`。[2026-10-23 重跑记录](../reviews/evidence/EV-20261023-302.json)记录本次代码快照、执行环境及实际断言范围；验证仅覆盖这个 Reflection 示例，不能由此推断全部能力场景或所有适配器已验证。

[2026-10-12 试点记录](../reviews/evidence/EV-20261012-003.json)和[2026-10-14 后续复验](../reviews/evidence/EV-20261014-009.json)继续保留历史范围；后者还包含独立的 MatchingBoundaries 示例。这些历史执行结果对应各自代码快照，不替代本次重跑。[后续表达式](matching.md)和[诊断](results-and-diagnostics.md)解释各例边界。

## 日常类型和方法查找

把表达式应用到已有候选集合，就能筛选类型或方法。例如，查找 `string` 的公开、返回 `string` 的方法：

```csharp
var engine = new AspectNEngine();
var compilation = engine.CompileMemberExpression("method(public string *(..))");
if (!compilation.TryGetMemberPattern(out var pattern))
    throw new InvalidOperationException("方法筛选表达式编译失败。");

var typeSystem = new ReflectionTypeSystem();
var methods = typeof(string).GetMethods()
    .Where(method => pattern.Match(typeSystem.Mapper.MapMethod(method), typeSystem).Status == MatchStatus.Matched)
    .ToArray();
```

这里由调用方提供候选方法集合，AspectN 判断哪些候选满足表达式。类型筛选可同样使用 `CompileTypePattern` 和 `Mapper.MapType`。该例明确只保留 `Matched`；遇到可能缺失元数据的候选时，按[结果与诊断](results-and-diagnostics.md)处理 `Unknown`。

[边界示例源码](examples/MatchingBoundaries/Program.cs)同时演示类型、方法查找，并包含本指南各项限制的断言。运行方式见[限制与常见坑](limits-and-pitfalls.md)。
