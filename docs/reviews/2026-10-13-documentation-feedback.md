# 使用指南反馈、中文化与边界示例审查

- 日期：2026-10-13
- 实施：Codex 主代理 /root
- 非作者审查：/root/documentation_pilot_review，独立只读代理
- 验证基线：8b14005b09fc1dd302e540f86b2a1f1def75a9e4，含本轮未提交文档和示例的工作树
- 依据：[文档工程设计](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md)、维护者的四项使用指南反馈及注释语言补充

## 反馈与交付

| 反馈 | 本轮处理 |
| --- | --- |
| 介绍不应绑定 AOP/代码织入 | README 和使用指南介绍通用 .NET 类型/成员匹配，列出日常查找、反射筛选、元数据搜索和代码分析；AOP 作为一种应用场景。快速开始新增方法筛选代码，完整示例同时执行类型和方法筛选。 |
| 文档统一中文 | 翻译原有英文 README、语言专题、迁移指南、规范、历史计划和规则；已是中文的讨论及审查记录保留原意。API、关键字、DSL、命令、路径和机器字段保留原样。 |
| 明确源码注释语言 | AGENTS 和 C# 规则规定对外 API 的 XML 注释使用英文，内部 XML 及代码注释使用中文；以有效可见性判断，内部类型中的 public 辅助成员属于内部范围。没有批量修改生产源码注释。 |
| 坑与边界需要代码 | 限制章节改为代码、预期结果和原因说明，覆盖数组、ref、可空性、关系、泛型元数、DSL 排除、正则、资源上限和有效输入约束；配套可运行项目和断言。H1 保留历史处置，不宣称已修复或已证明真实程序集可达。 |
| 面向用户的链接称谓 | 使用指南、知识库首页及相关链接使用“知识库”；Agent 阅读路由放在 AGENTS 规则中。 |

## 中文化与语义保真

审计范围为仓库原有 63 份受跟踪 Markdown，包括隐藏的 C# 规则文件；英文自然语言说明已中文化。模型名称、API/包名、命令输出、语言关键字和机器枚举不是翻译对象。历史计划保留原日期、任务、步骤、复选框状态和决策；其中 Legacy、临时扩展或邻接仓库操作不成为当前执行授权或支持承诺。

两份正式规范的编号序列保持一致。语言规范的 29 个代码/语法块和 21 行三态真值表原样保留；架构规范的三个其他代码块原样保留，仅两个文字流程图的自然语言标签中文化。工业化历史计划保留 38 个任务、174 个步骤和 80 个代码块。贡献规则历史计划的外层 Markdown 示例改用四个反引号，正确嵌套内部 C# 代码块；原始七个步骤和代码内容保留。

非作者使用 Markdown 解析器检查标题、加粗、链接与代码块。审查发现的标题/加粗标记、四个规范链接以及命令中 `<fixture>` 的显示问题已修复；泛型名称和正则示例使用代码标记，避免被当作 HTML 或转义文本。

## 影响范围与证据

| 能力/场景 | 影响与验证处置 |
| --- | --- |
| USE-ARRAY-001 / S4 | 新示例实际断言 `int[*]` 没有有效模式且存在编译诊断；由 unverified 更新为 verified，排除契约不变。证据 EV-20261013-001；没有固定具体诊断码。 |
| USE-ARRAY-001 / S1–S3 | 中文解释保持数组身份、方向和 rank 契约；重新执行原有 IL 一致性过滤器，以 EV-20261013-003 追加当前快照。边界示例的运行时类型输入不代替该 IL fixture。 |
| USE-REF-001 / S1 | 重新执行核心 ref 维度用例，追加 EV-20261013-002；契约不变。 |
| USE-REF-001 / S2、S5 | 新示例只补裸 Type 的可用性、out/ref 成员及部分注解。S2 的未知分类消费行为、S5 的完整 readonly/location 分类未覆盖，两者仍 unverified；新证据是部分补充。 |
| USE-REF-001 / S3、S4、S6 | 未新增 Cecil/Roslyn/Fody 精确分类证据，状态不变。 |
| GEN-IDENTITY-001 / S1–S3 | 规范与指导说明翻译，声明身份、闭合上下文及各后端原有证据范围不变；重新执行原过滤器，追加 EV-20261013-003。不能由 List 元数示例推出声明身份或完整绑定已验证。 |
| CROSS-AVAILABILITY-001 / S1 | 重新执行中立事实查询及不可变性用例，追加 EV-20261013-002。边界示例额外演示空属性占位、参数位置和选定超限输入。 |
| CROSS-AVAILABILITY-001 / S2–S4 | 没有新增读取失败、取消和生命周期宽场景证据；状态不变。H1 没有执行。 |
| CROSS-LOGIC-001 / S1、S2 | 规范真值表不变，重新执行完整核心逻辑/诊断/Boolean 用例，追加 EV-20261013-002；新边界示例不代替这些完整用例。 |
| INV-0006–0010、0012–0015 | 更新盘点说明和反馈出处，区分新增的选定用户示例与全量评估；全部继续 unassessed。未新建全量支持矩阵。 |

