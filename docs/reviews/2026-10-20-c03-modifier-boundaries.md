# C03 修饰符与类型名称边界修复

## 范围与确认

基线为 `cd70db182aaf638e7db7d5921f4ab0186caac469`。维护者在本聊天明确采纳“连续类型名称模式优先、只把独立关键字识别为修饰符”的方案并要求开始处理。本次关闭综合报告 C03，保留 C04–C07 待讨论；C01/C02 已另行提交。

本记录始于 2026-10-20；中断后于 2026-10-21 恢复，核对工作树一致后完成最终验证。文件名及 Finding 编号保留开始日期，证据的 recorded_on 使用最终记录日期。

对应能力为 `SYN-GRAMMAR-001/S1`、盘点 `INV-0007`，依据语言 3.2/3.4 的邻接与名称模式规则。修复记录在 [FND-20261020-003](findings/FND-20261020-003.md)，原综合报告及两个输入报告保持原文和未跟踪状态。

## 根因与修复

Lexer 将 `static*` 分为邻接的 IdentifierToken 与 StarToken；名称解析器本来会按跨度将它们拼成一个名称模式，但 `ParseModifiers` 只检查前一个记号文本，提前消费了 `static`。同一问题存在于否定前瞻、复合访问性的第二个词及访问性备选处理。

当前在 `SelectorParser` 提取已有 `async` 类型名称保护规则为 `HasTypePatternContinuation`。只有邻接的名称片段、限定/嵌套分隔符、泛型起始及类型后缀才延续类型模式；空白仍终止连续名称。该判断覆盖所有有关修饰符消费点，方法和属性共用，不修改 Lexer、Binder、匹配器、公开 API 或后端映射。

| 输入 | 当前解释 |
| --- | --- |
| `method(static* Run())` | 返回类型名称以 static 开头，不约束静态性 |
| `method(static * Run())` | 静态方法，返回类型任意 |
| `method(static static* Run())` | 静态方法且返回类型名称以 static 开头 |
| `method(!static* Run())` | 返回类型模式取反 |
| `method(!static * Run())` | 非静态方法，返回类型任意 |
| `method(private protected* Run())` | private 方法，返回类型名称以 protected 开头 |
| `method(private protected * Run())` | private protected 方法，返回类型任意 |
| `method(public|internal private* Run())` | public 或 internal 方法，返回类型名称以 private 开头 |

`public|internal`、括号化复合权限和空白分隔的正常修饰符保持原语义。裸连写复合权限仍给迁移诊断，独立 `async` 修饰符仍被拒绝；带名称延续的 `privateprotected*`、`protectedinternal*`、`async*` 保持类型模式含义。

## 失败与通过证据

- `ModifierBoundaryTests` 初始 176 项：旧实现 **142 失败、34 通过**；失败包括错误修饰符数量、被截断类型槽或错误语法诊断。覆盖方法/属性、五种有效关键字、两种旧连写权限、async、八种邻接延续、否定及空白/备选对照。
- 修复后完整核心 1133 项通过，其中新增 176 项全部通过。
- `ModifierNameBoundaryTests` 初始 15 项全部失败。失败发生在编译模式或首个后端匹配断言，不能把未执行到的旧实现后端断言冒充失败证据。
- 修复后 15 项全部通过：实际编译五种前缀的普通 C# 类型，分别验证无限制、静态限制、否定模式；每项在 Cecil、Reflection、Roslyn 源码和 PE 上检查实例前缀返回、静态其他返回及静态前缀返回，共 180 次匹配断言。

S1 的正式证据仍限定为核心解析/绑定；真实后端匹配是本次缺陷的额外控制，不改变现有 N/A 判定。没有新增或修改 N/A 记录，也不声称穷举全部恢复输入。

## 文档与证据适用性

同步语言 3.2、使用指南“编写匹配表达式”、SYN-GRAMMAR/S1、INV-0007 及派生矩阵。其他使用章节没有新增行为或语法承诺，无需机械修改。旧规范与证据保留，新增修复不将 supported 改为 excluded。

语言规范的新增文字只解释 3.2 的既有名称边界，不改变 C02 的 13.1 构造契约；C01 身份编码和 C02 构造实现均未修改。历史证据保留原始哈希与结果，本次完整矩阵重跑其现有回归，不把整个规范文件哈希变化直接当成无关章节语义变化。

当前执行结果、环境和源文件哈希见 [EV-20261020-003](evidence/EV-20261020-003.json)。TRX 位于本机忽略的 `artifacts/c03-fix/`，不将这些本机日志冒充随 Git 发布的证据；回归源码可独立重跑。

## 最终执行结果（2026-10-21）

| 测试项目 | 通过数 |
| --- | --- |
| AspectN.Tests | 1133 |
| AspectN.Cecil.Tests | 173 |
| AspectN.Reflection.Tests | 109 |
| AspectN.Roslyn.Tests | 28 |
| AspectN.Conformance.Tests | 326 |
| AspectN.NetFramework.Tests / net472 | 7 |
| AspectN.NetFramework.Tests / net10.0 | 8 |
| AspectN.Cecil.Fody.Tests | 175 |
| 合计 | 1959 |

完整矩阵零失败、零跳过。`dotnet build AspectN.sln -c Release --no-restore --disable-build-servers -m:1 -v:minimal` 通过，零警告、零错误；`dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity quiet` 通过。

`eng/generate-knowledge.ps1` 已生成派生矩阵，`-Check` 一致性核验通过；`eng/verify-documentation.ps1` 检查 17 条能力、97 个场景和 9 章使用指南，零错误、零告警。`git diff --check` 通过。

## 独立复核

`/root/c03_boundary_review` 复核正向/否定、复合访问性的第二个词、访问性备选中的两个消费点、方法/属性共用路径及 EOF 前瞻，未发现需要修复的问题。文档批准来源、S1 入口范围及未变更的四项 N/A 已核对。首轮审查因用量限制未形成结论；2026-10-21 恢复后完成的是独立静态审查，未执行测试，测试结果以主执行方统一矩阵为准。
