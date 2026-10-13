> 历史设计/执行记录。下列机器路径和分支名描述原会话，不是新检出的前提。当前实现及验证入口见[2026-10-05 修复记录](../reviews/2026-10-05-review-ds-remediation.md)。Legacy/对照实现及相邻仓库步骤已由单一语言计划取代，不属于当前任务。

# AspectN 工业化重构实施计划

> 范围更新（2026-10-05）：用户决定移除自定义语义扩展。以下扩展任务和 API 示例属于历史内容，由当前语言规范及 `2026-10-05-review-ds-decisions.md` 取代。

> 性能更新（2026-10-05）：用户决定后置可复现基准。以下基准任务仍是未来工作，不是已完成发布证据。未使用的 BenchmarkDotNet 版本锁定已移除；正式设计记录性能闸门仍未通过。

> **已被取代的要求（2026-10-03）：** [单一语言计划](2026-10-03-single-language-aspectn.md)取代本计划的 Legacy 模式、对照一致性、兼容入口和 approved-differences 要求。历史任务保留作为背景；当前 API 与验证遵循单一语言契约。Rougamo 迁移和发布仍是未来独立工作。

> **面向 Claude 的原执行说明：** 必须使用 superpowers:executing-plans，逐项执行本计划。

**目标：** 建立生产级、与后端无关的 AspectN 匹配器，原计划包含版本化 Legacy/Standard 语言、Mono.Cecil/Roslyn/Reflection 适配器，以及 Rougamo 的“零未批准差异”迁移路径。

**架构：** AspectN 核心负责源文本、词法、不可变语法、绑定、中立签名、编译计划、三态求值、扩展、诊断和有界缓存。AspectN.Cecil/Roslyn/Reflection 将原生类型系统转换为共同模型并实现 ITypeSystem，后端类型不进入核心公共 API。冻结的 Shared.Cecil.AspectN 提交 `03a9d1030f92a64449b6220eb45d7198d31653db` 在原 1.0 发布闸门通过前作为 Legacy 差分对照。

**技术栈：** .NET SDK 10.0.302、最新 C#、netstandard2.0/net10.0 库、xUnit 2.9.3、FsCheck.Xunit 3.3.4、Mono.Cecil 0.11.5 基线/0.11.6 兼容分支、Microsoft.CodeAnalysis.CSharp 4.12.0 基线/5.6.0 兼容分支、BenchmarkDotNet 0.15.8、GitHub Actions。

---

## 执行约束

- 直接在 `D:\Code\Hourglass\AspectN` 的 codex/aspectn-redesign-spec 分支工作。维护者明确不使用 Git worktree。
- 以下两份书面规范作为正式依据：
  - `docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md`
  - `docs/superpowers/specs/2026-07-31-aspectn-language-spec.md`
- 不挑拣提交或编辑 `D:\Code\Hourglass\rougamo-eco\AspectN`。它是历史原型，包含两个未推送提交和大量未提交工作，仅作参考，不作为实现基线。
- 不修改 `D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN` 的生产代码，其 README.md 已含维护者改动，在最终归档任务前保持不变。
- Legacy 对照只使用 `03a9d1030f92a64449b6220eb45d7198d31653db`，提供其他版本时测试失败。
- 不默默批准 Legacy 差异。每项已批准修正记录到 `tests/AspectN.LegacyOracle.Tests/approved-differences.json`，包含模式、候选、旧/新结果及原因。
- 提交小而聚焦单个任务。每个阶段结束提交前，先运行 dotnet format --verify-no-changes、聚焦测试项目，再运行解决方案测试。
- 未通过本计划明确检查点，不发布程序包、推送分支、移除 Rougamo 子模块或归档 Shared 仓库。

## 目标仓库布局

```text
AspectN.sln
Directory.Build.props
Directory.Packages.props
global.json
src/
  AspectN/
  AspectN.Cecil/
  AspectN.Roslyn/
  AspectN.Reflection/
tests/
  AspectN.Tests/
  AspectN.Cecil.Tests/
  AspectN.Roslyn.Tests/
  AspectN.Reflection.Tests/
  AspectN.Conformance.Tests/
  AspectN.LegacyOracle.Tests/
  AspectN.NetFramework.Tests/
  AspectN.TestAssembly/
benchmarks/
  AspectN.Benchmarks/
eng/
  prepare-legacy-oracle.ps1
  verify-packages.ps1
docs/
  language/
  migration/
  plans/
```

## 公共 API 基线

实现可以增加内部类型，但以下公共职责和依赖方向固定：

```csharp
AspectNSyntaxTree AspectNParser.ParseMemberExpression(
    string source,
    AspectNParseOptions? options = null);

AspectNSyntaxTree AspectNParser.ParseTypePattern(
    string source,
    AspectNParseOptions? options = null);

AspectNCompilation AspectNCompilation.Create(
    AspectNSyntaxTree syntaxTree,
    AspectNCompilationOptions? options = null,
    AspectNExtensionRegistry? extensions = null);

bool AspectNCompilation.TryGetMemberPattern(
    out CompiledMemberPattern? pattern);

bool AspectNCompilation.TryGetTypePattern(
    out CompiledTypePattern? pattern);

MatchResult CompiledMemberPattern.Match(
    CallableSignature candidate,
    ITypeSystem typeSystem,
    CancellationToken cancellationToken = default);

MatchResult CompiledTypePattern.Match(
    TypeSignature candidate,
    ITypeSystem typeSystem,
    CancellationToken cancellationToken = default);
```

AspectNEngine 是这些显式阶段外可选的有界缓存入口。不使用引擎也能解析和编译。

---

## 阶段 1：仓库与核心基础

### 任务 1：建立解决方案与集中构建策略

**文件：**
- 新增： `global.json`
- 新增： `Directory.Build.props`
- 新增： `Directory.Packages.props`
- 新增： `AspectN.sln`
- 新增： `src/AspectN/AspectN.csproj`
- 新增： `tests/AspectN.Tests/AspectN.Tests.csproj`
- 仅本地修改，不提交： `.git/info/exclude`

**步骤 1：锁定 SDK**

创建 global.json：

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**步骤 2：添加全仓确定性构建设置**

创建 Directory.Build.props：

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RepositoryUrl>https://github.com/AspectN/AspectN</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <Authors>AspectN contributors</Authors>
    <Copyright>Copyright © AspectN contributors</Copyright>
  </PropertyGroup>
  <PropertyGroup Condition="'$(IsPackable)' == 'true'">
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

**步骤 3：集中管理依赖版本**

创建 Directory.Packages.props，初始锁定如下：

```xml
<Project>
  <PropertyGroup>
    <CecilVersion Condition="'$(CecilVersion)' == ''">0.11.5</CecilVersion>
    <RoslynVersion Condition="'$(RoslynVersion)' == ''">4.12.0</RoslynVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Mono.Cecil" Version="$(CecilVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="$(RoslynVersion)" />
    <PackageVersion Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="5.6.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="Microsoft.NETFramework.ReferenceAssemblies.net472" Version="1.0.3" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="FsCheck.Xunit" Version="3.3.4" />
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>
</Project>
```

编译基线有意匹配 Rougamo/Fody 的 Mono.Cecil 0.11.5 和 Rougamo.Analyzers 的 Roslyn 4.12.0。后续兼容任务覆盖为 0.11.6/5.6.0。

**步骤 4：创建核心及核心测试项目**

src/AspectN/AspectN.csproj：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
    <IsPackable>true</IsPackable>
    <PackageId>AspectN</PackageId>
    <Description>Backend-neutral .NET type and callable pattern matching.</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

tests/AspectN.Tests/AspectN.Tests.csproj 面向 net10.0，引用 src/AspectN 和四个测试包；runner/collector 设置 PrivateAssets="all"。

**步骤 5：创建解决方案，核验空基线**

运行：

```powershell
dotnet new sln -n AspectN --format sln
dotnet sln AspectN.sln add src/AspectN/AspectN.csproj
dotnet sln AspectN.sln add tests/AspectN.Tests/AspectN.Tests.csproj
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
```

预期：还原与构建成功，零警告。

**步骤 6：本地缓存不进入状态**

只在 .git/info/exclude 添加 local_cache/，不写入已提交的 .gitignore；它是 Agent 本地缓存，不是项目约定。

**步骤 7：提交**

```powershell
git add global.json Directory.Build.props Directory.Packages.props AspectN.sln src/AspectN tests/AspectN.Tests
git commit -m "build: scaffold AspectN core solution"
```

### 任务 2：添加源文本与稳定诊断

**文件：**
- 新增： `src/AspectN/Text/TextSpan.cs`
- 新增： `src/AspectN/Text/SourceText.cs`
- 新增： `src/AspectN/Diagnostics/DiagnosticSeverity.cs`
- 新增： `src/AspectN/Diagnostics/AspectNDiagnostic.cs`
- 新增： `src/AspectN/Diagnostics/DiagnosticCodes.cs`
- 新增： `src/AspectN/Diagnostics/DiagnosticBag.cs`
- 新增： `tests/AspectN.Tests/Text/SourceTextTests.cs`
- 新增： `tests/AspectN.Tests/Diagnostics/DiagnosticTests.cs`

