# RM-19：正则复用的测量与处置

日期：2026-10-23；对应 GLM-M3。基准 `782c38f4952374ecb6141bdcd26065926e11e209`。维护者批准先比较单规则、多规则交错与预建实例，再决定生产改动。[计划](../plans/2026-10-23-rm19-regex-reuse.md)与[性能报告](../performance/2026-10-23-rm19-regex-reuse.md)保留方法、原始状态和取舍。

## 实现与边界

原“每次重新解析”的指控不成立：静态 Regex API 有缓存。本轮七项修改前基准中，单规则静态调用没有分配；32 条规则交错时，静态调用多分配约 1,556 B/次。实际 AspectN Match 出现相同差额，因此选择复用 Binder 验证时已创建的 Regex。

Binder 按不可变编译选项的 RegexTimeout 创建 CultureInvariant 实例，BoundRegexPattern 随计划持有；Evaluator 在规范文本生成后调用该实例。无效语法继续产生 AN3002，错误绑定树不发布计划；其诊断节点可以持有 null。未启用 RegexOptions.Compiled，不修改全局 Regex.CacheSize，不新增公共 API、选项或匹配状态。

子串语义、受限语法、默认250ms/显式有限/Unlimited、超时Unknown/AN3003、规范文本额度、缺事实与取消规则保留。实例可共享的依据参见 [Regex 官方线程安全契约](https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex?view=net-10.0#thread-safety)；本轮也实际验证并发结果及诊断隔离，但不据此承诺第三方后端的任意并发访问。

代价为计划及其缓存引用期间保留正则和可复用缓冲。本轮简单规则的 2,048 份计划首次使用后额外占约 3.61—3.62 MiB，约 1.81 KiB/份；未使用时约 0.84 KiB/份。不是单个对象的精确尺寸，也不代表复杂正则的上界。使用指南已说明控制缓存及计划引用的方式。

## 能力与证据适用性

直接涉及 SEM-REGEX-001/S1、S3 的实例创建、语法错误及超时；关联 CROSS-LIFETIME-001/S1 的计划复用、缓存键与并发，以及 CROSS-RESOURCES-001/S1 的取消和正则文本上限。EV-20261023-305 登记当前源码和执行范围，历史报告不改写。GEN-IDENTITY-001 与规范文本算法未改，相关格式化及适配器测试仍随完整矩阵运行，不扩展为新身份承诺。未新增或修改 N/A。

性能工程继续登记 INV-0020，尚未独立建档，不把测量提升成完整发布性能闸门。用户章节只增加正则实例随计划持有的生命周期说明；表达式、公开调用、配置、返回值和诊断未改变，无需迁移说明。

## 当前验证

新增 RegexReuseTests 共七项：分配比较、四组默认/1ms/500ms/Unlimited 超时与同值缓存复用、共享模式的八个并发工作者各64次成功/失败检查，以及复用后的取消传播和再次匹配。

分配回归在原实现先失败：256 次重复单规则分配 1,338,432 B，交错32条为 2,134,976 B；其余两项并发/取消通过。修改后分配相等，相关正则、资源和配置 115 项通过；随后补充四项超时持有断言，与完整矩阵一起执行。回归不改变全局缓存设置，使用禁并行的 xUnit 集合；测试输入确定，无墙钟阈值，也不承诺完整 Match 零分配。

修改前七项和修改后三项 BDN 均完成，前后基准验证入口通过。实际32规则Match从7668降至6112 B/次，本机均值2.656降至2.131μs；单规则和编译耗时不作改善判断。完整编译分配增加8 B/份，存活内存代价另列；不只报告有利指标。

完整 Release 构建通过，生产 netstandard2.0/net10.0 均编译；net472 测试工程保留一条既有 MSB3277 System.Memory 依赖冲突告警。完整矩阵共 2,436 项通过，0 失败、0 跳过：核心 1,398，Cecil 232，Reflection 110，Roslyn 30，一致性 415，.NET Framework 工程 net472 为8、net10.0 为9，Fody 234。格式检查退出码0，保留工作区加载告警。

```powershell
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false --logger trx --results-directory artifacts/rm19/tests -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --include src/AspectN/Binding/Binder.cs src/AspectN/Binding/BoundSelector.cs src/AspectN/Evaluation/RegexPatternEvaluator.cs tests/AspectN.Tests/Evaluation/RegexReuseTests.cs benchmarks/AspectN.Benchmarks --verbosity minimal
```

独立只读审查通过，无阻塞发现。逐项核对五份原始报告与紧凑数据：十个案例的均值、误差、标准差、样本数、内存字段、全部调整后样本和120条原始实测样本一致；前后环境、源码哈希及十次独立存活堆记录匹配。EV-20261023-305 的27项源码/数据快照、八份TRX哈希和计数、各场景测试方法均核对通过。未扩张后端并发或性能承诺。

`eng/verify-documentation.ps1 -AsJson` 返回0错误、0告警；知识库生成与 `eng/generate-knowledge.ps1 -Check` 通过；`git diff --check` 通过。汇总见本地 `artifacts/rm19/verification.json`。历史证据未修改，原始综合报告保留；未跟踪的核对清单仅更新RM-19进度，既有README及其他无关文件不纳入提交，不推送或发布。
