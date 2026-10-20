# C01 方法声明自定义修饰符身份修复

日期：2026-10-20。基线：`fac5f0b26afa8a408381788c8c748956c9daf698`，分支 `codex/aspectn-redesign-spec`。

## 范围与关联问题核对

本次仅处理 2026-10-19 综合报告的 C01。C02–C07 与 C01 没有同源根因或必须联动的依赖；C04 虽同样涉及泛型形参，但其错误来自直接特性查询的原生记录入口。其他问题保持待沟通，不实施修复。

受影响能力为 `GEN-IDENTITY-001/S4–S7`。修复恢复已批准架构要求的声明身份保留，不新增稳定 OwnerId 文本契约。`CROSS-LOGIC-001` 仅作为关系结果的依赖；三态逻辑、`USE-REF-001` 的 RefKind 和 `USE-ARRAY-001` 的数组模式语义未改变，无须更新这些能力的承诺。原综合报告与历史证据保留，不改写原始失败事实。

## 根因与修复

旧编码剥离返回/参数类型的自定义修饰符，把不同 CLI 声明的泛型形参合并。Cecil 缓存与声明引用查找同时消费该编码，导致 `IComparable` 重载误用另一重载的 `IDisposable` 约束，并误匹配 readonly 返回表达式。

三个适配器现在在展示形状之外记录有序的修饰符结构路径，保留 required/optional 混排、类型程序集身份和嵌套位置。Cecil 的声明解析验证使用同一路径。Reflection 的分立 modifier API 丢失混排信息且同类顺序相反，故改读原始方法签名及类型 token。核心公共 API、DSL 与类型显示规则保持现有契约。

原始签名不可读时，先尝试 modified 结构树中的无歧义信息；仅有根位置 API 的旧宿主保留零/单修饰符事实。多个修饰符无法恢复顺序，或可读形状无法区分同名声明时，使用 MVID/token 保守区分，防止重载约束串用。仓库实际 MetadataLoadContext 为 7.0.0，其无 modified Type API；已验证普通及根单修饰符跨 Runtime/MLC 的身份一致，另验证混排和所列嵌套重载在 MLC 中不会合并。不宣称旧宿主恢复了完整嵌套修饰符信息，物理身份回退也不保证这类声明跨不同视图使用相同文本。独立探针使用的现代 MLC 不能替代仓库版本证据。

独立审查在第一版中发现同类反序、混排合并和数组元素丢失问题；已纳入回归并重修，不把第一版的通过结果作为最终证据。

复审还补充了不可读兄弟重载控制：当前候选完整可读且可调用，而兄弟方法的依赖缺失时，排歧失败只能触发物理身份回退，不得中断当前映射。该用例先复现 `FileNotFoundException`，再补恢复性读取保护；保护只作用于兄弟扫描，当前候选的 native limit 与取消边界保持原规则。

## 回归与证据

- [共享 PE fixture](../../tests/Shared/CustomModifierOwnerFixture.cs) 使用公开 Cecil API 发射真实程序集。
- [Cecil/Fody 回归](../../tests/AspectN.Cecil.Tests/CecilCustomModifierOwnerIdentityTests.cs) 实际加载、枚举和调用九组重载，再检查独立身份、约束、MemberRef 解析及原 C01 的 DSL 结果。Fody 项目独立编译执行相同源码。
- [跨适配器回归](../../tests/AspectN.Conformance.Tests/CustomModifierOwnerIdentityTests.cs) 比较 Cecil、Reflection、Roslyn PE 的身份并查询关联后的原生形参约束。覆盖根、ref 元素、数组、泛型实参、指针与函数指针内部位置；指针/函数指针新增 fixture 只做加载、映射与查询，不声称经反射实际调用。
- [兼容资产回归](../../tests/AspectN.NetFramework.Tests/CompatibilityAssetTests.cs) 在 net472/net10 宿主加载 netstandard2.0 资产，验证返回修饰符重载的声明区分。

原始五组跨端及五组 Fody 回归已在旧实现观察到身份碰撞失败。独立审查补充的四组顺序/嵌套回归在第一版失败；MLC 双视图回归也先观察到失败再修复。重修后聚焦 32 项通过。正式证据见 [EV-20261020-001](evidence/EV-20261020-001.json)，处置见 [FND-20261020-001](findings/FND-20261020-001.md)。

最终完整矩阵为 1,755 项通过、0 失败、0 未执行，其中 Conformance 308、Cecil 173、Fody 175；包含新增兄弟依赖缺失回归。Release 构建为 0 警告、0 错误，格式、文档校验及派生知识库检查通过。独立审查主体 `/root/review_adapters` 于 2026-10-20 复跑原反例及兄弟读取反例后确认无剩余阻塞。实际命令、八份测试计数与 SHA256 记录在关联证据中。

## 审查抽样与限制

本轮能力记录和四项不变量共 `N=5`，抽查 `ceil(5/3)=2` 条：能力记录及 `INVPARITY`。前者检查不同声明、独立约束与原 DSL 复现；后者比较同一 PE 的三适配器身份，另在实际 Fody Cecil 执行等价回归。未改动 N/A 记录。

未穷举全部 CLI 生产者、嵌套/vararg/转发组合、历史 Cecil/Roslyn/CLR 版本或全部不可读元数据宿主。原有闭合上下文、返回形状与完整测试矩阵用于回归控制；不由单次通过扩大支持承诺。未推送分支或发布程序包。