**步骤 1：先写值语义失败测试**

覆盖：

```csharp
[Fact]
public void TextSpan_End_Is_Start_Plus_Length()
{
    Assert.Equal(7, new TextSpan(2, 5).End);
}

[Fact]
public void Diagnostic_Stores_Stable_Code_And_Span()
{
    var diagnostic = new AspectNDiagnostic(
        DiagnosticCodes.UnexpectedToken,
        DiagnosticSeverity.Error,
        "Expected ')'.",
        new TextSpan(12, 0));

    Assert.Equal("AN1001", diagnostic.Code);
    Assert.Equal(new TextSpan(12, 0), diagnostic.Span);
}
```

**步骤 2：运行聚焦测试**

运行：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SourceTextTests|FullyQualifiedName~DiagnosticTests"
```

预期：类型尚不存在，测试失败。

**步骤 3：实现不可变基础类型**

使用带负值构造保护的 readonly struct TextSpan，以及密封 AspectNDiagnostic 类。保留诊断码范围：

- AN0xxx：词法。
- AN1xxx：语法。
- AN2xxx：绑定/版本。
- AN3xxx：求值/后端。
- AN4xxx：迁移。

DiagnosticBag 为内部类型，构建时只追加，返回数组快照。

**步骤 4：测试通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SourceTextTests|FullyQualifiedName~DiagnosticTests"
git add src/AspectN/Text src/AspectN/Diagnostics tests/AspectN.Tests/Text tests/AspectN.Tests/Diagnostics
git commit -m "feat(core): add source text and diagnostics"
```

### 任务 3：实现词法分析器

**文件：**
- 新增： `src/AspectN/Syntax/SyntaxKind.cs`
- 新增： `src/AspectN/Syntax/SyntaxToken.cs`
- 新增： `src/AspectN/Parsing/Lexer.cs`
- 新增： `tests/AspectN.Tests/Parsing/LexerTests.cs`

**步骤 1：编写记号黄金测试**

使用强类型 theory 数据，让多记号用例不依赖 xUnit InlineData/params 绑定：

```csharp
public static TheoryData<string, SyntaxKind[]> CoreTokenCases => new()
{
    { "!", new[] { SyntaxKind.BangToken } },
    { "&&", new[] { SyntaxKind.AmpersandAmpersandToken } },
    { "||", new[] { SyntaxKind.PipePipeToken } },
    { "..", new[] { SyntaxKind.DotDotToken } },
    { "$T", new[] { SyntaxKind.DollarToken, SyntaxKind.IdentifierToken } }
};

[Theory]
[MemberData(nameof(CoreTokenCases))]
public void Lexes_Core_Tokens(string source, SyntaxKind[] expected)
{
    Assert.Equal(expected, Lex(source).Select(x => x.Kind));
}
```

还需测试：

- Unicode 标识符。
- 转义字符串。
- 扩展实参上下文中的有符号十进制整数。
- 非法字符产生 BadToken 和 AN0001。
- 每个记号具有精确原文本和跨度。
- 始终产生文件结束记号。

**步骤 2：验证失败**

运行：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~LexerTests
```

预期：Lexer 尚不存在，测试失败。

**步骤 3：实现延迟、按上下文工作的词法分析器**

必须：

- 保留原文本，不将标识符改为小写。
- 产生不含附属文本的记号。
- 分别识别 . 和 ..。
- 只在解析器消费 regex( 后公开 LexRegexBody()；扫描转义、字符类及嵌套分组，把外层 ) 留给普通解析。
- 对任意用户输入都不抛异常。

**步骤 4：添加损坏输入测试并使其通过**

添加未闭合字符串、未配对正则字符类、单独 &、单独 |。

运行：

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~LexerTests
```

预期：通过。

**步骤 5：提交**

```powershell
git add src/AspectN/Syntax/SyntaxKind.cs src/AspectN/Syntax/SyntaxToken.cs src/AspectN/Parsing/Lexer.cs tests/AspectN.Tests/Parsing/LexerTests.cs
git commit -m "feat(parser): add contextual AspectN lexer"
```

### 任务 4：添加不可变语法节点与树

**文件：**
- 新增： `src/AspectN/Syntax/SyntaxNode.cs`
- 新增： `src/AspectN/Syntax/ExpressionSyntax.cs`
- 新增： `src/AspectN/Syntax/TypePatternSyntax.cs`
- 新增： `src/AspectN/Syntax/SelectorSyntax.cs`
- 新增： `src/AspectN/Syntax/SignatureSyntax.cs`
- 新增： `src/AspectN/Syntax/AspectNSyntaxTree.cs`
- 新增： `src/AspectN/Parsing/AspectNParseKind.cs`
- 新增： `tests/AspectN.Tests/Syntax/SyntaxTreeTests.cs`

**步骤 1：先写语法树不变量失败测试**

断言：

- FullSpan 按源码顺序覆盖子节点。
- 子节点集合在构造时复制。
- 缺失记号宽度为零。
- 损坏语法树仍有根和诊断。
- 语法不引用元数据/后端命名空间。

**步骤 2：运行并观察失败**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SyntaxTreeTests
```

**步骤 3：实现最小语法层级**

使用密封具体节点表示：

- 一元、二元及括号表达式。
- callable/execution/method/property/getter/setter/ctor/cctor/attribute/regex/extension 选择器。
- 可调用、属性、构造器、参数、修饰符、泛型列表和类型使用语法。
- 有名、通配、元组、任务类、变量、数组、可空、关系及括号类型语法。

所有公开子属性只读。语法节点不保存语义符号、缓存或适配器状态。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SyntaxTreeTests
git add src/AspectN/Syntax src/AspectN/Parsing/AspectNParseKind.cs tests/AspectN.Tests/Syntax
git commit -m "feat(parser): add immutable syntax model"
```

### 任务 5：解析布尔表达式并恢复错误

**文件：**
- 新增： `src/AspectN/AspectNParser.cs`
- 新增： `src/AspectN/AspectNParseOptions.cs`
- 新增： `src/AspectN/AspectNLanguageVersion.cs`
- 新增： `src/AspectN/Parsing/Parser.cs`
- 新增： `tests/AspectN.Tests/Parsing/BooleanParserTests.cs`
- 新增： `tests/AspectN.Tests/Parsing/ParserRecoveryTests.cs`

**步骤 1：编写优先级测试**

断言以下树形：

```text
a() || b() && !c()
(a() || b()) && c()
!!a()
```

第一个根必须是 OR，其右子节点必须是 AND。

**步骤 2：编写恢复测试**

