# AspectN 文档与知识库五主题试点审查

- 日期：2026-10-12
- 作者/实施：Codex 主代理 /root
- 非作者审查：/root/documentation_pilot_review（独立只读代理）
- 代码基线：23a4f3363191e0556b753067ad2d98d5097372a9，dirty 工作树；产品实现未改动
- 依据：[最终设计](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md)、[配套格式](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-examples.md)、[实施计划](../plans/2026-10-12-documentation-knowledge-base-implementation.md)

## 1. 范围与交付

A：登记规范/委托/历史地位、九分类入口、四个条件不变量和 17 项盘点。泛型编码指南仍为解释性 guide，没有因登记升级为独立规范。

B：五条能力、19 个场景、7 章用户指南、可执行 Reflection 最小示例。行为承诺和验证状态在场景层分开；未执行场景保留 unverified。

C：非作者实际走阅读路由、核验初始全部六项 N/A、完成下述变更影响分析并审查证据边界。修订后独立复核已通过，结论见末节。

D：PowerShell 7 标准库校验器、38 个黑盒 fixture、现有 Windows/Linux CI job 的校验步骤、PR 模板与 AGENTS 初期同步规则。Finding 引用缺失/关闭仍为告警，不误改成硬错误。

E：全量分类条目、版本/适配器/输入模式矩阵和自动派生展示留待扩展。完整全仓同步闸门未启用，不因本试点通过自动升级。

## 2. 非作者实际阅读路由

审查者实际从 [Agent 入口](../knowledge/index.md) 进入分类，继而读取条目、norm_refs 和必要测试源码，记录了以下结果：

| 路由 | 找到的关键依据与边界 |
| --- | --- |
| 入口 → 类型使用位置 → USE-ARRAY-001 | Shape 身份与 DSL rank 分开；int[*] DSL 排除有规范，但本轮没有登记该解析断言；一般 CLR 协变不由三后端 shape fixture 推出 |
| 入口 → 类型使用位置 → USE-REF-001 | ByReference 与精确 RefKind 分开；裸 by-ref Type 不足以推断 ref/in/out，S2 仍 unverified；完整匹配入口不被纯 mapper N/A 豁免 |
| 入口 → 泛型 → GEN-IDENTITY-001 | 声明身份与闭合查询上下文分开；字符串 owner 编码不新增稳定契约；进一步查测试发现 Roslyn 场景原有越界结论 |
| 入口 → 横向契约 → CROSS-AVAILABILITY-001 / CROSS-LOGIC-001 | 缺失事实按消费点判断，阶段异常与 MatchStatus 不混合；诊断与三态值分别治理；H1 通过 related_findings 和用户坑说明持续可见 |

分类层后来补充每条能力的具体摘要、关键边界及有关未盘点编号。正文与分类页不维护另一份状态枚举。

成本观察：三项路由可以按分类定位；初版薄分类迫使审查者直接加载详情。人工源码比对实际发现 Roslyn 证据越界，维护工作集中于逐场景拆分、类型校验与人工 N/A 核验。本轮没有量测精确耗时或上下文体积，不设置未经测量的阈值。

## 3. N/A 核验

本轮是初建记录：新增候选 N=6，全部核验；未改动存量 N=0，抽查 ceil(0/3)=0。没有用能力数量替代“能力+不变量记录”分母。审查者为 /root/documentation_pilot_review，日期 2026-10-12。

| 初始候选 | 非作者结果 | 当前处置及理由 |
| --- | --- | --- |
| GEN-IDENTITY-001 / INVLOGIC | rejected | 约束/关系和模式查询消费 MatchStatus，改 applicable；不能用 MapMethod 单独入口描述豁免整个条目 |
| GEN-IDENTITY-001 / INVCANCEL | rejected | 如实纳入的关系/模式查询支持 CancellationToken，改 applicable；实际证据仍没有取消断言 |
| USE-REF-001 / INVCANCEL | confirmed | 当前选定 MapMethod、MapTypeUse、IsRefKindMatch 不声明 token；完整匹配入口明确排除 |
| CROSS-LOGIC-001 / INVPARITY | confirmed | 核心纯值函数不进行后端映射；各后端 evaluator 的组合义务由使用它的能力治理 |
| CROSS-LOGIC-001 / INVMISSING | confirmed | 不查询元数据，只消费已形成的 MatchStatus/MatchResult |
| CROSS-LOGIC-001 / INVCANCEL | confirmed | And/Or/Not/ToBoolean 均无 token；不豁免完整匹配流程 |

