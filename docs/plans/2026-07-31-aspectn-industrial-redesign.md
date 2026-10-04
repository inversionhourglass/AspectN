# AspectN Industrial Redesign Implementation Plan

> Scope update (2026-10-05): custom semantic extensions were removed by user decision. Extension tasks and API examples below are historical and are superseded by the current language specification and `2026-10-05-review-ds-decisions.md`.

> **Superseded requirements (2026-10-03):** The [single-language plan](2026-10-03-single-language-aspectn.md) replaces this plan's Legacy mode, oracle parity, compatibility facade and approved-differences requirements. Historical tasks below remain as context; current API and verification follow the single-language contract. Rougamo migration and publication remain separate future work.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a production-grade, backend-neutral AspectN matcher with versioned `Legacy` and `Standard` languages, adapters for Mono.Cecil, Roslyn, and Reflection, and a zero-unapproved-difference migration path for Rougamo.

**Architecture:** The `AspectN` core owns source text, lexer, immutable syntax, binding, backend-neutral signatures, compiled plans, three-state evaluation, extensions, diagnostics, and bounded caching. `AspectN.Cecil`, `AspectN.Roslyn`, and `AspectN.Reflection` translate native type systems into the common model and implement `ITypeSystem`; no adapter type enters the core public API. The frozen Shared.Cecil.AspectN commit `03a9d1030f92a64449b6220eb45d7198d31653db` remains the Legacy differential oracle until the 1.0 release gate passes.

**Tech Stack:** .NET SDK 10.0.302, C# latest, `netstandard2.0;net10.0` libraries, xUnit 2.9.3, FsCheck.Xunit 3.3.4, Mono.Cecil 0.11.5 baseline/0.11.6 compatibility lane, Microsoft.CodeAnalysis.CSharp 4.12.0 baseline/5.6.0 compatibility lane, BenchmarkDotNet 0.15.8, GitHub Actions.

---

## Execution Constraints

- Work directly in `D:\Code\Hourglass\AspectN` on `codex/aspectn-redesign-spec`. The maintainer explicitly does not want Git worktrees.
- Treat these two written specifications as normative:
  - `docs/superpowers/specs/2026-07-31-aspectn-redesign-design.md`
  - `docs/superpowers/specs/2026-07-31-aspectn-language-spec.md`
- Do not cherry-pick or edit `D:\Code\Hourglass\rougamo-eco\AspectN`. It is a historical prototype with two unpushed commits and extensive uncommitted work; it is reference-only and not an implementation base.
- Do not change production code in `D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN`. Its `README.md` already contains maintainer edits and must remain untouched until the final archive task.
- Use `03a9d1030f92a64449b6220eb45d7198d31653db` as the only Legacy oracle revision. Fail tests when another revision is supplied.
- Do not silently approve Legacy differences. Every approved correction must be recorded in `tests/AspectN.LegacyOracle.Tests/approved-differences.json` with pattern, candidate, old result, new result, and rationale.
- Keep commits small and scoped to one task. Run `dotnet format --verify-no-changes`, the narrow test project, and then the solution test before each phase-ending commit.
- Do not publish packages, push branches, remove the Rougamo submodule, or archive the Shared repository without the explicit checkpoint in this plan.

## Target Repository Layout

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

## Public API Baseline

Implementation may add internal types, but these public roles and dependency directions are fixed:

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

`AspectNEngine` is the optional bounded-cache facade around these explicit stages. Parsing and compilation remain usable without an engine.

---

## Phase 1 — Repository and Core Foundations

### Task 1: Bootstrap the solution and central build policy

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `AspectN.sln`
- Create: `src/AspectN/AspectN.csproj`
- Create: `tests/AspectN.Tests/AspectN.Tests.csproj`
- Modify locally, do not commit: `.git/info/exclude`

**Step 1: Pin the SDK**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**Step 2: Add deterministic repository-wide build settings**

Create `Directory.Build.props` with:

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

**Step 3: Centralize package versions**

Create `Directory.Packages.props` with these initial pins:

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

The compile baselines intentionally match Rougamo/Fody (`Mono.Cecil` 0.11.5) and Rougamo.Analyzers (`Roslyn` 4.12.0). Compatibility jobs later override them with 0.11.6 and 5.6.0.

**Step 4: Create core and core-test projects**

`src/AspectN/AspectN.csproj`:

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

`tests/AspectN.Tests/AspectN.Tests.csproj` targets `net10.0`, references `src/AspectN`, and references the four test packages with runner/collector marked `PrivateAssets="all"`.

**Step 5: Create the solution and verify the empty baseline**

Run:

```powershell
dotnet new sln -n AspectN --format sln
dotnet sln AspectN.sln add src/AspectN/AspectN.csproj
dotnet sln AspectN.sln add tests/AspectN.Tests/AspectN.Tests.csproj
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
```

Expected: restore and build succeed with zero warnings.

**Step 6: Keep local caches out of status**

Add only `local_cache/` to `.git/info/exclude`, not the committed `.gitignore`; this is an agent-local cache, not a project convention.

**Step 7: Commit**

```powershell
git add global.json Directory.Build.props Directory.Packages.props AspectN.sln src/AspectN tests/AspectN.Tests
git commit -m "build: scaffold AspectN core solution"
```

### Task 2: Add source text and stable diagnostics

**Files:**
- Create: `src/AspectN/Text/TextSpan.cs`
- Create: `src/AspectN/Text/SourceText.cs`
- Create: `src/AspectN/Diagnostics/DiagnosticSeverity.cs`
- Create: `src/AspectN/Diagnostics/AspectNDiagnostic.cs`
- Create: `src/AspectN/Diagnostics/DiagnosticCodes.cs`
- Create: `src/AspectN/Diagnostics/DiagnosticBag.cs`
- Create: `tests/AspectN.Tests/Text/SourceTextTests.cs`
- Create: `tests/AspectN.Tests/Diagnostics/DiagnosticTests.cs`

**Step 1: Write failing value-semantics tests**

Cover:

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

**Step 2: Run the focused tests**

Run:

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SourceTextTests|FullyQualifiedName~DiagnosticTests"
```

Expected: FAIL because the types do not exist.

**Step 3: Implement immutable primitives**

Use a `readonly struct TextSpan` with constructor guards for negative values and a sealed `AspectNDiagnostic` class. Reserve code ranges:

- `AN0xxx`: lexical
- `AN1xxx`: syntax
- `AN2xxx`: binding/version
- `AN3xxx`: evaluation/backend
- `AN4xxx`: migration

`DiagnosticBag` is internal, append-only while building, and returns an array snapshot.

**Step 4: Run tests and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SourceTextTests|FullyQualifiedName~DiagnosticTests"
git add src/AspectN/Text src/AspectN/Diagnostics tests/AspectN.Tests/Text tests/AspectN.Tests/Diagnostics
git commit -m "feat(core): add source text and diagnostics"
```

### Task 3: Implement the lexer

**Files:**
- Create: `src/AspectN/Syntax/SyntaxKind.cs`
- Create: `src/AspectN/Syntax/SyntaxToken.cs`
- Create: `src/AspectN/Parsing/Lexer.cs`
- Create: `tests/AspectN.Tests/Parsing/LexerTests.cs`

**Step 1: Write token golden tests**

Use strongly typed theory data so multi-token cases do not depend on xUnit `InlineData`/`params` binding:

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

Also test:

- Unicode identifiers.
- Escaped strings.
- Signed decimal integers in extension-argument context.
- Bad characters produce `BadToken` plus `AN0001`.
- Every token has exact original text and span.
- End-of-file is always emitted.

**Step 2: Verify failure**

Run:

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~LexerTests
```

Expected: FAIL because `Lexer` does not exist.

**Step 3: Implement a lazy, contextual lexer**

The lexer must:

- Keep original text; do not lowercase identifiers.
- Emit trivia-free tokens.
- Tokenize `.` and `..` distinctly.
- Expose `LexRegexBody()` only after the parser consumes `regex(`; it scans escapes, character classes, and nested groups and leaves the outer `)` for normal parsing.
- Never throw for arbitrary user input.

**Step 4: Add malformed-input tests and pass**

Add unterminated string, unmatched regex class, isolated `&`, and isolated `|` cases.

Run:

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~LexerTests
```

Expected: PASS.

**Step 5: Commit**

