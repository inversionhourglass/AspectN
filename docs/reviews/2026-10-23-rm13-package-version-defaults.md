# RM-13：明确读取默认依赖版本

日期：2026-10-23；对应 K3-M19；基准 `31a61600c13a94e9d99d7c5fc40d288bed6be398`。维护者批准按[计划](../plans/2026-10-23-rm13-package-version-defaults.md)改用明确 XPath，检查默认节点唯一且非空，保留显式参数优先级。

## 问题与修改

原包验证脚本通过 PowerShell XML 属性链读取版本，依赖单个 PropertyGroup 及节点表示。增加无关分组、拆分属性或去掉节点属性，都可能导致严格模式属性访问错误；重复定义可能产生拼接的多个版本值。

三个版本现在分别查询 `/Project/PropertyGroup/<属性名>`，要求匹配节点恰好一个，并拒绝空或全空白 InnerText。缺失、重复或空值会给出包含 Directory.Packages.props 和具体属性名的明确错误。只在对应参数为空白时读取默认值；显式参数逐属性优先，空白参数继续回退。XML 加载和后续打包、元数据及消费者流程保持。

这仍是仓库默认值读取器，不执行 MSBuild 的条件、属性展开或导入求值。节点可以放在不同 PropertyGroup，重复节点即使值相同也拒绝；复杂条件下的多份定义需要显式传入所需版本。当前三个默认版本与 Directory.Packages.props 均未修改。

## 初始化回归与 CI

新增 `eng/tests/verify-package-versions.Tests.ps1`，从真实脚本 AST 提取到 buildProperties 赋值结束，在合成仓库中以独立子进程执行原有参数绑定和默认值初始化。探针在实际打包前截断，单独核对退出码、明确诊断、三个版本、传给构建的属性以及输入 XML 未改变；不能将这些用例称为完整包验证。

26项包括原布局、无属性节点、多分组/拆分/重排、三个属性分别缺失/重复异值/重复同值/空值/全空白、错误位置、显式全部/部分覆盖、空白回退和未覆盖属性继续检查。旧实现5项通过、21项失败；新实现 PowerShell 7 与 Windows PowerShell 5.1 各26项通过、0失败。旧宿主的合成测试在允许读取本地执行策略的环境中执行，没有修改机器策略。

CI 仅在既有 Windows 包验证 job 中、Verify packages 前新增一步，使用 PowerShell 7 测试宿主驱动 Windows PowerShell 初始化探针。原包验证步骤、其他 job、矩阵、触发条件、Release/Compatibility 和 RM-11 双平台格式检查保持。

```powershell
pwsh -NoProfile -File eng/tests/verify-package-versions.Tests.ps1
pwsh -NoProfile -File eng/tests/verify-package-versions.Tests.ps1 -VerifierHost powershell
```

## 实际包流程与既有阻塞

用当前脚本、真实默认依赖版本和全新 `artifacts/rm13/package-flow-*` 目录运行打包与验证。五个 nupkg、五个 snupkg 生成并通过包内容、两个依赖组及符号检查。core、Cecil、Fody 三个包的 net472/net10.0 消费者构建与六次运行完成；随后 Roslyn 消费者的 net472 构建因 MSB3277 失败：System.Memory 程序集版本4.0.1.2与4.0.2.0冲突。Roslyn 两次运行及后续 Reflection 消费者阶段未执行，完整流程退出1。

从基准提交取得原脚本，用 `-ValidateExisting` 和同一批包、同一 PackageVersion 做对照。它也完成包检查和前三个消费者阶段，在同一 Roslyn/net472 构建处以相同 MSB3277 退出1。因此该失败不由本次默认值读取修改引入；依赖冲突保留为独立验证阻塞，没有把完整五包消费者流程记为通过。

对照脚本临时放在原 eng 目录，以保留它自身的 PSScriptRoot 语义；结束后仅删除本次创建且哈希一致的临时副本，基准快照及两份失败日志保留在本地 artifacts/rm13。此次没有签名或发布。

红绿结果、源码/包/日志哈希及工作流结构检查见[本轮数据](data/2026-10-23-rm13-package-version-defaults.json)。本机执行范围为 Windows；没有执行远端 Actions、Linux 或其他依赖版本。没有重复产品完整测试矩阵。

## 能力与文档影响

工程资料关联 INV-0014；CROSS-COMPAT-001/S1—S5 的 API、程序集身份和兼容资产实现均未改，场景状态、evidence_ids 及 N/A 保持。包验证的本轮成功部分与既有阻塞分别记录，不扩展产品兼容性承诺或改写历史证据。依赖版本、目标框架及用户调用方式无变化，用户使用章节无修订需求；工程命令和边界由本报告说明。

独立只读复核通过，无可操作问题。审查者核对初始化红绿记录、源码、两份完整流程结果及失败日志、基准Git blob和十个包哈希，均与本轮数据一致，确认完整消费者流程仍为失败。全仓文档校验0错误、0告警，知识库生成及-Check、工作流结构比较、diff检查通过。

只提交本项脚本、测试、CI步骤和配套资料。核对清单保持未跟踪，其他既有未提交文件保留，不推送、不发布；System.Memory冲突留待单独处理。
