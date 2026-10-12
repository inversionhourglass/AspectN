# 横向契约（CROSS）

- [CROSS-AVAILABILITY-001：事实可用性与阶段失败](../../capabilities/cross-cutting/CROSS-AVAILABILITY-001.md)。只在求值消费缺失事实时判断 Unknown，映射抛异常与匹配返回值分别说明。中立签名的核心场景有证据，后端缺失/读取失败仍待取证；H1 accepted_risk 保持可见。
- [CROSS-LOGIC-001：三态与诊断](../../capabilities/cross-cutting/CROSS-LOGIC-001.md)。NOT/AND/OR 遵循登记真值表，已知状态仍可能保留 Unknown 诊断；布尔转换由调用方选策略。纯值函数的 N/A 不豁免完整 evaluator 的元数据与取消义务。

未盘点主题：[台账](../../coverage.json) INV-0006（损坏 Cecil 对象输入）、INV-0013（取消、资源、缓存和生命周期）、INV-0014（公开 API、包边界和兼容矩阵）。未盘点不表示不支持。

先读条目摘要，再按任务加载场景和正式依据。[盘点台账](../../coverage.json)记录当前缺口；状态以条目 JSON 场景为准。[返回入口](../../index.md)。