method(、a() && && b()、((a()) 等输入必须返回含多个稳定诊断的树，不能抛异常。

**步骤 3：运行失败测试**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BooleanParserTests|FullyQualifiedName~ParserRecoveryTests"
```

**步骤 4：实现优先级爬升**

使用：

```csharp
private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
{
    ExpressionSyntax left = Current.Kind == SyntaxKind.BangToken
        ? ParseUnaryExpression()
        : ParsePrimaryExpression();

    while (GetBinaryPrecedence(Current.Kind) > parentPrecedence)
    {
        var op = NextToken();
        var right = ParseExpression(GetBinaryPrecedence(op.Kind));
        left = new BinaryExpressionSyntax(left, op, right);
    }

    return left;
}
```

在 )、逗号、&&、|| 和 EOF 处添加缺失记号插入及同步恢复。

**步骤 5：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BooleanParserTests|FullyQualifiedName~ParserRecoveryTests"
git add src/AspectN/AspectNParser.cs src/AspectN/AspectNParseOptions.cs src/AspectN/AspectNLanguageVersion.cs src/AspectN/Parsing/Parser.cs tests/AspectN.Tests/Parsing
git commit -m "feat(parser): parse Boolean expressions with recovery"
```

### 任务 6：解析 Standard 类型模式

**文件：**
- 修改： `src/AspectN/Parsing/Parser.cs`
- 新增： `src/AspectN/Parsing/TypePatternParser.cs`
- 新增： `tests/AspectN.Tests/Parsing/TypePatternParserTests.cs`

**步骤 1：每类语法添加一个失败 theory**

包括：

```text
MyCompany..Services.*Service
Outer<T>/Inner<string>
System.IDisposable+
int?[]
(int, string)
tasklike
$T
ref readonly int
Name<>
Name<,>
Name<..>
```

断言语法种类、跨度、泛型元数表示、嵌套分隔符、后缀顺序和 ref readonly。

**步骤 2：添加拒绝用例**

Standard 必须产生诊断：

```text
Name<!>
out int
tasklike<>
()
(int)
Name<T,>
```

(int) 是分组而非元组，只在允许分组类型的位置有效。

**步骤 3：只实现语言规范中的 EBNF**

元组/分组消歧留在解析器内部。显式存储全逗号泛型元数，不构造虚假的通配子节点；后缀适用性由绑定器核验。

**步骤 4：运行并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~TypePatternParserTests
git add src/AspectN/Parsing tests/AspectN.Tests/Parsing/TypePatternParserTests.cs
git commit -m "feat(parser): parse Standard type patterns"
```

### 任务 7：解析选择器与签名

**文件：**
- 修改： `src/AspectN/Parsing/Parser.cs`
- 新增： `src/AspectN/Parsing/SelectorParser.cs`
- 新增： `tests/AspectN.Tests/Parsing/SelectorParserTests.cs`
- 新增： `tests/AspectN.Tests/Parsing/RegexParserTests.cs`

**步骤 1：添加选择器 theory**

覆盖全部 Standard 选择器：

```text
callable(public tasklike<string> Service.Get(..))
method(ref readonly int Buffer.Get())
execution(* Service..*.*(..))
property(public string Model.Name)
getter(* Model.Name)
setter(* Model.Name)
ctor(Service(string, ref int))
cctor(Service)
attr(para[0] RequiredAttribute)
regex(^public .*\\.Get\\(.*\\)$)
vendor.feature("x", -2, true, System.IDisposable+)
```

**步骤 2：添加版本化解析测试**

- Standard 直接接受 execution/property；`tasklike<void>` 和特性目标长名称保留迁移诊断。
- Legacy 接受 execution、property、attr(para 0 ...)、privateprotected、<!>。
- Standard 关键字小写且区分大小写。
- Legacy 选择器遵循对照实现不区分大小写的行为。

**步骤 3：实现选择器解析与上下文正则扫描**

扩展不能消费任意记号流。扩展实参只允许字符串、整数、布尔或类型模式节点。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SelectorParserTests|FullyQualifiedName~RegexParserTests"
git add src/AspectN/Parsing tests/AspectN.Tests/Parsing
git commit -m "feat(parser): parse member selectors and signatures"
```

---


## 阶段 2：语义模型、绑定与求值

### 任务 8：定义与后端无关的元数据签名

**文件：**
- 新增： `src/AspectN/Metadata/AssemblyIdentity.cs`
- 新增： `src/AspectN/Metadata/TypeIdentity.cs`
- 新增： `src/AspectN/Metadata/TypeSignature.cs`
- 新增： `src/AspectN/Metadata/TypeUseSignature.cs`
- 新增： `src/AspectN/Metadata/GenericParameterSignature.cs`
- 新增： `src/AspectN/Metadata/ParameterSignature.cs`
- 新增： `src/AspectN/Metadata/CallableSignature.cs`
- 新增： `src/AspectN/Metadata/PropertySignature.cs`
- 新增： `src/AspectN/Metadata/AttributeSignature.cs`
- 新增： `src/AspectN/Metadata/MetadataEnums.cs`
- 新增： `tests/AspectN.Tests/Metadata/SignatureTests.cs`

**步骤 1：编写不可变性与身份测试**

核验：

- 程序集身份参与有名类型身份。
- 显式保留嵌套包含类型及泛型元数。
- 同时保留构造类型实参和原始定义。
- 区分数组元素/维数、泛型形参声明者/位置及不支持的类型形状。
- TypeUseSignature 的 RefKind 独立于所引用类型。
- 属性和访问器签名分开但互相关联。
- 实例/静态构造器种类不能与普通方法混淆。

**步骤 2：实现不可变类型结构**

使用抽象 TypeSignature，以及密封 NamedTypeSignature、ArrayTypeSignature、GenericParameterTypeSignature、UnsupportedTypeSignature 子类。构造器将输入集合复制为数组，不存储 Type、TypeReference、ISymbol 或 object 句柄。

CallableSignature 必须公开：

```csharp
public CallableKind Kind { get; }
public Accessibility Accessibility { get; }
public bool IsStatic { get; }
public bool IsAsync { get; }
public TypeSignature DeclaringType { get; }
public TypeUseSignature ReturnType { get; }
public IReadOnlyList<ParameterSignature> Parameters { get; }
public IReadOnlyList<GenericParameterSignature> GenericParameters { get; }
public CallableSignature? OriginalDefinition { get; }
public PropertySignature? AssociatedProperty { get; }
```

**步骤 3：添加反射检查**

测试扫描 AspectN 公共 API；公开属性类型来自 Mono.Cecil、Microsoft.CodeAnalysis 或 System.Reflection 时失败。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SignatureTests
git add src/AspectN/Metadata tests/AspectN.Tests/Metadata
git commit -m "feat(core): add backend-neutral metadata signatures"
```

### 任务 9：定义类型系统能力与关系

**文件：**
- 新增： `src/AspectN/TypeSystem/ITypeSystem.cs`
- 新增： `src/AspectN/TypeSystem/TypeSystemCapabilities.cs`
- 新增： `src/AspectN/TypeSystem/TypeRelationResult.cs`
- 新增： `src/AspectN/TypeSystem/StructuralTypeComparer.cs`
- 新增： `tests/AspectN.Tests/TypeSystem/StructuralTypeComparerTests.cs`
- 新增： `tests/AspectN.Tests/TestDoubles/StubTypeSystem.cs`

**步骤 1：编写结构相等测试**

测试有名定义、构造泛型、嵌套泛型、数组和泛型形参。结构不等返回 NotMatched，身份缺失/不支持返回 Unknown。

**步骤 2：定义服务**

```csharp
public interface ITypeSystem
{
    TypeSystemCapabilities Capabilities { get; }

    TypeRelationResult AreEqual(TypeSignature left, TypeSignature right);

    TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default);

    TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default);
}
```

TypeRelationResult 包含 MatchStatus 和诊断，普通解析引用失败不抛异常。

**步骤 3：在核心实现结构相等**

关系遍历仍归适配器所有；核心相等使用签名已有事实。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~StructuralTypeComparerTests
git add src/AspectN/TypeSystem tests/AspectN.Tests/TypeSystem tests/AspectN.Tests/TestDoubles
git commit -m "feat(core): define type-system relationship contract"
```

### 任务 10：实现识别语言版本的语义绑定

**文件：**
- 新增： `src/AspectN/Binding/Binder.cs`
- 新增： `src/AspectN/Binding/BoundNode.cs`
- 新增： `src/AspectN/Binding/BoundExpression.cs`
- 新增： `src/AspectN/Binding/BoundTypePattern.cs`
- 新增： `src/AspectN/Binding/BoundSelector.cs`
- 新增： `src/AspectN/Binding/VariableSymbol.cs`
- 新增： `src/AspectN/AspectNCompilationOptions.cs`
- 新增： `src/AspectN/AspectNCompilation.cs`
- 新增： `tests/AspectN.Tests/Binding/BinderTests.cs`
- 新增： `tests/AspectN.Tests/Binding/LanguageVersionTests.cs`

**步骤 1：编写绑定诊断测试**

覆盖：

- 未知选择器。
- 所选版本不可用的选择器。
- 无效目标域。
- 无效修饰符组合。
- out/in 返回类型。
- 重复变量或首次在否定下引入的变量。
- OR 分支引入的变量不能向外逃逸。
- 缺失扩展。

**步骤 2：运行并观察失败**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BinderTests|FullyQualifiedName~LanguageVersionTests"
```

**步骤 3：分开实现语法和绑定模型**

绑定器：

- 归一基础类型别名。
- 解析 Standard/Legacy 选择器名称。
- 计算 Standard 泛型元数语义。
- 保留 Legacy 省略子句即任意元数和 <!>。
- 在 Standard 中创建有作用域的 $T 符号。
- 只按 Legacy 规则创建隐式泛型变量。
- 只有相关语法语义合法时才生成不可变绑定节点。

否定或 OR 分支内部的绑定不逃逸，避免匹配器变为多解查询引擎。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BinderTests|FullyQualifiedName~LanguageVersionTests"
git add src/AspectN/Binding src/AspectN/AspectNCompilationOptions.cs src/AspectN/AspectNCompilation.cs tests/AspectN.Tests/Binding
git commit -m "feat(binding): bind versioned AspectN semantics"
```

### 任务 11：添加三态求值与编译计划

**文件：**
- 新增： `src/AspectN/Evaluation/MatchStatus.cs`
- 新增： `src/AspectN/Evaluation/MatchResult.cs`
- 新增： `src/AspectN/Evaluation/MatchLogic.cs`
- 新增： `src/AspectN/Evaluation/EvaluationContext.cs`
- 新增： `src/AspectN/CompiledPattern.cs`
- 新增： `src/AspectN/CompiledMemberPattern.cs`
- 新增： `src/AspectN/CompiledTypePattern.cs`
- 新增： `tests/AspectN.Tests/Evaluation/MatchLogicTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/CompiledPatternTests.cs`

**步骤 1：将真值表全部行编码为测试**

AND/OR 各九行，NOT 三行，不使用简写的“任意”断言。

**步骤 2：测试计划不可变性与隔离**

并发对不同候选执行同一编译计划，绑定和诊断不能在调用间泄漏。

**步骤 3：实现**

MatchResult 包含：

```csharp
public MatchStatus Status { get; }
public IReadOnlyList<AspectNDiagnostic> Diagnostics { get; }
public bool ToBoolean(UnknownMatchBehavior behavior);
```

