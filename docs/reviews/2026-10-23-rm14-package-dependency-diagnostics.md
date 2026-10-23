# RM-14：先核对依赖集合，再检查版本

日期：2026-10-23；对应 K3-M20；基准 `b36fe9e4e3d627725179ef63cbf3ecb3789c254d`。维护者批准按[计划](../plans/2026-10-23-rm14-package-dependency-diagnostics.md)最小调整检查顺序。

## 问题与修改

原脚本先检查每项依赖版本，再检查整体依赖集合。未知依赖没有预期版本，首先报 unexpected dependency version，容易引导维护者寻找一个不该存在的依赖的正确版本。集合和版本同时错误时，也可能优先报告版本错误。异常包原本已经会被拒绝，本项改善失败原因的呈现。

现在仅把现有 dependencyIds 和 Assert-DependencySet 块移到版本循环前：集合不符先显示 Actual 与 expected，集合正确再检查版本。版本提取规则、许可依赖集合、各目标框架的集合及数量检查、RM-13 默认值读取和其他代码保持。移除两份源码中的该同一代码块后，其余字节完全相同。

## 回归与实际输入

新增 `eng/tests/verify-package-dependencies.Tests.ps1`，由真实脚本 AST 提取消费者初始化之前的完整前缀，以 `-ValidateExisting` 在独立子进程校验合成五对 ZIP 包。包含实际 nuspec、元数据、库/文档路径和符号路径检查；其中 DLL/PDB 是占位内容，测试在消费者阶段前截断，不执行合成程序集，也不代表 NuGet 消费者可用性。

11项包括正常五包、未知/已知额外依赖、缺失依赖、额外或缺失依赖与版本同时错误、两类纯版本错误、仅单个目标框架缺依赖、分组数量和身份异常。校验明确退出码及诊断，并核对输入包哈希未改变。基准8项通过、3项因先报版本错误而失败；修改后 PowerShell 7 与 Windows PowerShell 5.1 各11项通过、0失败。纯版本错误和各目标框架的失败检查继续有效。

```powershell
pwsh -NoProfile -File eng/tests/verify-package-dependencies.Tests.ps1
pwsh -NoProfile -File eng/tests/verify-package-dependencies.Tests.ps1 -VerifierHost powershell
```

另复用 RM-13 生成的五对真实包，使用当前脚本相同的完整包内容校验前缀，在 Windows PowerShell 执行默认版本读取、五包元数据与符号检查，退出0。运行前后十个包的 SHA256 均与 RM-13 数据一致，没有重新打包。临时探针放在 eng 目录以保持 PSScriptRoot，结束仅删除本次创建且哈希匹配的副本，本地 artifacts 保留快照和结果。

本轮没有再次运行消费者阶段。[RM-13 记录](2026-10-23-rm13-package-version-defaults.md)中的 Roslyn/net472 System.Memory MSB3277 冲突及完整流程退出1仍然有效；不能把当前元数据阶段通过解释为完整流程恢复。也未运行远端 Actions、Linux、签名、发布或产品完整测试矩阵。

## CI、能力与文档影响

CI 仅在原 Windows 包 job 中，Verify packages 前新增独立依赖诊断测试步骤。RM-13 初始化测试、RM-11 双平台格式检查、触发条件、矩阵、其余 job、Release 和 Compatibility 保持。工作流解析比较、红绿及真实包输入哈希见[本轮数据](data/2026-10-23-rm14-package-dependency-diagnostics.json)。

INV-0014 补充工程记录；CROSS-COMPAT-001/S1—S5 的产品代码、API、兼容资产、场景验证状态、evidence_ids 和 N/A 均未改。用户调用与包策略没有变化，使用章节无行为修订需求，工程入口及诊断变化由本报告覆盖。历史报告和既有完整流程失败记录保持。

独立只读复核通过，无可操作问题。审查者核对代码只移动原块、33份留存合成探针与对应源码一致、真实包及记录哈希匹配，并独立验证测试末尾失败分支会向宿主返回退出码1。CI新增步骤未放宽失败传播。

文档校验0错误、0告警，知识库生成及-Check、工作流结构比较、diff检查通过。仅提交本项脚本、测试、CI步骤与配套资料；核对清单保持未跟踪，其他既有未提交文件保留，不推送、不发布。
