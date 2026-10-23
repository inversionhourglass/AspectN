# 发布包未声明的 System.Memory 依赖修复

日期：2026-10-24；基准 `33eb272a919d61780e1c284be8887d5e8fbb4062`。维护者批准缓存键逐项比较、SourceLink 资产隔离及完整真实包消费者验证。本轮在原检出目录实施，不修改相邻仓库。

## 原因与修复

核心的 `_limits.SequenceEqual(other._limits)` 在当前编译环境绑定到 `System.MemoryExtensions.SequenceEqual(ReadOnlySpan<T>, ReadOnlySpan<T>)`。实际 IL 中包含数组到 ReadOnlySpan 的转换，netstandard2.0 核心 DLL 因而引用 System.Memory 4.0.2.0。它来自 SourceLink→System.IO.Hashing→System.Memory 的私有构建依赖链，核心发布包却没有声明这个运行时依赖。Roslyn 消费者还原的 System.Memory 4.5.5 提供程序集 4.0.1.2，产生 RM-13 中的 MSB3277。

本轮进一步证实这也影响只安装核心包的应用：原消费者每条表达式只编译一次，没有触发缓存键 Equals。增强消费者后，旧真实包在 net472 的第二次编译抛 FileNotFoundException，缺少 System.Memory 4.0.2.0。因此修复覆盖实际运行路径，不只消除构建告警。

[缓存键](../../src/AspectN/Caching/CompiledPatternCache.cs)改为先比较标量字段和数组长度，再逐项比较 ResourceLimit 值，最后按 Ordinal 比较源码；字段、顺序相等、哈希及取消控制保持。比较不创建枚举器或临时集合，不声明完整编译或缓存命中零分配。

五个生产项目的 SourceLink PackageReference 保留 PrivateAssets=all 和原版本，新增 ExcludeAssets=compile;runtime。构建目标、源码链接及符号保持；排除资产不等于从还原图删除节点，Roslyn 的真实依赖路径仍可提供 System.Memory。当前核心实际 ReferencePath 已无 System.Memory/System.IO.Hashing。五个 netstandard2.0 发布 DLL 均没有意外 Memory 引用；net10.0 可以正常引用框架自带的 System.Memory，不能把该合法引用当成同一个问题。

[包验证器](../../eng/verify-packages.ps1)的核心消费者复用一个引擎，成员表达式和类型表达式分别编译两次并检查对象复用。原包依赖集合、版本、两个 TFM、严格告警及发布工作流保持，没有给消费者注入 System.Memory 或放宽诊断。

## 红绿回归与当前结果

[数据记录](data/2026-10-24-package-runtime-dependencies.json)保存源码、产物、日志和 TRX 哈希；本地取证位于 artifacts/package-runtime-dependencies。

- 修改前，新增 `LoadedAssets_CoreAssembly_DoesNotReferenceSystemMemory` 在实际加载 netstandard2.0 核心的 net10.0 宿主下失败，明确匹配到 System.Memory 4.0.2.0；增强后的验证器对 O-02 旧真实包在 net472 缓存命中处失败。
- 修改后完整 Release 解决方案构建通过，零警告、零错误，原 MSB3277 消失。完整矩阵八份 TRX 共 2,438 项通过，零失败、零跳过；兼容项目的 net472 为9项、net10.0为10项，新引用断言在两个宿主均通过。已有缓存测试及 ResourceContractTests 继续验证全部限额按值复用、Unlimited 隔离、LRU、并发及取消。
- 使用唯一版本 `0.0.0-validation-runtime-a6c61790e4dc` 重新生成五个 nupkg 和五个 snupkg，原验证器完整运行到结束：五包内容、依赖、符号检查及十个 net472/net10.0 消费者构建/运行均通过，消费者构建均零警告、零错误。
- 对真实 ZIP 内的 DLL/PDB 配对读取，十个资产均能加载匹配符号且带非空 SourceLink；五个 netstandard DLL 无意外 System.Memory 引用。SourceLink 映射只核验存在、结构和符号匹配，没有抓取远端源码。
- 默认版本初始化回归在 PowerShell 7 和 Windows PowerShell 各26项、依赖诊断各11项通过，合计74项。有关 C# 格式检查退出0，工具仍提示工作区加载警告，不将格式结果称为加载零告警。五个项目 XML 仅另做换行规范化。

测试命令为 `dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false --logger "trx;LogFilePrefix=runtime-dependencies" --results-directory artifacts/package-runtime-dependencies/tests`；完整包入口为 `powershell -NoProfile -File eng/verify-packages.ps1 -PackageDirectory <本轮唯一产物目录> -PackageVersion <本轮唯一版本>`。没有将截断到消费者之前的探针当作完整恢复证据。

## 能力与证据影响

关联 CROSS-LIFETIME-001/S1、CROSS-COMPAT-001/S3、S4 和工程盘点 INV-0014。S1 增加当前缓存比较实现的重验证据，S3/S4 补充实际兼容资产的引用断言；当前证据为 [EV-20261024-001](evidence/EV-20261024-001.json)。包消费者属于独立工程验证，不把它替换成所有兼容场景或任意依赖版本通过。公开 API、语言、资源默认值、后端依赖版本及 N/A 不变，用户章节同步当前构建/运行依赖边界。

两项能力及各四项不变量共10条，抽查最低ceil(10/3)=4；本轮核对两项能力、CROSS-LIFETIME/INVCANCEL和CROSS-COMPAT/INVPARITY。缓存取消及并发控制未改且原回归通过，兼容资产按实际宿主/TFM区分；未改变N/A。资源配置、三态和可用性依赖没有行为修改，相关既有测试纳入完整矩阵，不扩展性能、其他平台、Fody旧宿主或任意加载器承诺。

RM-13、RM-14 和 O-02 的历史失败及当时局限保留；本轮新证据证明所列消费者恢复，原独立问题据此关闭。未跟踪后续清单只更新当前状态，README 与其他既有未跟踪文件保留。未执行远端 Actions、签名、推送或发布。

## 收尾核验

知识库正式生成及 -Check 通过，严格文档校验零错误、零告警；派生矩阵由生成器同步三个场景的新证据及 INV-0014 备注，未手工维护状态。当前18项实现/测试/配置快照、八份TRX及包产物哈希均已核对，历史报告未修改。

独立只读审查 `/root/runtime_dependency_review` 于2026-10-24复核缓存值比较、资产隔离、实际程序集引用、红绿记录、十消费者、符号和文档/知识库范围，未发现 Critical、Important 或 Minor 可操作问题。审查者未修改文件或索引，也未替主代理重跑构建测试；执行结果由本报告及数据单独保存。
