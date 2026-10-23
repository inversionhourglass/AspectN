# RM-17：匹配热路径分配测量与修饰符优化

日期：2026-10-23。对应 K3-M26、GLM-M2。生产基准为 `a8b19b12491646f46ff79866368ab3c29e328acf`。维护者批准先建立有代表性的匹配分配与吞吐基准，再按证据决定优化范围；[实施计划](../plans/2026-10-23-rm17-matching-performance.md)限定本轮为匹配专项，不替代历史任务 35 的完整发布性能闸门。

## 实现与边界

新增独立的 [AspectN.Benchmarks](../../benchmarks/AspectN.Benchmarks/README.md)，使用已编译模式和预映射初始候选。九个场景分别运行 Declaration/Capture，共 18 个参数组合；覆盖普通方法、修饰符成功/失败/备选、命名空间成功/失败、嵌套闭合类型、开放变量与祖先通配遍历。Setup 同时验证预期状态及无诊断，避免把无效输入测成快速结果。

生产优化只针对 `ModifierMatcher` 的临时单元素数组及只读列表枚举器：集合入口与单元素入口共享判断，逐项可用性检查与三态合取保留。没有引入池、全局缓存或可共享的执行状态；没有修改 Fork、命名空间拆分、类型链、DP 缓冲、预算及取消规则。

祖先场景包含真实 Reflection 闭包查询及该查询内部必要的祖先映射；仅初始输入映射和表达式编译在计时外。默认生产预算不变。完整原始测量保存在本地忽略目录 `artifacts/rm17`，可审查摘要与复跑方式见专项性能报告。

## 能力、证据与用户文档

直接涉及 `MEM-SELECTORS-001/S1` 的修饰符判断，以及 `CROSS-AVAILABILITY-001/S1` 的独立事实可用性。EV-20261023-303 为这两个中立场景登记当前回归和源码快照；原有选择器、复合权限、否定、权限备选与缺失事实均在修改后重验。33 份相关历史证据哈希与修改前一致，不改写原结论。局部分配回归只承诺预热后的已知事实修饰符判断不分配，不承诺整个 Match 零分配。

`GEN-BINDING-001`、`TYPE-NOMINAL-001` 和 `CROSS-RESOURCES-001` 仅作为基准关联范围：本轮不修改变量绑定、类型关系、解析或资源实现。两种变量模式和所选类型输入用于性能观察，不替代这些能力全部场景的证据。不新增或改动 N/A。

基准工程与测量方法登记为 INV-0020 性能盘点项，因尚未独立建档而标记 unassessed，保留完整发布闸门及其他运行时/后端的证据缺口，不新增产品语言或公共 API。用户调用、返回值、诊断和可配置选项不变，现有使用章节没有行为修订；新增工程 README 与性能报告负责解释运行方式和数据限制。BenchmarkDotNet 只由不打包的基准工程引用，不进入生产包依赖。

## 当前验证

新增 `Evaluate_KnownModifiers_DoesNotAllocate` 五组输入。修改前四组双修饰符输入均因 40,960 B/256 次分配而失败，空修饰符对照通过；修改后五组均为 0 B，且状态符合成功、失败、备选、否定各自预期。相关 ModifierMatcherTests 与 MetadataAvailabilityTests 合计 61 项通过，红绿 TRX 与日志在 `artifacts/rm17/red`、`green` 及其上级目录。

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~ModifierMatcherTests|FullyQualifiedName~MetadataAvailabilityTests' --logger 'trx;LogFileName=allocation-green.trx' --results-directory artifacts/rm17/green -v:minimal
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false --logger trx --results-directory artifacts/rm17/tests -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --include src/AspectN/Evaluation/ModifierMatcher.cs tests/AspectN.Tests/Evaluation/ModifierMatcherTests.cs benchmarks/AspectN.Benchmarks --verbosity minimal
```

完整 Release 构建通过，生产 netstandard2.0/net10.0 均编译；net472 测试工程仍有一条 MSB3277 依赖冲突告警，不将构建结果称为零告警。完整测试共 2,343 项通过，0 失败、0 跳过：核心 1,305，Cecil 232，Reflection 110，Roslyn 30，一致性 415，.NET Framework 工程 net472 为 8、net10.0 为 9，Fody 234。格式检查退出码为 0，保留工作区加载告警。

两轮各 18 项基准完成，前后验证入口也各 18 项通过。两种模式下，修饰符成功、失败、权限备选共六项每次 Match 均减少 160 B；其余 12 项分配量保持。均值与误差及逐进程数据见[性能报告](../performance/2026-10-23-rm17-matching.md)。耗时有升有降，不据此认定整体吞吐提升或退化。

独立代码复核通过，确认单项谓词逐分支等价、索引遍历适用于内部固定列表、Evaluate 没有新增失败短路，分配测试的计量范围准确。最终只读复核又逐项对照两轮完整 JSON、仓库紧凑 JSON 和报告 18 行：参数唯一配对、环境一致、每项两个进程与 3/6 预热/实测轮次、均值/误差/分配及逐进程样本均匹配；八份 TRX 的哈希、计数与实际结果一致，19 个证据源码快照和 33 份历史证据哈希均匹配。没有阻断发现。

`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；生成知识库后，`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 通过。初次生成预检指出尚无独立能力 ID 的 INV-0020 不能使用 needs_evidence，已按工程盘点规则改为 unassessed 并说明范围；未修改校验规则。`git diff --check` 通过。汇总核验保存在 `artifacts/rm17/verification.json`。

既有 README、原始 review、综合报告及 nul 保留；未跟踪的后续核对清单只更新进度，不纳入本次提交。不推送、不发布。
