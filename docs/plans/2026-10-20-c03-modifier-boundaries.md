# C03 修饰符与类型名称边界修复计划

**目标：** 落实用户采纳的方案，保留连续类型名称模式，只把独立关键字识别为修饰符；本次仅修 C03。

**架构：** 在 `SelectorParser` 复用 `async` 已有的邻接前瞻规则，统一正向/否定修饰符、复合访问性的第二个词及访问性备选后的类型槽。方法和属性共用该判断，不修改 Lexer 或匹配器。

**环境：** 现有检出目录、C#、xUnit；使用已有依赖，保留未跟踪报告及 `nul`，不推送。

**执行状态（2026-10-21）：** 176 项边界及 15 项真实类型匹配回归已完成，旧实现失败证据已保留；最终完整矩阵 1959 项通过，Release 构建零警告零错误，格式、文档和派生一致性检查通过。独立只读复核无待修复项，形成单独 C03 提交。

## 1. 失败回归

- 新增 `tests/AspectN.Tests/Parsing/ModifierBoundaryTests.cs`，检查关键字前缀、邻接的类型延续、否定、复合权限及空白/备选对照的 AST 和编译结果。
- 执行 `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 --filter FullyQualifiedName~ModifierBoundaryTests`，记录旧实现的断言失败。

## 2. 最小修复与实际匹配

- 修改 `src/AspectN/Parsing/SelectorParser.cs`，提取当前位置/前瞻偏移的类型延续判定，并用于所有有关关键字消费点。
- 新增 `tests/AspectN.Conformance.Tests/ModifierNameBoundaryTests.cs`，使用真实编译的普通类型前缀、实例/静态方法验证三端映射后的正确选择，覆盖正反例。
- 运行核心及一致性聚焦测试；保持 `public|internal`、括号化复合权限、正常 `static *`、迁移诊断和不支持的 `async` 行为。

## 3. 文档与证据

- 语言规范 3.2/3.4 及使用指南补充 `static*`、`static *`、`static static*` 的区别。
- 同步 `SYN-GRAMMAR-001/S1`、INV-0007、正式 Finding、修复报告与证据 JSON；历史综合报告保持原文及未跟踪状态。
- 生成知识库并运行 `eng/generate-knowledge.ps1 -Check`、`eng/verify-documentation.ps1`；N/A 的入口范围不扩写为匹配语义承诺。

## 4. 验证与提交

- 运行完整 Release 构建、完整测试矩阵、`dotnet format --verify-no-changes --no-restore` 与 `git diff --check`。
- 按 requesting-code-review 请求只读独立复核并处理本次发现。
- 单独提交 C03，保留原有未跟踪文件，不推送。
