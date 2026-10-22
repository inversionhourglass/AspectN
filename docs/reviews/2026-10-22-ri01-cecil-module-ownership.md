# RI-01：Cecil 模块默认借用与所有权说明

日期：2026-10-22。来源为综合审查 GLM-I4 / 后续核对 RI-01；正式记录为 [FND-20261022-101](findings/FND-20261022-101.md)。本轮只处理模块所有权及其公开说明，不扩大到 RM-01 的全库 XML 整理。

## 维护者裁决与适用范围

维护者确认将 `CecilMappingOptions.LeaveOpen` 默认值设为 `true`。传入的 `ModuleDefinition` 默认由调用方或 Fody 宿主持有并最终释放；只有显式传入 `leaveOpen: false` 才把模块释放责任交给 `CecilTypeSystem`。维护者同时说明产品仍在开发且没有现有使用者，本轮不增加兼容分支或迁移说明。

无论模块是否保留，`CecilTypeSystem.Dispose()` 都清空适配器缓存、使受守卫查询及映射入口失效，且允许重复调用。`LeaveOpen` 保留的是原生模块，不延长适配器的可用期。共享模块的多个适配器默认各自管理其缓存；模块本身的任意并发读写不在本次承诺内。

该裁决落实到当前[架构规范第 9.1 节](../superpowers/specs/2026-07-31-aspectn-redesign-design.md)、公开 XML、[适配器指南](../usage/adapters.md)和[生命周期指南](../usage/lifetimes-and-compatibility.md)。历史综合报告保持原样。

## 实现与回归范围

- [CecilMappingOptions](../../src/AspectN.Cecil/CecilMappingOptions.cs)及 API 基线采用默认 `true`；原有显式 `false` 的释放逻辑保持有效。
- [CecilTypeSystem](../../src/AspectN.Cecil/CecilTypeSystem.cs)的构造器和 `Dispose` XML 说明默认值、最终释放责任、重复释放和释放后操作限制，并提供释放适配器后写回模块的例子。
- [CecilOwnershipTests](../../tests/AspectN.Cecil.Tests/CecilOwnershipTests.cs)用真实文件和 `ReadingMode.Deferred` 验证省略选项、默认选项、显式 `true`、显式 `false`，以及两个适配器共享同一模块。保留模块时实际读取延迟方法体并写回；接管模块时读取延迟方法体抛出 `ObjectDisposedException`。两种配置均验证适配器和原有 mapper 的释放守卫。
- Cecil.Fody 链接相同生产源码、API 基线和测试源码；在独立 Fody 测试宿主中执行，避免仅由源码共享推断行为一致。
- 测试工厂 `EmittedFixture.Open()` 自行打开文件且只返回适配器，因此显式使用 `leaveOpen: false` 保持资源释放责任；借用模块的用户示例则保留外层 `using module`。

受影响能力为 `CROSS-LIFETIME-001/S2`（Cecil）及 `/S3`（Cecil.Fody）。当前契约及证据已登记到[能力条目](../knowledge/capabilities/cross-cutting/CROSS-LIFETIME-001.md)和 [EV-20261022-101](evidence/EV-20261022-101.json)。未新增或修改 N/A。

## 验证结果

修改默认值前，新增 5 项所有权测试中 3 项失败、2 项控制通过。失败项分别是省略选项、默认选项和默认共享模块，真实失败为延迟读取方法体时访问已关闭文件。显式 `true` / `false` 两项控制通过。

修改后，普通 Cecil 与 Fody 各执行 **9 项生命周期测试，全部通过**，没有跳过。包含新增所有权用例、既有 `CecilContextTests`、`Dispose_CachedTypes` 缓存回收和 `Dispose_LeaveOpen` 模块写回回归。两个生产项目分别在 `netstandard2.0` 和 `net10.0` 完成 Release 构建，均零警告、零错误。

在修正独占文件工厂后，两后端还各执行一项实际使用该工厂的既有嵌套类型映射测试并通过，最终独立用例为 **Cecil 10 项、Fody 10 项**。只读独立审查未发现本次范围内的阻塞问题。修改的两个生产文件、新测试和工厂文件通过聚焦空白格式检查；这不代表全仓格式或完整分析器检查通过。

测试宿主位于忽略目录 `artifacts/ri01/cecil` 和 `artifacts/ri01/fody`，引用当前实际生产项目并直接编译仓库原测试源码；未复制或替换被测实现。宿主项目文本、命令、源码哈希和 TRX 摘要记录在证据 JSON 中。VSTest 在允许本地进程通信的执行环境运行。

## 当前验证限制

常规 Cecil 测试项目的构建被同工作区正在进行的资源配置改动阻塞：`CecilReturnOnlyOwnerIdentityTests.cs:46` 的 `Select(system.Mapper.MapMethod)` 与新增可选取消参数产生 CS0411。本轮没有修改该无关测试，因此上述结果仅覆盖独立宿主所列用例，不能称为完整项目或解决方案矩阵通过。

`pwsh -NoProfile -File eng/generate-knowledge.ps1`、加 `-Check` 的派生检查，以及 `pwsh -NoProfile -File eng/verify-documentation.ps1` 均已执行并返回失败。校验器报告 248 项错误、0 告警，均为资源配置改动迁移 Reflection/Roslyn 文件后既有证据引用的文件不存在；未出现本次新证据或能力条目的格式错误。日志保存在 `artifacts/ri01/generate-knowledge.log`、`check-knowledge.log` 和 `verify-documentation.json`。

知识库源条目已同步，但生成器因全仓预检失败未更新派生目录和矩阵，仍需随资源配置任务完成重验。本轮不改写历史证据，也不绕过生成器预检或手工更新派生矩阵。现有 Shared.Cecil 迁移指南仅纠正一处已经失效的默认值事实，没有新增本次改动的兼容或迁移流程。
