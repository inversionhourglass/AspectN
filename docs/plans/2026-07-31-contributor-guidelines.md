# 贡献指南实施计划

> **历史说明：** 本计划的 Legacy 对照和 approved-differences 要求已由[单一语言计划](2026-10-03-single-language-aspectn.md)取代。当前贡献规则见根 AGENTS.md。下列模板保留当时内容，现行文档语言要求不使用原英文词数规则。

> **面向 Claude 的原执行说明：** 必须使用 superpowers:executing-plans，逐项执行本计划。

**目标：** 添加简明仓库指南、可执行 C# 格式默认值和详细 Agent C# 规则。

**架构：** AGENTS.md 是简明入口，关联 .agents/rules/csharp-style.md。.editorconfig 保存 Roslyn 可检查规则；详细规则解释 EditorConfig 无法表达的语义控制流偏好。维护者既有 .gitignore 变化保留，与三个新文件一起提交。

**技术栈：** Markdown、EditorConfig、Roslyn/.NET 命名规则、Git。

---

## 执行约束

- 直接使用当前仓库和分支，不创建 worktree。
- 保留 .gitignore 全部已有行，维护者已从忽略列表移除 .agents。
- 原任务时 AspectN.sln 和项目尚不存在，属于目标状态，不运行解决方案构建/测试。
- 实施提交只暂存 .gitignore、.editorconfig、.agents/rules/csharp-style.md 和 AGENTS.md。

### 任务 1：添加可机器校验的 C# 约定

**文件：**
- 新增：.editorconfig。

**步骤 1：创建根 EditorConfig**

```editorconfig
root = true

[*]
charset = utf-8
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
csharp_style_namespace_declarations = file_scoped:error

dotnet_naming_rule.private_constants_should_be_upper_snake_case.severity = error
dotnet_naming_rule.private_constants_should_be_upper_snake_case.symbols = private_constants
dotnet_naming_rule.private_constants_should_be_upper_snake_case.style = upper_snake_case

dotnet_naming_rule.constants_should_be_upper_snake_case.severity = error
dotnet_naming_rule.constants_should_be_upper_snake_case.symbols = constants
dotnet_naming_rule.constants_should_be_upper_snake_case.style = upper_snake_case

dotnet_naming_rule.private_static_fields_should_be_pascal_case.severity = error
dotnet_naming_rule.private_static_fields_should_be_pascal_case.symbols = private_static_fields
dotnet_naming_rule.private_static_fields_should_be_pascal_case.style = underscore_pascal_case

dotnet_naming_rule.private_fields_should_be_camel_case.severity = error
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = underscore_camel_case

dotnet_naming_symbols.private_constants.applicable_kinds = field
dotnet_naming_symbols.private_constants.applicable_accessibilities = private
dotnet_naming_symbols.private_constants.required_modifiers = const

dotnet_naming_symbols.constants.applicable_kinds = field
dotnet_naming_symbols.constants.applicable_accessibilities = *
dotnet_naming_symbols.constants.required_modifiers = const

dotnet_naming_symbols.private_static_fields.applicable_kinds = field
dotnet_naming_symbols.private_static_fields.applicable_accessibilities = private
dotnet_naming_symbols.private_static_fields.required_modifiers = static

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.upper_snake_case.capitalization = all_upper
dotnet_naming_style.upper_snake_case.word_separator = _

dotnet_naming_style.underscore_pascal_case.required_prefix = _
dotnet_naming_style.underscore_pascal_case.capitalization = pascal_case

dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case

[*.md]
trim_trailing_whitespace = false
```

私有常量交集规则防止 private const 字段因隐含 static 而继承私有静态字段命名。

**步骤 2：核验必需键**

运行：

```powershell
rg -n "file_scoped|private_constants|private_static_fields|underscore_pascal_case|upper_snake_case" .editorconfig
```

预期：全部命名空间和命名规则存在。

### 任务 2：添加详细 C# 规则

**文件：**
- 新增：.agents/rules/csharp-style.md。

**步骤 1：编写详细规则文档**

````markdown
# C# 风格规则

适用于本仓库全部 C# 项目；机器校验格式与命名以 .editorconfig 为准。

## 命名空间与缩进

