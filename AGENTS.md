# 仓库规则

## 项目结构与模块组织

AspectN 是与元数据后端无关的表达式解析与匹配工具，只有现有 Standard 语法这一种语言契约。目标布局见 `docs/plans/2026-07-31-aspectn-industrial-redesign.md`；其中的 Legacy 兼容性要求已由 `docs/plans/2026-10-03-single-language-aspectn.md` 取代。核心语法、语义、签名和匹配归属 `src/AspectN`，公共 API 不得暴露后端类型。Mono.Cecil、Roslyn、Reflection 映射分别放在 `src/AspectN.Cecil`、`src/AspectN.Roslyn`、`src/AspectN.Reflection`；Fody 通过 `src/AspectN.Cecil.Fody` 复用 Cecil 源码。

`src/Shared` 仅包含适配器共享源码，由各后端程序集编译到 `AspectN.AdapterInternals` 命名空间中；它不是独立程序集，也不得进入核心公共 API。

`tests/` 与生产项目一一对应。跨适配器一致性、.NET Framework 兼容性和测试用程序集保留在专用项目中。基准测试放在 `benchmarks/`，工程脚本放在 `eng/`，正式架构及语言文档放在 `docs/superpowers/specs/`。

## 构建、测试与开发命令

- `dotnet restore AspectN.sln`：还原集中管理的依赖。
- `dotnet build AspectN.sln -c Release --no-restore`：以 Release 配置编译全部库并检查警告。
- `dotnet test AspectN.sln -c Release --no-build --no-restore`：运行完整测试矩阵。
- `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release`：运行聚焦测试。
- `dotnet format AspectN.sln --verify-no-changes --no-restore`：提交前核验格式。

## 代码风格与命名

C# 使用四空格缩进和文件范围命名空间。私有实例字段使用 `_camelCase`，私有静态字段使用 `_PascalCase`，常量使用 `UPPER_SNAKE_CASE`。优先使用前置条件检查和提前返回，避免深层嵌套。详见 `.agents/rules/csharp-style.md`；可自动校验的规则以 `.editorconfig` 为准。

## 文档和注释语言

Markdown 说明文档必须使用中文，包括 README、用户指南、知识库、规范、计划、审查记录和规则文件。标题、解释、表格说明及新增示例中的说明性注释使用中文；API/类型/包名、文件路径、命令、DSL、机器字段和枚举值保留原样。翻译历史资料时保留其时间、状态、原始结论和条款编号，不能把旧计划改成当前承诺，也不改写历史证据报告。

对外公开的类型、接口、枚举、方法、构造器和属性使用英文 XML API 文档注释，包括适用的参数、返回值与契约异常说明；以类型与成员的有效可见性为准，内部类型中的 public 辅助成员仍属于内部范围。内部 XML 注释及代码注释使用中文，解释不明显的算法、状态转换、元数据映射、兼容规则或不变量；不为显然的语句添加注释。

产品介绍将 AspectN 定位为通用 .NET 类型与成员匹配工具，覆盖日常查找、元数据检索与代码分析。AOP/代码织入可以作为一种应用场景说明，不能成为工具定位的前提。

## 测试规则

示例测试使用 xUnit，有界属性测试使用 FsCheck。测试类按被测对象命名，方法使用 `Method_State_ExpectedResult`。测试数据保持确定性。保留 Standard 回归覆盖，包括泛型元数、变量作用域、特性、诊断和适配器生命周期。主动改变语言契约时必须同步规范和一致性测试。解决方案不包含 Legacy 对照实现，也没有 approved-differences 基线。

## 提交与拉取请求

沿用 Conventional Commit 风格，例如 `feat(core): add lexer`、`test: prove Cecil parity`、`docs: clarify syntax`。每个提交聚焦当前任务。PR 说明行为和架构影响，关联有关问题及规范，列出准确验证命令。改变兼容性、公共 API、程序包或性能范围时，提供对应证据。

## Agent 专用说明

### 文档与知识库维护

用户说明从 `docs/usage/index.md` 读取。Agent 分析能力变更或执行审查时，从 `docs/knowledge/index.md` 按需加载受影响条目及其声明的依赖，再按 `docs/knowledge/review/index.md` 执行审查。已批准流程设计为 `docs/superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md`。

已建档能力（包括 E 阶段扩展条目）的变更必须声明受影响能力/场景编号，并在同一可审查变更中同步知识库、用户受影响章节和证据，或给出具体无影响理由。未盘点范围更新 `docs/knowledge/coverage.json`，继续遵守现有规范和一致性测试要求。不能从缺少文档推断排除，也不能仅因存在测试源码就标记为已验证。实现、测试、契约或环境变化时重评估证据适用性，保留历史报告。

运行 `pwsh -NoProfile -File eng/verify-documentation.ps1` 检查有关变更。修改能力或盘点资料后运行 `pwsh -NoProfile -File eng/generate-knowledge.ps1`，并用 `-Check` 核验派生目录与场景矩阵，不手工维护第二份状态。Finding 引用初期仅告警；语义批准、N/A 理由及当前证据适用性仍需审查。新增或改动的 N/A 记录需要非作者核验主体和日期。全仓强制同步需通过准备闸门并由维护者确认；九类已建档不表示所有功能、后端、输入或版本均已覆盖。

在现有检出目录工作，不创建 Git worktree。保留既有未跟踪文件。未经明确授权，不发布程序包、推送分支、移除 Rougamo 子模块、修改相邻仓库或冻结的对照缓存。Rougamo 迁移和大版本发布属于独立任务。
