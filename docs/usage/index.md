# AspectN 使用指南

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
    "INV-0007",
    "INV-0008",
    "INV-0010",
    "INV-0014"
  ]
}
-->

AspectN 将 .NET 类型和可调用成员的元数据映射为统一签名，用单一 Standard 表达式解析、绑定和匹配。匹配结果是 Matched、NotMatched 或 Unknown。AspectN 负责匹配；织入如何执行由调用方决定。

- [快速开始](quick-start.md)：编译、映射、匹配，以及可运行的五主题示例。
- [选择适配器](adapters.md)：Cecil/Fody、Roslyn、Reflection 及上下文。
- [编写匹配表达式](matching.md)：选择器、泛型、ref 和数组。
- [结果与诊断](results-and-diagnostics.md)：三态、缺失信息和调用方策略。
- [限制和常见坑](limits-and-pitfalls.md)：不支持的形式、原因及阶段失败。
- [迁移](migration.md)：从 Shared.Cecil.AspectN 迁移。

当前知识库只对五主题建档：[Agent 入口](../knowledge/index.md)。其他功能仍依[正式语言规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[已批准架构](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)；缺少能力条目不表示不支持。包发布版本可能落后于工作树，本文以当前工作树为范围。
