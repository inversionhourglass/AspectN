# 两项能力证据补齐与分类审查实施计划

> **执行者：** 按 writing-plans 明确已授权范围，按 requesting-code-review 进行非作者核验；在当前检出中执行，不创建 worktree。

**目标：** 补齐 USE-REF-001/S8 与 ATTR-TARGETS-001/S5 的具体执行证据，再按九分类进行一轮有范围、有反证的正式审查。
**架构：** 复用现有核心编译入口、Cecil/Fody 链接测试及四视图程序集 fixture。先写断言并执行，保持已有 Standard 契约；新证据另存，不改历史报告。继承排除需区分原生后端输入，不把核心语法通过外推为后端查询通过。
**技术栈：** C#、xUnit、现有 net10.0 测试项目、PowerShell 7、既有知识库校验及生成器。

## 基线与边界

用户明确授权“先补前两项证据缺口，再按知识库分类开展正式 code review”。基线为858c590aedea76b278951820b222b47afe8df528，保留原始未跟踪nul。H1维持既有接受风险裁决，不重开、不添加容错；全仓同步闸门不自动启用。不推送、不发布，不修改相邻仓库。

本轮以证据补齐及审查为目标，不主动更改产品行为。发现契约违约先给可执行复现、规范出处、反证和Finding；不通过修改承诺掩盖问题。发现范围超出现有承诺时记录待裁决事项。

## 任务1：Fody引用参数方向标志

新增 `tests/AspectN.Cecil.Tests/Fixtures/InteropRefKindFixture.cs` 与 `tests/AspectN.Cecil.Tests/CecilInteropRefKindTests.cs`；Fody项目按现有规则编译相同测试源码，不改项目依赖。

1. 真实C#编译两个方法：`[In] ref int`、`[In, Out] ref int`。
2. 读取各自测试程序集PE，先断言原生In/Out位和by-ref形状。
3. 映射后断言RefKind.Ref；参数模式`int`/`ref int`匹配，`out int`/`in int`/`ref readonly int`/`!ref int`不匹配。每种方向标志独立执行六个输入。
4. 先运行聚焦测试，再分别运行普通Cecil和Fody全套；记录实际Mono.Cecil程序集身份，不能以普通Cecil报告代替Fody。

## 任务2：特性排除与直接记录

新增 `tests/AspectN.Tests/Parsing/AttributeExclusionTests.cs` 与 `tests/AspectN.Conformance.Tests/AttributeInheritanceTests.cs`。

1. 旧位置名callable/property/parameter/return触发AN4005且没有有效成员模式；对应短名可编译。参数覆盖括号/空格及索引/通配写法。
2. get/set位置被拒绝；getter/setter选择器与`attr(call ...)`的组合可编译。
3. 有效C# fixture包括带Inherited=true特性的基类、无直接标记的派生override、显式直接标记的派生override。运行时继承查询作正向对照，直接记录查询与AspectN分别按类型/方法断言。
4. 同一源码/PE分别验证Cecil、Reflection、Roslyn源码和Roslyn PE，逐后端记录范围；不涵盖Fody特性继承查询或所有继承拓扑。
5. 原S5只保留核心语法排除，继承查询拆为原生后端/输入场景，并保留规范排除理由；不改变既有语义。
6. 运行核心与一致性全套；报告使用新的证据编号，保留旧15份报告。已有报告的适用性按新增测试源与产品未变事实复核，不因新增测试一概作废或借此扩张旧范围。

## 任务3：按知识库分类审查

从九分类入口、17条能力及必要依赖加载规范和实现；针对已建档承诺、失败阶段及所需事实挑选边界与反证，不宣称全.NET/全版本穷举。

- 解析、泛型、绑定/匹配：入口诊断、变量分支隔离、三态、正则受限子集。
- 类型、类型使用位置、横向契约：身份与闭合上下文、ref/数组/包装形状、缺事实、取消/上限、生命周期和兼容范围。
- 成员、C#元数据、特性：域划分、源码与PE差异、生成记录、直接记录与排除。

按 requesting-code-review 请求只读审查，明确每个审查者的分类责任及不写入规则。有效发现须带绝对路径/行号、能力/场景、独立预期、实际结果、复现/反证和影响范围；未经运行的疑点单独标明。无发现结论仅限声明的审查范围。

## 任务4：同步、验收与提交

1. 同步能力、coverage、使用指南、索引计数和新证据，重新生成矩阵。
2. 保存精确执行命令、时间、TRX/日志SHA、环境及相关源码/测试/规范快照；阶段成功按实际结果登记。
3. 非作者核验新增/改动场景、证据范围及不变量适用性，确认生成资料没有第二份状态来源。
4. 运行 `eng/verify-documentation.ps1`、`eng/generate-knowledge.ps1 -Check`、`dotnet format AspectN.sln --verify-no-changes --no-restore`、完整暂存差异检查。生成器/校验器实现不变时不重复无关回归。
5. 将分类审查的范围、已确认发现、未执行反证及后续事项写入 `docs/reviews/2026-10-13-capability-evidence-and-review.md`；按此前用户提交授权提交任务文件，不推送。

## 执行记录

2026-10-13完成两项补证据：核心9个语法case、继承6个四视图case、Cecil/Fody各12个方向标志case；四个受影响项目全套共1519通过。新增EV013–019，旧15份报告保持；未改产品src。

九分类审查登记4组已确认未修复契约缺陷及1项待裁决边界，并完成独立复核。元数据原审查者中断后由另一位非作者接手终审，原生PE返回CA控制另行补入并重跑两探针。知识库、用户指南、coverage和生成矩阵同步；文档/生成一致性/格式及证据适用性校验通过。结果、边界和后续事项见[本轮记录](../reviews/2026-10-13-capability-evidence-and-review.md)。提交按既有用户授权执行，不推送。