```powershell
git add src/AspectN/Syntax/SyntaxKind.cs src/AspectN/Syntax/SyntaxToken.cs src/AspectN/Parsing/Lexer.cs tests/AspectN.Tests/Parsing/LexerTests.cs
git commit -m "feat(parser): add contextual AspectN lexer"
```

### Task 4: Add immutable syntax nodes and syntax trees

**Files:**
- Create: `src/AspectN/Syntax/SyntaxNode.cs`
- Create: `src/AspectN/Syntax/ExpressionSyntax.cs`
- Create: `src/AspectN/Syntax/TypePatternSyntax.cs`
- Create: `src/AspectN/Syntax/SelectorSyntax.cs`
- Create: `src/AspectN/Syntax/SignatureSyntax.cs`
- Create: `src/AspectN/Syntax/AspectNSyntaxTree.cs`
- Create: `src/AspectN/Parsing/AspectNParseKind.cs`
- Create: `tests/AspectN.Tests/Syntax/SyntaxTreeTests.cs`

**Step 1: Write failing tree invariants**

Assert:

- `FullSpan` covers children in source order.
- Child collections are copied on construction.
- Missing tokens have zero width.
- A malformed tree still has a root and diagnostics.
- Syntax contains no metadata/backend namespace references.

**Step 2: Run and observe failure**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SyntaxTreeTests
```

**Step 3: Implement the minimum syntax hierarchy**

Use sealed concrete nodes for:

- unary, binary, and parenthesized expressions;
- callable/execution/method/property/getter/setter/ctor/cctor/attribute/regex/extension selectors;
- callable, property, constructor, parameter, modifier, generic-list, and type-use syntax;
- named, wildcard, tuple, task-like, variable, array, nullable, assignable, and parenthesized type syntax.

All public child properties are get-only. Do not put semantic symbols, caches, or adapter state on syntax nodes.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SyntaxTreeTests
git add src/AspectN/Syntax src/AspectN/Parsing/AspectNParseKind.cs tests/AspectN.Tests/Syntax
git commit -m "feat(parser): add immutable syntax model"
```

### Task 5: Parse Boolean expressions with recovery

**Files:**
- Create: `src/AspectN/AspectNParser.cs`
- Create: `src/AspectN/AspectNParseOptions.cs`
- Create: `src/AspectN/AspectNLanguageVersion.cs`
- Create: `src/AspectN/Parsing/Parser.cs`
- Create: `tests/AspectN.Tests/Parsing/BooleanParserTests.cs`
- Create: `tests/AspectN.Tests/Parsing/ParserRecoveryTests.cs`

**Step 1: Write precedence tests**

Assert the tree shapes for:

```text
a() || b() && !c()
(a() || b()) && c()
!!a()
```

The first root must be OR; its right child must be AND.

**Step 2: Write recovery tests**

Inputs such as `method(`, `a() && && b()`, and `((a())` must return trees with multiple stable diagnostics and must not throw.

**Step 3: Run failing tests**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BooleanParserTests|FullyQualifiedName~ParserRecoveryTests"
```

**Step 4: Implement precedence climbing**

Use:

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

Add missing-token insertion and synchronization at `)`, `,`, `&&`, `||`, and EOF.

**Step 5: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BooleanParserTests|FullyQualifiedName~ParserRecoveryTests"
git add src/AspectN/AspectNParser.cs src/AspectN/AspectNParseOptions.cs src/AspectN/AspectNLanguageVersion.cs src/AspectN/Parsing/Parser.cs tests/AspectN.Tests/Parsing
git commit -m "feat(parser): parse Boolean expressions with recovery"
```

### Task 6: Parse Standard type patterns

**Files:**
- Modify: `src/AspectN/Parsing/Parser.cs`
- Create: `src/AspectN/Parsing/TypePatternParser.cs`
- Create: `tests/AspectN.Tests/Parsing/TypePatternParserTests.cs`

**Step 1: Add one failing theory per grammar family**

Include:

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

Assert syntax kind, spans, generic arity representation, nested separator, suffix order, and `ref readonly`.

**Step 2: Add rejection cases**

Standard must diagnose:

```text
Name<!>
out int
tasklike<>
()
(int)
Name<T,>
```

`(int)` is grouping, not tuple; it is valid only where a grouped type is valid.

**Step 3: Implement only the EBNF in the language spec**

Keep tuple/group disambiguation parser-local. Store all-comma generic arity explicitly instead of inventing wildcard child nodes. Let the binder validate suffix applicability.

**Step 4: Run and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~TypePatternParserTests
git add src/AspectN/Parsing tests/AspectN.Tests/Parsing/TypePatternParserTests.cs
git commit -m "feat(parser): parse Standard type patterns"
```

### Task 7: Parse selectors and signatures

**Files:**
- Modify: `src/AspectN/Parsing/Parser.cs`
- Create: `src/AspectN/Parsing/SelectorParser.cs`
- Create: `tests/AspectN.Tests/Parsing/SelectorParserTests.cs`
- Create: `tests/AspectN.Tests/Parsing/RegexParserTests.cs`

**Step 1: Add selector theories**

Cover every Standard selector:

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

**Step 2: Add versioned parse tests**

- Standard accepts `execution(...)` and `property(...)` directly; migration diagnostics remain for `tasklike<void>` and the long attribute positions.
- Legacy accepts `execution`, `property`, `attr(para 0 ...)`, `privateprotected`, and `<!>`.
- Standard keywords are lowercase and case-sensitive.
- Legacy selector keywords follow the oracle's case-insensitive behavior.

**Step 3: Implement selector parsing and contextual regex scanning**

Do not let extensions consume an arbitrary token stream. Extension arguments are exactly string, integer, Boolean, or type-pattern nodes.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~SelectorParserTests|FullyQualifiedName~RegexParserTests"
git add src/AspectN/Parsing tests/AspectN.Tests/Parsing
git commit -m "feat(parser): parse member selectors and signatures"
```

---

## Phase 2 — Semantic Model, Binding, and Evaluation

### Task 8: Define backend-neutral metadata signatures

**Files:**
- Create: `src/AspectN/Metadata/AssemblyIdentity.cs`
- Create: `src/AspectN/Metadata/TypeIdentity.cs`
- Create: `src/AspectN/Metadata/TypeSignature.cs`
- Create: `src/AspectN/Metadata/TypeUseSignature.cs`
- Create: `src/AspectN/Metadata/GenericParameterSignature.cs`
- Create: `src/AspectN/Metadata/ParameterSignature.cs`
- Create: `src/AspectN/Metadata/CallableSignature.cs`
- Create: `src/AspectN/Metadata/PropertySignature.cs`
- Create: `src/AspectN/Metadata/AttributeSignature.cs`
- Create: `src/AspectN/Metadata/MetadataEnums.cs`
- Create: `tests/AspectN.Tests/Metadata/SignatureTests.cs`

**Step 1: Write immutability and identity tests**

Verify:

- Assembly identity participates in named-type identity.
- Nested containing types and generic arity are explicit.
- Constructed type arguments and original definition are both retained.
- Array element/rank, generic parameter owner/position, and unsupported type shapes are distinct.
- `TypeUseSignature` carries `RefKind` separately from the referenced type.
- Property and accessor signatures are separate but linked.
- Constructor and static-constructor kinds cannot be confused with ordinary methods.

**Step 2: Implement an immutable algebra**

Use an abstract `TypeSignature` with sealed `NamedTypeSignature`, `ArrayTypeSignature`, `GenericParameterTypeSignature`, and `UnsupportedTypeSignature` subclasses. Constructors copy incoming collections to arrays. Do not store `Type`, `TypeReference`, `ISymbol`, or `object` handles.

`CallableSignature` must expose:

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

**Step 3: Add reflection guards**

