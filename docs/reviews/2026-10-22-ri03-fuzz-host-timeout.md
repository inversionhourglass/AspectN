# RI-03：解析 fuzz 的墙钟断言与宿主挂起保护

日期：2026-10-22。来源为综合审查 GLM-I8 / 后续核对 RI-03。基准为 `HEAD 2f1d56a717983dc4b72d747200b58f39ac272925` 加现有工作区改动；资源配置任务的在途实现不纳入本次修复验收。

## 维护者裁决与实现

维护者批准区分挂起防线与性能要求：删除每个随机输入必须在一秒内解析完成的墙钟断言，保留确定性 fuzz 和结构断言，并为本地与 CI 统一提供宿主级挂起保护。本轮没有新增生产 API、解析限额或性能基准。

- [ParserTerminationTests](../../tests/AspectN.Tests/Fuzz/ParserTerminationTests.cs)移除 Stopwatch 和一秒阈值。保留四个属性的生成数量、长度上限、种子和原有 FsCheck Timeout，以及文本长度、诊断起止位置检查。
- 新增延迟回归：同一解析辅助路径在返回前停顿 1.2 秒，正常返回的解析仍应通过。此为模拟调度停顿的确定性反例，没有宣称复现了远端 CI 的偶发失败。
- [核心测试项目](../../tests/AspectN.Tests/AspectN.Tests.csproj)通过 `RunSettingsFilePath` 自动加载 [core-test.runsettings](../../eng/core-test.runsettings)。常规 `dotnet test`、解决方案测试和现有 CI 的项目测试命令使用同一默认配置，无须新增命令行参数。
- 配置使用 VSTest blame 的 `CollectDumpOnTestSessionHang`，`TestTimeout=60s`、`HangDumpType=none`。连续 60 秒没有测试开始或结束事件后终止宿主，使运行失败，不生成内存转储。计时以测试事件为进度，其他并行测试事件也会重置计时，因此不是每个随机输入或每个并行测试的精确截止时间。

四个属性分别生成两组 10,000 和两组 1,000 个输入，共 22,000 个；短、长文本上限分别为 128、1,024，回放种子保留 `20260731,1801`。这些数量不构成全部输入的终止证明。

## 宿主保护的实际边界

最初建议泛化为终止任意子进程，实测发现该承诺超出所选框架模式：当前 VSTest 在 `HangDumpType=none` 时只终止宿主。带额外子进程的合成检查因此正确失败；日志与失败观察保留于 `artifacts/testhost-timeout/a98adc00d83e4cff882d2705c02cfd31`，探针进程已清理。不能把救援清理算作框架通过。

VSTest 的 [BlameCollector 实现](https://github.com/microsoft/vstest/blob/main/src/Microsoft.TestPlatform.Extensions.BlameDataCollector/BlameCollector.cs)在 none 分支跳过转储及进程树处理，最后对宿主调用 `Kill()`；[官方选项说明](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest#options)也单独说明 none 模式终止宿主且不生成转储。自定义宿主内部的 `Kill(true)` 方案经复核不可行，.NET 禁止目标树包含调用进程，相关约束见 [Kill(Boolean) 文档](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0#system-diagnostics-process-kill(system-boolean))。该尝试未进入最终变更。

最终防线针对本来就不启动子进程的解析 fuzz：使用受支持的 VSTest 宿主终止模式，不新增自制跨平台运行器或通用进程清理承诺。以后若加入会启动子进程的测试，须另行设计其生命周期，不能依赖本配置清理它们。直接运行 DLL、其他运行器或显式覆盖 settings 时，也须自行确认配置适用性。

## 当前验证与限制

环境为本机 Windows、.NET SDK 10.0.401 / runtime 10.0.12，Microsoft.NET.Test.Sdk 18.8.1、xUnit 2.9.3、VSTest adapter 3.1.5、FsCheck.Xunit 3.3.4。没有执行远端 Actions 或 Linux；同一项目属性被 SDK 传给 VSTest 的行为通过本机实际运行证明，不能把平台配置相同解释为已执行所有平台。

| 验证 | 当前结果与证据 |
| --- | --- |
| 修复前延迟回归 | 失败，`Parsing took 00:00:01.2187465 for 13 code units.`；TRX 位于 `artifacts/ri03/red`。 |
| 修复后解析终止测试 | 5 项通过，包括四个属性及延迟回归；TRX 和 VSTest 配置日志位于 `artifacts/ri03/green`。 |
| 最终受支持配置下的完整核心测试 | 1,233 项通过，0 失败、0 跳过；TRX 位于 `artifacts/ri03/core-final-supported`。 |
| 最终合成宿主检查 | 3 项通过：正常与延迟返回退出码为 0；真实挂起由 60 秒保护终止，退出码为 1，无外层救援、无遗留宿主、无转储。日志、TRX、Sequence 和 observation 位于 `artifacts/testhost-timeout/c02493f995084b6a83b43888089fb935`。 |

完整核心测试命令：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --logger trx --results-directory artifacts/ri03/core-final-supported -v:minimal
pwsh -NoProfile -File eng/tests/verify-testhost-timeout.Tests.ps1
```

[合成检查脚本](../../eng/tests/verify-testhost-timeout.Tests.ps1)创建独立临时测试项目，读取核心项目求值后的 `RunSettingsFilePath`，没有传入 `--settings` 或超时覆盖；分别执行正常返回、1.2 秒延迟、同步无限循环的 FsCheck 属性。检查 runner 退出码、60 秒超时警告、预期测试在 Sequence 中未完成、无转储及原宿主的退出。PID 与启动时间共同识别进程，避免 PID 重用误判。外层 120 秒只用于救援，若依赖救援则检查失败，不能算宿主防线生效。日志和生成项目保留在忽略的 artifacts 目录。

## 能力、文档与证据适用性

关联 `SYN-GRAMMAR-001` 的解析入口；S1—S3 的语言契约、N/A 和验证元数据不变。此次只调整测试执行防线，没有改变场景输入的语言含义，也没有以完整核心运行替换历史报告或提高其他后端的验证状态。新增盘点 `INV-0019`，将尚未独立建档的 fuzz 与测试宿主工程范围显式登记为 unassessed。

README 的构建测试章节、有关知识库正文、盘点及后续清单同步说明最终边界。库使用指南与公开 XML API 不需要变动，因为这不是库使用方的运行策略或资源选项变更。历史综合报告和历史证据保持原结论；没有重新验收 C01—C07、D01、U01、L01、RI-01 或 RI-02。

已执行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`、同命令加 `-Check` 及 `pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson`，均通过；文档校验为 0 错误、0 告警。派生矩阵由正式生成器更新，没有手工维护第二份状态。聚焦 `ParserTerminationTests.cs` 的 `dotnet format --verify-no-changes --no-restore` 返回 0；工具提示工作区加载告警，不将其描述为整个解决方案格式或编译无告警的证明。`git diff --check` 未报告空白错误。最终代码文件哈希、运行目录和观察摘要保存在 `artifacts/ri03/final-state.json`。

独立只读审查者 `/root/ri03_timeout_review` 已复核最终源码、配置、脚本、TRX、Sequence 及观察记录，未发现本轮范围内的阻塞问题；确认不以普通崩溃或外层清理充当挂起保护通过。本轮没有提交、推送或发布，也没有修改相邻仓库。
