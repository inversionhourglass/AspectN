# RM-04：测试名称对应实际断言

日期：2026-10-23。对应 K3-M8、K3-M10 与后续 RM-04。基准为 `5f9403e7e3fc5455bcd91355ab63660ef438eb24`。维护者批准只调整测试名称、核对引用并验证，不修改断言、不新增重复测试。

## 名称与范围

四个测试文件共改名六个方法：

| 文件 | 原名称 | 当前名称 |
| --- | --- | --- |
| BooleanParserTests.cs | ParseMemberExpression_WithoutOptions_UsesStandardLanguage | ParseMemberExpression_ValidMethodSelector_HasNoDiagnostics |
| DiagnosticTests.cs | Diagnostic_Stores_Stable_Code_And_Span | Constructor_ValidArguments_PreservesValues |
| SourceTextTests.cs | TextSpan_End_Is_Start_Plus_Length | End_ValidSpan_ReturnsStartPlusLength |
| SourceTextTests.cs | TextSpan_EqualValues_Are_Equal | Equals_IdenticalComponents_ReturnsTrue |
| SourceTextTests.cs | SourceText_From_Preserves_Content | From_NonEmptyText_PreservesContent |
| NetFrameworkSmokeTests.cs | AdapterAssemblies_PublicEntryPoints_AreConsumableOnNet472 | AdapterAssemblies_TypeSystemEntryPoints_ArePublic |

未传可选配置仍是合法调用；第一个用例实际只断言合法方法选择器没有诊断，名称据此收窄。适配器可见性用例仅断言三个 TypeSystem 类型的 IsPublic，不能独立证明适配器消费能力。该项目同时运行 net472/net10.0，新名称不将公共类型可见性绑定到某一个宿主。

同类中实际执行编译、映射、匹配以及变量模式/预算的测试继续保留。SourceTextTests 中原本已是三段式的负数构造回归保持原名；不扩大为全仓命名整理。

## 引用与证据适用性

初次检索 tests 与 docs 中六个完整旧名称，除这四个待改测试文件外，其他命中仅属于 2026-07-31 的历史计划及 EV-20261013-010/011、EV-20261014-007/008 的历史证据。它们记录当时的名称，按仓库规则保留原文，不将新名称回填成历史测试输出；本表供当前重跑和旧记录追溯。五份历史资料哈希保存在 `artifacts/rm04/historical-hashes.json`，本轮保持一致。

关联 `SYN-GRAMMAR-001/S1` 的解析测试名称、`CROSS-COMPAT-001/S3、S4` 的兼容测试定位及诊断/文本的基础回归。本轮没有生产 API、输入、断言或环境契约变化；将当前名称反向替换回旧名称后，四个测试文件与修改前快照完全一致，故行为证据的断言语义保持。历史测试的名称定位需按本表转换，不声称旧文件哈希与当前源码相同。

知识库当前条件引用这些测试类或专用 fixture，没有直接引用这六个旧方法名，因而无需替换场景字段。既有验证状态、N/A 和用户指南运行方式不变；本轮聚焦重跑只证明下述输入，不替代完整兼容矩阵或扩大可见性检查的覆盖。

## 当前验证

核心 BooleanParserTests、DiagnosticTests、SourceTextTests 共 10 项通过；通过新的测试方法名发现并执行。源码修改前快照位于 `artifacts/rm04/before`，核心 TRX 位于 `artifacts/rm04/core/rm04-core.trx`。

兼容项目的 NetFrameworkSmokeTests 在 net472、net10.0 宿主各 3 项通过，包含入口类型可见性、实际编译匹配、变量模式和预算的原有断言；本轮合计 16 项通过，0 失败、0 跳过。两份 TRX 位于 `artifacts/rm04/net472/rm04-net472.trx`、`artifacts/rm04/net10/rm04-net10.trx`。net472 构建仍有 MSB3277 依赖引用冲突告警，本轮没有改动依赖，不宣称构建零告警。

`artifacts/rm04/verification.json` 记录六个名称映射、名称之外源码一致性、历史资料哈希检查及三个测试计数，并核对每个新名称出现在相应 TRX 的 TestMethod 中。不增加行为断言，不把这些聚焦测试当作完整矩阵重验。

四个修改文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include ... --verbosity minimal` 检查退出码为 0；工具提示工作区加载告警，故不将结果表述为工作区加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警，`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 和 `git diff --check` 通过。

独立只读复核通过，无阻断问题；复核者确认六处名称与实际断言一致、名称之外源码保持、五份历史资料哈希不变，并核对三个 TRX 的计数及新名称发现结果。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新 RM-04 进度，继续未跟踪。没有推送、发布或修改相邻仓库。
