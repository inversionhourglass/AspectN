# RM-09：小栈探针的内部反射入口校验

日期：2026-10-23。对应 K3-M14 与后续 RM-09。基准为 `ae2165b8132f515a0509c91d0688ddf19d5178b9`。维护者批准保留反射和现有小栈回归，补充类型/方法定位、精确签名与返回类型校验，不扩张公共 API 或修改生产算法。

## 实现与范围

[MappingDepthProbe/Program.cs](../../tests/AspectN.MappingDepthProbe/Program.cs) 的 hidden-pointer 分支改用内部 [RuntimeTypeFactsProbe](../../tests/AspectN.MappingDepthProbe/RuntimeTypeFactsProbe.cs) 定位入口。新增辅助类只编译到测试探针可执行程序集，不进入生产适配器；所有成员均为内部或私有。

定位要求类型名为 `AspectN.Reflection.Internal.RuntimeTypeFacts`，方法名为 `ContainsIntPtr`，且唯一匹配非公开、非泛型、静态、返回 `bool`、恰好一个 `System.Type` 参数的方法。显式比较参数类型，避免默认绑定器把 `object` 重载作为匹配。同名的其他重载可以存在；缺失、签名不符或完整匹配不唯一均明确失败。

类型缺失消息包含期望类型、入口签名及实际程序集完整身份；方法不符消息另包含匹配数量和实际同名候选的可见性、实例/静态分类与签名。注释说明该反射入口用于在现代宿主定点验证旧运行时所需的扫描算法。

主程序除新增 using 和两行查找委托外保持一致：256 KiB 线程、1024 层 IntPtr 数组输入、`List<int[]>` 反向对照、Invoke 调用、错误捕获及成功标记均不变。父测试 [MappingDepthTests](../../tests/AspectN.Conformance.Tests/MappingDepthTests.cs) 未修改，仍以独立子进程和原超时机制执行。未修改 RuntimeTypeFacts、生产资源配置或其他探针形状，也未增加公开测试入口或 InternalsVisibleTo。

## 知识库与历史记录

关联 `CROSS-RESOURCES-001/S4` 的 Reflection 小栈探针，以及 `USE-SHAPES-001` 中函数指针表示的相关边界。新增检查只判断测试入口是否有效，不更改类型映射、旧运行时降级规则、可用性或逻辑契约，不改变能力状态或 N/A，也不影响用户调用方式，故无需修改能力元数据、其依赖或用户章节。

六份引用主程序的历史证据 EV-20261012-002、EV-20261013-003、EV-20261013-009、EV-20261013-014、EV-20261014-006、EV-20261022-201 保留原文和哈希。新记录不替换历史快照，也不将现代宿主中的算法探针解释为旧 CLR 全链路验证。

## 当前验证

修改前后原有 `Reflection/hidden-pointer/1024` 隔离进程场景各 1 项通过，0 失败、0 跳过，实例名称保持。修改后命令如下；修改前在原已构建产物上使用相同过滤器和 `--no-build`，TRX 为 `artifacts/rm09/before/rm09-before.trx`：

```powershell
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~MappingDepthTests&DisplayName~hidden-pointer' --logger 'trx;LogFileName=rm09-after.trx' --results-directory artifacts/rm09/after -v:minimal
```

修改前后还分别直跑已构建探针，均输出 `mapped:Reflection:hidden-pointer:1024`，日志位于 `artifacts/rm09/before/small-stack.log`、`artifacts/rm09/after/small-stack.log`。初次直跑命令将构建选项传入了探针参数，产生 FormatException，原日志保存在 `artifacts/rm09/before/invalid-arguments.log`；已修正调用后重跑，不将该次失败当作通过或产品问题。

临时控制台工程 `artifacts/rm09/probe` 不纳入提交。它用合成程序集调用实际内部定位方法，七组错误输入涵盖类型缺失、方法缺失、错误参数、错误返回值、实例方法、公开方法、泛型方法；均产生包含期望签名和实际程序集身份的明确错误。另有合法入口、合法入口加 object 同名重载两组成功对照；九组均通过，见 `artifacts/rm09/lookup-probe.log`。这些合成入口只用于校验定位，不声称真实生产程序集出现过这些变化，也不证明所有 CLI 方法修饰符组合均已验证。

`artifacts/rm09/verification.json` 记录主程序精确差异、父测试源码哈希不变、原小栈与正反检查保持、两个 TRX 的结果/哈希、成功标记一致、九组定位对照计数及六份历史证据哈希检查。本轮未重跑其余 MappingDepthTests 参数行或完整兼容矩阵。

两个探针源文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include ... --verbosity minimal` 检查退出码为 0；工具有工作区加载告警，不将结果表述为加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 与 `git diff --check` 通过。

独立只读复核通过，无需修复项。复核者确认唯一非公开静态非泛型 `bool ContainsIntPtr(Type)` 的精确筛选、失败消息、主程序范围与原正反断言保持；独立核对两个 TRX、九组定位对照以及父测试和六份历史证据不变，确认生产算法与公共 API 未改动，报告的验证边界准确。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新进度，继续未跟踪。不推送、不发布、不修改相邻仓库。
