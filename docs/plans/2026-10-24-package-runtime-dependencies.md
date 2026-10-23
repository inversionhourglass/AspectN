# 发布包运行时依赖修复计划

**目标：** 消除核心缓存键比较意外引入的 System.Memory 引用，隔离 SourceLink 构建依赖，完整验证五包的 net472/net10.0 消费者。

**批准与基准：** 维护者已批准上一轮三点建议；基准 `33eb272a919d61780e1c284be8887d5e8fbb4062`。在当前检出目录实施，保留 README 和既有未跟踪资料，不修改相邻仓库，不推送或发布。

**结构：** 核心只以长度和 ResourceLimit 值逐项比较缓存键数组；五个生产项目的 SourceLink 保留 PrivateAssets=all，并排除 compile/runtime 资产。真实包消费者保留严格告警检查，增加同引擎重复编译的缓存命中检查，不注入 System.Memory 或关闭冲突诊断。

## 1. 建立失败回归和基准

- 修改 `tests/AspectN.NetFramework.Tests/CompatibilityAssetTests.cs`，通过实际加载的 netstandard2.0 核心程序集元数据，断言没有 System.Memory 引用。修改前在兼容宿主观察断言失败，排除单纯加载测试掩盖未声明依赖。
- 保存旧 DLL 引用、SourceLink→System.IO.Hashing→System.Memory 的还原图，以及 `CompiledPatternCacheKey.Equals` 调用 MemoryExtensions.SequenceEqual 的实际 IL。
- 检查已有缓存测试是否覆盖同值限额复用和不同限额隔离，复用这些语义回归。

## 2. 修改实现与构建配置

- 修改 `src/AspectN/Caching/CompiledPatternCache.cs`：保留键其余字段，先比较数组长度，再逐项调用值相等，不引入枚举器或 Span 依赖。
- 修改五个 `src/*/*.csproj` 的 SourceLink PackageReference，使用 `ExcludeAssets="compile;runtime"`，保留构建目标与版本。
- 还原和构建后检查实际 ReferencePath、DLL 引用及 SourceLink/PDB。配置若影响构建任务执行，先定位再最小修正，不改用关闭 SourceLink 的绕过方案。

## 3. 加强真实包消费者

- 修改 `eng/verify-packages.ps1` 的核心消费者源码：一个引擎对成员及类型表达式分别编译两次，检查 ReferenceEquals，实际触发缓存键比较。
- 保留包依赖集合、版本、两个目标框架、严格告警、签名和发布工作流。
- 以新唯一验证版本生成五对真实包，完整运行原验证器到十个消费者执行完成；复跑已有默认版本、依赖诊断脚本回归。失败不记为完整恢复。

## 4. 验证与同步

- 运行完整 Release 构建、测试矩阵及有关格式检查，记录真实告警和退出码。
- 更新 CROSS-COMPAT-001/S3、S4 与 CROSS-LIFETIME-001/S1 的当前有限证据及 INV-0014 工程记录；不扩展任意宿主、依赖版本或性能承诺。用户兼容章节解释包的构建依赖边界。
- 新增处置报告、机器数据和正式证据；历史失败报告保留，未跟踪核对清单只更新当前独立问题状态。
- 运行知识库生成及 -Check、严格文档检查，经独立只读审查后仅提交本项。

## 执行结果

四组已完成。新增引用断言先失败，旧真实包在net472缓存命中抛FileNotFound；修改后完整构建零告警零错误、2,438项测试和五包十消费者全部通过，两个PowerShell宿主工程回归合计74项通过。五个netstandard DLL无意外Memory引用，十个包资产PDB匹配且SourceLink保留；现代框架合法Memory引用不被误判。

知识库生成/-Check、严格文档与diff检查通过，文档零错误零告警；独立只读审查无可操作问题。当前结论及证据边界见[处置报告](../reviews/2026-10-24-package-runtime-dependencies.md)。历史失败保留，未跟踪清单同步独立问题关闭状态，不推送、签名或发布。
