# RM-02：Unknown 结果的诊断参数与异常契约

日期：2026-10-22。对应综合审查 K3-M2 与后续 RM-02。基准为 `0f493eb556f6ccc6901d1a6cb8b61adb0fc2e2ab`。开始时已有的 README 修改、两份原始 review、综合报告和 `nul` 原样保留；既有未跟踪的后续核对清单仅更新 RM-02 进度，继续保留未跟踪状态，不混入本轮提交。

## 维护者裁决与范围

维护者批准补全 XML 和使用说明、补充必要回归，保持现有 API 与运行时行为。不调整 `params` 参数形状，不新增首条必填诊断的重载。

`MatchResult.Unknown()` 与 `TypeRelationResult.Unknown()` 可以通过编译，但运行时会因缺少诊断抛 `ArgumentException`。null/空诊断集合和构造器省略 Unknown 诊断遵循同一约束；构造器创建 Matched 或 NotMatched 时则允许省略诊断。现有实现没有生成无诊断 Unknown 的漏洞，本轮处理的是调用方看不到完整前置条件及异常说明的问题。

两个生产源文件仅修改英文 XML：构造器说明 Unknown 至少需要一条诊断及 `ArgumentException`；工厂说明同一约束、零参调用的实际结果及异常。使用指南同步两种结果类型，并提供带诊断的自定义后端示例。

## 能力与证据适用性

归属 `CROSS-LOGIC-001`，新增 S3 记录结果构造约束；同步[结果与诊断](../usage/results-and-diagnostics.md)。S1/S2 的三态运算、Boolean 转换、原证据范围及既有 N/A 均未改变。新增 S3 不读取元数据、不声明取消参数，其纯值范围仍符合既有 N/A 条件；没有新增或改动 N/A 记录。

生产源码的方法声明和方法体不变，已有产品行为证据不因本轮 XML 修订失效。新增证据 [EV-20261022-301](evidence/EV-20261022-301.json)只验证所列中立结果构造输入；不覆盖 null 诊断元素、任意 IEnumerable 实现、所有后端失败或完整兼容矩阵。历史报告不改写。

## 当前验证与限制

- 修改 XML 前，新增 18 项结果构造回归全部通过，证明它们记录既有行为；本轮没有生产逻辑修复，不人为制造行为失败。
- 修改 XML 后，两个新测试类与既有 MatchLogicTests、MetadataContractTests 共 55 项通过，0 失败、0 跳过。拒绝路径核验精确异常类型与 `diagnostics` 参数名，单条有效诊断和已知状态为空分别作为正向对照。
- 核心库 Release 构建覆盖 netstandard2.0、net10.0，0 错误、0 告警。生成 XML 与非 XML 源码一致性另有检查；执行结果见 `artifacts/rm02/verification.json`。
- 两个目标框架的生成 XML 均解析成功；四个构造入口均包含诊断数量约束和 ArgumentException，工厂另含零参说明。去除 XML 注释后，两个生产文件与开始前快照完全一致，确认没有 API 声明或方法体变更。
- `dotnet format tests/AspectN.Tests/AspectN.Tests.csproj --verify-no-changes --no-restore --include` 指定两个生产文件和两个新增测试文件，退出 0。工具另提示工作区加载告警；该结果不作为全解决方案无告警的证明。
- 知识库由 `eng/generate-knowledge.ps1` 生成；同命令加 `-Check` 通过。`eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警。既有场景及 N/A 状态保持，仅新增有本轮实测证据的 S3。
- 独立只读审查者 `/root/rm02_contract_review` 核对 XML 与实现、两个 TRX、11 个证据文件哈希及最终报告，未发现阻断问题。

测试结果保存于 `artifacts/rm02/baseline/rm02-baseline.trx`、`artifacts/rm02/contracts/rm02-contracts.trx`；源文件修改前快照保存于 `artifacts/rm02/before`。没有运行远端 CI、非 Windows 宿主、全解决方案测试或发布。
