# 限制与常见坑

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
    "INV-0009",
    "INV-0010",
    "INV-0012",
    "INV-0013"
  ]
}
-->

| 边界 | 原因与处理 |
| --- | --- |
| int[*] 不属于 DSL | 模型区分 Shape，表达式按 rank；不能从 regex 文本恢复额外 selector |
| 裸 by-ref Type 不给精确 ref-kind | ref/out/in 的区别来自成员上下文及标记，Type 自身信息不足 |
| + 不引入完整 CLR variance/covariance | AspectN 使用批准的名义关系闭包与结构身份；不能拿运行时 IsAssignableFrom 作为全部语义 oracle |
| 引用类型 nullable 注解不参与身份 | CLR 类型身份不保留该源代码注解；string? 的模式不是静态 nullability 分析 |
| field/event 声明 selector、params/scoped、指针/函数指针 DSL 无支持承诺 | 已批准架构/规范的语言范围，不把可映射模型/显示文本当作新 DSL |
| 未知或未建档 | 证据/盘点缺口，不据此宣称“不支持” |

类型结构最多 64 层，含根；包含链有效 type arity 与方法自身 arity 各最多 64。原生超限 AN3005/AN3006 抛 AspectNLimitException；外部中立签名匹配超限 Unknown。解析深度、regex 子集/超时、规范文本限制依[规范 §15/21](../superpowers/specs/2026-07-31-aspectn-language-spec.md)，不要自设阈值。

显式 ref 注解只在成员参数/返回槽；!ref 不能分开。省略泛型参数对子串名字不代表任何 arity。手工集合已知为空与 unavailable 空列表不同；参数 Position 必须与声明序一致，否则构造器拒绝，不自动重排。

H1 的损坏人工 Cecil 对象（缺 ReturnType/ElementType）保留异常，维护者决定不补容错/Unknown；尚未证明真实程序集可达。[历史处置](../reviews/findings/FND-20261011-001.md)保留输入条件及证据缺口，不代表这些风险已修复。

不支持原因来自[规范](../superpowers/specs/2026-07-31-aspectn-language-spec.md)和[架构范围](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)，五主题详细证据从[知识库](../knowledge/index.md)按任务读取。
