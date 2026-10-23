# RM-20 Cecil 类型缓存成本评估

日期：2026-10-23。生产基准 `34de9bc8554d18867f253661384679f31da1383f`。本轮完成有限输入的性能评估，保留生产实现，没有优化后的第二轮或性能缺陷修复声明。具体处置见[审查记录](../reviews/2026-10-23-rm20-cecil-cache.md)。

## 输入与测量方法

基准调用公开 CecilSignatureMapper.MapType，包含输入结构守卫、预算、缓存查找，以及未命中后的必要映射。原生输入在计时前构造，成功的缓存命中也不能解释为整次 MapType 零成本。

三种类型形状：

- Named：内存程序集中的 `CacheFixture.Value0000` 等具名类。
- GenericArray：`Pair<Value0000[,], Pair<Value0000[], Value0000>>`，包含两级泛型、向量和二维数组。
- DeepArray：具名元素外包32层向量数组，总深度33，在默认限额内。

每种形状分别测试 ReferenceHit、StableIdentityHit、Miss。ReferenceHit 重复映射同一个预映射对象；StableIdentityHit 只预映射独立种子，每个被测对象都是新引用但根类型结构相等；Miss 的每个根包含不同的 `Value####`，根稳定身份首次出现。Miss 不表示所有子图都未命中，例如泛型声明仍可能复用。

每轮1024次 MapType，计时方法只执行一次批次并保存结果。IterationSetup 在计时外释放旧适配器并创建新的实例缓存，必要时预映射种子，避免后续轮次被已有引用缓存污染。没有在计时内构造输入、重建适配器或打印；GlobalSetup 和独立验证入口检查全部1024份结果的程序集/名称、泛型实参、各层数组rank/Shape，以及是否复用预期签名。

使用 BenchmarkDotNet 0.15.8 / MemoryDiagnoser，两个独立进程、每进程三轮预热和六轮测量。IterationSetup 强制 InvocationCount=1、UnrollFactor=1，见 [BenchmarkDotNet 官方说明](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)。OperationsPerInvoke=1024，使耗时与分配按每次 MapType 归一化。短轮次不适合精确吞吐判断，不能为获得更多采样而反复调用同一批输入并改变缓存路径。

环境为 Windows 11 25H2（10.0.26200.9711）、Intel Core i9-13900H、14物理核/20逻辑核、x64，SDK10.0.401、.NET10.0.12、RyuJIT、Concurrent Workstation GC，无调试器。BDN使用高性能电源计划，结束后恢复。测量期间未并行构建或运行测试。

## 基线结果

下表为完整公开映射调用。Error是99.9%置信区间半宽；分配是BDN报告的累计托管B/次，不是缓存存活内存。默认异常值处理后N为10—12；[紧凑数据](data/2026-10-23-rm20-results.json)保存全部108条原始实测样本及调整后的统计样本、源码和原始报告哈希。

| 形状 | 路径 | 均值 ± Error（μs/次） | B/次 |
| --- | --- | ---: | ---: |
| Named | ReferenceHit | 1.361 ± 0.931 | 1760 |
| Named | StableIdentityHit | 3.724 ± 0.417 | 3868 |
| Named | Miss | 6.991 ± 0.504 | 5585 |
| GenericArray | ReferenceHit | 5.430 ± 1.477 | 3944 |
| GenericArray | StableIdentityHit | 15.966 ± 1.132 | 9890 |
| GenericArray | Miss | 44.819 ± 13.434 | 24254 |
| DeepArray | ReferenceHit | 12.856 ± 1.037 | 16960 |
| DeepArray | StableIdentityHit | 42.161 ± 14.892 | 27936 |
| DeepArray | Miss | 208.489 ± 46.507 | 218340 |

这些输入显示缓存路径确有成本差异：同一对象走引用查找，跨引用识别相等类型后复用签名，根未命中还要创建签名及缓存条目。但差额还包含校验、缓存字典增长和映射等，不能全部归因于结构键，也不是“删除稳定身份缓存”的净收益实验。

八项出现 MinIterationTime 警告；部分轮次低于推荐的100ms，Named/ReferenceHit最短不足1ms；GC、JIT与轮次噪声会显著影响均值。本轮将分配量和路径观察作为基线，不认定普遍吞吐关系或整体程序集处理速度。

## 候选局部优化及决定

源码中 Append 接收 object，布尔和整数参数看起来可能装箱。为避免据静态形式贸然改动，另运行[局部写入探针](reproductions/2026-10-23-rm20/README.md)：反射定位和委托构造在计量外，生成委托直接调用真实私有写入器，StringBuilder预留容量并预热。

原实现中，bool true/false、int1/2/32各写入256次，测得增量均为0B；文本相等、布尔文本额度边界和Invariant整数格式控制也通过，共12项。这仅证明本次 .NET10 预热委托上下文，不能外推实际 AppendShape 的所有调用点、其他值或其他运行时均没有装箱，也不作为产品零分配承诺。

因此没有采用该候选改动。现有双层查找和键/名称/作用域复用已工作；本轮没有得到可复现的局部收益，也未获得支持替换结构键算法的真实负载剖析。保留实现比增加未经验证的代码更合适。这不表示当前遍历或字符串表示已最优，未来若真实工作负载显示其成本占主导，可结合新引用比例、共享子图、泛型形参、函数指针和存活内存重新评估。

## 复跑与范围

```powershell
dotnet build benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --validate-cecil-cache
dotnet benchmarks/AspectN.Benchmarks/bin/Release/net10.0/AspectN.Benchmarks.dll --filter '*CecilCacheBenchmarks*' --artifacts artifacts/rm20/before
dotnet test docs/performance/reproductions/2026-10-23-rm20/TokenProbe.csproj -c Release -m:1 -nr:false -p:UseSharedCompilation=false --logger trx --results-directory artifacts/rm20/reproduction
```

只有一轮生产基线。`before` 是为可能的优化预留的目录名，没有未执行的 after。完整JSON/CSV、BDN日志、源码快照及TRX保存在本地忽略目录 `artifacts/rm20`；提交的数据含测量样本和哈希，独立探针源码及工程可直接复跑。

未测真实程序集整体负载、不同新引用比例、共享子图分布、函数指针/泛型形参、并发争用、跨模块、旧CLR或缓存存活内存。只测普通Cecil，不把结果外推为Fody性能；二者共享生产源码，但性能环境仍须分别测量。本轮不改变身份、预算、生命周期或公共API，没有新增CI墙钟阈值。
