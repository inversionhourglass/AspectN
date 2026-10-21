# 泛型变量双模式与捕获预算实施审查

**日期：** 2026-10-21。**任务基线：** `7b86bbf7d8c667cd1bbf6535c155dff581d923ba`，在现有 `codex/aspectn-redesign-spec` 检出中实施；本报告与实现同次提交，不推送。原综合报告及其他历史证据保留。

## 授权、行为与原因

维护者希望同时保留声明占位符与任意类型捕获，默认采用前者，为后者新增预算，并在已提交[完整设计](../superpowers/specs/2026-10-21-generic-variable-modes-design.md)后明确要求“开始实施”。本变更据此取代旧默认捕获语义及“预算待实现”状态。

默认 `Declaration` 将变量编译为固定的原始候选声明槽位：按 owner kind、owner ID、assembly、position 比较，含嵌套类型外层槽位和方法独立槽位。实际具体或外部形参构造不会因 OriginalDefinition 自动变成自身定义。间接首次捕获在默认模式给 AN2006；已有别名可用于返回、参数、特性及祖先。默认 Fork/OR/NOT 没有动态环境或备选收集器。

显式 `Capture` 保留原动态能力，受每集合 4096 个唯一备选和每 Match 1,000,000 个累计逻辑工作单位的默认预算保护。每个 Fork、OR、NOT、特性目标/记录和祖先共享本次额度；记录/条目复制、比较、去重访问及节点/候选访问在处理前计费。去重按原实例身份和确定性，重复项也耗工作。预算耗尽整体中止并返回 Unknown/AN3007，保存已收集诊断及触发跨度；取消优先，第三方异常继续传播。

保留旧 TimeSpan? 构造器二进制签名和 null/default/位置调用。新重载前两个参数必填；针对 RS0026 的局部说明仅用于这两个无歧义构造器。模式与两个预算按值进入缓存键。公开 API 不暴露后端类型，ITypeSystem 和后端映射源码未改；构造实参和声明身份继续由后端完整保留。

## 测试与有界压力证据

环境：Windows、.NET SDK 10.0.401 / Runtime 10.0.12、Mono.Cecil 0.11.5、FodyHelpers 6.8.1、Roslyn 4.12.0。实际执行及文件哈希见 [EV-20261021-007](evidence/EV-20261021-007.json)。

| 项目 / 实际资产 | 通过数 |
| --- | ---: |
| AspectN.Tests / net10.0 | 1204 |
| AspectN.Cecil.Tests / net10.0 | 223 |
| AspectN.Reflection.Tests / net10.0 | 109 |
| AspectN.Roslyn.Tests / net10.0 | 28 |
| AspectN.Conformance.Tests / net10.0 | 359 |
| AspectN.NetFramework.Tests / net472 | 8 |
| AspectN.NetFramework.Tests / net10.0（实际 netstandard2.0 库） | 9 |
| AspectN.Cecil.Fody.Tests / net10.0 独立 Fody Cecil 身份 | 225 |
| 合计 | 2165 |

同源编译成功的 C# 和发射 PE 在 Reflection、Cecil、Roslyn source/PE 验证嵌套开放定义、闭合构造、借用 Other<T> 形参构造、方法自身形参、嵌套参数引用及属性访问器；Fody 单独执行公共 Cecil 对象用例，不冒充同源 PE 证据。兼容项目实际核验 netstandard2.0 资产，并在两个宿主执行双模式及 AN3007。

原依赖具体实参、特性或祖先首次捕获的测试逐项显式选择 Capture，开放定义及无变量用例继续默认。C05 的 Unknown 备选、否定筛选和诊断回归保留；数量/工作边界、复制前拒绝、实例与确定性差异、分支共享、后续/并发重置、取消和外部异常另有直接回归。

初始默认模式回归在生产修改前执行，5 个用例中 4 个真实失败，1 个开放定义对照通过；后续 API 测试先出现新符号尚不存在的编译错误，不能将其当运行时缺陷复现。捕获预算测试在预算实现后补齐，未取得该部分实现前的运行时红灯；本报告不将新增测试全部描述成先红后绿。后续两次新测试失败分别是将第三个空绑定备选漏算、预算低到尚未进入取消回调，以及误估 12 组会超限，已按真实集合和访问计数修正预期，没有为测试放宽实现。

压力 fixture 在编译完成且预热后，对同一中立开放 `Box<>` 候选执行 `(Box<$L_i> || Box<$R_i>)` 的合取，记录一次当前线程分配。默认预算不变：

| 模式 | 组数 | 结果 | 捕获工作量 | 分配字节 |
| --- | ---: | --- | ---: | ---: |
| Declaration | 8 | Matched | 0 | 28008 |
| Declaration | 12 | Matched | 0 | 42152 |
| Declaration | 13 | Matched | 0 | 45416 |
| Capture | 8 | Matched | 17409 | 605040 |
| Capture | 12 | Matched | 409579 | 12358928 |
| Capture | 13 | Unknown/AN3007 | 708593 | 20878024 |

