# RM-06：合并重复的单元素括号诊断测试

日期：2026-10-23。对应 K3-M11 与后续 RM-06。基准为 `7f02b1440bd89b042c17a4a71c7b1b6f05ed2a9b`。维护者批准删除较弱的重复独立用例，保留 Theory 输入和全部断言，移动解释性注释。

## 变更与覆盖

[TypePatternParserTests](../../tests/AspectN.Tests/Parsing/TypePatternParserTests.cs) 的 `ParseTypePattern_SingleParenthesizedType_ReportsDiagnostic` 仅对 `(int)` 断言诊断非空；`ParseTypePattern_InvalidStandardForm_ReportsDiagnostic` 的同一输入已包含该断言，并检查根节点存在及所有诊断跨度位于源码范围内。

本轮删除前者，将其解释性注释放到后者的 `[InlineData("(int)")]` 旁。Theory 的四组输入及方法体保持，其他测试保持，生产解析器、语法契约和诊断规则均无改动。不新增测试或具体诊断码/数量/精确跨度承诺；这些内容不是现有用例的验证目标。

## 知识库与历史记录

关联 `SYN-GRAMMAR-001/S1` 的核心类型模式解析输入。删除的是对已有输入的重复弱断言，`(int)` 仍由同入口、同默认配置和包含原断言的 Theory 执行；知识库现有条件定位测试类，不需替换方法名。本轮没有改变能力范围、验证状态或 N/A，也不涉及绑定依赖和用户使用方式，故无需修改能力元数据、用户指南或生成矩阵。

两份历史证据 EV-20261013-004、EV-20261014-001 记录过旧独立方法，保留原名、原计数和快照哈希，不回填为当前测试清单。今后重跑该输入使用 `ParseTypePattern_InvalidStandardForm_ReportsDiagnostic(source: "(int)")`。本轮不将聚焦重跑等同于 S1 全部词法/解析/绑定场景重验。

## 当前验证

修改前该类 25 项通过，修改后 24 项通过，均为 0 失败、0 跳过；减少的一项恰为被删除的独立 Fact。已直接核对 TRX 中保留的 `(int)` Theory 实例通过，其他测试实例名称集合保持。执行命令如下，修改前使用同命令并将结果路径改为 `artifacts/rm06/before`、文件名改为 `rm06-before.trx`：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~TypePatternParserTests --logger 'trx;LogFileName=rm06-after.trx' --results-directory artifacts/rm06/after -v:minimal
```

`artifacts/rm06/verification.json` 记录源码比对和测试实例集合检查：当前文件恰等于修改前文件删除该 Fact、移动该注释后的结果；保留 Theory 的输入和断言未变。两份历史证据哈希与 `artifacts/rm06/historical-hashes.json` 一致。

该文件的 `dotnet format AspectN.sln --verify-no-changes --no-restore --include tests/AspectN.Tests/Parsing/TypePatternParserTests.cs --verbosity minimal` 退出码为 0；工具有工作区加载告警，不将结果表述为加载零告警。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 与 `git diff --check` 通过。

独立只读复核通过，无需修改项。复核者按基准构造预期差异，确认仅删除重复 Fact 并移动原注释，Theory 四组输入及全部断言保持；独立解析两个 TRX，确认唯一减少的实例是被删 Fact，保留的 `(int)` 实例通过，并核对两份历史证据哈希及知识库场景引用。

既有 README 修改、原始 review、综合报告及 `nul` 保留；既有未跟踪的后续核对清单仅更新进度，继续未跟踪。不推送、不发布、不修改相邻仓库。