默认核心 API 不强制转换 Unknown，Rougamo 后续调用 ToBoolean(UnknownMatchBehavior.False)。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~MatchLogicTests|FullyQualifiedName~CompiledPatternTests"
git add src/AspectN/Evaluation src/AspectN/CompiledPattern.cs src/AspectN/CompiledMemberPattern.cs src/AspectN/CompiledTypePattern.cs tests/AspectN.Tests/Evaluation
git commit -m "feat(core): add three-state compiled evaluation"
```

### 任务 12：求值基础类型模式

**文件：**
- 新增： `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- 新增： `src/AspectN/Evaluation/NamePatternMatcher.cs`
- 新增： `tests/AspectN.Tests/Evaluation/NamePatternMatcherTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/TypePatternEvaluatorTests.cs`

**步骤 1：迁移确定性通配用例**

迁移 Rougamo 下列测试的意图：

- `Signatures/WildcardMatchTests.cs`。
- 命名空间 * 和 ..。
- 嵌套 /。
- 区分大小写的 Standard 名称。

不复制旧匹配器实现。

**步骤 2：添加数组、别名、分组、布尔类型运算与类型关系**

关系查询由 StubTypeSystem 返回 Matched/NotMatched/Unknown。

**步骤 3：实现**

通配匹配使用线性时间算法，不转为正则。关系遍历前检查取消。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~NamePatternMatcherTests|FullyQualifiedName~TypePatternEvaluatorTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): evaluate names and base type patterns"
```

### 任务 13：求值泛型和模式变量

**文件：**
- 修改： `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- 新增： `src/AspectN/Evaluation/BindingEnvironment.cs`
- 新增： `tests/AspectN.Tests/Evaluation/GenericPatternTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/PatternVariableTests.cs`

**步骤 1：先写元数及构造类型失败测试**

覆盖 <>、<,>、<..>、精确泛型实参、嵌套实参、原始定义和有效替换。

**步骤 2：编写变量测试**

```text
method($T Service.Convert<$T>($T))
```

首次正向出现绑定；后续使用 ITypeSystem.AreEqual。测试 AND 共享、OR 隔离、否定隔离和并发求值。

**步骤 3：实现求值局部环境**

字典以 VariableSymbol 为键，只在 OR/否定边界复制，从不在 MatchResult 中公开。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~GenericPatternTests|FullyQualifiedName~PatternVariableTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): add generics and unification variables"
```

### 任务 14：求值修饰符、引用分类、可空性、元组和任务类类型

**文件：**
- 修改： `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- 新增： `src/AspectN/Evaluation/ModifierMatcher.cs`
- 新增： `tests/AspectN.Tests/Evaluation/ModifierMatcherTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/RefKindTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/NullableTupleTasklikeTests.cs`

**步骤 1：迁移修饰符意图**

迁移 Rougamo SignatureBasicTests.cs 的可见性矩阵，以中立签名表达断言。

**步骤 2：添加原计划的 Standard 语义**

- private protected、protected internal 是复合可见性。
- 备选可见性要求表达式级 OR。
- 省略参数引用分类表示按值，但完整通配符可忽略引用分类。
- 返回类型只允许 ref、ref readonly。
- 引用 T? 等于 T，值类型 T? 等于 `Nullable<T>`。
- 元组语法只表示 ValueTuple。
- `tasklike<T>` 匹配 `Task<T>`/`ValueTask<T>`，与编译器 async 标记无关；Standard 没有 async 修饰符。

**步骤 3：添加 Legacy 语义**

- 空格分隔的可见性保持备选含义。
- (T1,T2) 接受 Tuple/ValueTuple。
- async T 匹配 `Task<T>`/`ValueTask<T>`，async null 匹配非泛型任务类类型。

**步骤 4：实现、通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~ModifierMatcherTests|FullyQualifiedName~RefKindTests|FullyQualifiedName~NullableTupleTasklikeTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): add modifiers ref kinds and type sugar"
```

### 任务 15：求值可调用、访问器及构造器选择器

**文件：**
- 新增： `src/AspectN/Evaluation/MemberPatternEvaluator.cs`
- 新增： `tests/AspectN.Tests/Evaluation/CallableSelectorTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/PropertySelectorTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/ConstructorSelectorTests.cs`

**步骤 1：编写适用域测试**

- callable 匹配普通方法、访问器、事件访问器、运算符和转换，不匹配构造器。
- execution 匹配所有方法类成员，包含两种构造器。
- method 只匹配普通方法。
- property 使用关联属性类型/名称/特性，匹配 getter/setter。
- getter/setter 缩小访问器种类。
- ctor/cctor 互斥。
- 选择器不隐含方法体存在。

**步骤 2：测试构造成员**

默认使用替换后的返回/参数类型匹配，原始定义保留给未来扩展。

**步骤 3：实现、通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~CallableSelectorTests|FullyQualifiedName~PropertySelectorTests|FullyQualifiedName~ConstructorSelectorTests"
git add src/AspectN/Evaluation/MemberPatternEvaluator.cs tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): evaluate member selectors"
```

### 任务 16：添加特性、正则安全及语义扩展

**文件：**
- 新增： `src/AspectN/Evaluation/AttributePatternEvaluator.cs`
- 新增： `src/AspectN/Evaluation/CanonicalSignatureFormatter.cs`
- 新增： `src/AspectN/Evaluation/RegexPatternEvaluator.cs`
- 新增： `src/AspectN/Extensibility/IAspectNExtension.cs`
- 新增： `src/AspectN/Extensibility/AspectNExtensionRegistry.cs`
- 新增： `src/AspectN/Extensibility/ExtensionContexts.cs`
- 新增： `tests/AspectN.Tests/Evaluation/AttributePatternTests.cs`
- 新增： `tests/AspectN.Tests/Evaluation/RegexPatternTests.cs`
- 新增： `tests/AspectN.Tests/Extensibility/ExtensionRegistryTests.cs`

**步骤 1：编写特性位置测试**

测试直接类型、可调用成员、关联属性、指定参数、任意参数、返回及任意位置，不考虑继承特性。

**步骤 2：编写规范正则测试**

核验与区域性无关的版本化签名、嵌套 /、泛型格式和引用前缀。用高回溯成本正则及很短测试超时，断言 Unknown/AN3003。

**步骤 3：编写扩展注册表测试**

构建器拒绝重复限定名称，构建后注册表不可变。注册表版本变化时缓存身份变化。缺少所需后端能力的扩展返回 Unknown。

**步骤 4：实现**

扩展必须将固定语法实参编译为不可变、线程安全的匹配器，不公开内部绑定节点。

**步骤 5：通过并提交**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~AttributePatternTests|FullyQualifiedName~RegexPatternTests|FullyQualifiedName~ExtensionRegistryTests"
git add src/AspectN/Evaluation src/AspectN/Extensibility tests/AspectN.Tests/Evaluation tests/AspectN.Tests/Extensibility
git commit -m "feat(core): add attributes regex safety and extensions"
```

### 任务 17：添加显式有界引擎缓存并固定核心 API

**文件：**
- 新增： `src/AspectN/AspectNEngine.cs`
- 新增： `src/AspectN/AspectNEngineOptions.cs`
- 新增： `src/AspectN/Caching/CompiledPatternCache.cs`
- 新增： `src/AspectN/PublicAPI.Shipped.txt`
- 新增： `src/AspectN/PublicAPI.Unshipped.txt`
- 修改： `src/AspectN/AspectN.csproj`
- 新增： `tests/AspectN.Tests/Caching/CompiledPatternCacheTests.cs`
- 新增： `tests/AspectN.Tests/PublicApiTests.cs`

**步骤 1：编写缓存测试**

核验：

- 用 LRU 淘汰维持容量上限。
- 源码、解析种类、语言版本、编译选项和扩展注册表身份均属于键。
- 没有静态缓存。
- 并发相同编译只产生一个缓存的不可变计划。
- 容量零禁用缓存。

**步骤 2：实现受锁保护的字典与链表**

不添加 MemoryCache 或其他依赖，缓存归 AspectNEngine 所有。

**步骤 3：锁定首个公共 API 基线**

填充 PublicAPI.Unshipped.txt，在 AspectN.csproj 添加私有 Microsoft.CodeAnalysis.PublicApiAnalyzers 引用并启用。反射测试证明核心程序集不引用适配器。

**步骤 4：运行全部核心测试**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release
dotnet build src/AspectN/AspectN.csproj -c Release
```

预期：两个 TFM 均通过且零警告。

**步骤 5：提交**

```powershell
git add src/AspectN tests/AspectN.Tests
git commit -m "feat(core): finalize cached compilation API"
```

### 任务 18：添加解析模糊/属性测试

**文件：**
- 新增： `tests/AspectN.Tests/Fuzz/ArbitrarySourceTests.cs`
- 新增： `tests/AspectN.Tests/Fuzz/ParserTerminationTests.cs`
- 新增： `tests/AspectN.Tests/Fuzz/SyntaxRoundTripTests.cs`

**步骤 1：编写 FsCheck 属性**

对有界大小的任意 Unicode 字符串：

- 除 OperationCanceledException 外，解析不抛异常。
- 在测试超时内完成。
- 全部诊断跨度在源码范围内。
- 规范合法示例重解析后保留语义树形。