Tests scan the `AspectN` public API and fail if any public property type comes from `Mono.Cecil`, `Microsoft.CodeAnalysis`, or `System.Reflection`.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~SignatureTests
git add src/AspectN/Metadata tests/AspectN.Tests/Metadata
git commit -m "feat(core): add backend-neutral metadata signatures"
```

### Task 9: Define type-system capabilities and relationships

**Files:**
- Create: `src/AspectN/TypeSystem/ITypeSystem.cs`
- Create: `src/AspectN/TypeSystem/TypeSystemCapabilities.cs`
- Create: `src/AspectN/TypeSystem/TypeRelationResult.cs`
- Create: `src/AspectN/TypeSystem/StructuralTypeComparer.cs`
- Create: `tests/AspectN.Tests/TypeSystem/StructuralTypeComparerTests.cs`
- Create: `tests/AspectN.Tests/TestDoubles/StubTypeSystem.cs`

**Step 1: Write structural-equality tests**

Test named definitions, constructed generics, nested generics, arrays, and generic parameters. Structurally unequal types return `NotMatched`; missing/unsupported identity returns `Unknown`.

**Step 2: Define the service**

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

`TypeRelationResult` contains `MatchStatus` plus diagnostics; it never throws for normal resolution failure.

**Step 3: Implement structural equality in core**

Relationship traversal remains adapter-owned. Core equality handles facts already present in the signatures.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~StructuralTypeComparerTests
git add src/AspectN/TypeSystem tests/AspectN.Tests/TypeSystem tests/AspectN.Tests/TestDoubles
git commit -m "feat(core): define type-system relationship contract"
```

### Task 10: Implement language-version-aware semantic binding

**Files:**
- Create: `src/AspectN/Binding/Binder.cs`
- Create: `src/AspectN/Binding/BoundNode.cs`
- Create: `src/AspectN/Binding/BoundExpression.cs`
- Create: `src/AspectN/Binding/BoundTypePattern.cs`
- Create: `src/AspectN/Binding/BoundSelector.cs`
- Create: `src/AspectN/Binding/VariableSymbol.cs`
- Create: `src/AspectN/AspectNCompilationOptions.cs`
- Create: `src/AspectN/AspectNCompilation.cs`
- Create: `tests/AspectN.Tests/Binding/BinderTests.cs`
- Create: `tests/AspectN.Tests/Binding/LanguageVersionTests.cs`

**Step 1: Write binding-diagnostic tests**

Cover:

- Unknown selector.
- Selector unavailable in selected version.
- Invalid target domain.
- Invalid modifier combinations.
- `out`/`in` return types.
- Duplicate or first-under-negation variables.
- Variable introduced in an OR branch cannot escape that branch.
- Missing extension.

**Step 2: Run and observe failure**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BinderTests|FullyQualifiedName~LanguageVersionTests"
```

**Step 3: Implement separate syntax and bound models**

The binder:

- Normalizes primitive aliases.
- Resolves Standard/Legacy selector names.
- Computes Standard generic arity semantics.
- Preserves Legacy omission-as-any-arity and `<!>`.
- Creates scoped `$T` symbols in Standard.
- Creates implicit Legacy generic variables only under Legacy rules.
- Emits immutable bound nodes only when the relevant syntax is semantically valid.

Bindings created inside negation or an OR branch do not escape. This prevents the matcher from becoming a multi-solution query engine.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~BinderTests|FullyQualifiedName~LanguageVersionTests"
git add src/AspectN/Binding src/AspectN/AspectNCompilationOptions.cs src/AspectN/AspectNCompilation.cs tests/AspectN.Tests/Binding
git commit -m "feat(binding): bind versioned AspectN semantics"
```

### Task 11: Add three-state evaluation and compiled plans

**Files:**
- Create: `src/AspectN/Evaluation/MatchStatus.cs`
- Create: `src/AspectN/Evaluation/MatchResult.cs`
- Create: `src/AspectN/Evaluation/MatchLogic.cs`
- Create: `src/AspectN/Evaluation/EvaluationContext.cs`
- Create: `src/AspectN/CompiledPattern.cs`
- Create: `src/AspectN/CompiledMemberPattern.cs`
- Create: `src/AspectN/CompiledTypePattern.cs`
- Create: `tests/AspectN.Tests/Evaluation/MatchLogicTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/CompiledPatternTests.cs`

**Step 1: Encode all truth-table rows as tests**

Use nine rows each for AND and OR and three for NOT. Do not use abbreviated “any” assertions.

**Step 2: Test plan immutability and isolation**

Evaluate one compiled plan concurrently against different candidates. Variable bindings and diagnostics must not leak between calls.

**Step 3: Implement**

`MatchResult` has:

```csharp
public MatchStatus Status { get; }
public IReadOnlyList<AspectNDiagnostic> Diagnostics { get; }
public bool ToBoolean(UnknownMatchBehavior behavior);
```

The default core API does not coerce `Unknown`. Rougamo later calls `ToBoolean(UnknownMatchBehavior.False)`.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~MatchLogicTests|FullyQualifiedName~CompiledPatternTests"
git add src/AspectN/Evaluation src/AspectN/CompiledPattern.cs src/AspectN/CompiledMemberPattern.cs src/AspectN/CompiledTypePattern.cs tests/AspectN.Tests/Evaluation
git commit -m "feat(core): add three-state compiled evaluation"
```

### Task 12: Evaluate basic type patterns

**Files:**
- Create: `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- Create: `src/AspectN/Evaluation/NamePatternMatcher.cs`
- Create: `tests/AspectN.Tests/Evaluation/NamePatternMatcherTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/TypePatternEvaluatorTests.cs`

**Step 1: Port deterministic wildcard cases**

Port the intent of Rougamo:

- `Signatures/WildcardMatchTests.cs`
- namespace `*` and `..`;
- nested `/`;
- case-sensitive Standard names.

Do not copy old matcher implementation.

**Step 2: Add arrays, aliases, grouping, Boolean type operators, and assignability**

Use `StubTypeSystem` to return Matched, NotMatched, or Unknown for assignability.

**Step 3: Implement**

Use a linear-time wildcard matcher; do not translate wildcard patterns to regex. Check cancellation before relationship traversal.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~NamePatternMatcherTests|FullyQualifiedName~TypePatternEvaluatorTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): evaluate names and base type patterns"
```

### Task 13: Evaluate generics and pattern variables

**Files:**
- Modify: `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- Create: `src/AspectN/Evaluation/BindingEnvironment.cs`
- Create: `tests/AspectN.Tests/Evaluation/GenericPatternTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/PatternVariableTests.cs`

**Step 1: Write failing arity and constructed-type tests**

Cover `<>`, `<,>`, `<..>`, exact generic arguments, nested generic arguments, original definition, and effective substitution.

**Step 2: Write variable tests**

```text
method($T Service.Convert<$T>($T))
```

The first positive occurrence binds. Later occurrences use `ITypeSystem.AreEqual`. Test AND sharing, OR isolation, negation isolation, and concurrent evaluation.

**Step 3: Implement an evaluation-local environment**

Use a dictionary keyed by `VariableSymbol`, clone only at OR/negation boundaries, and never expose it from `MatchResult`.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~GenericPatternTests|FullyQualifiedName~PatternVariableTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): add generics and unification variables"
```

### Task 14: Evaluate modifiers, ref kinds, nullability, tuples, and task-like types

**Files:**
- Modify: `src/AspectN/Evaluation/TypePatternEvaluator.cs`
- Create: `src/AspectN/Evaluation/ModifierMatcher.cs`
- Create: `tests/AspectN.Tests/Evaluation/ModifierMatcherTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/RefKindTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/NullableTupleTasklikeTests.cs`

**Step 1: Port modifier intent**

Port the accessibility matrix from Rougamo `SignatureBasicTests.cs`, but express assertions against backend-neutral signatures.

**Step 2: Add Standard semantics**

- `private protected` and `protected internal` are composite accessibilities.
- Alternative accessibilities require expression-level OR.
- Omitted parameter ref kind means by-value, except a full wildcard may ignore ref kind.
- Return types allow only `ref` and `ref readonly`.
- Reference `T?` equals `T`; value `T?` equals `Nullable<T>`.
- Tuple syntax means only `ValueTuple`.
- `tasklike<T>` matches `Task<T>`/`ValueTask<T>` regardless of the compiler `async` flag; Standard has no `async` modifier.

**Step 3: Add Legacy semantics**

- Space-separated accessibilities remain alternatives.
- `(T1,T2)` accepts `Tuple` and `ValueTuple`.
- `async T` matches `Task<T>`/`ValueTask<T>` and `async null` matches non-generic task-like types.

**Step 4: Implement, pass, and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~ModifierMatcherTests|FullyQualifiedName~RefKindTests|FullyQualifiedName~NullableTupleTasklikeTests"
git add src/AspectN/Evaluation tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): add modifiers ref kinds and type sugar"
```

### Task 15: Evaluate callable, accessor, and constructor selectors

**Files:**
- Create: `src/AspectN/Evaluation/MemberPatternEvaluator.cs`
- Create: `tests/AspectN.Tests/Evaluation/CallableSelectorTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/PropertySelectorTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/ConstructorSelectorTests.cs`

