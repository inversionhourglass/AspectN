# 选择适配器和输入

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "USE-REF-001",
    "USE-ARRAY-001",
    "CROSS-AVAILABILITY-001",
    "TYPE-NOMINAL-001",
    "ATTR-TARGETS-001",
    "CROSS-COMPAT-001",
    "CROSS-RESOURCES-001"
  ],
  "inventory_refs": [
    "INV-0013",
    "INV-0014",
    "INV-0016",
    "INV-0017"
  ]
}
-->

| 场景 | 适配器 | 原生输入 |
| --- | --- | --- |
| 离线程序集元数据 | AspectN.Cecil | ModuleDefinition / TypeReference / MethodReference |
| Fody 织入器 | AspectN.Cecil.Fody | FodyHelpers 使用的 Cecil 身份 |
| 编译期分析 | AspectN.Roslyn | Compilation 与类型/成员符号 |
| 已加载运行时对象 | AspectN.Reflection | Type / MethodBase / PropertyInfo |

Cecil 与 Cecil.Fody 同 API 但不同强名称依赖，不能同时引用。Roslyn 的 SyntaxNode 便利入口还需要 SemanticModel 或 Compilation，不能把裸语法树当作完整语义输入。生命周期依[架构 §9](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)：Cecil 默认借用传入模块，`LeaveOpen=true`，模块由调用方或宿主负责最终释放；只有显式 `new CecilMappingOptions(leaveOpen: false)` 才将模块释放责任交给适配器。Roslyn 缓存绑定 Compilation，Reflection 使用弱原生关联。

Roslyn 语法便利入口还要求实例对应：传入的 `Compilation` 必须就是创建 mapper 时使用的实例；传入 `SemanticModel` 时，它必须来自该编译实例，且声明节点必须属于模型对应的同一棵语法树。即使源码文本一致，不同编译或语法树实例也不能混用；上下文不符抛 `ArgumentException`，无法取得声明符号抛 `InvalidOperationException`。详见 [RoslynSyntaxExtensions 的公开 XML](../../src/AspectN.Roslyn/RoslynSyntaxExtensions.cs)。

同样显示文本不保证后端拥有同样事实。裸 by-ref Type 无成员上下文，不能区分 ref/out/in/ref readonly；闭合泛型形参的声明身份和约束上下文必须分开。数组模型保留 Vector/General，但 DSL [] 按 rank 匹配。

程序集加载失败可能发生在 Reflection 能得到 Type 之前；这与在已有对象上读取失败不同。把 Cecil 中立签名交给 Reflection 是 detached 关联场景，不能假称验证了 Reflection 原生读取故障。

本轮跨后端证据只覆盖[数组](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)和[泛型 fixture](../knowledge/capabilities/generics/GEN-IDENTITY-001.md)所列模式。源码/PE、依赖版本和 Fody 各自有缺口，不从一项通过扩大承诺。

映射器构造时可接收 `AspectNMappingOptions`；Cecil 通过 `CecilMappingOptions.Resources` 配置。类型系统另接收 `queryOptions` 作为独立公开查询的默认额度；编译计划发起的内部查询改用计划的匹配额度，查询内映射同时遵守映射配置和外层额度。Map 入口支持可选取消令牌。默认值、示例和第三方计量边界见[资源配置](resource-limits.md)。

自行实现 `ITypeSystem` 时，`IsAssignableTo.target` 与 `HasDirectAttribute.attributeType` 可能是部分限定的查询条件；空程序集名、空命名空间和 Unknown kind 在查询侧表示相应字段不限，`AreEqual` 仍严格比较身份。[后端实现指南](backend-implementation.md)说明递归结构、明确限定字段和验收用例。
