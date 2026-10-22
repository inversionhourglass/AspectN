# RM-16：快速开始示例的当前验证与历史证据

日期：2026-10-23。对应 K3-M22 与后续 RM-16。生产及示例基准为 `007f9aa936099c28b2abf56cebf57d21e9da5684`。维护者批准重跑示例、保存当前证据并说明历史关系；本轮不修改生产或示例源码。

## 执行与处理

执行 `dotnet run --project docs/usage/examples/ReflectionQuickStart/ReflectionQuickStart.csproj -c Release --no-restore -p:UseSharedCompilation=false`，在本机环境退出 0，输出 `Documentation examples passed.`。本次记录为 [EV-20261023-302](evidence/EV-20261023-302.json)，主日志保存在 `artifacts/rm16/ReflectionQuickStart-supported.log`。

初始受限环境的并行 MSBuild 调用失败，诊断日志最终为 Build FAILED、0 Warning、0 Error；没有观察到示例断言失败。随后串行构建通过（0 告警、0 错误），`--no-build --no-restore` 执行通过；本机环境重跑原命令也通过。分别保留失败诊断、分步运行及最终日志，不将环境失败改写为成功，也不据此改动产品实现。

快速开始改为引用本次快照的重跑记录，保留 EV-20261012-003 的试点记录及 EV-20261014-009 的后续复验，明确后者还运行了 MatchingBoundaries。原始两份 JSON 的 SHA256 与修改前记录一致，历史报告不改写。示例说明将笼统的 ref/out 收窄为实际执行的 out 参数，避免扩张断言范围。

## 能力与证据适用性

关联 `GEN-IDENTITY-001/S3`、`USE-ARRAY-001/S3`、`USE-REF-001/S1`、`CROSS-AVAILABILITY-001/S1`、`CROSS-LOGIC-001/S1、S2`。与早期入门示例一样，这些是部分行为的补充证据：闭合类型方法形参相等、Reflection 两种一维数组匹配及身份区分、有效 out 成员、缺失可见性的三行对照、NOT U/F AND U/Unknown→False。普通 public string 成员匹配也是实际执行的控制项。

scenario_refs 只记录相关场景，实际覆盖由证据中的 covered_assertions 和 coverage_notes 限定。未验证声明约束或重载区分、全部 ref 分类、跨后端同源 IL 数组、完整三态表、诊断保留或 Throw；不能据本次示例通过独立判定这些完整场景已重验。也没有重跑 MatchingBoundaries 或所有适配器矩阵。

生产及示例源码均与基准一致，只有使用指南及证据索引发生变化。因此知识库原场景条件、验证状态与 N/A 没有语义变化，不将这次部分重跑追加为整套场景的新验证结论；知识库已有完整回归证据仍按各自范围适用。本轮无需新增能力场景或修改派生矩阵，指南以新证据直接提供本次快照的追溯入口。

`eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`eng/generate-knowledge.ps1 -Check` 通过，派生资料无需修改。`git diff --check` 通过。

独立只读审查者 `/root/rm16_evidence_review` 已核对 17 个快照哈希、执行日志与退出码、历史报告哈希以及实际断言范围，未发现阻断问题。

## 保留范围

既有 README 修改、原始 review、综合报告与 `nul` 原样保留；未跟踪的后续核对清单只更新 RM-16 进度，继续保留未跟踪状态。没有推送、发布或修改相邻仓库。
