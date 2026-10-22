# 配置资源限额

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": ["CROSS-RESOURCES-001", "CROSS-LIFETIME-001", "CROSS-LOGIC-001", "SYN-GRAMMAR-001", "GEN-BINDING-001", "SEM-REGEX-001"],
  "inventory_refs": []
}
-->

解析表达式、映射原生元数据和执行匹配分别配置。三个配置对象不可变，可以复用；每次操作独立计数。同一个 `*` 也可能面对复杂候选，因此提高源码长度不会提高映射或匹配额度。

## 默认额度

| `AspectNParseOptions` | 默认值 | 范围 |
| --- | ---: | --- |
| `MaximumSourceLength` | 256 | 完整源码 UTF-16 长度 |
| `MaximumTokens` | 1,024 | 非 EOF token，包含重新扫描 |
| `MaximumSyntaxNodes` | 2,048 | 创建的节点，含 token、EOF 和临时节点 |
| `MaximumParserDepth` | 128 | 解析逻辑帧深度 |
| `MaximumSyntaxDepth` | 256 | 最终语法树深度 |
| `MaximumTypeDepth` | 64 | 类型结构深度 |
| `MaximumTypeGenericParameters` | 64 | 含外层的类型槽位 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位 |

深度根为 1。源码按 `string.Length` 计量：256 个代码单元允许，257 个在词法扫描前报告 AN1003。空白、换行和表达式中的转义都算；补充平面字符在 .NET 字符串中占两个代码单元。C# 字面量外围引号不算，C# 转义先还原成实际字符串再计算。没有自动 Trim。

| `AspectNMappingOptions` | 默认值 | 范围 |
| --- | ---: | --- |
| `MaximumTypeDepth` | 64 | 原生结构 |
| `MaximumTypeGenericParameters` | 64 | 含外层的类型槽位 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位 |
| `MaximumWork` | 100,000 | 一次最外层 Map 的累计工作 |
| `MaximumAttributeRecords` | 1,024 | 扫描记录，含被过滤记录 |
| `MaximumGeneratedTextLength` | 16,384 | 每个类型、缓存键、OwnerId 文本的 UTF-16 长度 |

最外层 `MapType`、`MapTypeUse`、`MapMethod` 或 `MapProperty` 中的声明类型、参数、返回、原始定义、特性和修饰符共享额度。

| `AspectNMatchOptions` | 默认值 | 范围 |
| --- | ---: | --- |
| `MaximumTypeDepth` | 64 | 候选及查询类型结构 |
| `MaximumTypeGenericParameters` | 64 | 含外层的类型槽位 |
| `MaximumMethodGenericParameters` | 64 | 方法自身槽位 |
| `MaximumWork` | 250,000 | 整次 Match 的累计工作 |
| `MaximumRelationNodes` | 1,024 | 展开的关系节点 |
| `MaximumRelationEdges` | 4,096 | 考察的关系边，含重复边 |
| `MaximumAttributeRecords` | 1,024 | 扫描记录，含被过滤记录 |
| `MaximumCanonicalSignatureLength` | 16,384 | 每个规范签名的 UTF-16 长度 |
| `MaximumBindingAlternatives` | 256 | Capture 每个收集器的不同备选 |
| `MaximumBindingWork` | 50,000 | 整次 Match 的 Capture 绑定工作 |
| `RegexTimeout` | 250 ms | 每次正则匹配 |

工作单位表示处理规模，包含节点、关系、特性、绑定条目和字符串字符处理，不是毫秒或字节。专项工作也计入总工作，同一作用域不重复计同一动作。Declaration 不创建 Capture 环境，仍受总工作限制。格式化耗费工作和文本额度，完成后才开始正则超时计时。

## 提高额度与显式不限

```csharp
using AspectN;

var parse = new AspectNParseOptions(
    maximumSourceLength: ResourceLimit.Finite(1024));
var match = new AspectNMatchOptions(
    maximumWork: ResourceLimit.Finite(500_000),
    maximumBindingAlternatives: ResourceLimit.Finite(512),
    regexTimeout: TimeLimit.Finite(TimeSpan.FromMilliseconds(500)));
var options = new AspectNCompilationOptions(GenericVariableMode.Capture, match);
var compilation = new AspectNEngine().CompileTypePattern(
    "List<$T>", parseOptions: parse, compilationOptions: options);
```

