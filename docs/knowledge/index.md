# AspectN 知识库

当前已按九类建档，覆盖 17 条能力、97 个具体场景。按任务先读一个分类/条目摘要，随后仅加载有关场景、依赖和规范。默认不加载历史讨论；这些记录仍不代表全部 .NET/C# 功能或所有后端、版本、输入组合。

## 阅读路由

- [类型与身份](categories/types/index.md)
- [泛型](categories/generics/index.md)
- [类型使用位置](categories/type-uses/index.md)
- [成员](categories/members/index.md)
- [C# 与元数据表示](categories/csharp-metadata/index.md)
- [特性记录](categories/attributes/index.md)
- [解析与词法](categories/syntax/index.md)
- [绑定与匹配](categories/semantics/index.md)
- [横向契约](categories/cross-cutting/index.md)

用户问题先查[使用指南](../usage/index.md)；改动/review 先查[审查流程](review/index.md)。[来源](sources.json)、[盘点](coverage.json)、[适用 profile](profiles.json)、[冲突](conflicts.md)按需读取。

[分类能力目录](generated/catalog.md)与[场景支持及验证矩阵](generated/support-matrix.md)由条目元数据生成，适合按范围查找。源条目是唯一状态依据，生成页不能替代规范及证据适用性审查。

本轮扩展、非作者 N/A 核验及剩余缺口见[E 阶段审查记录](../reviews/2026-10-13-documentation-expansion.md)；首批五主题的历史结论保留在[试点审查记录](../reviews/2026-10-12-documentation-pilot.md)。

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

[本轮证据补齐与分类审查](../reviews/2026-10-13-capability-evidence-and-review.md)保存当时新场景、未修复Finding和待裁决边界；历史E阶段记录保持原结论与计数。

[五项修复与当前验证](../reviews/2026-10-14-classified-review-fixes.md)落实类型特性维护者裁决、更新当前场景证据并保留原始审查及失败报告。