两个原始 rejected 记录保存在此表；能力的当前 applicable 记录写实际主体/日期及修正理由。confirmed 表示适用性判断经过核验，不表示该不变量所有行为已有测试证据。其余 applicable 记录仍 pending，未伪造全量签章。

## 4. 发现与处置

| 发现 | 反证/复现 | 最终处置 |
| --- | --- | --- |
| Roslyn 场景误用其他后端的闭合查询与同形重载证据 | MethodGenericOwnerIdentityTests：闭合 Roslyn 只比较 OwnerId；IsAssignableTo/闭合模式查询是 Cecil/Reflection；同形重载 fixture 无 Roslyn | [FND-20261012-001](findings/FND-20261012-001.md) documentation_resolved：S2 按实际断言收窄；S1/S3 写完整查询入口；缺口保持可见 |
| EV-003 是部分补充，不能单独证明整个宽场景 | 最小示例只执行 NOT U、F AND U、Unknown→False，没有 OR/完整真值表/诊断保留/Throw；泛型只展示身份相等 | 不改写历史报告；CROSS-LOGIC 与 GEN 条目补清逐项覆盖。场景状态只关联完整范围的 EV-001/002，EV-003 仅作为用户示例补充 |
| 文字字段被 PowerShell 隐式转换放行 | 数字 123 作为 notes、pending_reason、reviewer、risk 重验条件、blocking_reason，旧实现 exit 0；新增 5 个负例实际 RED | 增加严格字符串检查；38 个 fixture GREEN。负例不得因 Unexpected validator failure 通过；无告警用例也核对零告警 |
| 初始 JSON 解析器把 ISO 时间转为 DateTime | 合法日期报告被误拒；专门的 dated evidence 用例 RED | 改标准库 JsonDocument，保留 JSON String；dated evidence 用例 GREEN，不引入第三方依赖 |
| 分类页缺具体摘要，渐进披露停留于链接 | 对照设计 §3.1，三张已建档分类页只有编号和通用提示 | 补充五条摘要/边界和有关盘点编号 |
| 两个盘点章节入口不准确 | 架构的缓存/资源/API 条目实际位于 §9/12/13、§5/14.2/16 | 修正 INV-0013 / INV-0014 来源章节 |

历史 [H1 Finding](findings/FND-20261011-001.md) 沿用维护者接受风险的裁决；本轮不声称合成损坏输入已被修复，也没有凭空添加真实程序集可达性证据。

## 5. 实际变更影响分析

本轮选择真实发生的“JSON 日期保留为字符串”变更作影响分析。受影响处：报告读取、ISO 日期验证、verified 场景与证据关联、审查日期；用户 API/产品匹配行为不受影响。

读取顺序：最终格式 §1.1 → format.md → 校验器 Convert-DocumentJson/Test-Date → 有日期的 EV-001/002/003 → verified 场景。针对原缺陷补合法报告 fixture；在真实文档树上重验。代码语义、规范预期、运行环境未变化，历史产品报告保持原快照，无需因 checker 变更重写或废弃全部报告。

后续文字类型收紧只影响文档结构合法性，复现用独立最小仓库；用户章节新增规范说明不要求产品源码变化。GEN 修订撤回越界的验证结论，未以 excluded 替代产品缺陷，也未扩大 EV-002 的测试范围。

## 6. 验证命令、报告与限制

实际执行（Release / net10.0 / SDK 10.0.401 / Windows）：

