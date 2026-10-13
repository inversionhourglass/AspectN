# 横向契约

- [CROSS-LOGIC-001：三态逻辑、诊断及 Boolean 转换](../../capabilities/cross-cutting/CROSS-LOGIC-001.md)。Unknown 的否定仍未知。确定匹配状态不意味着可以丢掉所有诊断。
- [CROSS-AVAILABILITY-001：元数据可用性与独立事实](../../capabilities/cross-cutting/CROSS-AVAILABILITY-001.md)。默认 Complete 代表手工传入值已知；显式 Unknown 和 unavailable placeholder 有独立含义。
- [CROSS-RESOURCES-001：取消、类型深度、元数与文本预算](../../capabilities/cross-cutting/CROSS-RESOURCES-001.md)。限制按阶段体现，取消是独立控制流；深度、元数、正则时间与规范文本预算不是同一个开关。
- [CROSS-LIFETIME-001：引擎缓存、后端生命周期与并发隔离](../../capabilities/cross-cutting/CROSS-LIFETIME-001.md)。缓存、签名和原生上下文拥有不同生存期；缓存命中不保证跨后端共享，也不保证并发匹配共享可变状态。
- [CROSS-COMPAT-001：公开 API、程序集身份与兼容资产](../../capabilities/cross-cutting/CROSS-COMPAT-001.md)。库目标框架、测试宿主、原生程序集身份与发布包版本分别记录；一个通过的宿主不能代表整个兼容矩阵。
- [CROSS-INPUT-001：有效输入、候选加载与人工损坏对象](../../capabilities/cross-cutting/CROSS-INPUT-001.md)。签名构造失败、原生候选无法加载、已有候选缺事实和人工损坏对象是四类不同输入，不能混写成统一Unknown能力。

先按问题选择一个条目，再加载其场景、依赖和规范；具体后端、输入模式、验证状态与缺口以条目为准。盘点来源见 [coverage.json](../../coverage.json)。建档不表示所有版本、输入或后端组合已经验证。
