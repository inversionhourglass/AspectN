# 迁移

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001"
  ],
  "inventory_refs": [
    "INV-0007",
    "INV-0008",
    "INV-0014"
  ]
}
-->

AspectN 只保留单一 Standard 语言，Legacy 与语言选择 API 已移除。从 Shared.Cecil.AspectN 的迁移是破坏性变更；表达式不能只改包名就假设匹配范围相同。

按[已有迁移指南](../migration/shared-cecil-to-aspectn.md)检查入口、语法、泛型 arity 与变量、属性目标、缺失元数据及 Unknown 策略。泛型声明/查询上下文保持区分，别用旧显示字符串作新中立身份。

Rougamo 集成和大版本发布属于独立工作，本次文档试点不声称已完成其迁移。迁移行为没有全量知识库证据时继续查正式规范，并在[盘点台账](../knowledge/coverage.json)登记范围。