~~~powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RefKindTests|FullyQualifiedName~MetadataAvailabilityTests|FullyQualifiedName~MatchLogicTests" --logger "trx;LogFileName=core-pilot.trx" --results-directory artifacts/documentation-pilot
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArrayShapeModelTests|FullyQualifiedName~MethodGenericOwnerIdentityTests" --logger "trx;LogFileName=conformance-pilot.trx" --results-directory artifacts/documentation-pilot
dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release --no-restore
pwsh -NoProfile -File eng/verify-documentation.ps1
pwsh -NoProfile -File eng/tests/verify-documentation.Tests.ps1
git diff --check
~~~

| 验证 | 实际结果 | 保留依据 |
| --- | --- | --- |
| 核心五主题有关 suite | 68 passed | [EV-20261012-001](evidence/EV-20261012-001.json)，原始 TRX 路径与 SHA |
| 后端 conformance fixture | 19 passed | [EV-20261012-002](evidence/EV-20261012-002.json)，原始 TRX 路径与 SHA |
| 用户最小示例 | Documentation examples passed. | [EV-20261012-003](evidence/EV-20261012-003.json)，观察输出与样例/实现/配置快照；没有原始日志文件 |
| 结构与本地引用 | 零错误、零告警 | 5 能力、19 场景、7 章用户指南、3 报告、2 Finding |
| 校验器黑盒 fixture | 38 passed | 独立最小仓库，包含日期、类型、路径/引用、警告级别、历史豁免、目录移动 |
| 工作树差异 | diff --check 通过 | 既有未跟踪讨论稿、正式设计稿与 nul 保留；未修改产品 src/ 或既有测试 |

非作者独立运行了初始 33 个 fixture，并在修订后再次独立执行全部 38 个 fixture，均通过；检查三份报告的 155/169/118 个文件 SHA，均无漂移；EV-001/002 原 TRX 的 SHA 与报告一致。最后一次独立文档检查为零错误、零告警。

没有执行全仓 .NET/.NET Framework 测试矩阵；没有 Fody、所有版本/PE/源码/脱离上下文/损坏输入或取消场景的新增证明。CI 已接入步骤，但本地 Windows 验证不能声称远端 Linux job 已运行。上述验收在提交前的工作树执行；后续提交不会改写报告中的历史验证状态。未发布或推送。

### 6.1 提交前格式与证据适用性

维护者随后授权分批提交。对实际暂存新增文件运行 diff --cached --check，发现终端多余空行并清理；原讨论稿正文与历史报告保持不变。解决方案与用户样例项目的 dotnet format --verify-no-changes --no-restore 均通过。

样例 Program.cs 和 csproj 的快照 SHA 因清理终端空行改变。分别恢复原来的 3 个和 2 个末尾 LF 重建旧字节后，哈希与 EV-003 原记录相同，确认没有其他代码变化。以原命令重新运行样例，输出 Documentation examples passed.，退出 0；判定 EV-003 的部分行为证据仍适用，不改写历史报告。提交前当前 SHA256：

- Program.cs：7100f66b7a0060fb0d518c1bcd5300862caf36e8523adf9d3a08a8e587cd1b40
- ReflectionQuickStart.csproj：f55cd21023eaa11d4c09b82fdbae44e4abf953bfb66bf30173878f7b7c227c15

## 7. 试点结论与准备闸门

阶段 A/B/C/D 的本轮五主题试点完成。2026-10-12，非作者 /root/documentation_pilot_review 复核最终修订、独立运行 lint 与 38/38 fixture，确认阶段 C 通过，当前没有未解决的阻塞问题。结论限于已声明试点范围，不表示所有不变量已有执行证据。

完整全仓同步规则仍需设计要求的准备闸门与维护者确认。新增主题先登记盘点，按实际证据建档；不把“没有条目”当作不支持。

当前优先缺口：ref 后端精确分类/裸 Type；Roslyn 闭合查询与同形重载；缺失事实后端行为；取消/资源/生命周期；Fody 及其他输入模式。扩展时保持来源批准、场景状态、用户章节和证据联动。
