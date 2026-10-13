# 选择适配器和输入

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "USE-REF-001",
    "USE-ARRAY-001",
    "CROSS-AVAILABILITY-001"
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

Cecil 与 Cecil.Fody 同 API 但不同强名称依赖，不能同时引用。Roslyn 的 SyntaxNode 便利入口还需要 SemanticModel 或 Compilation，不能把裸语法树当作完整语义输入。生命周期依[架构 §9](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)：Cecil 拥有的模块按 LeaveOpen 管理，Roslyn 缓存绑定 Compilation，Reflection 使用弱原生关联。

同样显示文本不保证后端拥有同样事实。裸 by-ref Type 无成员上下文，不能区分 ref/out/in/ref readonly；闭合泛型形参的声明身份和约束上下文必须分开。数组模型保留 Vector/General，但 DSL [] 按 rank 匹配。

程序集加载失败可能发生在 Reflection 能得到 Type 之前；这与在已有对象上读取失败不同。把 Cecil 中立签名交给 Reflection 是 detached 关联场景，不能假称验证了 Reflection 原生读取故障。

本轮跨后端证据只覆盖[数组](../knowledge/capabilities/type-uses/USE-ARRAY-001.md)和[泛型 fixture](../knowledge/capabilities/generics/GEN-IDENTITY-001.md)所列模式。源码/PE、依赖版本和 Fody 各自有缺口，不从一项通过扩大承诺。
