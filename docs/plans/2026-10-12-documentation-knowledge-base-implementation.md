# AspectN 文档与知识库试点实施计划

> **执行者：** 使用已批准设计逐项实施；当前会话由 Codex 执行，完成后按 requesting-code-review 技能请求独立审查。

**目标：** 建立可使用、可审查、可校验的用户文档和五主题知识库。
**架构：** Markdown 正文配一个 JSON 元数据块；JSON 来源、profiles、coverage 与证据报告共享编号引用。PowerShell 7 校验结构和本地引用，产品语义仍依既有正式规范。五主题按场景分别登记证据，其余主题保留未盘点状态。
**技术栈：** Markdown、JSON v1、PowerShell 7 标准库、既有 xUnit/.NET 10 测试、现有 GitHub Actions CI。

正式依据：[最终设计](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md)及[字段格式](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-examples.md)。
用户于 2026-10-12 确认设计并授权开始实施。按 AGENTS.md 使用现有 checkout，保留原讨论稿与 nul；不修改产品实现，不发布或推送。

## 执行状态（2026-10-12）

任务 1–8 已完成。非作者最终复核通过，结构检查零错误/零告警，38/38 fixture、87 项相关产品测试和可执行示例通过；工作树范围与空白检查通过。实际命令、证据及未覆盖范围统一记录在[试点审查记录](../reviews/2026-10-12-documentation-pilot.md)。后续全量扩展与完整全仓同步闸门不属于本轮完成声明。

## 任务 1：来源、盘点与 profiles
**文件：** docs/knowledge/{sources.json,coverage.json,profiles.json,conflicts.md,index.md}、docs/knowledge/categories/*/index.md。
1. 将批准语言规范登记为 normative；metadata/array 专题按委托范围登记，泛型身份按可追溯批准范围确认，历史计划与讨论登记为 history。
2. 建立九分类前缀表与四个条件不变量；feature/cross_cutting 共用九个正文节标题。
3. 五主题 documented，其余主题/API/适配器事实 unassessed；无条目不能推断 excluded。
4. 记录冲突登记规则；当前未确认冲突时明确为空，不制造产品语义裁决。

## 任务 2：五主题条目与使用指南
**文件：** docs/knowledge/capabilities/{generics/GEN-IDENTITY-001.md,type-uses/USE-REF-001.md,type-uses/USE-ARRAY-001.md,cross-cutting/CROSS-AVAILABILITY-001.md,cross-cutting/CROSS-LOGIC-001.md}；docs/usage/*.md；README.md。
1. 逐场景登记 core/cecil/roslyn/reflection 范围，执行前为 unverified、evidence_ids=[]。
2. 使用已批准规范和已定位测试，写摘要、契约、输入条件、分层能力、阶段失败、例子与边界、规范依据、实现与证据、关联与缺口。
3. 写 quick-start、adapters、matching、results-and-diagnostics、limits-and-pitfalls、migration 和入口，行为陈述关联五主题或明确未盘点来源。
4. 每主题给出可核验例子；精确区分模型数组身份与 DSL rank、泛型声明与闭合查询上下文、ByReference 与 RefKind。

## 任务 3：审查路由、模板及历史 Finding
**文件：** docs/knowledge/review/index.md；docs/knowledge/templates/{capability.md,finding.md,evidence.json}；docs/reviews/findings/FND-20261011-001.md；AGENTS.md；.github/PULL_REQUEST_TEMPLATE.md。
1. 路由强调只加载触及能力和依赖，不默认加载讨论历史。
2. H1 按维护者“不修复、不阻塞”迁入 accepted_risk，保留合成输入及真实可达性证据缺口。
3. PR 同步清单和初期 AGENTS 规则区分已建档范围与盘点例外；完整全仓规则暂不自动生效。
4. N/A 默认 pending；由非作者审查实际核验，记录核验主体与日期。

## 任务 4：先验证校验器的失败案例
**文件：** eng/tests/verify-documentation.Tests.ps1。
1. 建立独立最小仓库 fixture，命令为 `pwsh -NoProfile -File eng/verify-documentation.ps1 -RepositoryRoot <fixture> -AsJson`。
2. 测试有效记录 exit=0；非法状态、重复 ID、缺字段、错误 profile、越界路径、失效规范/能力/证据引用和用户声明 exit=1。
3. 测试 Finding 引用缺失/已关闭只 warning，历史正文不受新模板约束；Markdown fenced 示例链接不当正文链接校验。
4. 运行 `pwsh -NoProfile -File eng/tests/verify-documentation.Tests.ps1`，记录功能尚未实现的 红阶段 结果。

## 任务 5：实现轻量校验
**文件：** eng/verify-documentation.ps1。
1. 读取 JSON 和恰好一个版本块，检查字段、容器、类型、枚举、条件必填和 profile 节标题。
2. 建立来源/能力/场景/证据/Finding/盘点集合，解析仓库内文件路径并验证引用；未知规范批准不由 CI 猜测。
3. Finding 引用统一告警；外部链接、条款含义、源码符号与自动证据适用性不做硬断言。
4. 返回稳定 JSON {errors:[],warnings:[],counts:{...}}；有错误 exit=1，否则 exit=0；默认输出简短文本。
5. 运行全部 fixture 测试，修复至 绿阶段；不引入第三方依赖。

## 任务 6：执行真实证据与用户示例
**文件：** docs/reviews/evidence/EV-20261012-*.json；docs/usage/examples/ReflectionQuickStart/{ReflectionQuickStart.csproj,Program.cs}；有关能力元数据。
1. `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~RefKindTests|FullyQualifiedName~MetadataAvailabilityTests|FullyQualifiedName~MatchLogicTests" --logger "trx;LogFileName=core-pilot.trx" --results-directory artifacts/documentation-pilot`。
2. `dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --filter "FullyQualifiedName~ArrayShapeModelTests|FullyQualifiedName~MethodGenericOwnerIdentityTests" --logger "trx;LogFileName=conformance-pilot.trx" --results-directory artifacts/documentation-pilot`。
3. `dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release`。
4. 保存命令、范围、TFM/SDK/依赖、HEAD/dirty/相关文件 SHA256 和测试计数；只标记实际覆盖场景 verified。Fody、源码模式或未知输入未跑的场景保持 unverified。

## 任务 7：接入 CI 与独立试点审查
**文件：** .github/workflows/ci.yml；docs/reviews/2026-10-12-documentation-pilot.md。
1. 在现有 build-and-test job 加 pwsh 文档校验及 fixture 回归，不创建新 workflow。
2. 按 requesting-code-review 请求非作者审查：从入口找 ref/array/identity 边界，做一次实际变更影响分析，并核验 N/A；记录证据而非虚构签章。
3. 修复重要发现，记录已修复文档 Finding 与有效验证证据；未得到核验的项保持 pending，不宣布全仓闸门通过。

## 任务 8：最终验证与交付
1. `pwsh -NoProfile -File eng/verify-documentation.ps1`。
2. `pwsh -NoProfile -File eng/tests/verify-documentation.Tests.ps1`。
3. `git diff --check`；检查 git status，确认只有任务范围修改且保留原未跟踪文件。
4. 更新实际交付和待扩展范围：A/B/D 的工程产出、C 的真实审查记录，E 保留后续扩展；不把试点完成描述成全量能力完备。