- 四空格，不使用制表符。
- 使用文件范围命名空间：`namespace AspectN.Syntax;`。
- 每文件一个主要类型，文件名与类型一致。

## 字段命名

- 私有实例字段 _camelCase，如 _sourceText。
- 私有静态字段（含 static readonly）_PascalCase，如 _DefaultOptions。
- 常量 UPPER_SNAKE_CASE，如 DEFAULT_CACHE_SIZE。

## 控制流

优先前置条件检查和提前 return/continue/break。先处理异常及不合条件，让主路径保持最浅缩进。

```csharp
public void Process(List<int> values)
{
    if (values.Count < 10) return;

    ProcessCore(values);
}
```

不要用反向条件包裹主路径：

```csharp
if (values.Count >= 10)
{
    ProcessCore(values);
}
```

简单条件后只有 return/continue/break 时，单行并省略大括号：

```csharp
if (candidate is null) return;
if (!candidate.IsSupported) continue;
if (matchFound) break;
```

多语句、解释注释或单行降低可读性时用大括号，不把普通业务逻辑压缩为单行条件。
````

**步骤 2：核验规则文件可跟踪**

运行：

```powershell
git check-ignore .agents/rules/csharp-style.md
```

预期：无输出，退出码 1。

### 任务 3：添加仓库入口并提交

**文件：**
- 新增：AGENTS.md。
- 修改：.gitignore，保留维护者既有变化。

**步骤 1：编写简明根指南**

````markdown
# 仓库规则

## 项目结构与模块组织

AspectN 计划重构为与后端无关的表达式解析及匹配工具，目标布局见 docs/plans/2026-07-31-aspectn-industrial-redesign.md。核心语法、语义、签名、匹配在 src/AspectN，公共 API 不暴露后端类型。Cecil/Roslyn/Reflection 映射放在对应后端项目。

tests/ 对应生产项目，一致性、Legacy 对照、.NET Framework 和测试程序集独立。benchmarks/ 存基准，eng/ 存工程脚本，docs/superpowers/specs/ 存正式架构/语言文档。

## 构建、测试及开发

解决方案建立后使用：

- dotnet restore AspectN.sln：还原集中依赖。
- dotnet build AspectN.sln -c Release --no-restore：Release 编译并检查警告。
- dotnet test AspectN.sln -c Release --no-build：完整测试矩阵。
- dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release：聚焦测试。
- dotnet format AspectN.sln --verify-no-changes：提交前核验格式。

## 代码风格与命名

四空格和文件范围命名空间。私有实例 _camelCase、私有静态 _PascalCase、常量 UPPER_SNAKE_CASE。优先前置检查和提前退出，不深层嵌套。细则见 .agents/rules/csharp-style.md，自动规则以 .editorconfig 为准。

## 测试

xUnit 示例、FsCheck 有界属性测试。类名按对象，方法名 Method_State_ExpectedResult，数据确定。原计划的 Legacy 变化要求 approved-differences.json 明确记录，不默改兼容基线。

## 提交与拉取请求

沿用 Conventional Commit，如 feat(core): add lexer、test: prove Cecil parity、docs: clarify syntax。提交聚焦任务。PR 说明行为/架构、关联问题/规范及准确验证命令。兼容、API、包或性能变化提供对应证据。

## Agent 专用说明

现有检出工作，不创建 worktree。未通过实施计划明确检查点，不发布、推送、移除 Rougamo 子模块或修改冻结 Legacy 对照。
````

**步骤 2：核验长度、链接、格式与范围**

运行：

```powershell
$words = ((Get-Content -Raw AGENTS.md) -split '\s+' | Where-Object { $_ }).Count
if ($words -lt 200 -or $words -gt 400) { throw "AGENTS.md has $words words." }
Test-Path .agents/rules/csharp-style.md
Test-Path docs/plans/2026-07-31-aspectn-industrial-redesign.md
git diff --check
git status --short
```

原预期：200–400 个英文词，两条路径返回 True、无空白错误，只修改四个预定文件。该词数属于历史验收，不用于现行中文文档。

**步骤 3：精确暂存并提交**

```powershell
git add -- .gitignore .editorconfig .agents/rules/csharp-style.md AGENTS.md
git diff --cached --check
git commit -m "docs: add repository contributor guidelines"
```
