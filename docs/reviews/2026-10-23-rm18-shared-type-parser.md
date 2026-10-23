# RM-18：共享类型语法解析核心

日期：2026-10-23。来源为 GLM-M1。基准为 `835709d2f5986c3d24ecbc020c857eb7ed3361b9`。维护者批准[计划](../plans/2026-10-23-rm18-shared-type-parser.md)中的内部去重与两入口对照回归，保留公共 API、语言规则、诊断、预算和取消行为。本项属于可维护性改进，没有将未来可能出现的规则分叉写成已经复现的产品错误。

## 共同算法与入口差异

新增内部抽象 [TypeSyntaxParser](../../src/AspectN/Parsing/TypeSyntaxParser.cs)，由 [TypePatternParser](../../src/AspectN/Parsing/TypePatternParser.cs) 和 [SelectorParser](../../src/AspectN/Parsing/SelectorParser.cs) 继承。优先级、原子类型、名称与泛型、元组、tasklike、数组/可空/关系后缀统一实现，两个原副本已删除。成员名的名称段与泛型仍委托同一共享算法，不增加公开扩展点或第二种语言。

| 上下文 | 保留方式 |
| --- | --- |
| 词法流、前瞻、诊断和资源额度 | 各入口继续持有自己的 Lexer/Parser、诊断袋和 ParseContext；共享核心不重新解析子串或创建独立预算。Peek 保留原有起点和重扫计费。 |
| 独立入口的 ref 注解恢复 | 根 TypeUse 恢复仍在独立入口，嵌套 ref 检查通过入口钩子执行；原消息及跨度保持。成员的 ParseTypeUse 继续自行处理 ref/out/in/ref readonly/!ref。 |
| 声明类型的 tasklike 名称 | taskLikeIsName 只沿布尔/括号/元组递归传播；进入泛型实参和 tasklike 结果时恢复普通类型解释。 |
| 声明类型与成员分隔点 | `_memberSeparatorStart` 继续由原 MemberPaths 管理，同时约束星号是否转为名称、名称路径何时停止，以及相关恢复消息。 |
| 节点预留顺序 | 独立入口的变量先消费标识符，再创建变量节点；名称先解析泛型再创建名称段。选择器继续按原时点预留节点，通过小型创建钩子保留差异。 |
| 外层恢复与工作栈 | 成员签名/参数/成员名/后续选择器和两种根的尾部恢复继续由原入口处理。所有递归类型算法保持 StackWork/WorkStack，没有引入 CLR 递归。 |

## 回归与受控变异

新增 [TypeSyntaxParityTests](../../tests/AspectN.Tests/Parsing/TypeSyntaxParityTests.cs)，原实现与最终共享实现分别通过 86 项：共同类型在返回/参数位置 50 项、声明类型边界 11 项、ref 位置 5 项、tasklike 声明名称 1 项、无效类型诊断/恢复 9 项、精确节点预算边界 9 项、复杂成员与后续选择器 1 项。形状比较包含节点实际类型、Kind、相对跨度、token 文本/值/缺失标记，以及泛型 Form/Arity。

这是保持行为的重构。新增测试先验证原行为，再受控地只将独立类型入口 OR 优先级从 1 改为 2：当时 76 项中 2 项捕获单入口漂移、74 项通过，随后恢复原源码，再补齐节点预算与复杂边界至 86 项。变异失败不代表原产品存在该错误。`artifacts/rm18/mutation.json`、`parity-mutation.log` 与 TRX 保留该实验，原实现最终基线为 `before/parity-before-final.trx`。

另有本地临时探针 `artifacts/rm18/probe`，限定独立类型、成员返回类型、成员参数类型三种位置。40 个有效/无效类型各跑默认设置，另对 8 个类型分别遍历节点额度 1—40、token 额度 1—40、解析深度 1—5，共 2,160 条记录；其中 default 120、nodes 960、tokens 960、depth 120。包含嵌套 ref、非法变量、泛型空槽、邻接错误、布尔、元组、tasklike 和未闭合输入。

每条记录包含类型、入口、配置、语法树摘要及完整诊断（码、严重性、消息、跨度）。前后 JSONL 逐字节相同，SHA256 均为 `59d3eb7f2ccaf9dc2de61c57abb2da5b63533cda808849ad73783e4d776e5228`。声明类型和成员名上下文由持久测试及完整矩阵支持，不将探针扩大解释为所有入口、任意输入或所有中途取消时序的等价证明。原始记录与完整日志保存在忽略目录 `artifacts/rm18`。

## 当前验证

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~TypeSyntaxParityTests' --logger 'trx;LogFileName=parity-after.trx' --results-directory artifacts/rm18/after -v:minimal
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false --logger trx --results-directory artifacts/rm18/tests -v:minimal
dotnet format AspectN.sln --verify-no-changes --no-restore --include src/AspectN/Parsing/TypeSyntaxParser.cs src/AspectN/Parsing/TypePatternParser.cs src/AspectN/Parsing/SelectorParser.cs tests/AspectN.Tests/Parsing/TypeSyntaxParityTests.cs --verbosity minimal
```

完整 Release 构建通过，生产 netstandard2.0/net10.0 均编译；net472 测试工程保留一条既有 MSB3277 依赖冲突告警。完整矩阵 2,429 项通过，0 失败、0 跳过：核心 1,391，Cecil 232，Reflection 110，Roslyn 30，一致性 415，.NET Framework 工程 net472 为 8、net10.0 为 9，Fody 234。

四个已有 FrontendProbe 形状 `parse-generic`、`compile-generic`、`parse-member-type`、`compile-member-type` 各用独立进程、256 KiB 线程、1000 层泛型输入通过，输出各自的 `mapped:Core:<shape>:1000` 标记。父进程对每个子进程设置 30 秒截止，日志及标记哈希在 `artifacts/rm18/frontend-results.json`；这些属于本轮追加手工执行的现有探针，不计入 2,429 项自动测试总数。未据此宣称其他形状或所有恢复组合都已验证。

四个改动源码的格式核验退出码为 0，工具保留工作区加载告警，不将结果称为加载零告警。最终格式整理后再次构建并运行有限探针，前后 2,160 条记录仍完全相同。`pwsh -NoProfile -File eng/verify-documentation.ps1 -AsJson` 返回 0 错误、0 告警；生成知识库后，`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check` 通过，`git diff --check` 通过。汇总核验记录在 `artifacts/rm18/verification.json`。

## 知识库与验证边界

直接影响 `SYN-GRAMMAR-001/S1` 的两个类型解析入口，以及 `CROSS-RESOURCES-001/S9` 的前端资源配置。当前回归记录为 EV-20261023-304。34 份相关历史证据记录的哈希保持原样，新增证据只覆盖实际执行的输入和环境；没有修改 N/A、排除语法、默认限额或错误恢复契约。

公共 API、选项、使用方式及语言规则保持，用户章节无需行为修订。新核心不涉及后端映射、候选签名或匹配算法，不扩大泛型/成员能力的承诺。代码去重不作为性能改善证据，本轮未测解析吞吐或重新测量 RM-17 的匹配基准。

独立源码复核确认实际语法算法合并、两入口差异与收费顺序保持。最终只读复核逐项确认 32 个源码快照、八个 TRX 的哈希与计数、两个场景列出的实际测试方法、86 项前后回归、受控变异、探针程序和输出、四个 FrontendProbe 标记及 34 份历史证据哈希均匹配；未发现阻断问题。既有 README、原始 review、综合报告及 nul 保留，核对清单只更新进度且继续未跟踪。只提交 RM-18，不推送、不发布。
