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

可完整运行的[示例源码](examples/ReflectionQuickStart/Program.cs)包含 Service 和 Owner<T> 定义，并断言普通成员匹配、ref/out、数组两种形状、泛型声明身份、缺失可见性和三态转换：

```powershell
dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release
```

预期输出 Documentation examples passed. 测试通过范围按[正式证据](../reviews/evidence/EV-20261012-003.json)记录；不能由此推断所有适配器已验证。[后续表达式](matching.md)和[诊断](results-and-diagnostics.md)解释各例边界。
