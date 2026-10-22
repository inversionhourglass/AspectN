# 迁移

<!-- aspectn-usage:v1
{
  "schema_version": 1,
  "capability_ids": [
    "GEN-IDENTITY-001",
    "CROSS-AVAILABILITY-001",
    "CROSS-LOGIC-001",
    "CROSS-RESOURCES-001"
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

2026-10-21 起，默认变量改为 Declaration 声明槽位引用。需要原任意类型捕获的调用传入 `new AspectNCompilationOptions(GenericVariableMode.Capture)`。资源配置改造移除旧编译选项重载；正则时间和捕获预算移至 `AspectNMatchOptions`，由编译选项的 `MatchOptions` 保存。

| 原用法 | 新默认行为 | 保留原行为 |
| --- | --- | --- |
| `method($T Service.Echo<$T>($T))` 对开放自身声明 | 按同一声明身份匹配 | 无需修改 |
| `List<$T>` 对 `List<int>` | NotMatched | 显式 Capture |
| `IValue<$T>+`、特性或嵌套实参首次捕获 | 编译诊断 AN2006 | 显式 Capture |
| Capture 的大量不同绑定备选 | 耗尽有限预算后 Unknown/AN3007 | 评估并配置正限额，处理 Unknown |

解析/编译入口增加解析选项及取消令牌，原第二个位置的编译选项应改为命名参数 `compilationOptions:`。`ITypeSystem.AreEqual` 增加可选取消令牌，第三方实现须同步签名。移除固定策略类型 `AspectNLimits`，改用三阶段配置；源码默认长度 256，Capture 默认 256 个备选/50,000 个绑定工作单位，规范文本默认 16,384。完整签名和所有默认值见[资源配置](resource-limits.md)。

语法仍只有 Standard；不是语言版本切换。后端继续映射实际实参，不靠丢失构造上下文迁移。

Rougamo 集成和大版本发布属于独立工作，本次文档试点不声称已完成其迁移。迁移行为没有全量知识库证据时继续查正式规范，并在[盘点台账](../knowledge/coverage.json)登记范围。
