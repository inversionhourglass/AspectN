# C05 祖先闭包限制检查修复计划

**目标：** 为核心消费第三方 `ITypeSystem` 返回的祖先补齐深度和元数检查，单独修复并提交 C05。

**架构：** 在 `TypePatternEvaluator.EvaluateAssignablePatternWalk` 对每个祖先求值前复用 `MetadataLimitGuard`。超限分支保留原诊断及未求值的 Unknown 绑定备选，继续其他祖先；正常分支仍可使最终结果为 Matched。保持具体关系目标的后端快速路径、公开 API、限制数值及内置适配器行为。

**环境：** 当前检出目录；C#、xUnit、公共中立签名与受控第三方闭包。既有未跟踪文件保留，不推送。

## 1. 建立失败证据

- 新增 `tests/AspectN.Tests/Evaluation/InheritanceClosureLimitTests.cs`。
- 覆盖构造实参深度、包含类型深度、声明元数、构造实参数量和包含链累计元数；同一祖先直接输入及闭包输入都应 Unknown/AN3005 或 AN3006。
- 增加 64 边界、正常匹配祖先的两种顺序、未知变量备选、否定、成员返回槽、闭包原诊断、取消及具体目标路径控制。
- 执行聚焦测试并保留红灯；不得以编译失败代替语义失败。

## 2. 最小实现

- 修改 `src/AspectN/Evaluation/TypePatternEvaluator.cs` 的祖先循环，先检查，再在分支副本记录失败或执行求值。
- 复用现有诊断合并、三态 OR 和 `RetainAlternative`，不为超限祖先绑定模式变量。
- 重跑聚焦测试；然后执行完整 Release 编译和测试矩阵。

## 3. 同步知识库

- 受影响能力为 `CROSS-RESOURCES-001/S1` 和 `TYPE-NOMINAL-001` 新增的核心第三方闭包场景。
- 更新用户限制说明、盘点、正式 Finding、修复报告和当前证据；保留旧报告的历史结果，不更改 N/A。
- 执行知识库生成、一致性核验和文档校验。

## 4. 复核与提交

- 执行 `dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet`、`git diff --check`。
- 按 `requesting-code-review` 进行独立只读复核，重点检查 Unknown 备选是否丢失、已知匹配是否继续，以及诊断保留。
- 单独提交 C05；C06、C07 留待后续讨论。
