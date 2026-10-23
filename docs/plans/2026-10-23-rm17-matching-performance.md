# RM-17 匹配分配与吞吐实施计划

> 执行说明：维护者已在当前对话批准先测量、再按证据优化并复验；继续在现有检出目录执行。本文仅覆盖 RM-17，不替代历史任务 35 的完整发布性能闸门。

**目标：** 建立可复跑的纯匹配阶段基准，并量化、消除证据明确的临时分配。

**架构：** 使用 BenchmarkDotNet 的独立基准工程，GlobalSetup 完成初始候选映射和模式编译，计时方法只调用已编译模式的 Match；必要的后端关系查询和祖先映射仍计入 Match。所有场景分别运行 Declaration/Capture，保留现有默认预算和真实 ReflectionTypeSystem。生产改动优先限于修饰符单元素数组路径，不改变语义、计量、取消或公开 API。

**技术栈：** .NET 10、BenchmarkDotNet 0.15.8、现有 Reflection 适配器、xUnit；依赖版本仍由 Directory.Packages.props 集中管理。

**执行记录：** 修改前后各 18 项基准完成；六项双修饰符场景每次减少 160 B，其余 12 项分配不变，不认定吞吐变化。局部分配红绿回归、完整 2,343 项测试、生产双目标编译、格式与文档检查均完成，独立只读复核通过。结果与范围见[处置记录](../reviews/2026-10-23-rm17-matching-allocations.md)和[性能报告](../performance/2026-10-23-rm17-matching.md)。

## 任务 1：建立测量入口

新增 `benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj`、`Program.cs`、`MatchingBenchmarks.cs`、`MatchScenario.cs`、`Fixtures/` 和中文 README，修改 `Directory.Packages.props` 与 `AspectN.sln` 注册实际使用的工程。

1. 固定当前基准 `a8b19b12491646f46ff79866368ab3c29e328acf`，保留既有未提交文件；保存待改源文件和历史证据哈希。
2. 增加无修饰符、修饰符成功/失败/备选、命名空间成功/失败、嵌套闭合类型、开放类型变量、祖先模式遍历场景；Setup 校验预期状态及无诊断。
3. 提供 `--validate` 只执行全部参数组合的正确性检查；benchmark 的公开辅助类型使用英文 XML，Markdown 使用中文。
4. 运行 `dotnet run --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release -- --validate`，必须全部通过后才测量。

## 任务 2：记录修改前数据

1. 用 MemoryDiagnoser 记录分配量，独立进程预热并测量耗时；计时内不编译、不进行初始候选映射、不构造初始输入、不打印，Match 内部必要的查询及映射计入测量。
2. 同一工程、同一机器与作业设置运行完整场景，结果写入 `artifacts/rm17/before`；测量期间不并行运行构建或测试。
3. 保留原始日志、完整 JSON/CSV、环境和参数；先审查测量设置及场景路径，再做生产优化。

## 任务 3：按实测进行小范围优化

涉及 `src/AspectN/Evaluation/ModifierMatcher.cs`；如测量确认该路径有可消除分配，先建立有意义的分配回归或等价性能验收失败证据，再将单个修饰符判断提取为不构造临时集合的路径。现有集合入口继续复用同一逻辑。可为只读列表使用索引遍历，避免额外枚举器分配。

1. 保持 static、正向/否定权限、权限备选及 unavailable 事实语义。
2. 运行现有修饰符、可用性和两种变量模式相关回归；必要的新测试位于 `tests/AspectN.Tests/Evaluation/ModifierMatcherTests.cs`，不得用不稳定的耗时阈值作测试。
3. 类型链、namespace、DP 与 Fork 先记录真实成本；仅在证据和回归充分时扩大优化，不预先引入共享缓存、池或改变预算。

## 任务 4：复测、文档与提交

1. 使用完全相同设置和场景写入 `artifacts/rm17/after`，比较每次分配、均值及误差。区分确定的分配减少与受环境噪声影响的吞吐变化。
2. 新增 `docs/performance/2026-10-23-rm17-matching.md`，说明命令、环境、结果、范围和未测项；同步 `docs/reviews/2026-10-23-rm17-matching-allocations.md` 与受影响知识库，或给出具体无影响理由。历史证据不改写。
3. 生产改动后运行核心完整测试及相关适配器/一致性回归，核验生产双目标编译、格式、`eng/verify-documentation.ps1` 和 `eng/generate-knowledge.ps1 -Check`。
4. 按 requesting-code-review 技能完成独立只读复核，只提交本次明确列出的文件。不提交 README 的既有改动、原始 review、综合报告、未跟踪核对清单或 nul，不推送或发布。
