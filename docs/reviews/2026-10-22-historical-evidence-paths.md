# 历史证据路径导致文档生成阻塞的修复

日期：2026-10-22。基准为 `HEAD 2f1d56a717983dc4b72d747200b58f39ac272925` 加现有工作区改动。维护者授权修复文档校验和知识库生成的 248 项旧路径阻塞；本轮保留既有资源配置、RI-01 和 RI-02 的改动。

## 根因与修复边界

修复前运行 `eng/verify-documentation.ps1 -AsJson` 稳定得到 248 项错误。缺失文件只有五个，均为 Reflection/Roslyn 从 SignatureMapper 分部文件调整到 MappingOperation 后保留在旧报告中的路径。43 份既有证据中，共有 124 个缺失源码快照条目和 124 个缺失依赖条目；每个缺失依赖均在同份报告的源码快照中登记。

校验器原先将 `code_state.files[].path` 与 `dependency_scope.paths` 都交给当前文件存在性检查。该实现把“取证时的文件路径”误当成“当前工作区必须存在的引用”，与历史报告保留原始代码状态的规则冲突。直接将旧路径改成新文件名，也会错误地把旧哈希和旧执行结论关联到重构后的实现。

本轮在 [verify-documentation.ps1](../../eng/verify-documentation.ps1) 中分离路径合法性和当前文件存在性检查：

- 历史快照路径检查仓库相对格式、目录边界和 SHA256 格式，不要求同名文件仍存在。
- 依赖路径只有精确匹配同一报告的有效快照条目时才按历史路径处理；未登记的缺失依赖仍报错，不跨报告匹配。
- 当前规范、来源、oracle_refs 和 Markdown 正文引用继续要求文件存在；绝对路径、越界路径和非法哈希继续拒绝。
- 不查询或拉取 Git 历史，不假定 HEAD 能恢复 dirty 快照，也不修改旧报告的路径、哈希、结果或日期。

这是一项文档工具修复，不改变产品语言、公共 API、适配器、测试矩阵或发布流程。历史报告能够通过格式检查，不等于其执行结果仍适用于当前资源配置改动；后者仍需由该任务的能力影响分析与重验落实。

## 回归与当前证据

在生产校验逻辑修改前新增回归，观察到校验器 55 项 fixture 中仅新增的两项历史路径成功场景失败，均准确报告旧源码路径不存在；生成器新增的真实重命名场景也因同一原因失败。

修复后校验器 **55 项 fixture 全部通过**，知识库生成器 **14 项黑盒检查全部通过**。新增输入覆盖普通校验与生成预检中的源码重命名、未快照的缺失依赖、跨报告记录隔离、当前证据依据和正文断链、绝对及越界路径、非法哈希。生成器回归还验证重命名后生成与 `-Check` 成功，且生成前后历史报告文本不变。

复现和验证命令：

```powershell
pwsh -NoProfile -File eng/tests/verify-documentation.Tests.ps1
pwsh -NoProfile -File eng/tests/generate-knowledge.Tests.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
```

本地红绿日志及修复前 43 份证据的文件哈希快照保存在 `artifacts/docs-history-fix/`。本报告记录文档工具的验证，不新增产品能力的 verified 场景，也不代表远端 Actions 或当前工作区完整 .NET 测试矩阵通过。

真实仓库执行生成、`-Check` 和完整文档校验均返回 0，校验结果为 **0 错误、0 告警**。43 份既有证据 JSON 的 SHA256 与修复前逐一相同。支持矩阵已由正式生成器同步此前被阻塞的 RI-01 生命周期证据、RI-02 盘点说明及本轮工具盘点，分类目录也完成一致性检查；未手工修改派生状态。完整结果见 `artifacts/docs-history-fix/after.json`。

文档工具此前未单独建档，本轮以盘点 `INV-0018` 登记工程范围；不分配虚构的产品能力编号。格式说明、审查流程和盘点同步更新；历史规范及报告保持原始结论。知识库派生资料由正式生成器重建，不手工修改。

独立只读审查者 `/root/historical_evidence_review` 已复核逻辑、文档、原始日志及 43 份证据哈希，未发现阻塞问题。根据其建议，绝对路径、越界路径与非法哈希的回归分别隔离其他缺失依赖，避免无关错误掩盖目标检查；修正后的用例再次得到复核确认。
