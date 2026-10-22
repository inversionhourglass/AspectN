# v1 格式与校验范围

权威字段约定见[最终设计配套格式](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-examples.md)。能力用 aspectn-capability:v1、Finding 用 aspectn-finding:v1 的唯一 JSON 注释块。

本次实现补充：usage/ 每个 Markdown 章用唯一 aspectn-usage:v1 块，含 schema_version、capability_ids、inventory_refs。未建档行为关联盘点并给规范入口，不用假的能力编号。模板目录仅供复制，不作为实际条目检查。

运行 `pwsh -NoProfile -File eng/verify-documentation.ps1`；有错误退出 1，有 Finding 引用告警仍退出 0。

首批检查字段、枚举、编号、profile、条件必填、本地引用和章级能力引用。不联网检查外部 URL，不自动核实批准真实性、不验证条款含义或证据当前适用性。review 必须判断这些语义。[fixture 回归](../../eng/tests/verify-documentation.Tests.ps1)验证错误与告警级别。

JSON 由标准库 System.Text.Json.JsonDocument 读取，ISO 日期保持字符串；不会依赖 PowerShell ConvertFrom-Json 的日期自动转换。文字字段禁止用数字替代，verified 场景的 unverified_notes 可以是空字符串。

报告 code_state 按声明的实现/测试/规范/配置范围保存文件 SHA256。更新报告和状态不使证据自身失效；历史报告不改写，影响变化在条目重新判断。

`code_state.files[].path` 是取证时的历史快照路径。校验器检查仓库相对路径、目录边界和 SHA256 格式，不要求同名文件仍存在，也不将当前文件内容或 Git HEAD 当作 dirty 快照的原始内容。`dependency_scope.paths` 中精确匹配同一报告有效快照条目的路径沿用该历史语义；其他依赖路径仍必须存在于当前工作区。不同报告不能互相豁免缺失依赖。

规范、来源、`oracle_refs` 和当前 Markdown 正文中的文件引用仍要求实际存在。允许保存历史路径不表示旧证据适用于当前实现，也不证明历史内容可从 Git 恢复；代码、规范或环境变化后的适用性仍由审查和重验决定。校验器不拉取 Git 历史、不替换旧路径、不更新旧哈希，浅克隆和无 Git 的合成 fixture 使用相同规则。

## 派生目录与矩阵

能力条目的场景元数据及 coverage.json 是唯一状态来源。修改后运行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`，生成 generated/catalog.md 与 generated/support-matrix.md；不能手工修改这两页。

生成器写入前复用完整文档校验，仅在生成预检中允许这两个派生目标缺失或正文陈旧。其他来源、能力、证据、章节及引用仍完整校验；默认文档校验检查生成后的全部链接。生成器不判断语义批准或当前证据适用性。

CI 运行 `pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check`，派生资料缺失或陈旧时失败，且不会改写文件。[生成器回归](../../eng/tests/generate-knowledge.Tests.ps1)覆盖独立状态、确定性、陈旧输出、完整格式预检、路径及 Markdown 转义；[文档校验器回归](../../eng/tests/verify-documentation.Tests.ps1)保留完整格式规则的回归覆盖。