配置参数省略或传 null 使用默认值。`ResourceLimit.Finite` 只接受正 `long`；零、负数、`default(ResourceLimit)` 均无效。`TimeLimit.Finite` 要求正值，且不超过 2,147,483,646 毫秒；`default(TimeLimit)` 无效。有限值恰好用完允许完成，再申请一个单位失败。

```csharp
var parse = new AspectNParseOptions(
    maximumSourceLength: ResourceLimit.Unlimited,
    maximumParserDepth: ResourceLimit.Unlimited,
    maximumSyntaxDepth: ResourceLimit.Unlimited,
    maximumTypeDepth: ResourceLimit.Unlimited);
var match = new AspectNMatchOptions(regexTimeout: TimeLimit.Unlimited);
```

这里只关闭所写项目：token、节点、其他结构或工作限额仍生效。需要处理深输入时应共同评估所有相关项目。Unlimited 不关闭取消或签名合法性，也不改变 CLR、Roslyn、Cecil 等外部实现自身的限制。不要用 `long.MaxValue` 代替显式 Unlimited。

语法树保存使用的解析配置；`AspectNCompilation.Create(tree, options, token)` 绑定时沿用它。编译计划固定匹配配置，每次 Match 没有覆盖参数。不同字段值的编译形成不同缓存键，字段完全相同的独立配置实例可以共享已完成缓存条目。

## 映射与独立查询

```csharp
using AspectN.Reflection;

var mapping = new AspectNMappingOptions(
    maximumGeneratedTextLength: ResourceLimit.Finite(32_768));
var queries = new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(400_000));
var backend = new ReflectionTypeSystem(mappingOptions: mapping, queryOptions: queries);
var signature = backend.Mapper.MapType(typeof(string), cancellationToken: token);
var equal = backend.AreEqual(signature, signature, token);
```

上例的 `token` 由调用方提供。Roslyn 类型系统在 `Compilation` 之后接收同名配置；Cecil 使用 `new CecilMappingOptions(resources: mapping)`，并在类型系统构造时传 `queryOptions`。`LeaveOpen` 的模块所有权规则保持独立。

独立公开查询每次使用新的查询预算。Match 内部的内置查询使用编译计划的匹配配置，不与上例的 `queries` 取最小值或叠加。内置查询内的全部映射共享一个映射子预算，同时消耗外层查询/匹配预算；映射限额仍取自适配器实例。因此提高匹配额度不代表原生映射也无限。

## 超限、取消与结果

| 情况 | 处理 |
| --- | --- |
| 源码/token/节点超限 | 编译失败 AN1003 |
| 解析/语法/类型深度超限 | AN1002 |
| 泛型条款超槽 | AN2011 |
| 显式原生 Map 超限 | `AspectNLimitException`，检查其 Diagnostic |
| 匹配/查询类型深度或元数 | Unknown/AN3005、AN3006；祖先分支可继续 |
| Capture 专项额度耗尽 | 整次 Unknown/AN3007 |
| 总工作、关系、特性、生成或规范文本额度耗尽 | 整次 Unknown/AN3008 |
| 正则超时 | 局部 Unknown/AN3003，继续既有三态逻辑 |
| 取消 | `OperationCanceledException` |

资源中止不能被其他 OR 分支的成功覆盖；不要将它视为普通 NotMatched。结构超限保留 C05：一个祖先超深仍可检查其他合法祖先。真正缺少格式化事实仍可能 AN3001，不能与文本配额耗尽混为一谈。取消在操作、预算和结果发布边界优先检查，也适用于编译缓存命中。

同一模式并发 Match、同一配置重复操作互不消耗额度；适配器的原生对象仍需遵守自身并发和生命周期要求。缓存只发布完成结果，不缓存取消或异常；并发未命中不共用调用方令牌。

## 第三方后端边界与证据

第三方 `ITypeSystem` 只接收普通输入和取消令牌，不实现内部预算上下文。Core 计量自身预检查、求值、查询派发及返回结果消费，不能精确计量第三方内部遍历、抢占忽略取消的调用或限制其返回前分配。第三方装饰器调用内置公开查询仍是独立作用域。返回 AN3007/AN3008 会传播资源中止。实现者应自行管理内部资源，见[后端指南](backend-implementation.md)。

默认值依据和完整计量口径见[资源规范](../superpowers/specs/2026-10-22-configurable-resource-limits-design.md)及[批准计划](../plans/2026-10-21-configurable-resource-limits.md)。[资源条目](../knowledge/capabilities/cross-cutting/CROSS-RESOURCES-001.md)记录实际验证范围；有界回归不构成生产耗时或精确内存承诺。