12 组的 4096 个唯一备选仍在默认数量限额内；13 组在增量收集下一唯一备选前中止。相同映射的重复 OR 在数量限额 1 下可运行，但小工作限额仍会中止。该局部、确定输入证据支持保留 4096/1,000,000 初始值，不是整体性能闸门、跨机器精确分配承诺、墙钟超时或字节配额；哈希/相等比较次数可随实例哈希改变。

## 能力、证据适用性与范围

| 能力与场景 | 本轮处置 |
| --- | --- |
| GEN-BINDING-001/S1–S4 | 重验既有元数/作用域/合法构造，明确动态用例选择 Capture |
| GEN-BINDING-001/S5–S11 | 新增默认核心、捕获预算、Cecil/Reflection/Roslyn source/PE/Fody 独立场景 |
| GEN-IDENTITY-001/S1–S7 | 编码和查询上下文未改，原 fixture 随完整矩阵重验；新消费身份的证据在 GEN-BINDING 新场景 |
| TYPE-NOMINAL-001/S1、S3、S5、S6、S12 | 原关系/拒绝范围重验，C05 用例显式 Capture，补默认先声明后引用 |
| ATTR-TARGETS-001/S1、S26 | 原直接目标规则不改；区分默认间接声明诊断、固定引用与 Capture 多记录/预算 |
| CROSS-RESOURCES-001/S1、S8 | C05 守卫不改；新增整次预算中止，区别 AN3005/6 和 AN3007 |
| CROSS-LOGIC-001/S1、S2 | 纯值真值表/ToBoolean 重验，没有把预算中止写成运算符新真值 |
| CROSS-LIFETIME-001/S1 | 缓存按完整选项值隔离，每 Match 独立环境/事实/额度 |
| CROSS-COMPAT-001/S1–S4 | 公开 API 登记、旧构造兼容、独立 Fody 和两个兼容宿主；S5 查询契约未改 |

盘点更新 INV-0008、INV-0011、INV-0013、INV-0014、INV-0015。历史 EV 文件、原始 Finding 和综合报告不改写；当前证据以当前文件哈希、配置和实际测试为准，不将旧 Capture 结果改称默认 Declaration 证明。未受影响的映射、ref/数组、类型可用性、U01 必要签名异常和 D01 查询限制通道保持各自既有范围，完整矩阵用于回归控制，不自动扩大其承诺。

存量抽查候选为上述 8 个能力和各自 4 个不变量，共 40 条，按 ceil(N/3) 取 14 条：GEN-BINDING/能力、INVLOGIC、INVPARITY、INVMISSING、INVCANCEL；GEN-IDENTITY/能力、INVPARITY；TYPE-NOMINAL/INVLOGIC；ATTR-TARGETS/INVMISSING；CROSS-RESOURCES/能力、INVCANCEL；CROSS-LOGIC/INVCANCEL；CROSS-LIFETIME/能力；CROSS-COMPAT/INVPARITY。作者核对均与本轮范围相符。现有 N/A 未新增或改动；CROSS-LOGIC 的纯值 N/A 继续只指值函数，不能扩展到 Match。下一轮轮转优先抽其余 26 条。

## 审查与交付检查

独立只读 `/root/generic_modes_core_review` 审查当前核心代码，未发现 Critical/Important 问题；建议的成员入口预算及 Fork 累计额度已补直接回归并通过。该结论是静态代码审查，不替代实际测试。

独立只读 `/root/generic_modes_final_review` 最终复核通过，未发现 Critical/Important 问题；逐一核对八份 TRX 的 SHA256、实际程序集/宿主、2165 计数和六组压力数据，复核上述 14 条存量抽查及未改变的全部不变量元数据。CROSS-LOGIC 三项 N/A 仍只适用于纯值函数。该审查不修改文件或执行构建。

最终完整 Release 构建零警告、零错误，格式 verify-no-changes 通过。格式/注释收尾及特性收集器冗余空检查简化后，又聚焦执行 AttributePatternTests、BindingBudgetTests、GenericVariableModeTests，68/68 通过；该运行单独存档，不覆盖完整矩阵报告。知识库派生资料一致性检查通过，文档校验为 17 能力、128 场景、10 用户章节，零错误、零告警；git diff --check 通过。证据最后刷新当前任务文件哈希，保留基线 HEAD 与 dirty 状态说明本次同提交快照。执行命令：

```powershell
dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:quiet
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 --logger trx --results-directory artifacts/generic-modes/final -v:quiet
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~AttributePatternTests|FullyQualifiedName~BindingBudgetTests|FullyQualifiedName~GenericVariableModeTests" --logger trx --results-directory artifacts/generic-modes/post-review -v:quiet
dotnet run --project docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj -c Release --no-restore --disable-build-servers
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
```

用户示例通过 64 项检查。没有向相邻仓库写入、发布包、推送分支或创建 worktree；既有未跟踪审查文件保持原状。
