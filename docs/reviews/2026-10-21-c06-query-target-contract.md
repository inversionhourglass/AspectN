# C06 非限定查询目标契约补全

日期：2026-10-21。基线：`5c0fd16cf5b5ed9365833135298a274d7ea649dc`；目标为其上本次 C06 变更。保留原综合报告及既有未跟踪文件；C07 留待后续讨论。

## 维护者确认与范围

维护者先确认此次方案只补全文档和测试、保持运行逻辑，再明确要求“暂时不新增公开的查询目标比较器，开始修复”。本次据此将现有查询侧语义写入 `ITypeSystem` 的公开 XML 契约，并提供中文[后端实现指南](../usage/backend-implementation.md)。该确认覆盖空程序集名、空命名空间及 Unknown kind 的查询含义，及严格 `AreEqual` 不变；没有批准额外查询 API 或新的语言行为。

公共查询契约来源登记为 `SRC-TYPE-SYSTEM-QUERY`。指南是实现说明，不能独立扩大公开契约或后台版本承诺。

## 问题与补全

核心将可具体化的关系/特性模式转换为 `TypeSignature`，其身份可能部分限定。内置后端已使用内部查询比较规则，公共接口却未说明这些值不是完整原生类型身份。第三方若直接调用严格结构比较器，会得到与现有语义不符的结果。

XML 注释明确：查询侧空程序集名使整个程序集身份不限；否则比较完整 `AssemblyIdentity`。空命名空间及 Unknown kind 分别不限；已指定名称、元数、包含链和实参结构仍有约束。规则递归用于包含身份及命名实参，保留数组形状信息。`AreEqual` 仍是对称的严格相等，不应用查询放宽。

只修改 `src/AspectN/TypeSystem/ITypeSystem.cs` 的 XML 注释，没有新增、删除或修改生产可执行语句、方法签名及公开类型。没有公开辅助比较器，也没有把内置内部源码转成公共 API。

## 回归与验证

在 XML 补全前运行新增 27 项测试，基线全部通过：核心 5 项、同源一致性 22 项。本次为契约文档缺口，没有产品红灯；不将它记成修正内置计算结果。

- 核心记录型后端仅检查实际传入查询，包括省略/指定命名空间、泛型、嵌套及二维数组。它返回确定不匹配，不承担匹配算法验证。
- Cecil、Reflection、Roslyn 源码/PE 四视图在真实同源输入上核验独立限定字段、指定完整程序集和错误版本、名称大小写、元数、实参及包含链。
- 数组实参分别检查 rank 和 Vector/General；严格 `AreEqual` 的两个方向保持一致，精确模式和相应通配模式在同一正例均匹配。
- 新证据不声称新增 Fody 输入矩阵，不证明任意第三方实现正确，也不是可直接引用的公共测试套件。

```powershell
dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 --logger trx --results-directory artifacts/c06-fix/final -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet
pwsh -NoProfile -File eng/generate-knowledge.ps1
pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check
pwsh -NoProfile -File eng/verify-documentation.ps1
git diff --check
```

完整 Release 编译零警告、零错误；完整矩阵 2040 项全部通过、零跳过，包含 net472 及 net10.0 宿主。格式、知识库生成与派生一致性、文档检查及 `git diff --check` 均通过；文档校验为 17 条能力、109 条场景、10 个使用章节，零错误、零警告。本地日志位于忽略目录 `artifacts/c06-fix/`，回归已作为源码保留。

去除 XML 注释后将 `ITypeSystem.cs` 与 HEAD 文本逐行对比，公开声明完全一致。生产目录 diff 只有该接口注释。

## 独立复核

按 `requesting-code-review` 技能由只读审查者 `/root/c06_contract_review` 于 2026-10-21 复核。其确认契约与现有实现、指南及来源批准范围一致，未扩大第三方或 Fody 承诺；EV 的 17 项哈希、基线 27/27 及最终 8 份 TRX 的 2040/2040 全部相符。无 Critical、Important 或 Minor 发现，结论为可以提交。

## 知识库与适用性

受影响能力为 `TYPE-NOMINAL-001/S1、S7–S10`、`ATTR-TARGETS-001/S1、S17–S20`、`CROSS-COMPAT-001/S5`；盘点为 `INV-0015、INV-0011、INV-0014`。已同步用户指南、适配器入口和派生目录，没有修改 N/A。

[FND-20261021-003](findings/FND-20261021-003.md)以 documentation_resolved 关闭公开契约缺口；[EV-20261021-003](evidence/EV-20261021-003.json)记录本次有限输入、版本、文件哈希及矩阵。旧报告保持历史结果，完整矩阵重跑不扩大其原有范围。既有语言与运行逻辑未变，不修改语法规范或重写历史裁决。
