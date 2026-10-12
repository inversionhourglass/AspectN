# AspectN Agent 知识库

当前是五主题试点，不是全量支持矩阵。按任务先读一个分类/条目摘要，随后仅加载有关场景、依赖和规范。默认不加载历史讨论。

## 阅读路由

- [类型与身份](categories/types/index.md)
- [泛型](categories/generics/index.md)
- [类型使用位置](categories/type-uses/index.md)
- [成员](categories/members/index.md)
- [C# 与元数据表示](categories/csharp-metadata/index.md)
- [属性记录](categories/attributes/index.md)
- [解析与词法](categories/syntax/index.md)
- [绑定与匹配](categories/semantics/index.md)
- [横向契约](categories/cross-cutting/index.md)

用户问题先查[使用指南](../usage/index.md)；改动/review 先查[审查流程](review/index.md)。[来源](sources.json)、[盘点](coverage.json)、[适用 profile](profiles.json)、[冲突](conflicts.md)按需读取。

本轮交付、非作者 N/A 核验、证据范围修订和后续缺口见[试点审查记录](../reviews/2026-10-12-documentation-pilot.md)。

## 状态与依据

承诺 supported/excluded/pending；验证 unverified/verified/needs_reverification/failed；方法 automated/manual。状态在场景层，不能由一个后端通过测试推出整个能力已验证。未盘点≠不支持；实现 bug 不改写承诺。Finding 的 accepted_risk 不代表风险已修复。

正式规范与明确委托条款共同生效；guide/history 不自动批准新语义。泛型身份编码指南暂为解释性来源；声明保留依已批准架构，字符串编码不新增稳定承诺。来源冲突先登记，由维护者裁决。

## 术语

| 中文 | 原名 | 区别 |
| --- | --- | --- |
| 声明身份 | declaration identity | 与闭合 query context 分开 |
| 类型使用位置 | type use | 与 type identity 分开 |
| 引用标记 / 精确分类 | ByReference / RefKind | 可用性可独立 |
| 数组形状 / 维数 | Shape / rank | 模型形状与 DSL [] 分开 |
| 所需事实不可用 | unavailable fact | 占位值不能直接解释 |
| 规范承诺 / 证据 | contract / evidence | 当前实现与未覆盖范围分别说明 |

[字段说明](format.md)和[模板](templates/capability.md)供新增记录使用；[最终设计](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md)定义治理规则。
