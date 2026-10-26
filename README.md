# AspectN

AspectN 是面向 .NET 类型和可调用成员表达式的解析与匹配工具，不依赖特定元数据后端。表达式只需编译一次，即可对所用后端映射的候选签名进行匹配，明确返回 `Matched`、`NotMatched` 或 `Unknown`。

可用于日常查找类型与方法、按签名筛选反射结果、编译期代码分析和程序集元数据检索。AOP 框架也可以用它选择需要织入的成员，织入是其中一种应用场景。

## 程序包

| 程序包 | 用途 |
| --- | --- |
| `AspectN` | 语法、绑定、与后端无关的签名及匹配 |
| `AspectN.Cecil` | Mono.Cecil 元数据适配器 |
| `AspectN.Cecil.Fody` | 面向使用 FodyHelpers 的项目的 Cecil 适配器 |
| `AspectN.Roslyn` | Microsoft.CodeAnalysis 符号适配器 |
| `AspectN.Reflection` | 运行时反射适配器 |

所有程序包均面向 `netstandard2.0` 和 `net10.0`；`netstandard2.0` 资产支持面向 .NET Framework 4.7.2 的使用方。

`AspectN.Cecil` 和 `AspectN.Cecil.Fody` 针对不同强名称的 Cecil 程序集提供相同适配器 API，不能同时引用。在引用 `FodyHelpers` 的 Fody 织入器中使用 `AspectN.Cecil.Fody`。

## 快速开始

[使用指南](docs/usage/index.md)提供按任务组织的说明和可运行示例；[知识库](docs/knowledge/index.md)记录具体范围的契约、边界及审查证据。

[场景支持与验证矩阵](docs/knowledge/generated/support-matrix.md)按九类汇总已建档范围，区分支持承诺与实际验证，不将缺少记录视为不支持。

```csharp
using AspectN;
using AspectN.Evaluation;
using AspectN.Reflection;

var compilation = new AspectNEngine().CompileMemberExpression(
    "method(public string Example.Service.GetName())");

if (!compilation.TryGetMemberPattern(out var pattern))
{
    throw new InvalidOperationException(
        string.Join(Environment.NewLine, compilation.Diagnostics));
}

var method = typeof(Example.Service).GetMethod("GetName")!;
var typeSystem = new ReflectionTypeSystem();
var candidate = typeSystem.Mapper.MapMethod(method);
var result = pattern.Match(candidate, typeSystem);

if (result.Status == MatchStatus.Unknown)
{
    // 检查 result.Diagnostics，并明确选择应用程序的处理策略。
}
```

## 表达式语言

AspectN 采用现有 Standard 语法和匹配语义，只提供一种区分大小写的语言。语言选择 API 和 Legacy 兼容层已经移除。从 Shared.Cecil.AspectN 迁移是破坏性变更，需要核对表达式及其预期匹配范围。

成员路径支持否定与布尔组合，例如 `method(* (A || B).(Run || Stop)(..))`。属性名或命名空间段上的泛型子句、连续 `!`，以及 `tasklike<int>` 结果子句之前的空白会产生诊断。

`regex(...)` 支持 .NET 正则表达式的受限子集。注释、内联选项、环视、反向引用、其他特殊分组，以及嵌套或相减字符类会产生编译错误。普通分组、`(?:...)`、字符类、分支、锚点和量词仍可使用；正则匹配默认使用 250 ms 超时，可通过匹配选项配置。

类型结构深度、包含外层类型的有效类型泛型元数与方法自身的泛型元数默认上限均为 64，深度根计为第一层。解析、映射和匹配阶段分别通过 `AspectNParseOptions`、`AspectNMappingOptions` 和 `AspectNMatchOptions` 配置，计数额度可用 `ResourceLimit.Unlimited` 显式不限。原生映射遇到结构超限输入时抛出带 AN3005 或 AN3006 的 `AspectNLimitException`；匹配结构超限的中立签名时返回 `Unknown` 并附相同诊断。详见[资源配置指南](docs/usage/resource-limits.md)。

详见[语言指南](docs/language/README.md)、[正式语言规范](docs/superpowers/specs/2026-07-31-aspectn-language-spec.md)和 [Shared.Cecil.AspectN 迁移指南](docs/migration/shared-cecil-to-aspectn.md)。

## 构建与测试

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

仓库将警告视为错误，并通过临时使用方项目验证程序包依赖边界。

核心测试项目 `AspectN.Tests` 通过项目属性自动加载 [core-test.runsettings](eng/core-test.runsettings)，本地常规 `dotnet test` 和 CI 使用同一配置：连续 60 秒没有测试开始或结束事件时，VSTest 终止测试宿主，并将运行报告为失败，不收集内存转储。这是同步挂起防线，不是逐个随机输入的性能要求；解析 fuzz 保留固定种子、样本数及诊断跨度检查。直接运行测试 DLL 或使用其他运行器时，需要确认其加载了该配置。可用 `pwsh -NoProfile -File eng/tests/verify-testhost-timeout.Tests.ps1` 独立验证正常返回、调度延迟及真实挂起；探针会保留日志，挂起场景约需等待 60 秒。此配置不承诺终止任意子进程；解析 fuzz 本身不启动子进程。见 [RI-03 处置与验证范围](docs/reviews/2026-10-22-ri03-fuzz-host-timeout.md)。

完整解决方案测试矩阵及程序包使用方脚本要求 Windows、兼容 .NET Framework 4.7.2 的运行时和 .NET 10 SDK。引用程序集允许在其他平台编译，但不提供运行 `net472` 测试及使用方所需的 .NET Framework 运行时。

在非 Windows 平台上，先还原解决方案，再分别构建和测试 `tests/` 下的六个 .NET 10 项目：`AspectN.Tests`、`AspectN.Cecil.Tests`、`AspectN.Cecil.Fody.Tests`、`AspectN.Reflection.Tests`、`AspectN.Roslyn.Tests`、`AspectN.Conformance.Tests`。还需使用现代运行时验证 `netstandard2.0` 资产：

```sh
dotnet test tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj -c Release -f net10.0
```

CI 的 Linux 任务运行上述子集；Windows 任务覆盖 `net472` 和程序包使用方。CI 默认依赖版本来自 `Directory.Packages.props`，兼容矩阵显式覆盖待验证的后端版本。

[CI](.github/workflows/ci.yml) 和 [Compatibility](.github/workflows/compatibility.yml) 在 `dev`、`main` 收到 push，以及创建、更新或重新打开目标为这两个分支的 PR 时运行，并保留手动触发。其他分支的普通 push 和标签 push 不触发这两个工作流。[Release](.github/workflows/release.yml) 在 `prerelease` 分支 push 时自动预发布，`v*` 标签用于正式发布且必须匹配基础版本；详见[发布版本规则](eng/release-versioning.md)。

在 fork 的 `dev` 分支开发时，启用 fork 的 GitHub Actions 后，每次 push 可获得独立验证。向上游 `dev` 创建 PR 后，上游另外验证模拟合并结果；PR 存在期间继续 push 会在两个仓库分别执行验证。这两轮各自保留，最终合并后上游还会验证其 `dev` 分支。见 [RI-02 触发策略与验证范围](docs/reviews/2026-10-22-ri02-fork-ci-triggers.md)。

## 许可证

AspectN 使用 MIT 许可证。