**Step 1: Write domain tests**

- `callable` matches ordinary methods, accessors, event accessors, operators, and conversions, but not constructors.
- `execution` matches every method-like member, including both constructor kinds.
- `method` matches ordinary methods only.
- `property` matches getter and setter using associated property type/name/attributes.
- `getter` and `setter` narrow the accessor kind.
- `ctor` and `cctor` are disjoint.
- Having a body is not implied.

**Step 2: Test constructed callables**

The default match uses substituted return/parameter types while retaining the original definition for future extensions.

**Step 3: Implement, pass, and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~CallableSelectorTests|FullyQualifiedName~PropertySelectorTests|FullyQualifiedName~ConstructorSelectorTests"
git add src/AspectN/Evaluation/MemberPatternEvaluator.cs tests/AspectN.Tests/Evaluation
git commit -m "feat(matching): evaluate member selectors"
```

### Task 16: Add attributes, regex safety, and semantic extensions

**Files:**
- Create: `src/AspectN/Evaluation/AttributePatternEvaluator.cs`
- Create: `src/AspectN/Evaluation/CanonicalSignatureFormatter.cs`
- Create: `src/AspectN/Evaluation/RegexPatternEvaluator.cs`
- Create: `src/AspectN/Extensibility/IAspectNExtension.cs`
- Create: `src/AspectN/Extensibility/AspectNExtensionRegistry.cs`
- Create: `src/AspectN/Extensibility/ExtensionContexts.cs`
- Create: `tests/AspectN.Tests/Evaluation/AttributePatternTests.cs`
- Create: `tests/AspectN.Tests/Evaluation/RegexPatternTests.cs`
- Create: `tests/AspectN.Tests/Extensibility/ExtensionRegistryTests.cs`

**Step 1: Write attribute-position tests**

Test direct type, callable, associated property, indexed parameter, any parameter, return, and any location. Inherited attributes are not considered.

**Step 2: Write canonical regex tests**

Verify a culture-invariant versioned signature, nested `/`, generic formatting, and ref-kind placement. Use a catastrophic regex with a tiny test timeout and assert `Unknown` plus `AN3003`.

**Step 3: Write extension registry tests**

The builder rejects duplicate qualified names. The built registry is immutable. Cache identity changes when registry version changes. An extension that lacks required backend capability returns `Unknown`.

**Step 4: Implement**

Require extensions to compile fixed syntax arguments into an immutable, thread-safe matcher. Do not expose internal bound nodes.

**Step 5: Pass and commit**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter "FullyQualifiedName~AttributePatternTests|FullyQualifiedName~RegexPatternTests|FullyQualifiedName~ExtensionRegistryTests"
git add src/AspectN/Evaluation src/AspectN/Extensibility tests/AspectN.Tests/Evaluation tests/AspectN.Tests/Extensibility
git commit -m "feat(core): add attributes regex safety and extensions"
```

### Task 17: Add the explicit bounded engine cache and finalize core API

**Files:**
- Create: `src/AspectN/AspectNEngine.cs`
- Create: `src/AspectN/AspectNEngineOptions.cs`
- Create: `src/AspectN/Caching/CompiledPatternCache.cs`
- Create: `src/AspectN/PublicAPI.Shipped.txt`
- Create: `src/AspectN/PublicAPI.Unshipped.txt`
- Modify: `src/AspectN/AspectN.csproj`
- Create: `tests/AspectN.Tests/Caching/CompiledPatternCacheTests.cs`
- Create: `tests/AspectN.Tests/PublicApiTests.cs`

**Step 1: Write cache tests**

Verify:

- Capacity is enforced with LRU eviction.
- Source, parse kind, language version, compilation options, and extension registry identity are all key parts.
- No static cache exists.
- Concurrent identical compiles produce one cached immutable plan.
- Capacity zero disables caching.

**Step 2: Implement a lock-protected dictionary plus linked list**

Do not add `MemoryCache` or another dependency. Keep cache ownership on `AspectNEngine`.

**Step 3: Lock the first public API baseline**

Populate `PublicAPI.Unshipped.txt`, add the private `Microsoft.CodeAnalysis.PublicApiAnalyzers` reference to `AspectN.csproj`, and enable the analyzer. Add a reflection test proving the core assembly has no adapter references.

**Step 4: Run all core tests**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release
dotnet build src/AspectN/AspectN.csproj -c Release
```

Expected: PASS with zero warnings for both TFMs.

**Step 5: Commit**

```powershell
git add src/AspectN tests/AspectN.Tests
git commit -m "feat(core): finalize cached compilation API"
```

### Task 18: Add parser fuzz/property tests

**Files:**
- Create: `tests/AspectN.Tests/Fuzz/ArbitrarySourceTests.cs`
- Create: `tests/AspectN.Tests/Fuzz/ParserTerminationTests.cs`
- Create: `tests/AspectN.Tests/Fuzz/SyntaxRoundTripTests.cs`

**Step 1: Write FsCheck properties**

For arbitrary Unicode strings up to a bounded size:

- Parsing never throws except `OperationCanceledException`.
- Parsing completes under the test timeout.
- Every diagnostic span is within source bounds.
- Reparse of canonical valid examples preserves semantic tree shape.

**Step 2: Run with a deterministic replay seed**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --filter FullyQualifiedName~Fuzz
```

Expected: PASS for at least 10,000 generated cases in CI configuration.

**Step 3: Add any minimized regressions as normal facts**

Do not merely increase timeouts.

**Step 4: Commit**

```powershell
git add tests/AspectN.Tests/Fuzz
git commit -m "test(core): add parser property and termination coverage"
```

---

## Phase 3 — Mono.Cecil and Legacy Compatibility

### Task 19: Scaffold AspectN.Cecil

**Files:**
- Create: `src/AspectN.Cecil/AspectN.Cecil.csproj`
- Create: `src/AspectN.Cecil/CecilMappingOptions.cs`
- Create: `src/AspectN.Cecil/CecilTypeSystem.cs`
- Create: `src/AspectN.Cecil/CecilSignatureMapper.cs`
- Create: `src/AspectN.Cecil/PublicAPI.Shipped.txt`
- Create: `src/AspectN.Cecil/PublicAPI.Unshipped.txt`
- Create: `tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj`
- Create: `tests/AspectN.Cecil.Tests/TestModuleFactory.cs`
- Modify: `AspectN.sln`

**Step 1: Add project references and package baseline**

`AspectN.Cecil` targets `netstandard2.0;net10.0`, references `AspectN`, and references `Mono.Cecil`. Tests target `net10.0`.

**Step 2: Write the first failing mapper test**

Create a Cecil module in memory and map `System.Int32`. Assert namespace, name, assembly identity, and arity.

**Step 3: Implement module-scoped context**

`CecilTypeSystem` owns one `ModuleDefinition`, exposes its `CecilSignatureMapper`, implements `ITypeSystem`, and implements `IDisposable`. No static dictionaries.

**Step 4: Pass, add to solution, and commit**

```powershell
dotnet sln AspectN.sln add src/AspectN.Cecil/AspectN.Cecil.csproj tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release
git add AspectN.sln src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): scaffold module-scoped adapter"
```

### Task 20: Map Cecil type definitions and references

**Files:**
- Modify: `src/AspectN.Cecil/CecilSignatureMapper.cs`
- Create: `src/AspectN.Cecil/Internal/CecilTypeCache.cs`
- Create: `tests/AspectN.Cecil.Tests/CecilTypeMappingTests.cs`

**Step 1: Write definitions/reference tests**

Cover:

- Top-level and nested types.
- Generic definitions and instances.
- Type and method generic parameters by owner/position.
- Jagged and multidimensional arrays.
- By-reference type use separated from element type.
- Unresolved named references retain a structural `NamedTypeSignature`, so name-based matching remains available; resolution-dependent base/interface/assignability queries return `Unknown` plus diagnostics.
- Genuinely unsupported Cecil type shapes produce `UnsupportedTypeSignature` plus diagnostics.
- Original definition and effective constructed arguments.

**Step 2: Implement mapping**

Cache within the `CecilTypeSystem` lifetime. Key by Cecil reference identity where safe and by stable full identity where references are recreated. Do not cache only on `typeRef.ToString()`.

**Step 3: Test no module retention after disposal**

