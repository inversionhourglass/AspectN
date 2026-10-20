# C02 泛型签名构造校验实施计划

> 执行方式：在当前检出目录按步骤实施；用户已确认“应在构造时拒绝结构不合法的签名”，不重新申请方向批准。

**目标：** 在公开中立签名构造时拒绝泛型槽位结构不一致，关闭 C02，不修改其他审查项。

**架构：** `NamedTypeSignature` 与 `MethodSignature` 在复制集合时执行核心内部校验，不依赖后端。泛型定义允许空实参；非空实参必须完整。类型按包含链总元数检查，方法按当前声明及所提供原始定义检查。合法开放构造保留泛型形参节点；缺少后端事实继续按现有三态契约处理。

**技术：** C#、xUnit、Cecil/Fody、Roslyn、Reflection；net10.0、netstandard2.0、net472。

**执行状态：** 构造校验、17 项构造回归、真实后端对照、文档与证据已完成；最终 1768 项测试、Release 构建、格式、文档及派生一致性检查通过，独立复核意见已处理。按本计划形成单独 C02 提交，不推送。

## 1. 构造回归先失败

- 新增 `tests/AspectN.Tests/Metadata/GenericSignatureValidationTests.cs`。
- 覆盖类型实参不足/过多、非泛型带实参、嵌套总元数、空节点、错误原始定义；方法实参与声明/原始定义不符、残缺声明槽位。
- 保留定义、闭合/开放构造和方法仅有完整实参的外部表示对照；检查异常类型及参数名。
- 执行 `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~GenericSignatureValidationTests`，先记录未抛异常的失败。

## 2. 最小实现与旧回归迁移

- 新增 `src/AspectN/Metadata/GenericSignatureValidation.cs`，为两类构造器复制并校验泛型集合；类型元数用逐层扣减避免溢出。
- 修改 `src/AspectN/Metadata/TypeSignature.cs`、`src/AspectN/Metadata/MethodSignature.cs`，接入校验并补英文 XML 异常契约。
- 将 `tests/AspectN.Tests/Evaluation/IncompleteGenericArgumentTests.cs` 中原先接受残缺结构的断言改成构造拒绝；完整定义、构造和真实后端映射继续匹配。
- 执行核心测试，逐项核对受新契约影响的人工 fixture，只迁移结构非法的输入，不改变合法输入预期。

## 3. 同步契约与证据

- 更新语言规范、使用指南、`GEN-BINDING-001/S4`、`SEM-REGEX-001/S4`；声明本次维护者裁决取代先前残缺集合支持规则。
- 保留 2026-10-13/14 历史报告与执行结果，追加当前裁决和证据适用性说明；新增 C02 Finding、修复报告与证据 JSON。
- 原综合报告和两个输入报告是既有未跟踪文件，保持原文及未跟踪状态；当前处置写入正式 C02 Finding 与修复记录，其他 Finding 不处理。
- 运行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`、`pwsh -NoProfile -File eng/generate-knowledge.ps1 -Check`、`pwsh -NoProfile -File eng/verify-documentation.ps1`。

## 4. 验证、复核与提交

- 执行 `dotnet build AspectN.sln -c Release --no-restore` 与 `dotnet test AspectN.sln -c Release --no-build --no-restore`。
- 执行 `dotnet format AspectN.sln --verify-no-changes --no-restore`、`git diff --check`。
- 按 requesting-code-review 请求只读独立复核，修复本次范围内的问题。
- 只提交 C02 相关文件，使用 Conventional Commit；保留既有未跟踪报告与 `nul`，不推送。