本轮没有产品实现、公开 API、包依赖、兼容范围或语义变更；未改变 profiles、不变量适用性或 N/A 记录，故没有新增 N/A 核验要求。文档同步仍按已批准试点规则执行，完整全仓同步闸门保持原状态。

三份历史 EV-20261012 报告保留原字节和当时快照。规范中文化改变文件哈希，因此通过独立语义比对及本次实际重验，追加三份 EV-20261013 报告和当前实现/规范/配置快照。旧报告继续说明历史执行事实；当前证据适用性由新增报告和此审查记录支撑。快速开始示例再次运行成功，当前输出日志保存在 `artifacts/documentation-feedback/quick-start.log`；其部分覆盖范围仍按原报告解释。

## 实际验证

环境：Windows、.NET SDK 10.0.401、net10.0、Release。正式报告记录具体运行时、依赖、完整 HEAD、工作树状态、相关文件 SHA256 及原始日志 SHA256。

~~~powershell
dotnet run --project docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj -c Release --no-restore
dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release --no-restore
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RefKindTests|FullyQualifiedName~MetadataAvailabilityTests|FullyQualifiedName~MatchLogicTests" --logger "trx;LogFileName=core-feedback.trx" --results-directory artifacts/documentation-feedback
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ArrayShapeModelTests|FullyQualifiedName~MethodGenericOwnerIdentityTests" --logger "trx;LogFileName=conformance-feedback.trx" --results-directory artifacts/documentation-feedback
dotnet format docs/usage/examples/MatchingBoundaries/MatchingBoundaries.csproj --verify-no-changes --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
~~~

| 验证 | 实际结果与依据 |
| --- | --- |
| 新边界示例 | 56 项检查通过，含编译检查；[EV-20261013-001](evidence/EV-20261013-001.json)，不解读为 56 个独立能力。 |
| 快速开始示例 | 输出 `Documentation examples passed.`，退出码 0；上述原始输出日志和执行回执保留。 |
| 核心主题回归 | 68 通过、0 失败；[EV-20261013-002](evidence/EV-20261013-002.json)。 |
| 跨适配器一致性回归 | 19 通过、0 失败；[EV-20261013-003](evidence/EV-20261013-003.json)。 |
| 新示例格式 | `dotnet format --verify-no-changes --no-restore` 通过，存在工作区加载警告，退出码 0。 |
| 解决方案格式 | `dotnet format AspectN.sln --verify-no-changes --no-restore` 通过，存在工作区加载警告，退出码 0。 |
| 文档结构与引用 | 5 能力、19 场景、7 章用户指南；零错误、零警告。 |
| 差异与新增文件空白 | `git diff --check` 和新增文件末尾检查通过；src、tests、eng、profiles 及旧证据报告无改动。 |

没有重新运行完整 .NET/.NET Framework 或 Linux 矩阵。校验器源码和 fixture 未变，本轮不重复扩展其测试；最终文档结构校验、差异检查及非作者复核结果在下节登记。原始执行日志属于忽略的 artifacts 目录，正式报告保留路径和摘要，可按命令重新验证。

## 最终复核

2026-10-13，非作者 /root/documentation_pilot_review 最终复核通过，无未解决的阻塞问题。独立核验新三份报告的 118/155/169 个文件快照、三个原始日志 SHA、56 项示例检查、68/19 项 TRX 计数及实际环境；确认只有 USE-ARRAY-001/S4 新增 verified，其余部分覆盖和未盘点状态没有升级。

复核发现类型使用位置分类摘要仍写着未登记 `int[*]` 断言，已同步为“验证编译拒绝及诊断存在，未固定具体诊断码”，随后再次通过文档校验。两规范渲染结构正常，历史任务、步骤和原有决策保留。结论限于本轮反馈及明确列出的证据范围。

沿用维护者此前的相关文件提交授权；提交范围仅为本轮文档、规则、示例与证据。既有未跟踪的 `nul` 保留，不推送或发布。
