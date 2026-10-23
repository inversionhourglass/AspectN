# RM-10：签名前校验未签名摘要

日期：2026-10-23；对应 K3-M15。基准为 `0cdcdc0d6712e279bf1aaac24b6bc835d7a35849`。维护者批准签名前强制核对下载产物，保留原包验证、签名验证和签后摘要。[实施计划](../plans/2026-10-23-rm10-unsigned-checksums.md)与[脚本说明](../../eng/verify-unsigned-packages.md)记录范围和入口。

## 问题与处理

原流程在 build job 记录并上传 `SHA256SUMS.unsigned`，签名 job 却未读取它，签名后直接删除。因此清单只有留存作用，未构成签名前的文件交接检查。

新增只读校验器：要求清单存在且格式合法，目录文件集合与清单完全一致，逐包 SHA256 必须相符。拒绝空清单、空记录、重复及大小写别名、路径、非包条目、目录和重解析点。校验失败以非零退出码中止，输入保持不变；摘要不是独立来源认证。

签名 job 检出事件提交 `${{ github.sha }}` 取得脚本，关闭凭据持久化；下载后执行校验，通过后才进入含证书环境变量及证书写入的签名步骤。新增步骤没有 `if` 或 `continue-on-error`，后续步骤保留默认成功条件，符合 GitHub Actions 的[默认状态检查规则](https://docs.github.com/en/actions/reference/workflows-and-actions/expressions#status-check-functions)。

现有五个 nupkg、五个 snupkg 数量检查保持，校验器自身不重复固化数量。build 的 NuGet 内容/消费者验证、逐包签名和签名验证、签后删除未签名清单及生成新摘要、上传和发布流程均保持。常规 CI 仅在既有 Windows/Linux 矩阵中增加脚本黑盒测试；触发条件、兼容矩阵和 Compatibility 工作流不变。

## 能力与文档影响

本项属于发布工程，新增 INV-0021 盘点，尚未独立建立产品能力条目，保持 `unassessed`，不提升产品场景验证状态或修改 N/A。库的公共 API、语言、匹配行为和生产包内容未改，用户使用章节无行为影响；工程运行方式由脚本说明覆盖。历史综合报告与既有证据保留，核对清单只更新本项进度。

## 当前验证

新增脚本前运行同一黑盒入口，32 项均因目标脚本尚不存在而失败；这只是新增入口的缺失基线，没有执行旧的远端发布流程。实现完成后，PowerShell 7 与 Windows PowerShell 5.1 各 32 项通过、0 失败。四个正例覆盖完整十包、大小写哈希、CRLF 和清单重排；28 个反例覆盖缺失/空/损坏清单、重复、非法路径、额外或缺失文件/目录、nupkg/snupkg 内容修改。所有用例核对运行前后输入快照。

Windows PowerShell 补测先暴露无 BOM 中文源码解码问题，已增加单文件 BOM 编码规则。其后本地受限执行环境阻止执行策略注册表访问；只针对本地合成测试使用允许该访问的环境，最终全部通过。测试启动旧宿主时不继承 PowerShell 7 的模块路径，不修改机器或用户的执行策略。

本地以 YAML 1.2 解析实际工作流，0 错误、0 告警，并做结构比较：Release 只新增 checkout 和校验两个步骤，CI 只新增一个测试步骤，Compatibility 哈希不变。顺序核对覆盖检出、下载、校验、签名、签后摘要、上传及发布；从实际 YAML 提取未签名摘要生成步骤，在十个合成包上执行，其输出被新校验器接受。

```powershell
pwsh -NoProfile -File eng/tests/verify-unsigned-packages.Tests.ps1
pwsh -NoProfile -File eng/tests/verify-unsigned-packages.Tests.ps1 -VerifierHost powershell
```

机器可读汇总见[本轮数据](data/2026-10-23-rm10-unsigned-checksums.json)，包含输入脚本、工作流及本地结果哈希。这里的跨平台 CI 是已配置的入口，本机只实际运行 Windows；没有触发远端 Actions、下载真实发布产物、处理真实证书、签名或发布。未运行 .NET 全矩阵，因为本项没有修改 .NET 源码、工程或包内容；不得把脚本用例计入产品测试总数。

独立只读审查未发现可操作缺陷或阻塞问题，复算源码、工作流输入、两份测试结果、缺失入口基线及结构检查结果的哈希，均一致。审查者另复用现有输入重跑正常、大小写别名重复、缺包、snupkg 篡改、Windows 父路径和清单目录六例，退出码与诊断符合预期，未改输入。重解析点拦截只经过源码审查，本轮没有对应 fixture；保留该验证边界。

文档校验返回 0 错误、0 告警；知识库生成与 `-Check`、`git diff --check` 通过。新增工程盘点的派生矩阵由生成器更新。只提交本项脚本、工作流和配套资料；既有 README、原始 review、综合报告和其他未跟踪文件保留，核对清单保持未跟踪。不推送、不发布。
