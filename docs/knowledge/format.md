# v1 格式与校验范围

权威字段约定见[最终设计配套格式](../superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-examples.md)。能力用 aspectn-capability:v1、Finding 用 aspectn-finding:v1 的唯一 JSON 注释块。

本次实现补充：usage/ 每个 Markdown 章用唯一 aspectn-usage:v1 块，含 schema_version、capability_ids、inventory_refs。未建档行为关联盘点并给规范入口，不用假的能力编号。模板目录仅供复制，不作为实际条目检查。

运行 `pwsh -NoProfile -File eng/verify-documentation.ps1`；有错误退出 1，有 Finding 引用告警仍退出 0。

首批检查字段、枚举、编号、profile、条件必填、本地引用和章级能力引用。不联网检查外部 URL，不自动核实批准真实性、不验证条款含义或证据当前适用性。review 必须判断这些语义。[fixture 回归](../../eng/tests/verify-documentation.Tests.ps1)验证错误与告警级别。

JSON 由标准库 System.Text.Json.JsonDocument 读取，ISO 日期保持字符串；不会依赖 PowerShell ConvertFrom-Json 的日期自动转换。文字字段禁止用数字替代，verified 场景的 unverified_notes 可以是空字符串。

报告 code_state 按声明的实现/测试/规范/配置范围保存文件 SHA256。更新报告和状态不使证据自身失效；历史报告不改写，影响变化在条目重新判断。
