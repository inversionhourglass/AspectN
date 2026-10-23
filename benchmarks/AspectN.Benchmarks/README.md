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

结果默认写入已忽略的 `artifacts/benchmarks`；对照测量使用独立 before/after 目录，保留原始 JSON/CSV 和日志。`--validate` 只验证输入及结果，不构成性能测量。此工程仅覆盖 RM-17 的选定匹配路径，不代表完整发布性能闸门、冷映射、其他适配器或 .NET Framework 的表现。
