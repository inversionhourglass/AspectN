# C05 祖先闭包限制检查修复

日期：2026-10-21。基线：`90c4e94a017ed09570a1a78ddd8e10caaa898075`；目标为其上本次 C05 变更。保留原综合报告和既有未跟踪文件；C06、C07 留待后续讨论。

## 范围与根因

语言第 21 节已要求关系查询保留超限祖先诊断并继续其他分支。原入口只对初始候选运行 `MetadataLimitGuard`；包含变量或通配目标的 `+` 模式调用 `GetInheritanceClosure` 后，直接对祖先求值，遗漏同一检查。

第三方后端可以返回结构完整但超过处理上限的中立签名。构造实参包裹 63 层数组使最外层有名类型总深度为 65；65 层包含类型链、65 个声明/构造实参，以及外层元数 65 加内层 1 均为有界、无循环的复现。它们没有使用残缺槽位、反射改字段或无限输入。

本次限定为核心消费第三方闭包。内置适配器已做原生预检，未证明会返回上述图；不保留旧 K3 中缺乏有效输入证据的永久挂死结论。

## 修复与控制

在 `EvaluateAssignablePatternWalk` 对每个祖先求值前复用 `MetadataLimitGuard`。失败时只给分支添加 AN3005/AN3006 并产生 Unknown，不求值内部结构、不绑定变量；随后使用原有诊断合并、三态 OR 和 `RetainAlternative`。

超限祖先不会中断其他祖先。另一个确定匹配分支仍能产生 Matched，并保留限制诊断；没有确定匹配时保留 Unknown。未知备选继续参与后续合取，防止唯一确定绑定被排除后错误变成 NotMatched。

未修改公开 API、限制数值、签名结构、变量作用域规则、具体目标的 `IsAssignableTo` 路径或内置适配器。没有将非法结构改成未知事实。

## 回归与验证

新增 [InheritanceClosureLimitTests](../../tests/AspectN.Tests/Evaluation/InheritanceClosureLimitTests.cs) 共 27 项。基线红灯为 20 失败、7 通过；最小修复后聚焦测试 27 项全部通过。最终测试名称将包含链案例限定为 `L6*<?>+`，避免 `L*` 同时匹配初始 `Leaf`。

- 五类上限：构造实参深度、包含链深度、声明元数、实际构造实参数量、包含链累计元数；直接输入与闭包输入对照。
- 每类均有 64 边界控制，以及正常匹配祖先在超限祖先前、后的两种顺序控制。64 层包含链通过入口限制检查，但仍按名称路径给出确定不匹配；不能把它写成限制失败。
- 后续变量约束分别保留未知备选、保留确定匹配；否定未知仍未知；成员返回槽同样经过祖先守卫。
- 原闭包 Failure 诊断保留；重复限制诊断去重；取得闭包后的取消传播；具体目标继续使用后端关系查询。

```powershell
dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 --logger trx --results-directory artifacts/c05-fix/final -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
```

完整 Release 编译零警告、零错误；完整矩阵 2013 项全部通过、零跳过，包含 net472 及 net10.0 宿主。格式、知识库生成与一致性、文档和 `git diff --check` 均通过；文档校验为 17 条能力、100 条场景、零错误、零警告。日志在本地忽略目录 `artifacts/c05-fix/`，可由已提交的回归独立重跑。

## 独立复核

按 `requesting-code-review` 技能由只读审查者 `/root/c05_closure_review` 于 2026-10-21 复核实现、测试、用户指南、能力条目和证据。审查者独立运行聚焦测试 27/27 通过，核验最终红灯 20 失败/7 通过；EV 的 16 个文件哈希和 8 份最终 TRX 全部匹配，计数 2013/2013。无 Critical、Important 或 Minor 问题，结论为可以提交 C05。

## 知识库与证据适用性

受影响能力为 `CROSS-RESOURCES-001/S1`、新增 `TYPE-NOMINAL-001/S6`；盘点为 `INV-0013、INV-0015`。同步用户限制说明、能力条目、盘点和派生目录/矩阵，没有新增或更改 N/A。

[FND-20261021-002](findings/FND-20261021-002.md) 对应原综合报告 C05；[EV-20261021-002](evidence/EV-20261021-002.json) 限定本次核心输入，记录代码哈希、版本及验证结果。既有规范第 21 节已经明确预期，因此无须改变语言或另立维护者裁决。

同一匹配核心的其余语法、三态、绑定和适配器用例随完整矩阵回归；这些执行不自动扩大旧证据的原有输入范围。本次没有证明所有第三方实现、任意图规模、循环/无限输入、全部运行时版本或新的性能承诺。
