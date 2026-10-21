# D01 直接特性查询原生超限通道统一

## 维护者裁决与取代范围

2026-10-21 维护者在讨论综合报告 D01 后明确：“统一返回 Unknown 吧”。本次据此确定 `ITypeSystem.HasDirectAttribute` 的查询边界：内部读取、映射原生特性类型超过类型深度或泛型元数上限时，返回 Unknown 并保留原 AN3005/AN3006，不外抛该限制异常，不改写成 AN3004。

此决定仅取代该查询内部超限通道的不一致；显式 `Mapper.MapType`、`MapMethod` 等原生映射入口仍抛 `AspectNLimitException`。调用方若先自行映射超限类型，异常发生在进入查询之前。上限数值、结构合法性、取消、参数及生命周期错误不变。

原综合报告将 D01 作为待裁决问题，不能把本次新规则追溯描述为旧契约下已明确违约；历史报告保持原始结论。H1 人工损坏对象的既有处置不在本次范围。

## 根因与实现

代码基线为 `a7d748e5c53e7179369093aff8b797b880cf8929`，分支 `codex/aspectn-redesign-spec`。Cecil 在查询类型直接特性时通过 `GetTypeAttributes` 惰性读取并映射 `CustomAttribute.AttributeType`。原生限制检查抛出 `AspectNLimitException` 后，通用元数据恢复不会捕获它，因此异常逃出查询；Reflection 和 Roslyn 已在各自查询边界保留限制诊断并返回 Unknown。

只在 Cecil `HasDirectAttribute` 的 `GetAttributes` 调用周围捕获该异常，返回 `TypeRelationResult.Unknown(exception.Diagnostic)`；Fody 编译同一份生产源码。空参数、已释放上下文和预先取消的检查仍在捕获范围外。未扩大 `CecilMetadataReader.IsRecoverable`，也未改变各映射器或记录列表的求值顺序。

## 回归与失败证据

新增 `CecilDirectAttributeLimitTests`，普通 Cecil 和 Fody 分别执行各 8 项。公开 Cecil 对象覆盖泛型特性类型深度 64、65、67 和元数 64、65；超限查询须 Unknown 且只有原限制诊断，上限内精确查询 Matched，空直接集合 NotMatched。重复查询不能将失败变为已知空。类型及方法泛型形参声明也覆盖超限记录；预先取消仍传播。显式 MapType 同一特性类型超限仍抛异常。

新增一致性 `DirectAttributeLimitTests` 共 5 项参数化测试，每项核验 Roslyn 源码、Roslyn PE、Cecil、Reflection 四个视图。同一源码真实编译发射并由 CLR 加载，声明 `Marker<T...> : Attribute`、带 `[Marker<...>]` 的 Marked、无标记 Empty，以及返回这些类型的普通方法。候选类型映射不提前消费其特性；另一个返回特性类型的方法证明显式映射超限仍抛异常。深度包含特性根和 int 叶节点，因此 62 层数组的总深度是 64，63 层是 65，原复现的 65 层是 67。

生产修复前，普通 Cecil 与 Fody 各 8 项均为 5 项限制异常失败、3 项控制通过；真实源码一致性回归为 3 项在 Cecil 查询外抛失败、2 项上限内通过。Roslyn 源码/PE 的检查在 Cecil 之前完成，失败用例未执行到 Reflection，修复后的绿灯矩阵再逐视图完成核验。首次全方案聚焦命令还暴露了新测试的 TypeIdentity 构造参数遗漏，补齐后另行执行一致性红灯；不将编译错误计为产品失败证据。

原始本地 TRX 分别保存在 `artifacts/d01-fix/red`、`red-conformance`，修复后聚焦在 `green`，均为忽略目录；可提交的确定性回归保留在测试项目。

## 知识库与证据适用性

- ATTR-TARGETS-001/S21–S25 分别登记 Cecil、Fody、Reflection、Roslyn 源码及 PE。Fody 本轮新增证据来自公共原生对象，不声称 Fody 执行了同源 PE 的四视图测试。
- CROSS-RESOURCES-001/S2–S5 补充查询入口与显式映射控制；Roslyn PE 的新场景单列于 ATTR/S25。不用本轮证据替代旧小栈、修饰符和祖先遍历验证。
- CROSS-AVAILABILITY-001 区分显式映射与内部查询阶段说明，旧 S2/S5 的原生边界表述限定为显式映射。新增查询证据通过上述独立场景关联，不把限制改成元数据缺失。
- 同步 INV-0004、INV-0011、INV-0013、公开接口 XML、语言第 21 节、可用性专题及用户/第三方后端指南；来源登记追加本次维护者批准范围，不借用 C06 的确认作为 D01 授权。

本轮未新增或改动 N/A 条目；旧证据保留历史范围，当前重跑不扩大其承诺。未验证任意损坏原生对象、所有运行时/依赖版本或并发调度。相关 Finding 为 FND-20261021-005，当前证据为 EV-20261021-005。

## 验证与独立复核

实际完成以下验证，命令与环境详见 [EV-20261021-005](evidence/EV-20261021-005.json)：

- 新增聚焦 21/21：普通 Cecil 8、Fody 8、一致性 5，失败与跳过均为零。
- 完整 Release 构建零警告、零错误；完整矩阵 2121/2121，失败与跳过均为零。核心 1165、Cecil 222、Reflection 109、Roslyn 28、一致性 358、Fody 224，兼容性项目 net472 为 7、net10.0 为 8。
- `dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet` 通过。
- 知识库生成、`-Check` 及文档校验通过；17 能力、117 场景、10 用户章节，零错误、零告警。`git diff --check` 通过。

按 `requesting-code-review` 技能，由独立只读审查者 `/root/d01_attribute_limit_review` 于 2026-10-21 复核，未发现 Critical、Important 或 Minor actionable 问题。其独立核对 21 个证据文件的 SHA-256 全部一致，按 TRX `TestMethod.codeBase` 区分普通 Cecil 与 Fody 测试程序集，确认上述红绿灯及完整矩阵计数，结论为可以单独提交。

按知识库审查流程登记存量抽查：候选是本轮 3 个能力（ATTR-TARGETS-001、CROSS-RESOURCES-001、CROSS-AVAILABILITY-001），及它们各自 INVLOGIC、INVPARITY、INVMISSING、INVCANCEL 的 12 条适用记录，共 15 项；抽查 `ceil(15/3)=5`。本轮作为明确登记的轮转起点，不追改历史报告。独立审查者核验如下，旧不变量的全范围 pending 状态不因局部抽查改成已验证：

| 抽中项 | 本轮核验结果 |
| --- | --- |
| ATTR-TARGETS-001 能力 | 仍查询直接记录，超限 Unknown 与已知空 NotMatched 区分；通过 |
| CROSS-RESOURCES-001 能力 | 查询边界与显式 Mapper 两个阶段分开，64/65 上限不变；通过 |
| ATTR-TARGETS-001/INVPARITY | 同源真实四视图及 Fody 独立对象证据分别登记，不相互外推；通过 |
| CROSS-RESOURCES-001/INVCANCEL | 超限记录存在时预先取消仍传播，未被捕获为 Unknown；通过 |
| CROSS-AVAILABILITY-001/INVMISSING | 限制诊断不改成 AN3004，未知列表不解释为已知空；通过 |