Drop strong references, force GC in a bounded retry loop, and assert a weak reference to the module can die after the adapter is disposed.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release --filter FullyQualifiedName~CecilTypeMappingTests
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): map type definitions and references"
```

### Task 21: Implement Cecil equality, assignability, and resolution diagnostics

**Files:**
- Modify: `src/AspectN.Cecil/CecilTypeSystem.cs`
- Create: `src/AspectN.Cecil/Internal/CecilResolver.cs`
- Create: `tests/AspectN.Cecil.Tests/CecilTypeSystemTests.cs`

**Step 1: Write relation tests**

Test exact type, base class, interface, constructed generic identity, unresolved assembly, resolver exception, and cancellation.

**Step 2: Implement guarded traversal**

Track visited type identities to prevent cycles. Convert ordinary `Resolve()` failures to `Unknown` with `AN3004`; never swallow cancellation.

**Step 3: Run against both Cecil versions**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.5
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6
```

Expected: both pass.

**Step 4: Commit**

```powershell
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): implement type relationships"
```

### Task 22: Map Cecil callables, accessors, attributes, and ref kinds

**Files:**
- Modify: `src/AspectN.Cecil/CecilSignatureMapper.cs`
- Create: `src/AspectN.Cecil/Internal/CecilAccessibility.cs`
- Create: `src/AspectN.Cecil/Internal/CecilAsyncDetector.cs`
- Create: `src/AspectN.Cecil/Internal/CecilRefKindDetector.cs`
- Create: `tests/AspectN.Cecil.Tests/CecilCallableMappingTests.cs`
- Create: `tests/AspectN.Cecil.Tests/CecilAccessorMappingTests.cs`

**Step 1: Create an emitted fixture**

Include ordinary methods, operators, conversions, constructors, async methods, generic instances, getter-only/setter-only/indexer properties, event accessors, direct attributes, and all supported ref kinds.

**Step 2: Test raw and composite accessibility**

`CecilMappingOptions.CompositeAccessibility` reproduces old Rougamo behavior without making it a language rule. Cache keys include this option.

**Step 3: Implement association**

Map a property accessor to its `PropertySignature` and keep method attributes separate from property attributes. Legacy binding later composes them for `attr(exec ...)`.

**Step 4: Pass and commit**

```powershell
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release --filter "FullyQualifiedName~CecilCallableMappingTests|FullyQualifiedName~CecilAccessorMappingTests"
git add src/AspectN.Cecil tests/AspectN.Cecil.Tests
git commit -m "feat(cecil): map callables accessors and attributes"
```

### Task 23: Complete Legacy grammar and semantic compatibility

**Files:**
- Create: `src/AspectN/Binding/Legacy/LegacyBinder.cs`
- Create: `src/AspectN/Binding/Legacy/LegacyGenericRules.cs`
- Create: `src/AspectN/Binding/Legacy/LegacyAttributeRules.cs`
- Create: `src/AspectN/Binding/Legacy/LegacyModifierRules.cs`
- Create: `src/AspectN/Evaluation/Legacy/LegacyCanonicalSignatureFormatter.cs`
- Create: `tests/AspectN.Tests/Legacy/LegacySyntaxTests.cs`
- Create: `tests/AspectN.Tests/Legacy/LegacySemanticTests.cs`

**Step 1: Port approved Legacy categories**

Port behavior, not implementation, for:

- `execution`, `method`, `property`, `getter`, `setter`, `ctor`, `cctor`, `attr`, and `regex`;
- old modifier phrases and accessibility-set behavior;
- implicit generic variables and virtual naming;
- wildcard parameter slots and arity;
- tuple/nullable/async behavior;
- legacy regex formatting.

**Step 2: Add known old parser accidents as rejection tests**

Only documented or currently tested valid syntax is compatibility surface. Invalid accidental inputs receive diagnostics rather than old `ArgumentException` text.

**Step 3: Implement version-isolated helpers**

Do not scatter `if (Legacy)` throughout Standard evaluators. Keep Legacy normalization and exceptional behavior under `Binding/Legacy` and `Evaluation/Legacy`.

**Step 4: Run all core and Cecil tests**

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release
```

**Step 5: Commit**

```powershell
git add src/AspectN/Binding/Legacy src/AspectN/Evaluation/Legacy tests/AspectN.Tests/Legacy
git commit -m "feat(legacy): implement Shared AspectN semantics"
```

### Task 24: Build the pinned external Legacy oracle harness

**Files:**
- Create: `eng/prepare-legacy-oracle.ps1`
- Create: `tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj`
- Create: `tests/AspectN.TestAssembly/LegacyFixture.cs`
- Create: `tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj`
- Create: `tests/AspectN.LegacyOracle.Tests/LegacyOracleRevisionTests.cs`
- Create: `tests/AspectN.LegacyOracle.Tests/LegacyDifferentialTests.cs`
- Create: `tests/AspectN.LegacyOracle.Tests/legacy-patterns.json`
- Create: `tests/AspectN.LegacyOracle.Tests/approved-differences.json`
- Create: `tests/AspectN.LegacyOracle.Tests/README.md`
- Modify: `AspectN.sln`

**Step 1: Implement the preparation script**

The script clones `https://github.com/inversionhourglass/Shared.Cecil.AspectN.git` into ignored `artifacts/legacy-oracle/Shared.Cecil.AspectN`, checks out exactly `03a9d1030f92a64449b6220eb45d7198d31653db`, and fails if the directory is dirty or at another revision.

It must support `-OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN` by extracting a Git archive of the pinned commit, not the dirty working tree. Both clone and offline modes write `.aspectn-oracle-commit`; tests verify that marker equals the pinned full SHA.

**Step 2: Import the oracle only into the test assembly**

`AspectN.LegacyOracle.Tests.csproj` defines a deterministic default and imports only test code:

```xml
<PropertyGroup>
  <LegacyOracleRoot Condition="'$(LegacyOracleRoot)' == ''">$(MSBuildThisFileDirectory)..\..\artifacts\legacy-oracle\Shared.Cecil.AspectN</LegacyOracleRoot>
</PropertyGroup>
<Import Project="$(LegacyOracleRoot)\Cecil.AspectN.projitems"
        Label="FrozenLegacyOracle"
        Condition="Exists('$(LegacyOracleRoot)\Cecil.AspectN.projitems')" />
```

The project references `AspectN.Cecil` and Mono.Cecil. No production project references or imports the old source.

**Step 3: Seed deterministic patterns**

Include all selector families and the pattern categories from:

- `D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Signatures`
- `D:\Code\Hourglass\rougamo-eco\AspectN.Tests\test\Tests\Cases`
- Rougamo `test\TestAssemblies\PatternUsage\Attributes`

Port the required fixture types into `tests/AspectN.TestAssembly/LegacyFixture.cs`; do not reference sibling repository build outputs. Avoid random candidate selection and store stable candidate IDs as `<assembly>::<declaring-type>::<metadata-token>`.

**Step 4: Write the differential assertion**

For every pattern/candidate/options tuple:

```csharp
Assert.Equal(
    oldMatcher.IsMatch(oldSignature),
    newResult.Status == MatchStatus.Matched);
Assert.NotEqual(MatchStatus.Unknown, newResult.Status);
```

Approved corrections are looked up by the full tuple and require a non-empty rationale.

**Step 5: Run and commit**

```powershell
powershell -NoProfile -File eng/prepare-legacy-oracle.ps1 -OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN
dotnet sln AspectN.sln add tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release
git add eng/prepare-legacy-oracle.ps1 tests/AspectN.TestAssembly tests/AspectN.LegacyOracle.Tests AspectN.sln
git commit -m "test(legacy): add pinned differential oracle"
```

### Task 25: Reach zero unapproved Cecil Legacy differences

**Files:**
- Modify: `tests/AspectN.LegacyOracle.Tests/legacy-patterns.json`
- Modify: `tests/AspectN.LegacyOracle.Tests/approved-differences.json`
- Modify: matching/binding files identified by minimized failures
- Create: `docs/migration/legacy-compatibility-report.md`
- Create: `tests/AspectN.LegacyOracle.Tests/packages.lock.json`

**Step 1: Expand the candidate corpus**

Run against:

- the deterministic local fixture;
- Rougamo `SignatureUsage` and `PatternUsage` assemblies;
- a fixed set of public package assemblies previously used by AspectN.Tests.

