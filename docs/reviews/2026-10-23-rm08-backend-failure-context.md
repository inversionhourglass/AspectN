# RM-08：限制一致性测试失败时标明后端和输入

日期：2026-10-23。对应 K3-M13 与后续 RM-08。基准为 `825297b8bfdc390b4e55be48e68e7c4250ccad28`。维护者批准补全失败定位，保留原测试输入、限制策略和通过标准，不修改生产代码。

## 范围与实现

只修改 [SignatureLimitConformanceTests](../../tests/AspectN.Conformance.Tests/SignatureLimitConformanceTests.cs)。四个既有用例族仍使用同一个 ReviewFixture，依次验证 Roslyn source、Roslyn PE、Cecil、Reflection；没有改变后端枚举顺序或构造方式。

每次后端验证通过私有 `WithBackendContext` 执行，失败时抛出带后端及输入信息的 `XunitException`，原异常保留为 `InnerException`，保留原断言消息与原异常堆栈。此包装只作用于测试中的映射和断言，不是生产异常恢复；fixture 构造及进入循环前的编译检查仍在原位置。

| 用例族 | 补充的输入信息 |
| --- | --- |
| 普通泛型元数 | arity、methodParameters |
| 嵌套类型元数 | outerArity=32、innerArity |
| 嵌套构造类型深度 | arrayLayers |
| 超限祖先的匹配分支 | text、expected |

三个 `Assert.Throws<AspectNLimitException>(map)` 和后续诊断码相等断言原样保留，仍要求精确异常类型；没有放宽为可接受派生异常。两个枚举相等条件改用 `Assert.Equal`，便于直接显示预期/实际状态；返回类型非空的条件保持。祖先诊断检查改为在诊断码序列中 `Assert.Contains` 指定码，仍执行同样的区分大小写字符串相等判断，并在失败时显示缺失的码与实际码集合。

首个用例的超限分支从循环 `continue` 改为当前同步验证委托的 `return`，随后继续下一后端；没有增加映射次数或跳过后端。未改 DirectAttributeLimitTests 或其他一致性用例，不扩大本次整改范围。

## 知识库与历史证据

关联 `CROSS-RESOURCES-001/S2—S4` 与 `CROSS-AVAILABILITY-001/S3` 中已登记的指定结构/元数输入。输入、默认限制、映射/查询和诊断语义保持，本轮只改善测试失败呈现，不改变能力元数据、验证状态、N/A、其逻辑/可用性依赖或用户章节。聚焦重跑不替代这些场景中其他修饰符、特性或小栈探针的验证。

六份引用该测试类的历史证据保留原文和哈希：EV-20261012-002、EV-20261013-003、EV-20261013-009、EV-20261013-014、EV-20261014-006、EV-20261022-201。方法名和测试实例未改，不回填历史失败输出或快照哈希。

## 当前验证

修改前和修改后均为 13 项通过，0 失败、0 跳过；两份 TRX 的测试实例名称集合一致。修改前执行完整构建的聚焦命令，修改后先由临时探针工程重新构建，再用 `--no-build` 运行相同过滤器：

```powershell
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~SignatureLimitConformanceTests --logger 'trx;LogFileName=rm08-before.trx' --results-directory artifacts/rm08/before -v:minimal
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~SignatureLimitConformanceTests --logger 'trx;LogFileName=rm08-after.trx' --results-directory artifacts/rm08/after -v:minimal
```

临时控制台工程 `artifacts/rm08/probe` 不纳入提交。修改前直接执行代表性原断言，六类失败均没有所需上下文，成功对照通过，见 `probe-before.log`。修改后通过反射调用实际私有辅助方法，未抛异常、错误异常类型、错误诊断码、错误状态、缺失诊断、非预期映射异常六类均满足定位检查，且成功委托只执行一次，见 `probe-after.log`。探针核对消息包含后端、输入及原消息，原异常保留堆栈；标签覆盖四种视图，但不将合成失败等同于真实适配器发生过这些故障。

`artifacts/rm08/verification.json` 记录 InlineData 不变、三个精确异常断言与诊断码断言保持、测试实例集合和计数、TRX 哈希、六份历史证据哈希检查及七个探针对照的通过数。

该测试文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include tests/AspectN.Conformance.Tests/SignatureLimitConformanceTests.cs --verbosity minimal` 检查退出码为 0；工具有工作区加载告警，不将结果表述为加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 与 `git diff --check` 通过。

独立只读复核通过，无需修复项。复核者确认四个循环、13 个输入实例及各分支映射次数保持，三个精确异常类型与诊断码检查保留，枚举相等和诊断码集合检查未放宽判定；独立核对两个 TRX、六份历史证据哈希及六类失败探针和成功对照，确认消息与原异常保留符合本次范围。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新进度，继续未跟踪。不推送、不发布、不修改相邻仓库。
