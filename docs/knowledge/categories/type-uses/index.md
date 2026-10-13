# 类型使用位置

- [USE-REF-001：ref-kind 与 by-reference 事实](../../capabilities/type-uses/USE-REF-001.md)。ByReference 和 RefKind 是两个可独立知道的事实；!ref 与精确 ref/out/in 消费不同信息。
- [USE-ARRAY-001：数组形状、rank 模式与名义关系](../../capabilities/type-uses/USE-ARRAY-001.md)。模型保留 SZ Vector 与 non-SZ General，DSL [] 只约束 rank，不能用 regex 绕过形状限制。
- [USE-SHAPES-001：可空、元组、任务类与不支持的类型形状](../../capabilities/type-uses/USE-SHAPES-001.md)。类型包装、源代码注解和运行时身份分层表达；模型能显示某个类型形状不代表 DSL 可按其结构筛选。

先按问题选择一个条目，再加载其场景、依赖和规范；具体后端、输入模式、验证状态与缺口以条目为准。盘点来源见 [coverage.json](../../coverage.json)。建档不表示所有版本、输入或后端组合已经验证。