Record package name/version in the report, enable `RestorePackagesWithLockFile`, commit `packages.lock.json`, and run the oracle with locked restore; do not depend on “latest.”

**Step 2: Minimize every mismatch**

Convert each mismatch to one focused unit test before changing code.

**Step 3: Require maintainer approval for corrections**

If a mismatch is an old bug or safety issue, stop and request approval before adding it to `approved-differences.json`.

**Step 4: Produce the report**

Include oracle commit, corpus hash, number of pattern/candidate pairs, approved differences, runtime, and platform.

**Step 5: Gate and commit**

Run:

```powershell
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release
```

Expected: zero unapproved differences and zero `Unknown` results.

```powershell
git add src tests/AspectN.LegacyOracle.Tests docs/migration/legacy-compatibility-report.md
git commit -m "test(legacy): reach zero unapproved Cecil differences"
```

---

## Phase 4 — Reflection, Roslyn, and Cross-Adapter Conformance

### Task 26: Implement AspectN.Reflection

**Files:**
- Create: `src/AspectN.Reflection/AspectN.Reflection.csproj`
- Create: `src/AspectN.Reflection/ReflectionTypeSystem.cs`
- Create: `src/AspectN.Reflection/ReflectionSignatureMapper.cs`
- Create: `src/AspectN.Reflection/Internal/ReflectionTypeCache.cs`
- Create: `src/AspectN.Reflection/PublicAPI.Shipped.txt`
- Create: `src/AspectN.Reflection/PublicAPI.Unshipped.txt`
- Create: `tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj`
- Create: `tests/AspectN.Reflection.Tests/ReflectionMappingTests.cs`
- Create: `tests/AspectN.Reflection.Tests/CollectibleAssemblyTests.cs`
- Modify: `AspectN.sln`

**Step 1: Write mapping tests**

Cover `Type`, `MethodBase`, `MethodInfo`, `ConstructorInfo`, `PropertyInfo`, generic definitions/constructed members, ref kinds, direct custom attribute data, async detection, and associated accessors.

**Step 2: Implement without attribute instantiation**

Use `CustomAttributeData`. Store only `WeakReference<Type>`/`WeakReference<MemberInfo>` in reverse lookup caches.

**Step 3: Test collectible ALC unload**

Load an emitted fixture into a collectible `AssemblyLoadContext`, map and match it, release all caller/adapter references, unload, and assert a weak ALC reference dies.

**Step 4: Pass and commit**

```powershell
dotnet sln AspectN.sln add src/AspectN.Reflection/AspectN.Reflection.csproj tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj
dotnet test tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj -c Release
git add AspectN.sln src/AspectN.Reflection tests/AspectN.Reflection.Tests
git commit -m "feat(reflection): add collectible-safe adapter"
```

### Task 27: Implement Symbol-first AspectN.Roslyn

**Files:**
- Create: `src/AspectN.Roslyn/AspectN.Roslyn.csproj`
- Create: `src/AspectN.Roslyn/RoslynTypeSystem.cs`
- Create: `src/AspectN.Roslyn/RoslynSignatureMapper.cs`
- Create: `src/AspectN.Roslyn/RoslynSyntaxExtensions.cs`
- Create: `src/AspectN.Roslyn/PublicAPI.Shipped.txt`
- Create: `src/AspectN.Roslyn/PublicAPI.Unshipped.txt`
- Create: `tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj`
- Create: `tests/AspectN.Roslyn.Tests/RoslynMappingTests.cs`
- Create: `tests/AspectN.Roslyn.Tests/RoslynSyntaxEntryPointTests.cs`
- Modify: `AspectN.sln`

**Step 1: Write Symbol-first tests**

Primary mapper overloads accept `ITypeSymbol`, `IMethodSymbol`, and `IPropertySymbol`. Test original definitions, constructed symbols, tuple normalization, nullability erasure, ref kinds, `IsAsync`, attributes, accessors, operators, and conversions.

**Step 2: Write syntax convenience tests**

`TypeDeclarationSyntax`/`MethodDeclarationSyntax` overloads require `SemanticModel` or `Compilation`. A syntax-only overload must not exist.

**Step 3: Scope cache to Compilation**

The adapter owns a `Compilation` and its caches. Discarding the adapter releases the compilation graph.

**Step 4: Test baseline and current Roslyn**

```powershell
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=4.12.0
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0
```

**Step 5: Commit**

```powershell
dotnet sln AspectN.sln add src/AspectN.Roslyn/AspectN.Roslyn.csproj tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj
git add AspectN.sln src/AspectN.Roslyn tests/AspectN.Roslyn.Tests
git commit -m "feat(roslyn): add Symbol-first adapter"
```

### Task 28: Add one-source cross-adapter conformance

**Files:**
- Modify: `tests/AspectN.TestAssembly/AspectN.TestAssembly.csproj`
- Create: `tests/AspectN.TestAssembly/ConformanceFixture.cs`
- Create: `tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj`
- Create: `tests/AspectN.Conformance.Tests/FixtureCompiler.cs`
- Create: `tests/AspectN.Conformance.Tests/CrossAdapterConformanceTests.cs`
- Create: `tests/AspectN.Conformance.Tests/conformance-cases.json`
- Modify: `AspectN.sln`

**Step 1: Build one source fixture**

The fixture contains nested/constructed generics, arrays, tuples, nullable value/reference types, every ref kind, async and task-like non-async methods, all accessibility forms, direct attributes, properties/indexers/events, operators, conversions, constructors, and unresolved external references.

**Step 2: Observe it through three backends**

- Roslyn before emit.
- Cecil from emitted PE.
- Reflection from a collectible load context.

**Step 3: Assert canonical signature and match parity**

For every case, compare backend-neutral signatures field-by-field and run the same Standard expression. Equivalent facts must have equal status. Expected missing data must be explicitly `Unknown`.

**Step 4: Run and commit**

```powershell
dotnet sln AspectN.sln add tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj
dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release
git add AspectN.sln tests/AspectN.TestAssembly tests/AspectN.Conformance.Tests
git commit -m "test: add cross-adapter conformance suite"
```

---

## Phase 5 — Packaging, Compatibility, and Continuous Integration

### Task 29: Validate NuGet packages and .NET Framework 4.7.2

**Files:**
- Create: `tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj`
- Create: `tests/AspectN.NetFramework.Tests/NetFrameworkSmokeTests.cs`
- Create: `eng/verify-packages.ps1`
- Create: `README.md`
- Create: `docs/language/README.md`
- Create: `docs/migration/shared-cecil-to-aspectn.md`
- Modify: each `src/*/*.csproj`
- Modify: `AspectN.sln`

**Step 1: Add a net472 consumer**

The project references the four projects, not internals, and exercises parse/compile plus Reflection matching. Reference `Microsoft.NETFramework.ReferenceAssemblies.net472` privately.

**Step 2: Complete package metadata**

Each package has distinct description/tags, project URL, repository URL, license expression `MIT`, readme, symbols, and documentation XML. Adapter packages depend only on `AspectN` plus their native dependency.

**Step 3: Implement package verification**

`eng/verify-packages.ps1`:

- packs Release into `artifacts/packages`;
- opens each `.nupkg`;
- checks expected `lib/netstandard2.0` and `lib/net10.0` assets;
- rejects Cecil/Roslyn/Reflection dependencies in core;
- rejects adapter-to-adapter dependencies;
- builds temporary consumers from the packages;
- fails on warnings.

**Step 4: Run**

```powershell
dotnet test tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj -c Release -f net472
powershell -NoProfile -File eng/verify-packages.ps1
```

Expected: PASS on Windows.

**Step 5: Commit**

```powershell
git add AspectN.sln src tests/AspectN.NetFramework.Tests eng/verify-packages.ps1 README.md docs/language docs/migration
git commit -m "build: validate packages and net472 compatibility"
```

