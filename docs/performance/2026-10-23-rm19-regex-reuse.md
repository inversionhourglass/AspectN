# RM-19 正则复用、分配与存活内存

日期：2026-10-23。生产基准为 `782c38f4952374ecb6141bdcd26065926e11e209`。本轮先测量再决定优化，将 Binder 语法验证时创建的普通 Regex 保留到编译计划，匹配调用实例方法。收益集中于多规则交错时的缓存压力；代价是计划存活期间持有更多对象及执行缓冲。[实施记录](../reviews/2026-10-23-rm19-regex-reuse.md)说明回归和能力范围。

## 方法与输入

.NET 静态 Regex API 有进程共享缓存，默认最多 15 项；复用单条规则不能描述为每次都重新解析。实例构造与静态缓存是不同路径，持有自己的实例也是复用方式，参见 [Microsoft 正则复用文档](https://learn.microsoft.com/en-us/dotnet/standard/base-types/compilation-and-reuse-in-regular-expressions)。本次不使用 RegexOptions.Compiled，也不调整生产全局缓存容量。

[基准工程](../../benchmarks/AspectN.Benchmarks/README.md)使用 BenchmarkDotNet 0.15.8。每项两个独立进程、每进程三轮预热和六轮测量，启用 MemoryDiagnoser。统计采用默认异常值处理，表中 Error 为 99.9% 置信区间半宽，因此统计样本可能少于 12；[机器可读数据](data/2026-10-23-rm19-results.json)保存调整后样本及逐进程原始实测轮次。

固定规则为 `Run|Unused0000` 等，只有未命中的第二分支随编号变化。分别使用 1/32 条规则，每条连续匹配两个候选：一个规范签名含 Run，另一个为 Math.Abs；所有调用成功/失败各半。64 次构成一批，匹配结果及分配按每次匹配归一化。32 条超过默认缓存容量 15，表示缓存压力输入，不假定每条规则每轮必然未命中。

RegexReuseBenchmarks 只匹配预建规范字符串，比较静态 API 与普通预建实例。RegexMatchingBenchmarks 使用预编译的 AspectN 模式和预映射的 Reflection 候选，计入完整 Match 的候选检查、上下文和规范文本生成。Setup 校验规范文本、所有预期状态及零诊断；Regex 均为 CultureInvariant、250ms。仅独立基准进程清空静态缓存并设为 15，退出时恢复原值。

RegexCompilationBenchmarks 关闭引擎编译缓存，测完整解析、绑定及编译结果构造；相同源码每次真正编译，不能把结果解释为纯 Regex 构造成本。修改前测全部七项，修改后复测实际 Match 和完整编译三项；预建字符串比较的实现未变，不重复运行。前后所有基准源码哈希完全一致，只有三个生产文件变化；源码快照和完整报告哈希随数据保留。

环境：Windows 11 25H2（10.0.26200.9711）、Intel Core i9-13900H、14 物理核/20 逻辑核、x64；SDK 10.0.401、.NET 10.0.12、RyuJIT、Concurrent Workstation GC，无调试器。BDN 测量使用高性能电源计划，结束后恢复。测量期间未并行构建、测试或运行其他 CPU 密集工作。

## 匹配与编译结果

预建字符串上的同场策略比较：

| 规则数 | 静态 API 均值 ± Error（ns） | 持有实例均值 ± Error（ns） | 静态 B/次 | 实例 B/次 |
| --- | ---: | ---: | ---: | ---: |
| 1 | 38.66 ± 3.07 | 41.45 ± 2.22 | 0 | 0 |
| 32 | 528.36 ± 40.12 | 41.56 ± 1.76 | 1556 | 0 |

单规则两种策略均无分配，误差区间重叠，不能称实例更快。32 条规则下的额外分配和时延支持缓存压力解释，但不能把该输入外推为所有正则的表现。

真实 AspectN 对照：

| 操作 | before 均值 ± Error（μs） | after 均值 ± Error（μs） | before B/次 | after B/次 |
| --- | ---: | ---: | ---: | ---: |
| Match，1 条规则 | 2.046 ± 0.170 | 2.126 ± 0.193 | 6112 | 6112 |
| Match，32 条规则 | 2.656 ± 0.219 | 2.131 ± 0.207 | 7668 | 6112 |
| 完整编译，关闭缓存 | 2.084 ± 0.141 | 2.004 ± 0.078 | 8304 | 8312 |

32 条规则交错的完整 Match 每次减少 1,556 B，与预建字符串对照相同；本机均值降低约 20%。这只描述本轮输入，不能作为跨机器或整体吞吐承诺。单规则 Match 与完整编译的前后区间重叠，不认定耗时改善或退化。编译分配增加 8 B/份，来自 Binder 增加的超时字段；原先也会构造 Regex，保留它并不等于再构造一次。

## 存活内存代价

单独运行 `--regex-retention`，在五个独立进程中各持有 2,048 份不同编译计划。先预热并清空静态缓存；源字符串、引擎和候选在基线前创建并保持存活，引擎缓存容量为 0；首次匹配前/后分别强制 GC 取堆大小差值。NoInlining 工厂和 GC.KeepAlive 防止计划提前失去强引用。该方法估计整个计划集合及静态缓存的存活堆，不能解释为单个 Regex 的精确对象图，也不是 MemoryDiagnoser 的累计分配量。

| 状态 | before 增量（B/2048 份） | after 增量（B/2048 份） | 额外增量 |
| --- | ---: | ---: | ---: |
| 尚未首次匹配 | 802840（5 次一致） | 2555928（5 次一致） | 1753088 B，约 0.84 KiB/份 |
| 每份各匹配成功/失败一次后 | 832840（5 次一致） | 4620312（4 次）/4628536（1 次） | 3787472—3795696 B，约 1.81 KiB/份 |

已使用计划额外保留约 3.61—3.62 MiB/2048 份；单次差异如实保留，不选择性删除。复杂表达式、捕获和较大输入的执行缓冲可不同，不将这些数字写成固定配额。计划由调用方及引擎缓存持有，失去引用后由 GC 回收；用户可按业务控制缓存容量和计划引用数量。此取舍换取多规则复用不依赖进程其他正则对共享缓存的竞争。

## 复跑与限制

```powershell
dotnet build benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --validate-regex
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*Regex*Benchmarks*' --artifacts artifacts/rm19/before
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --regex-retention
# 切换生产实现、重新构建并验证后：
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*RegexMatchingBenchmarks*' '*RegexCompilationBenchmarks*' --artifacts artifacts/rm19/after
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --regex-retention
```

修改前复跑须保留同一基准工程，将 Binder.cs、BoundSelector.cs、RegexPatternEvaluator.cs 三个生产文件换为基准提交内容再构建；不能对优化后的同一 DLL 仅改变输出目录便当成 before。存活堆探针每个状态执行五个独立进程。完整 JSON/CSV、日志、源码哈希和红绿回归保存在本地忽略目录 `artifacts/rm19`，提交的紧凑数据保留可审查测量及原始报告哈希。

本轮只测 .NET 10、单线程、简单分支、默认有限超时及两个有限规范签名；不外推复杂回溯、并发吞吐、其他运行时/后端、冷启动或无上限内存。默认/自定义/Unlimited 超时、并发结果隔离、取消、非法子集及错误语法另由语义回归验证，不能由本性能表替代。没有新增 CI 墙钟阈值或公共配置。