**步骤 2：用确定性重放种子运行**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~Fuzz
```

预期：CI 配置下至少 10,000 个生成用例通过。

**步骤 3：最小化回归补为普通测试**

不能仅增加超时。

**步骤 4：提交**

```powershell
git add tests/AspectN.Tests/Fuzz
git commit -m "test(core): add parser property and termination coverage"
```

---

## 阶段 3：Mono.Cecil 与 Legacy 兼容

### 任务 19：建立 AspectN.Cecil

**文件：**
- 新增： `src/AspectN.Cecil/AspectN.Cecil.csproj`
- 新增： `src/AspectN.Cecil/CecilMappingOptions.cs`
- 新增： `src/AspectN.Cecil/CecilTypeSystem.cs`
- 新增： `src/AspectN.Cecil/CecilSignatureMapper.cs`
- 新增： `src/AspectN.Cecil/PublicAPI.Shipped.txt`
- 新增： `src/AspectN.Cecil/PublicAPI.Unshipped.txt`
- 新增： `tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj`
- 新增： `tests/AspectN.Cecil.Tests/TestModuleFactory.cs`
- 修改： `AspectN.sln`

**步骤 1：添加项目引用和程序包基线**

AspectN.Cecil 面向 netstandard2.0/net10.0，引用 AspectN 和 Mono.Cecil；测试面向 net10.0。

**步骤 2：先写失败的映射测试**

内存创建 Cecil 模块并映射 System.Int32，断言命名空间、名称、程序集身份和元数。

**步骤 3：实现模块级上下文**

CecilTypeSystem 拥有一个 ModuleDefinition，公开 CecilSignatureMapper，实现 ITypeSystem/IDisposable，不使用静态字典。

**步骤 4：通过，加入解决方案并提交**

```powershell
dotnet sln AspectN.sln add src/AspectN.Cecil/AspectN.Cecil.csproj tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release
git add AspectN.sln src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): scaffold module-scoped adapter"
```

### 任务 20：映射 Cecil 类型定义和引用

**文件：**
- 修改： `src/AspectN.Cecil/CecilSignatureMapper.cs`
- 新增： `src/AspectN.Cecil/Internal/CecilTypeCache.cs`
- 新增： `tests/AspectN.Cecil.Tests/CecilTypeMappingTests.cs`

**步骤 1：编写定义/引用测试**

覆盖：

- 顶层及嵌套类型。
- 泛型定义与实例。
- 类型/方法形参的声明者及位置。
- 交错及多维数组。
- 引用类型使用位置与元素类型分开。
- 未解析有名引用保留结构 NamedTypeSignature，名称匹配仍可用；需要解析的基类/接口/关系查询返回带诊断的 Unknown。
- Cecil 真正不支持的类型形状产生 UnsupportedTypeSignature 和诊断。
- 原始定义及有效构造实参。

**步骤 2：实现映射**

缓存限于 CecilTypeSystem 生命周期。安全时按 Cecil 引用身份建键；引用会重建时使用稳定完整身份，不能只按 typeRef.ToString() 缓存。

**步骤 3：测试释放后不保留模块**

去掉强引用，在有界重试循环强制 GC，断言适配器释放后模块弱引用可以失效。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release --filter FullyQualifiedName~CecilTypeMappingTests
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): map type definitions and references"
```

### 任务 21：实现 Cecil 相等、类型关系及解析引用诊断

**文件：**
- 修改： `src/AspectN.Cecil/CecilTypeSystem.cs`
- 新增： `src/AspectN.Cecil/Internal/CecilResolver.cs`
- 新增： `tests/AspectN.Cecil.Tests/CecilTypeSystemTests.cs`

**步骤 1：编写关系测试**

测试精确类型、基类、接口、构造泛型身份、程序集缺失、解析器异常及取消。

**步骤 2：实现带保护的遍历**

跟踪已访问类型身份避免循环。普通 Resolve() 失败转为 Unknown/AN3004，不吞取消。

**步骤 3：在两个 Cecil 版本运行**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.5
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6
```

预期：均通过。

**步骤 4：提交**

```powershell
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): implement type relationships"
```

### 任务 22：映射 Cecil 成员、访问器、特性与引用分类

**文件：**
- 修改： `src/AspectN.Cecil/CecilSignatureMapper.cs`
- 新增： `src/AspectN.Cecil/Internal/CecilAccessibility.cs`
- 新增： `src/AspectN.Cecil/Internal/CecilAsyncDetector.cs`
- 新增： `src/AspectN.Cecil/Internal/CecilRefKindDetector.cs`
- 新增： `tests/AspectN.Cecil.Tests/CecilCallableMappingTests.cs`
- 新增： `tests/AspectN.Cecil.Tests/CecilAccessorMappingTests.cs`

**步骤 1：创建发射测试程序集**

包含普通方法、运算符、转换、构造器、async 方法、泛型实例、只有 getter/只有 setter/索引器属性、事件访问器、直接特性及全部支持的引用分类。

**步骤 2：测试原始和复合可见性**

CecilMappingOptions.CompositeAccessibility 复现旧 Rougamo 行为，不成为语言规则。缓存键包含该选项。

**步骤 3：实现关联**

属性访问器关联 PropertySignature，方法特性与属性特性分开。Legacy 绑定后续为 attr(exec ...) 组合它们。

**步骤 4：通过并提交**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release --filter "FullyQualifiedName~CecilCallableMappingTests|FullyQualifiedName~CecilAccessorMappingTests"
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): map callables accessors and attributes"
```

### 任务 23：完成 Legacy 语法及语义兼容

**文件：**
- 新增： `src/AspectN/Binding/Legacy/LegacyBinder.cs`
- 新增： `src/AspectN/Binding/Legacy/LegacyGenericRules.cs`
- 新增： `src/AspectN/Binding/Legacy/LegacyAttributeRules.cs`
- 新增： `src/AspectN/Binding/Legacy/LegacyModifierRules.cs`
- 新增： `src/AspectN/Evaluation/Legacy/LegacyCanonicalSignatureFormatter.cs`
- 新增： `tests/AspectN.Tests/Legacy/LegacySyntaxTests.cs`
- 新增： `tests/AspectN.Tests/Legacy/LegacySemanticTests.cs`

**步骤 1：迁移已批准 Legacy 类别**

迁移行为，不复制实现：

- execution、method、property、getter、setter、ctor、cctor、attr、regex。
- 旧修饰符短语及可见性集合行为。
- 隐式泛型变量和虚拟命名。
- 通配参数槽及元数。
- 元组/可空/async 行为。
- 历史正则格式。

**步骤 2：将已知旧解析偶然行为加入拒绝测试**

只把有文档或当前测试的合法语法作为兼容范围。意外无效输入提供诊断，不保留旧 ArgumentException 文案。

**步骤 3：实现版本隔离辅助方法**

不在 Standard 求值器中散布 if (Legacy)。Legacy 归一和例外行为放在 Binding/Legacy、Evaluation/Legacy。

**步骤 4：运行全部核心及 Cecil 测试**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release
```

**步骤 5：提交**

```powershell
git add src/AspectN/Binding/Legacy src/AspectN/Evaluation/Legacy tests/AspectN.Tests/Legacy
git commit -m "feat(legacy): implement Shared AspectN semantics"
```

### 任务 24：建立固定外部 Legacy 对照测试框架

**文件：**
- 新增： `eng/prepare-legacy-oracle.ps1`
- 新增： `tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj`
- 新增： `tests/AspectN.TestAssembly/LegacyFixture.cs`
- 新增： `tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj`
- 新增： `tests/AspectN.LegacyOracle.Tests/LegacyOracleRevisionTests.cs`
- 新增： `tests/AspectN.LegacyOracle.Tests/LegacyDifferentialTests.cs`
- 新增： `tests/AspectN.LegacyOracle.Tests/legacy-patterns.json`
- 新增： `tests/AspectN.LegacyOracle.Tests/approved-differences.json`
- 新增： `tests/AspectN.LegacyOracle.Tests/README.md`
- 修改： `AspectN.sln`

**步骤 1：实现准备脚本**

脚本克隆 https://github.com/inversionhourglass/Shared.Cecil.AspectN.git 到忽略目录 artifacts/legacy-oracle/Shared.Cecil.AspectN，精确检出 03a9d1030f92a64449b6220eb45d7198d31653db；目录脏或版本不符时失败。

支持 -OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN，通过固定提交的 Git archive 提取，不读取脏工作树。克隆和离线模式都写 .aspectn-oracle-commit，测试核验其等于固定完整 SHA。

**步骤 2：仅在测试程序集引入旧源码**

AspectN.LegacyOracle.Tests.csproj 定义确定性默认值，只导入测试代码：

```xml
<PropertyGroup>
  <LegacyOracleRoot Condition="'$(LegacyOracleRoot)' == ''">$(MSBuildThisFileDirectory)..\..\artifacts\legacy-oracle\Shared.Cecil.AspectN</LegacyOracleRoot>