### Task 30: Add CI, compatibility matrices, and package artifacts

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/compatibility.yml`
- Create: `.github/workflows/release.yml`
- Create: `.github/dependabot.yml`

**Step 1: Add normal CI**

On pull request and push:

- restore;
- `dotnet format --verify-no-changes`;
- Release build;
- core/adapter/conformance tests on Windows and Ubuntu;
- net472 tests on Windows;
- package verification;
- upload TRX and packages.

**Step 2: Add compatibility CI**

Matrix:

- Cecil 0.11.5 and 0.11.6.
- Roslyn 4.12.0 and 5.6.0.
- Legacy oracle pinned checkout at `03a9d10`.

**Step 3: Add release workflow without automatic publication**

Tag workflow builds and signs/verifies artifacts but requires a protected GitHub environment before `dotnet nuget push`. Do not configure or invoke credentials during implementation.

**Step 4: Validate workflow commands locally**

Run every shell command used by the workflows. Use a YAML linter if already available; do not add a Node toolchain solely for linting.

**Step 5: Commit**

```powershell
git add .github
git commit -m "ci: add build compatibility and release gates"
```

---

## Phase 6 — Rougamo Migration

### Task 31: Prepare a local prerelease package and a Rougamo migration branch

**Repositories:**
- AspectN: `D:\Code\Hourglass\AspectN`
- Rougamo: `D:\Code\Hourglass\rougamo-eco\Rougamo`

**Files:**
- Create in Rougamo: `src/Rougamo.Fody/AspectN/RougamoAspectNMatcher.cs`
- Create in Rougamo: `test/Rougamo.Fody.Tests/AspectN/RougamoAspectNMatcherTests.cs`
- Modify in Rougamo: `src/Rougamo.Fody/Rougamo.Fody.csproj`

**Step 1: Pack without publishing**

```powershell
dotnet pack D:\Code\Hourglass\AspectN\AspectN.sln -c Release -p:Version=0.1.0-rougamo.1 -o D:\Code\Hourglass\AspectN\artifacts\packages
```

**Step 2: Create the direct Rougamo branch**

Verify Rougamo is clean, then:

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo switch -c codex/aspectn-migration
```

Do not create a worktree.

**Step 3: Add temporary package restore source and references**

Add and restore the package without committing a machine-absolute source:

```powershell
dotnet add D:\Code\Hourglass\rougamo-eco\Rougamo\src\Rougamo.Fody\Rougamo.Fody.csproj package AspectN.Cecil --version 0.1.0-rougamo.1 --no-restore
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --source D:\Code\Hourglass\AspectN\artifacts\packages --source https://api.nuget.org/v3/index.json
```

**Step 4: Write the facade test first**

The facade owns one `AspectNEngine` and one `CecilTypeSystem` per weave operation, compiles with `AspectNLanguageVersion.Legacy`, maps `Unknown` explicitly to false, and accepts `compositeAccessibility`.

**Step 5: Implement the facade and pass**

Keep old production `PatternParser` calls for now; this task only introduces and tests the new path.

**Step 6: Commit in Rougamo**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src/Rougamo.Fody test/Rougamo.Fody.Tests
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "test: add AspectN migration facade"
```

### Task 32: Run old and new Rougamo engines in differential mode

**Files:**
- Modify in Rougamo: `src/Rougamo.Fody/ModuleWeaver.cs`
- Modify in Rougamo: `src/Rougamo.Fody/FindRous.cs`
- Modify in Rougamo: `src/Rougamo.Fody/Extensions/ModelExtension.cs`
- Create in Rougamo: `src/Rougamo.Fody/AspectN/AspectNDifferentialMatcher.cs`
- Create in Rougamo: `test/Rougamo.Fody.Tests/AspectN/AspectNDifferentialTests.cs`

**Step 1: Inject weave-scoped lifetime**

Create/dispose the facade inside `ExecuteInternal`; pass it through `FindRous` to `ModelExtension.Initialize`. Do not use static state.

**Step 2: Preserve old production result**

In differential mode, compute both results, report a deterministic failure/log on disagreement, but return the old engine result. This allows proving parity before cutover.

**Step 3: Run the full matrix**

At minimum:

```powershell
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461
```

Expected: zero differences and all weaving tests pass.

**Step 4: Record the migration report**

Create `docs/aspectn-migration-report.md` in Rougamo with package hash, pattern count, candidate count, framework matrix, and benchmark comparison.

**Step 5: Commit**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src/Rougamo.Fody test/Rougamo.Fody.Tests docs/aspectn-migration-report.md
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "test: prove AspectN parity in Rougamo"
```

### Task 33: Cut Rougamo production matching over to AspectN

**Files:**
- Modify in Rougamo: `src/Rougamo.Fody/Extensions/ModelExtension.cs`
- Modify in Rougamo: `src/Rougamo.Fody/AspectN/RougamoAspectNMatcher.cs`
- Modify in Rougamo: signature tests under `test/Rougamo.Fody.Tests/Signatures`
- Create in Rougamo: `test/Rougamo.Fody.Tests/AspectN/AspectNIntegrationTests.cs`

**Step 1: Write the production-path integration test**

Use real `PointcutAttribute` patterns to verify ordinary methods, properties, constructors, async, attributes, and composite accessibility are woven exactly once.

**Step 2: Switch returned result to the new engine**

Remove differential production code only after the previous task is green. Keep explicit `Unknown -> false` compatibility policy with a diagnostic log.

**Step 3: Relocate low-level tests**

Delete Rougamo tests that only exercise old parser internals after their behavior exists in AspectN core/oracle tests. Keep Rougamo-level integration/weaving tests.

**Step 4: Run all Rougamo tests**

Run the solution's supported test matrix, including the net461 smoke lane. Do not claim AspectN officially supports net461.

**Step 5: Commit**

```powershell
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add src test
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "feat: migrate Rougamo matching to AspectN"
```

### Task 34: Publish an RC checkpoint, then remove the Cecil.AspectN submodule from Rougamo

**Files:**
- Modify in Rougamo: `src/Rougamo.Fody/Rougamo.Fody.csproj`
- Modify in Rougamo: `.gitmodules`
- Modify in Rougamo: `Rougamo.sln`
- Modify in Rougamo: `src/Rougamo/Metadatas/PointcutAttribute.cs`
- Remove in Rougamo: `shared/Cecil.AspectN`

**Step 1: Stop for RC publication approval**

Present the exact AspectN commit, `1.0.0-rc.1` package SHA-256 values, compatibility/parity results, and the Rougamo cutover commit. Do not push, tag, or publish before explicit maintainer approval.

After approval, publish all four `1.0.0-rc.1` packages through the protected release workflow. Restore them from the public NuGet source in a clean temporary consumer and verify package identity, dependencies, Source Link, symbols, and signatures.

**Step 2: Move Rougamo from the local development package to the published RC**

Replace `AspectN.Cecil` version `0.1.0-rougamo.1` with `1.0.0-rc.1`. Ensure no committed restore source refers to `D:\Code\Hourglass\AspectN\artifacts\packages`, then perform a clean public-source restore and the full Rougamo test matrix:

```powershell
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --force-evaluate --source https://api.nuget.org/v3/index.json
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0 --no-restore
```

Do not remove the submodule unless this clean public-source verification passes.

**Step 3: Verify exact destructive target**

Confirm:

- `shared/Cecil.AspectN` resolves inside the Rougamo root.
- It is at oracle commit `03a9d10`.
- It has no uncommitted files.
- `shared/Fody.Simulations` is a different submodule and remains untouched.

**Step 4: Remove only the Cecil submodule**

Use native Git submodule commands against the validated path:

```powershell
$rougamoRoot = [System.IO.Path]::GetFullPath('D:\Code\Hourglass\rougamo-eco\Rougamo')
$submodulePath = [System.IO.Path]::GetFullPath((Join-Path $rougamoRoot 'shared\Cecil.AspectN'))
if (-not $submodulePath.StartsWith($rougamoRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe submodule path.' }
git -C $rougamoRoot submodule deinit -f -- shared/Cecil.AspectN
git -C $rougamoRoot rm -f -- shared/Cecil.AspectN
```

Then remove only its `.gitmodules` section and remove the solution Shared Project entry/import mappings. Retain the already verified `AspectN.Cecil` `1.0.0-rc.1` package reference. The untracked `.git/modules/shared/Cecil.AspectN` metadata may be removed only after resolving and validating that exact path inside the Rougamo Git directory.

**Step 5: Update documentation link**

Point `PointcutAttribute` remarks and Rougamo docs to `https://github.com/AspectN/AspectN`.

**Step 6: Verify no old references**

```powershell
rg -n "Cecil\.AspectN|Shared\.Cecil\.AspectN|shared/Cecil\.AspectN|shared\\Cecil\.AspectN" D:\Code\Hourglass\rougamo-eco\Rougamo
```

Expected: no production, test, project, solution, or documentation references.

**Step 7: Run full build/tests and commit**

