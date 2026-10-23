# RM-20：Cecil 类型缓存成本评估

日期：2026-10-23；对应GLM-M5。基准为 `34de9bc8554d18867f253661384679f31da1383f`。维护者批准先测量再决定优化；[计划](../plans/2026-10-23-rm20-cecil-cache.md)与[性能报告](../performance/2026-10-23-rm20-cecil-cache.md)记录输入和判断。本项结论为完成性能评估并保留生产实现，不登记为性能缺陷已修复。

## 路径、证据与判断

原“所有缓存命中仍付全价”的指控过强。CecilTypeCache.Find/GetOrAdd 先按引用查找；跨引用时才使用键，键、名称和作用域也有缓存。公开MapType仍执行结构守卫和预算，引用命中不能解释为整次调用免费。

新增9组公开映射基准，涵盖具名类型、泛型/数组组合和32层数组，各运行引用命中、跨引用结构命中、根稳定身份未命中。输入在计量外构造，每轮重建适配器缓存并只执行一次1024项批次，防止结构命中预热后退化成引用命中。GlobalSetup与CLI验证全部输入的形状及签名复用。未命中允许复用已见子图；没有无缓存对照，也没有隔离单个内部方法的全部成本。

九项、两个进程、每进程六条Actual数据完成；短轮次和较大误差保留，分配及108条原始样本见紧凑数据。DeepArray三条路径为16960/27936/218340 B/MapType，不能把路径差额全算作键构造；Allocated不是缓存存活内存。

Append(object) 的静态装箱疑点没有在局部探针中转成可复现收益：原实现的布尔/小整数预热委托写入均0B，文本/额度/文化控制通过，共12项。该结果不覆盖生产所有调用上下文，不能证明算法最优；但足以不为此静态疑点添加代码。探针已移出常规Cecil测试，保留为独立[重现附件](../performance/reproductions/2026-10-23-rm20/README.md)。生产源码、正式回归、API、身份编码、预算与生命周期均未修改，没有虚构after或红绿修复证据。

## 能力与文档影响

性能工程继续登记INV-0020，尚未独立建档，保持unassessed，不将测量提升为完整发布闸门。CROSS-LIFETIME-001正文补充查找层次及本轮评估链接，未修改其场景承诺、验证状态或evidence_ids。TYPE-NOMINAL-001的Cecil/Fody身份、USE-ARRAY-001的rank/Shape和CROSS-RESOURCES-001的预算实现均未改，不需要新的语义批准或更改N/A。

用户调用、表达式、缓存设置、返回值、诊断和默认配置完全保持，用户使用章节没有行为修订；新增基准README及性能报告负责解释工程运行方式和适用范围。历史证据报告保持，新增入口与Cecil项目引用只属于不打包的基准工程；不能将工程依赖变化外推为生产包依赖或旧性能数据的重新验证。

## 当前验证

九项 `--validate-cecil-cache` 通过，BDN全部完成；可重现附件12项通过，原实现中的探针同样通过。完整Release构建成功，生产netstandard2.0/net10.0均编译；net472测试工程保留一条既有MSB3277 System.Memory依赖冲突告警。

完整解决方案2,436项通过，0失败、0跳过：核心1398，Cecil232，Reflection110，Roslyn30，一致性415，net472为8、兼容工程net10.0为9，Fody234。独立探针12项单列，不计入解决方案总数。原始TRX哈希和计数附于[数据](../performance/data/2026-10-23-rm20-results.json)。

```powershell
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false --logger trx --results-directory artifacts/rm20/tests -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --include benchmarks/AspectN.Benchmarks --verbosity minimal
```

独立只读审查通过，无阻塞发现。逐项核对9组统计与原始完整JSON、全部108条Actual样本、9份源码快照、2份探针源码/工程哈希及9份TRX，均匹配；确认src/tests无diff。BDN日志包含8项短轮次警告，最短实测批次910.9μs，报告保留这些限制。

解决方案内基准源码及独立探针工程的格式检查均退出0，保留工作区加载告警。`eng/verify-documentation.ps1 -AsJson`返回0错误、0告警；知识库生成、`eng/generate-knowledge.ps1 -Check`与`git diff --check`通过。构建的既有MSB3277不记为零告警。汇总保存在本地 `artifacts/rm20/verification.json`。

只提交本项基准、重现附件及配套文档。核对清单更新评估进度并保持未跟踪；既有README、原始review、历史综合报告与nul保留，不推送、不发布。