</PropertyGroup>
<Import Project="$(LegacyOracleRoot)\Cecil.AspectN.projitems"
        Label="FrozenLegacyOracle"
        Condition="Exists('$(LegacyOracleRoot)\Cecil.AspectN.projitems')" />
```

项目引用 AspectN.Cecil 和 Mono.Cecil。生产项目不引用/导入旧源码。

**步骤 3：建立确定性模式种子**

覆盖全部选择器族，以及以下目录的模式类别：

- `D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Signatures`
- `D:\Code\Hourglass\rougamo-eco\AspectN.Tests\test\Tests\Cases`
- Rougamo `test\TestAssemblies\PatternUsage\Attributes`

所需测试类型迁入 tests/AspectN.TestAssembly/LegacyFixture.cs，不引用相邻仓库构建产物。避免随机候选选择；稳定候选 ID 使用 `<assembly>::<declaring-type>::<metadata-token>`。

**步骤 4：编写差分断言**

针对每个模式/候选/选项元组：

```csharp
Assert.Equal(
    oldMatcher.IsMatch(oldSignature),
    newResult.Status == MatchStatus.Matched);
Assert.NotEqual(MatchStatus.Unknown, newResult.Status);
```

按完整元组查找已批准修正，并要求非空原因。

**步骤 5：运行并提交**

```powershell
powershell -NoProfile -File eng/prepare-legacy-oracle.ps1 -OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN
dotnet sln AspectN.sln add tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release
git add eng/prepare-legacy-oracle.ps1 tests/AspectN.TestAssembly tests/AspectN.LegacyOracle.Tests AspectN.sln
git commit -m "test(legacy): add pinned differential oracle"
```

### 任务 25：达到零未批准 Cecil Legacy 差异

**文件：**
- 修改： `tests/AspectN.LegacyOracle.Tests/legacy-patterns.json`
- 修改： `tests/AspectN.LegacyOracle.Tests/approved-differences.json`
- 修改： 最小化失败定位到的匹配/绑定文件
- 新增： `docs/migration/legacy-compatibility-report.md`
- 新增： `tests/AspectN.LegacyOracle.Tests/packages.lock.json`

**步骤 1：扩展候选语料**

运行范围：

- 确定性本地测试程序集。
- Rougamo SignatureUsage/PatternUsage 程序集。
- AspectN.Tests 之前使用过的固定公共程序包程序集集合。

报告记录包名/版本；启用 RestorePackagesWithLockFile，提交 packages.lock.json，使用锁定还原运行对照，不依赖“最新”版本。

**步骤 2：最小化全部差异**

改代码前，将每个差异转换为聚焦单元测试。

**步骤 3：修正必须经维护者批准**

差异为旧 bug 或安全问题时，先请求批准，再加入 approved-differences.json。

**步骤 4：产出报告**

包含对照提交、语料哈希、模式/候选对数量、已批准差异、运行时和平台。

**步骤 5：闸门及提交**

运行：

```powershell
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release
```

预期：零未批准差异，零 Unknown 结果。

```powershell
git add src tests/AspectN.LegacyOracle.Tests docs/migration/legacy-compatibility-report.md
git commit -m "test(legacy): reach zero unapproved Cecil differences"
```

---


## 阶段 4：Reflection、Roslyn 与跨适配器一致性

### 任务 26：实现 AspectN.Reflection

**文件：**
- 新增： `src/AspectN.Reflection/AspectN.Reflection.csproj`
- 新增： `src/AspectN.Reflection/ReflectionTypeSystem.cs`
- 新增： `src/AspectN.Reflection/ReflectionSignatureMapper.cs`
- 新增： `src/AspectN.Reflection/Internal/ReflectionTypeCache.cs`
- 新增： `src/AspectN.Reflection/PublicAPI.Shipped.txt`
- 新增： `src/AspectN.Reflection/PublicAPI.Unshipped.txt`
- 新增： `tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj`
- 新增： `tests/AspectN.Reflection.Tests/ReflectionMappingTests.cs`
- 新增： `tests/AspectN.Reflection.Tests/CollectibleAssemblyTests.cs`
- 修改： `AspectN.sln`

**步骤 1：编写映射测试**

覆盖 Type、MethodBase、MethodInfo、ConstructorInfo、PropertyInfo、泛型定义/构造成员、引用分类、直接自定义特性数据、async 检测和关联访问器。

**步骤 2：不实例化特性**

使用 CustomAttributeData，反向查找缓存只存储 `WeakReference<Type>`/`WeakReference<MemberInfo>`。

**步骤 3：测试可回收 ALC 卸载**

将发射测试程序集加载到可回收 AssemblyLoadContext，映射并匹配；释放全部调用方/适配器引用，卸载并断言 ALC 弱引用失效。

**步骤 4：通过并提交**

```powershell
dotnet sln AspectN.sln add src/AspectN.Reflection/AspectN.Reflection.csproj tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj
dotnet test tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj -c Release
git add AspectN.sln src/AspectN.Reflection tests/AspectN.Reflection.Tests
git commit -m "feat(reflection): add collectible-safe adapter"
```

### 任务 27：实现以符号为主的 AspectN.Roslyn

**文件：**
- 新增： `src/AspectN.Roslyn/AspectN.Roslyn.csproj`
- 新增： `src/AspectN.Roslyn/RoslynTypeSystem.cs`
- 新增： `src/AspectN.Roslyn/RoslynSignatureMapper.cs`
- 新增： `src/AspectN.Roslyn/RoslynSyntaxExtensions.cs`
- 新增： `src/AspectN.Roslyn/PublicAPI.Shipped.txt`
- 新增： `src/AspectN.Roslyn/PublicAPI.Unshipped.txt`
- 新增： `tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj`
- 新增： `tests/AspectN.Roslyn.Tests/RoslynMappingTests.cs`
- 新增： `tests/AspectN.Roslyn.Tests/RoslynSyntaxEntryPointTests.cs`
- 修改： `AspectN.sln`

**步骤 1：编写符号优先测试**

主要映射重载接受 ITypeSymbol、IMethodSymbol、IPropertySymbol。测试原始定义、构造符号、元组归一、引用可空性擦除、引用分类、IsAsync、特性、访问器、运算符和转换。

**步骤 2：编写语法便利入口测试**

TypeDeclarationSyntax/MethodDeclarationSyntax 重载要求 SemanticModel 或 Compilation，不提供只接收语法的重载。

**步骤 3：缓存范围绑定 Compilation**

适配器拥有 Compilation 及缓存；丢弃适配器后释放编译图。

**步骤 4：测试基线与当前 Roslyn**

```powershell
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=4.12.0
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0
```

**步骤 5：提交**

```powershell
dotnet sln AspectN.sln add src/AspectN.Roslyn/AspectN.Roslyn.csproj tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj
git add AspectN.sln src/AspectN.Roslyn tests/AspectN.Roslyn.Tests
git commit -m "feat(roslyn): add Symbol-first adapter"
```

### 任务 28：添加同一源码的跨适配器一致性

**文件：**
- 修改： `tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj`
- 新增： `tests/AspectN.TestAssembly/ConformanceFixture.cs`
- 新增： `tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj`
- 新增： `tests/AspectN.Conformance.Tests/FixtureCompiler.cs`
- 新增： `tests/AspectN.Conformance.Tests/CrossAdapterConformanceTests.cs`
- 新增： `tests/AspectN.Conformance.Tests/conformance-cases.json`
- 修改： `AspectN.sln`

**步骤 1：创建同一源码测试程序集**

含嵌套/构造泛型、数组、元组、可空值/引用类型、全部引用分类、async 和非 async 任务类方法、全部可见性、直接特性、属性/索引器/事件、运算符、转换、构造器及未解析外部引用。

**步骤 2：通过三个后端检查**

- 发射前的 Roslyn。
- 发射 PE 的 Cecil。
- 可回收加载上下文中的 Reflection。

**步骤 3：断言规范签名与匹配一致**

每个案例逐字段比较中立签名，并运行相同 Standard 表达式。等价事实状态一致；预期缺失数据明确为 Unknown。

**步骤 4：运行并提交**

```powershell
dotnet sln AspectN.sln add tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release
git add AspectN.sln tests/AspectN.TestAssembly tests/AspectN.Conformance.Tests
git commit -m "test: add cross-adapter conformance suite"
```

---

## 阶段 5：打包、兼容性与持续集成

### 任务 29：验证 NuGet 程序包和 .NET Framework 4.7.2

**文件：**
- 新增： `tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj`
- 新增： `tests/AspectN.NetFramework.Tests/NetFrameworkSmokeTests.cs`
- 新增： `eng/verify-packages.ps1`
- 新增： `README.md`
- 新增： `docs/language/README.md`
- 新增： `docs/migration/shared-cecil-to-aspectn.md`
- 修改： each `src/*/*.csproj`
- 修改： `AspectN.sln`

**步骤 1：添加 net472 使用方**

引用四个项目而非内部实现，运行解析/编译及 Reflection 匹配；私有引用 Microsoft.NETFramework.ReferenceAssemblies.net472。

**步骤 2：补齐程序包元数据**

各包独立描述/标签、项目和仓库 URL、MIT 许可证表达式、README、符号包及 XML 文档。适配器只依赖 AspectN 和对应原生依赖。

**步骤 3：实现程序包验证**

eng/verify-packages.ps1：

- Release 打包到 artifacts/packages。
- 打开每个 .nupkg。
- 检查 lib/netstandard2.0 与 lib/net10.0 资产。
- 核心不允许 Cecil/Roslyn/Reflection 依赖。
- 适配器不允许相互依赖。
- 从程序包构建临时使用方。
- 遇到警告时失败。

**步骤 4：运行**

```powershell
dotnet test tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj -c Release -f net472
powershell -NoProfile -File eng/verify-packages.ps1
```

预期：Windows 通过。

**步骤 5：提交**

```powershell
git add AspectN.sln src tests/AspectN.NetFramework.Tests eng/verify-packages.ps1 README.md docs/language docs/migration
git commit -m "build: validate packages and net472 compatibility"
```

### 任务 30：添加 CI、兼容矩阵与程序包产物

**文件：**
- 新增： `.github/workflows/ci.yml`
- 新增： `.github/workflows/compatibility.yml`
- 新增： `.github/workflows/release.yml`
- 新增： `.github/dependabot.yml`

**步骤 1：添加常规 CI**

PR 和推送时：

- 还原。
- dotnet format --verify-no-changes。
- Release 构建。
- Windows/Ubuntu 核心、适配器和一致性测试。
- Windows net472 测试。
- 程序包验证。
- 上传 TRX 和程序包。

**步骤 2：添加兼容性 CI**

矩阵：

- Cecil 0.11.5/0.11.6。
- Roslyn 4.12.0/5.6.0。
- Legacy 对照固定检出 03a9d10。

**步骤 3：发布流程不自动发布**

标签流程构建并签名/验证产物，在 dotnet nuget push 前要求受保护 GitHub 环境。实现期间不配置或调用凭据。

**步骤 4：本地核验流程命令**

运行流程使用的全部 shell 命令。已有 YAML 检查工具时使用，不只为检查引入 Node 工具链。

**步骤 5：提交**

```powershell
git add .github
git commit -m "ci: add build compatibility and release gates"
```

---

## 阶段 6：Rougamo 迁移

### 任务 31：准备本地预发布包和 Rougamo 迁移分支

**仓库：**
- AspectN：`D:\Code\Hourglass\AspectN`
- Rougamo：`D:\Code\Hourglass\rougamo-eco\Rougamo`

**文件：**
- 在 Rougamo 新增： `src/Rougamo.Fody/AspectN/RougamoAspectNMatcher.cs`
- 在 Rougamo 新增： `test/Rougamo.Fody.Tests/AspectN/RougamoAspectNMatcherTests.cs`
- 在 Rougamo 修改： `src/Rougamo.Fody/Rougamo.Fody.csproj`

**步骤 1：打包，不发布**

```powershell
dotnet pack D:\Code\Hourglass\AspectN\AspectN.sln -c Release -p:Version=0.1.0-rougamo.1 -o D:\Code\Hourglass\AspectN\artifacts\packages
```

**步骤 2：直接创建 Rougamo 分支**

确认 Rougamo 干净后运行：

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo switch -c codex/aspectn-migration
```

不创建 worktree。

**步骤 3：添加临时还原源及引用**

添加、还原程序包，不提交机器绝对路径源：

```powershell
dotnet add D:\Code\Hourglass\rougamo-eco\Rougamo\src\Rougamo.Fody\Rougamo.Fody.csproj package AspectN.Cecil --version 0.1.0-rougamo.1 --no-restore
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --source D:\Code\Hourglass\AspectN\artifacts\packages --source https://api.nuget.org/v3/index.json
```

**步骤 4：先写统一入口测试**

每次织入拥有一个 AspectNEngine/CecilTypeSystem，按 AspectNLanguageVersion.Legacy 编译，显式将 Unknown 转 false，并接受 compositeAccessibility。

**步骤 5：实现入口并使测试通过**

暂保留生产 PatternParser 调用，本任务只引入并测试新路径。

**步骤 6：在 Rougamo 提交**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src/Rougamo.Fody test/Rougamo.Fody.Tests
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "test: add AspectN migration facade"
```

### 任务 32：差分运行新旧 Rougamo 引擎

**文件：**
- 在 Rougamo 修改： `src/Rougamo.Fody/ModuleWeaver.cs`
- 在 Rougamo 修改： `src/Rougamo.Fody/FindRous.cs`
- 在 Rougamo 修改： `src/Rougamo.Fody/Extensions/ModelExtension.cs`
- 在 Rougamo 新增： `src/Rougamo.Fody/AspectN/AspectNDifferentialMatcher.cs`
- 在 Rougamo 新增： `test/Rougamo.Fody.Tests/AspectN/AspectNDifferentialTests.cs`

**步骤 1：注入织入级生命周期**

ExecuteInternal 内创建/释放入口，经 FindRous 传入 ModelExtension.Initialize，不使用静态状态。

**步骤 2：保留旧生产结果**

差分模式计算双方结果，差异时报告确定性失败/日志，但返回旧引擎结果，以便切换前证明一致性。

**步骤 3：运行完整矩阵**

至少：

```powershell
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461
```

预期：零差异，全部织入测试通过。

**步骤 4：记录迁移报告**

在 Rougamo 创建 docs/aspectn-migration-report.md，记录包哈希、模式数、候选数、框架矩阵及基准比较。

**步骤 5：提交**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src/Rougamo.Fody test/Rougamo.Fody.Tests docs/aspectn-migration-report.md
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "test: prove AspectN parity in Rougamo"
```

### 任务 33：Rougamo 生产匹配切换到 AspectN

**文件：**
- 在 Rougamo 修改： `src/Rougamo.Fody/Extensions/ModelExtension.cs`
- 在 Rougamo 修改： `src/Rougamo.Fody/AspectN/RougamoAspectNMatcher.cs`
- 在 Rougamo 修改： 签名测试位于 `test/Rougamo.Fody.Tests/Signatures`
- 在 Rougamo 新增： `test/Rougamo.Fody.Tests/AspectN/AspectNIntegrationTests.cs`

**步骤 1：先写生产路径集成测试**

用真实 PointcutAttribute 模式验证普通方法、属性、构造器、async、特性和复合可见性只织入一次。

**步骤 2：返回结果切换为新引擎**

上一步全绿后才移除生产差分代码，保留明确 Unknown→false 兼容策略及诊断日志。

**步骤 3：迁移底层测试**

旧解析内部行为已进入 AspectN 核心/对照测试后，删除仅测试旧内部实现的 Rougamo 测试；保留 Rougamo 级集成/织入测试。

**步骤 4：运行全部 Rougamo 测试**

运行支持矩阵，含 net461 冒烟分支，不宣称 AspectN 官方支持 net461。

**步骤 5：提交**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src test
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "feat: migrate Rougamo matching to AspectN"
```

### 任务 34：发布 RC 检查点后移除 Rougamo 的 Cecil.AspectN 子模块

**文件：**
- 在 Rougamo 修改： `src/Rougamo.Fody/Rougamo.Fody.csproj`
- 在 Rougamo 修改： `.gitmodules`
- 在 Rougamo 修改： `Rougamo.sln`
- 在 Rougamo 修改： `src/Rougamo/Metadatas/PointcutAttribute.cs`
- 在 Rougamo 移除： `shared/Cecil.AspectN`

**步骤 1：请求 RC 发布批准**

提供精确 AspectN 提交、1.0.0-rc.1 包 SHA-256、兼容/一致性结果及 Rougamo 切换提交。维护者明确批准前不推送、打标签或发布。

批准后通过受保护流程发布全部四个 1.0.0-rc.1 包。干净临时使用方从公开 NuGet 源还原，核验包身份、依赖、Source Link、符号和签名。

**步骤 2：Rougamo 从本地开发包改为公开 RC**

将 AspectN.Cecil 的 0.1.0-rougamo.1 改为 1.0.0-rc.1。确认提交的源不引用 D:\Code\Hourglass\AspectN\artifacts\packages，再做干净公开源还原与完整 Rougamo 测试：

```powershell
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --force-evaluate --source https://api.nuget.org/v3/index.json
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0 --no-restore
```

干净公开源验证通过前不移除子模块。

**步骤 3：核验精确破坏性目标**

确认：

- shared/Cecil.AspectN 解析到 Rougamo 根目录内。
- 版本为对照提交 03a9d10。
- 没有未提交文件。
- shared/Fody.Simulations 是另一子模块，保持不变。

**步骤 4：仅移除 Cecil 子模块**

对已核验路径使用原生 Git 子模块命令：

```powershell
$rougamoRoot = [System.IO.Path]::GetFullPath('D:\Code\Hourglass\rougamo-eco\Rougamo')
$submodulePath = [System.IO.Path]::GetFullPath((Join-Path $rougamoRoot 'shared\Cecil.AspectN'))
if (-not $submodulePath.StartsWith($rougamoRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe submodule path.' }
git -C $rougamoRoot submodule deinit -f -- shared/Cecil.AspectN
git -C $rougamoRoot rm -f -- shared/Cecil.AspectN
```

随后只移除对应 .gitmodules 段及解决方案 Shared Project 项/导入映射。保留已验证 AspectN.Cecil 1.0.0-rc.1 引用。未跟踪的 .git/modules/shared/Cecil.AspectN 元数据，只能在精确解析并确认位于 Rougamo Git 目录内之后移除。

**步骤 5：更新文档链接**

PointcutAttribute 备注和 Rougamo 文档指向 https://github.com/AspectN/AspectN。

**步骤 6：检查无旧引用**

```powershell
rg -n "Cecil\.AspectN|Shared\.Cecil\.AspectN|shared/Cecil\.AspectN|shared\\Cecil\.AspectN" D:\Code\Hourglass\rougamo-eco\Rougamo
```

预期：生产、测试、项目、解决方案及文档均无引用。

**步骤 7：完整构建/测试并提交**

```powershell
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add -- .gitmodules Rougamo.sln src/Rougamo.Fody/Rougamo.Fody.csproj src/Rougamo/Metadatas/PointcutAttribute.cs
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "build: remove Shared.Cecil.AspectN submodule"
```

删除后说明子模块内容仍可从 03a9d10 和归档仓库恢复。

---

## 阶段 7：性能、发布及归档闸门

### 任务 35：添加基准与审查过的性能基线

**文件：**
- 新增： `benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj`
- 新增： `benchmarks/AspectN.Benchmarks/Program.cs`
- 新增： `benchmarks/AspectN.Benchmarks/ParsingBenchmarks.cs`
- 新增： `benchmarks/AspectN.Benchmarks/MatchingBenchmarks.cs`
- 新增： `benchmarks/AspectN.Benchmarks/AdapterBenchmarks.cs`
- 新增： `benchmarks/AspectN.Benchmarks/LegacyComparisonBenchmarks.cs`
- 新增： `docs/performance/1.0-baseline.md`
- 修改： `AspectN.sln`

**步骤 1：对必需路径建立基准**

- 词法/解析。
- 绑定/编译。
- 缓存热匹配。
- 密集类型关系查询匹配。
- Cecil/Roslyn/Reflection 映射。
- 旧对照与新 Legacy Cecil 路径比较。

**步骤 2：使用代表性参数规模**

含短、典型、长表达式，冷/热适配器缓存和已解析/未解析引用。

**步骤 3：运行 Release 基准**

```powershell
dotnet run -c Release --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -- --filter "*"
```

**步骤 4：记录环境和结果**

不建立脆弱 CI 微基准阈值。以已审查 BenchmarkDotNet 报告作为闸门，对 Rougamo 热路径任何统计显著退化给出解释。

**步骤 5：提交**

```powershell
git add AspectN.sln benchmarks docs/performance
git commit -m "perf: add AspectN 1.0 benchmark baseline"
```

### 任务 36：补齐文档和 1.0 发布清单

**文件：**
- 修改： `README.md`
- 新增： `CHANGELOG.md`
- 新增： `docs/language/standard.md`
- 新增： `docs/language/legacy.md`
- 新增： `docs/adapters/cecil.md`
- 新增： `docs/adapters/roslyn.md`
- 新增： `docs/adapters/reflection.md`
- 新增： `docs/extensibility.md`
- 新增： `docs/diagnostics.md`
- 新增： `docs/release/1.0-checklist.md`

**步骤 1：说明公开使用方式**

展示解析诊断、编译、匹配、Unknown、引擎缓存所有权和三个适配器入口。新示例不使用旧 CLR API 名称。

**步骤 2：说明迁移**

包含：

```text
execution(...) (unchanged; covers constructors as well)
property(...)  (unchanged)
privateprotected -> private protected
protectedinternal -> protected internal
tasklike<void> -> tasklike
attr(callable ...) -> attr(call ...)
attr(property ...) -> attr(prop ...)
attr(parameter ...) -> attr(para ...)
attr(return ...) -> attr(ret ...)
implicit T -> $T
```

保留 ctor(...)、cctor(...)。

**步骤 3：完成发布清单**

要求：

- 全部测试/兼容矩阵通过。
- 零未批准 Legacy 差异。
- 程序包验证及 API 文件干净。
- net472 通过，Rougamo net461 冒烟通过。
- 跨适配器一致性通过。
- 正则超时/模糊测试通过。
- 基准报告已审查。
- 无全局可变缓存。
- 核心公共 API 无原生后端类型。
- 无 Rougamo 子模块引用。

**步骤 4：构建最终候选，不发布**

```powershell
dotnet clean AspectN.sln -c Release
dotnet restore AspectN.sln
dotnet format AspectN.sln --verify-no-changes
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build
powershell -NoProfile -File eng/verify-packages.ps1
```

**步骤 5：提交**

```powershell
git add README.md CHANGELOG.md docs
git commit -m "docs: complete AspectN 1.0 documentation"
```

### 任务 37：维护者明确批准后才发布

**文件：**
- 批准后修改： `src/*/PublicAPI.Shipped.txt`
- 批准后修改： `CHANGELOG.md`
- 发布后在 Rougamo 修改： `src/Rougamo.Fody/Rougamo.Fody.csproj`
- 批准后打标签： `v1.0.0`

**步骤 1：请求批准**

提供：

- 精确提交。
- 包 SHA-256。
- 测试/兼容性汇总。
- Legacy 差异报告。
- 基准报告。
- Rougamo 迁移提交。

批准前不推送、打标签、发布或创建发布版本。

**步骤 2：提升 API 基线**

将各 PublicAPI.Unshipped.txt 已批准项移入 PublicAPI.Shipped.txt。

**步骤 3：重建精确产物**

从批准提交的干净检出构建并核对哈希。

**步骤 4：打标签/发布**

明确批准后创建签名/注释的 v1.0.0，推送分支/标签，批准受保护发布流程。

**步骤 5：Rougamo 从 RC 升为稳定版**

全部四个 1.0.0 包从公开 NuGet 源可见并验证后，将 Rougamo 的 AspectN.Cecil 从 1.0.0-rc.1 改为 1.0.0。提交前做干净还原、构建及完整测试：

```powershell
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --force-evaluate --source https://api.nuget.org/v3/index.json
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0 --no-restore
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add -- src/Rougamo.Fody/Rougamo.Fody.csproj
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "build: use AspectN 1.0"
```

仍引用 RC 时不得发布 Rougamo。

### 任务 38：安全归档 Shared.Cecil.AspectN

**仓库：**
- `D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN`

**文件：**
- 协调维护者既有改动后才修改： `README.md`
- 修改： `README_en.md`

**步骤 1：保留用户当前工作**

README.md 有未提交中文补充。先询问维护者要提交、整合到归档声明，还是放到其他分支，绝不覆盖或丢弃。

**步骤 2：在直接分支添加归档声明**

声明：

- 生产源码冻结在 03a9d10。
- 后继项目为 AspectN。
- Rougamo 不再使用该子模块。
- 历史保留供兼容性调查。

**步骤 3：核验链接并提交**

仅提交文档，不修改旧 .cs/.projitems。

**步骤 4：仓库所有者归档检查点**

AspectN 1.0 和 Rougamo 迁移公开后，请仓库所有者归档 GitHub 仓库。归档是明确所有者操作，不是自动实施步骤。

---

## 最终验证矩阵

声明原实施完成前运行全部行：

| 范围 | 命令/闸门 | 预期 |
|---|---|---|
| 核心 | `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release` | 通过 |
| Cecil 基线 | `dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.5` | 通过 |
| 当前 Cecil | `dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6` | 通过 |
| Roslyn 基线 | `dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=4.12.0` | 通过 |
| 当前 Roslyn | `dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0` | 通过 |
| Reflection 卸载 | `dotnet test tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj -c Release --filter FullyQualifiedName~CollectibleAssemblyTests` | 通过 |
| 跨适配器 | `dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release` | 仅等价/批准的 Unknown |
| Legacy 对照 | `dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release` | 零未批准差异 |
| .NET Framework | `dotnet test tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj -c Release -f net472` | 通过 |
| Rougamo 冒烟 | `dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461` | 通过，非官方支持 |
| 程序包 | `powershell -NoProfile -File .\eng\verify-packages.ps1` | 四个有效包 |
| 格式 | `dotnet format AspectN.sln --verify-no-changes` | 无变化 |
| API | `dotnet build AspectN.sln -c Release -p:RunAnalyzers=true -p:TreatWarningsAsErrors=true` | 无未批准变化 |
| 性能 | `dotnet run --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release -- --filter *` 并审查 docs/performance/1.0-baseline.md | 热路径无未解释退化 |

## 完成定义

原计划只有以下全部满足才完成：

1. 四个程序包均面向 netstandard2.0/net10.0 构建。
2. Standard 语法/语义符合语言规范。
3. 固定对照版本下，Legacy Cecil 行为零未批准差异。
4. 等价 Roslyn/Cecil/Reflection 候选得到一致结果。
5. Rougamo 使用程序包 AspectN.Cecil，不再包含 Cecil.AspectN 子模块。
6. 发布清单获批，Shared 旧仓库归档且保留既有 README 工作。
