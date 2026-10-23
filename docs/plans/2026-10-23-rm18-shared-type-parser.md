# RM-18 共享类型语法解析实施计划

> 维护者已在当前对话批准：提取内部共享的类型语法解析核心，保留独立类型入口与成员槽位的上下文规则，并补充两入口对照测试。在现有检出目录实施，不创建 worktree，不改公共 API 或语言契约。

**目标：** 消除两条类型语法算法的重复维护，同时保持现有语法树、诊断、词法前瞻、资源收费顺序与取消行为。

**架构：** 新增内部抽象 `TypeSyntaxParser`，由 `TypePatternParser` 和 `SelectorParser` 继承；共享优先级、原子类型、名称/泛型、元组、tasklike 及后缀解析。入口继续拥有词法流、诊断袋和 ParseContext，通过受保护的小型钩子提供 token 操作、成员分隔点、独立入口 ref 检查与少数节点的创建顺序。共享核心不创建新预算或重扫新的字符串，不引入公开扩展点。

**技术栈：** 现有 C#、StackWork/WorkStack、xUnit；不增加依赖。

**执行记录：** 86 项新增前后回归通过，受控变异捕获两项漂移，2,160 条前后探针逐字节相同；完整矩阵 2,429 项及四个 1000 层小栈探针通过。共享核心提取、生产双目标构建、格式、文档、派生资料和独立复核均完成；结果与范围见[处置记录](../reviews/2026-10-23-rm18-shared-type-parser.md)。

## 任务 1：固定基准与差异

- 基准：`835709d2f5986c3d24ecbc020c857eb7ed3361b9`。保留既有 README、原始 review、综合报告、核对清单及 nul。
- 读取 `TypePatternParser.cs`、`SelectorParser.cs`、`SelectorParser.MemberPaths.cs`、`Parser.Selectors.cs`，保存待改文件及相关历史证据哈希至忽略的 `artifacts/rm18`。
- 明确保留：独立入口禁止 ref 注解、成员槽位 TypeUse、声明类型的 tasklike 名称解释、成员分隔点及其恢复文案、各入口尾部恢复、Lexer 前瞻与节点预留顺序。SYN-GRAMMAR-001/S1、CROSS-RESOURCES-001/S9 为直接受影响场景。

## 任务 2：先建立可观察的回归

新增 `tests/AspectN.Tests/Parsing/TypeSyntaxParityTests.cs`。

1. 对同一类型字符串，从独立入口和成员返回/参数/声明类型等位置取得类型子树，比较节点类型、Kind、名称/泛型语义字段、token 与相对跨度；覆盖泛型空槽、特殊元数、嵌套、布尔、元组、tasklike、数组、可空、关系及名称通配。
2. 分别验证 ref 使用位置与 tasklike 声明名称上下文，不要求合法上下文差异被强行统一。
3. 保存选定无效输入的诊断与恢复结构、精确预算边界的原有表现。完整参数列表与后续选择器用于验证共享类型解析不会吞并外层 token。
4. 先在原实现执行。由于这是行为保持的重构，不伪造一个原有产品缺陷；用受控、可恢复的语法变异确认新增对照测试能发现单入口漂移，记录失败后恢复原文件并重跑。

聚焦命令：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~TypeSyntaxParityTests' --logger 'trx;LogFileName=parity-before.trx' --results-directory artifacts/rm18/before -v:minimal
```

## 任务 3：提取共同算法

新增 `src/AspectN/Parsing/TypeSyntaxParser.cs`；修改 `TypePatternParser.cs` 和 `SelectorParser.cs`，必要时只调整 MemberPaths 对入口的委托。

1. 共享核心只依赖当前 token、Next/Match/Peek、Context、DepthGuard、诊断及入口钩子；保留现有 WorkStack 深度处理，不改成 CLR 递归。
2. 将选择器的类型表达式、后缀、名称、泛型、tasklike 与元组逻辑移入核心，独立入口复用该算法；删除已迁移副本。
3. 保留独立入口在变量解析时先消费标识符、在名称解析时先解析泛型再创建名称节点的顺序；选择器继续按原时点预留节点。保留两端各自的 Peek 实现，避免改变 token 预算。
4. 受保护钩子只承载已列出的上下文差异，不复制类型语法分支。内部注释用中文。
5. 重跑新增对照测试以及原解析、恢复、ref、成员路径、泛型和资源回归；如出现差异，先判断是入口契约还是提取错误，不顺便改变语言。

## 任务 4：证据、复核与提交

1. 完整 Release 构建与测试，覆盖核心、适配器、一致性及 net472；运行改动源码的格式检查。
2. 新增 `docs/reviews/2026-10-23-rm18-shared-type-parser.md` 与当前证据，明确测试的实际范围；同步 SYN-GRAMMAR-001/S1、CROSS-RESOURCES-001/S9，保留历史报告。公共调用和语言没有变化，使用章节无需行为修订，报告记录理由。
3. 运行 `eng/verify-documentation.ps1`、`eng/generate-knowledge.ps1` 和 `-Check`；不手改派生矩阵。
4. 按 requesting-code-review 完成独立只读复核，处理有效问题后只提交 RM-18 文件；核对清单只更新进度并继续未跟踪。不推送、不发布。
