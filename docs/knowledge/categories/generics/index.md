# 泛型（GEN）

- [GEN-IDENTITY-001：声明身份与闭合查询上下文](../../capabilities/generics/GEN-IDENTITY-001.md)。区分 owner 身份和约束求值上下文，显示名称相同不合并声明。Roslyn 闭合场景只验证 OwnerId，闭合约束查询与同形重载不能沿用 Cecil/Reflection 的证据；缺失解析和取消仍有取证缺口。

未盘点主题：[台账](../../coverage.json) INV-0008（arity、变量绑定与作用域）、INV-0015（名称/程序集/转发与关系）、INV-0017（其他后端和模式）。未盘点不表示不支持。

先读条目摘要，再按任务加载场景和正式依据。[盘点台账](../../coverage.json)记录当前缺口；状态以条目 JSON 场景为准。[返回入口](../../index.md)。
