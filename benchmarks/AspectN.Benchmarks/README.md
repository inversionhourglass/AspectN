# 匹配阶段专项基准

使用 [BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet/0.15.8) 测量已编译模式的 `Match` 耗时及分配量。GlobalSetup 完成初始候选的 Reflection 映射、编译、预期状态和无诊断检查；计时内不编译表达式、构造初始候选或打印。祖先场景中 Match 必要的后端闭包查询和祖先映射计入测量。

从仓库根目录执行：

```powershell
dotnet run --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release -- --validate
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*' --artifacts artifacts/benchmarks
```

九种场景各运行 Declaration/Capture 两种模式，涵盖修饰符成功/失败/权限备选、命名空间、嵌套闭合类型、开放类型变量及祖先通配遍历。NamespaceMiss 使用 `AspectN.Missing..*Service`，类型名先匹配成功，再验证命名空间路径失败。普通方法作为对照。每次匹配保留生产默认资源策略，所有预期结果均由 Setup 检查。

第一条命令构建并验证；第二条直接运行已构建的 DLL。本轮 Windows 执行环境中，`dotnet run` 默认使用的 apphost 无法创建基准日志，直接使用 `dotnet` 运行 DLL 可正常写入并测量。

默认作业为 .NET 10、两个独立进程、每进程三轮预热和六轮测量，并启用 MemoryDiagnoser。输出包含环境、参数、均值、误差、GC 和每次操作分配量。运行期间避免并行构建、测试或其他 CPU 密集工作。对耗时差异结合误差及重复进程判断，不能将单次均值波动直接宣布为提升。

结果默认写入已忽略的 `artifacts/benchmarks`；对照测量使用独立 before/after 目录，保留原始 JSON/CSV 和日志。`--validate` 只验证输入及结果，不构成性能测量。匹配专项仅覆盖 RM-17 的选定路径，不代表完整发布性能闸门、冷映射、其他适配器或 .NET Framework 的表现。

## 正则复用专项

```powershell
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --validate-regex
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*Regex*Benchmarks*' --artifacts artifacts/rm19/before
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --regex-retention
# 修改生产实现、重新构建并验证后，复测完整匹配与编译：
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*RegexMatchingBenchmarks*' '*RegexCompilationBenchmarks*' --artifacts artifacts/rm19/after
```

RegexReuseBenchmarks 在预生成规范字符串上比较静态缓存与普通 Regex 实例；RegexMatchingBenchmarks 计入真实 Match 的校验、上下文、规范文本生成；两者都用 1/32 条规则，64 次一批，成功/失败各半，结果按每次匹配归一化。每条规则连续匹配两个候选，32 条超过默认缓存容量 15，但不假定每次都未命中。实例不启用 RegexOptions.Compiled。只有独立基准进程会清空静态缓存并设为 15，结束时恢复原值；生产实现不修改全局设置。

RegexCompilationBenchmarks 关闭引擎编译缓存，计时完整解析与绑定，不能解释为纯 Regex 构造。`--validate-regex` 校验全部结果、零诊断及规范文本；`--regex-retention` 预热后持有 2,048 份不同编译计划，以强制 GC 前后差值估计首次匹配前/后的存活堆，建议五个独立进程重复。该差值包含整个计划集合及静态缓存，并非单个对象的精确大小；与 MemoryDiagnoser 的每次分配量是不同指标。

本轮结果、源码状态及命令见 [RM-19 报告](../../docs/performance/2026-10-23-rm19-regex-reuse.md)。只测 .NET 10 单线程简单分支，不外推复杂回溯、并发吞吐、其他后端或运行时。