```powershell
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add -- .gitmodules Rougamo.sln src/Rougamo.Fody/Rougamo.Fody.csproj src/Rougamo/Metadatas/PointcutAttribute.cs
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "build: remove Shared.Cecil.AspectN submodule"
```

After deletion, report that the removed submodule content remains recoverable from commit `03a9d10` and the archived repository.

---

## Phase 7 — Performance, Release, and Archive Gates

### Task 35: Add benchmarks and a reviewed performance baseline

**Files:**
- Create: `benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj`
- Create: `benchmarks/AspectN.Benchmarks/Program.cs`
- Create: `benchmarks/AspectN.Benchmarks/ParsingBenchmarks.cs`
- Create: `benchmarks/AspectN.Benchmarks/MatchingBenchmarks.cs`
- Create: `benchmarks/AspectN.Benchmarks/AdapterBenchmarks.cs`
- Create: `benchmarks/AspectN.Benchmarks/LegacyComparisonBenchmarks.cs`
- Create: `docs/performance/1.0-baseline.md`
- Modify: `AspectN.sln`

**Step 1: Benchmark required paths**

- Lex/parse.
- Bind/compile.
- Cached hot match.
- Assignability-heavy match.
- Cecil/Roslyn/Reflection mapping.
- Old oracle versus new Legacy Cecil path.

**Step 2: Use representative parameter sizes**

Include short, typical, and long expressions; cold and hot adapter caches; resolved and unresolved references.

**Step 3: Run Release benchmarks**

```powershell
dotnet run -c Release --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -- --filter "*"
```

**Step 4: Record environment and results**

Do not create a brittle CI microbenchmark threshold. Gate on reviewed BenchmarkDotNet reports and explain any statistically significant regression in the hot Rougamo path.

**Step 5: Commit**

```powershell
git add AspectN.sln benchmarks docs/performance
git commit -m "perf: add AspectN 1.0 benchmark baseline"
```

### Task 36: Complete documentation and 1.0 release checklist

**Files:**
- Modify: `README.md`
- Create: `CHANGELOG.md`
- Create: `docs/language/standard.md`
- Create: `docs/language/legacy.md`
- Create: `docs/adapters/cecil.md`
- Create: `docs/adapters/roslyn.md`
- Create: `docs/adapters/reflection.md`
- Create: `docs/extensibility.md`
- Create: `docs/diagnostics.md`
- Create: `docs/release/1.0-checklist.md`

**Step 1: Document public usage**

Show parse diagnostics, compile, match, `Unknown`, engine cache ownership, and all three adapter entry points. Do not use old CLR API names in new examples.

**Step 2: Document migration**

Include:

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

Keep `ctor(...)` and `cctor(...)`.

**Step 3: Complete the release checklist**

Require:

- all tests and compatibility matrices green;
- zero unapproved Legacy differences;
- package verification and API files clean;
- net472 pass and Rougamo net461 smoke pass;
- cross-adapter conformance pass;
- regex timeout/fuzz pass;
- benchmark report reviewed;
- no global mutable caches;
- no native backend types in core public API;
- no Rougamo submodule reference.

**Step 4: Build final candidate without publishing**

```powershell
dotnet clean AspectN.sln -c Release
dotnet restore AspectN.sln
dotnet format AspectN.sln --verify-no-changes
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build
powershell -NoProfile -File eng/verify-packages.ps1
```

**Step 5: Commit**

```powershell
git add README.md CHANGELOG.md docs
git commit -m "docs: complete AspectN 1.0 documentation"
```

### Task 37: Publish only after explicit maintainer approval

**Files:**
- Modify after approval: `src/*/PublicAPI.Shipped.txt`
- Modify after approval: `CHANGELOG.md`
- Modify after publication in Rougamo: `src/Rougamo.Fody/Rougamo.Fody.csproj`
- Tag after approval: `v1.0.0`

**Step 1: Stop for approval**

Present:

- exact commit;
- package SHA-256 values;
- test/compatibility summary;
- Legacy difference report;
- benchmark report;
- Rougamo migration commit.

Do not push, tag, publish, or create a release before approval.

**Step 2: Promote API baseline**

Move approved entries from each `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`.

**Step 3: Rebuild exact artifacts**

Build from a clean checkout of the approved commit and confirm hashes.

**Step 4: Tag/publish**

After explicit approval, create signed/annotated `v1.0.0`, push the branch/tag, and approve the protected release workflow.

**Step 5: Move Rougamo from the RC to stable**

After all four `1.0.0` packages are visible and verified from the public NuGet source, change Rougamo's `AspectN.Cecil` reference from `1.0.0-rc.1` to `1.0.0`. Run a clean restore, build, and the full Rougamo test matrix before committing:

```powershell
dotnet restore D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln --force-evaluate --source https://api.nuget.org/v3/index.json
dotnet build D:\Code\Hourglass\rougamo-eco\Rougamo\Rougamo.sln -c Release --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net48 --no-restore
dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net8.0 --no-restore
git -C D:\Code\Hourglass\rougamo-eco\Rougamo add -- src/Rougamo.Fody/Rougamo.Fody.csproj
git -C D:\Code\Hourglass\rougamo-eco\Rougamo commit -m "build: use AspectN 1.0"
```

Rougamo must not be released while it still references the RC.

### Task 38: Archive Shared.Cecil.AspectN safely

**Repository:**
- `D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN`

**Files:**
- Modify only after reconciling existing maintainer edits: `README.md`
- Modify: `README_en.md`

**Step 1: Preserve current user work**

The current `README.md` has uncommitted Chinese documentation additions. Stop and ask the maintainer whether to commit them, incorporate them into the archive notice, or leave them on another branch. Never overwrite or discard them.

**Step 2: Add archive notices on a direct branch**

State that:

- production code is frozen at `03a9d10`;
- the successor is AspectN;
- Rougamo no longer consumes the submodule;
- history remains for compatibility investigation.

**Step 3: Verify links and commit**

Commit documentation only. Do not change any old `.cs` or `.projitems` file.

**Step 4: External archive checkpoint**

Ask the repository owner to archive the GitHub repository after AspectN 1.0 and Rougamo migration are public. Repository archival is an explicit owner action, not an automatic implementation step.

---

## Final Verification Matrix

Run all rows before declaring the implementation complete:

| Surface | Command / gate | Expected |
|---|---|---|
| Core | `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release` | PASS |
| Cecil baseline | `dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.5` | PASS |
| Cecil current | `dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6` | PASS |
| Roslyn baseline | `dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=4.12.0` | PASS |
| Roslyn current | `dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0` | PASS |
| Reflection unload | `dotnet test tests/AspectN.Reflection.Tests/AspectN.Reflection.Tests.csproj -c Release --filter FullyQualifiedName~CollectibleAssemblyTests` | PASS |
| Cross-adapter | `dotnet test tests/AspectN.Conformance.Tests/AspectN.Conformance.Tests.csproj -c Release` | Equal/approved Unknown only |
| Legacy oracle | `dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release` | Zero unapproved differences |
| .NET Framework | `dotnet test tests/AspectN.NetFramework.Tests/AspectN.NetFramework.Tests.csproj -c Release -f net472` | PASS |
| Rougamo smoke | `dotnet test D:\Code\Hourglass\rougamo-eco\Rougamo\test\Rougamo.Fody.Tests\Rougamo.Fody.Tests.csproj -c Release -f net461` | PASS, non-official |
| Packages | `powershell -NoProfile -File .\eng\verify-packages.ps1` | Four valid packages |
| Formatting | `dotnet format AspectN.sln --verify-no-changes` | No changes |
| API | `dotnet build AspectN.sln -c Release -p:RunAnalyzers=true -p:TreatWarningsAsErrors=true` | No unapproved changes |
| Performance | `dotnet run --project benchmarks/AspectN.Benchmarks/AspectN.Benchmarks.csproj -c Release -- --filter *` plus review of `docs/performance/1.0-baseline.md` | No unexplained hot regression |

## Completion Definition

The project is complete only when:

1. All four packages build for `netstandard2.0;net10.0`.
2. Standard syntax/semantics match the language specification.
3. Legacy Cecil behavior has zero unapproved differences at the pinned oracle revision.
4. Equivalent Roslyn/Cecil/Reflection candidates produce conformant results.
5. Rougamo uses the packaged AspectN.Cecil path and no longer contains the Cecil.AspectN submodule.
6. The release checklist is approved and the old Shared repository is archived without losing its existing README work.
