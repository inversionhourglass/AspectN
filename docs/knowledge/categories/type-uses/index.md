# 类型使用位置（USE）

- [USE-REF-001：ByReference 与 RefKind](../../capabilities/type-uses/USE-REF-001.md)。引用标记和精确 ref/in/out 分类是独立事实；裸 by-ref Type 不足以猜测精确分类。核心维度有证据，后端完整分类和缺失事实场景仍需取证。
- [USE-ARRAY-001：数组形状与维数](../../capabilities/type-uses/USE-ARRAY-001.md)。模型区分 Vector/General，DSL 按维数匹配，身份和方向关系分别核验。`int[*]` 的 DSL 排除有正式依据，新增示例已验证编译拒绝及诊断存在，未固定具体诊断码；不能外推完整 CLR 数组协变。

未盘点主题：[台账](../../coverage.json) INV-0009（nullable、tuple、指针、函数指针和 custom modifier）、INV-0017（其他后端和模式）。未盘点不表示不支持。

先读条目摘要，再按任务加载场景和正式依据。[盘点台账](../../coverage.json)记录当前缺口；状态以条目 JSON 场景为准。[返回入口](../../index.md)。
