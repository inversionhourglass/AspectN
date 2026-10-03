# Contributor Guidelines Implementation Plan

> **Historical guidance:** Legacy oracle and approved-differences requirements in this plan are superseded by the [single-language plan](2026-10-03-single-language-aspectn.md). Current contributor instructions are in the root AGENTS.md.

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a concise repository guide, enforceable C# formatting defaults, and detailed agent-facing C# rules.

**Architecture:** `AGENTS.md` is the short contributor entry point and links to `.agents/rules/csharp-style.md`. `.editorconfig` carries rules Roslyn can enforce; the rule document covers semantic control-flow preferences that EditorConfig cannot express. The maintainer's existing `.gitignore` change is preserved and committed with the three new files.

**Tech Stack:** Markdown, EditorConfig, Roslyn/.NET naming rules, Git.

---

## Execution Constraints

- Work directly in the current repository and branch; do not create a worktree.
- Preserve every existing `.gitignore` line. The maintainer has already removed `.agents` from the ignore list.
- Do not run solution build/test commands because `AspectN.sln` and the projects are target-state files that do not exist yet.
- Stage only `.gitignore`, `.editorconfig`, `.agents/rules/csharp-style.md`, and `AGENTS.md` in the implementation commit.

### Task 1: Add machine-checkable C# conventions

**Files:**
- Create: `.editorconfig`

**Step 1: Create the root EditorConfig**

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

The private-constant intersection rule prevents private `const` fields (which are implicitly static) from inheriting the private-static naming style.

**Step 2: Verify the required keys**

Run:

```powershell
rg -n "file_scoped|private_constants|private_static_fields|underscore_pascal_case|upper_snake_case" .editorconfig
```

Expected: every namespace and naming rule is present.

### Task 2: Add detailed C# rules

**Files:**
- Create: `.agents/rules/csharp-style.md`

**Step 1: Write the detailed rule document**

```markdown
# C# Style Rules

These rules apply to every C# project in this repository. `.editorconfig` is authoritative for machine-checkable formatting and naming.

## Namespaces and indentation

- Use four spaces; never use tabs.
- Use file-scoped namespaces: `namespace AspectN.Syntax;`.
- Keep one primary type per file and match the file name to that type.

## Field naming

- Private instance fields use `_camelCase`, for example `_sourceText`.
- Private static fields, including `static readonly`, use `_PascalCase`, for example `_DefaultOptions`.
- Constants use `UPPER_SNAKE_CASE`, for example `DEFAULT_CACHE_SIZE`.

## Control flow

Prefer guard clauses and early `return`, `continue`, or `break` statements. Handle exceptional or disqualifying conditions first so the main path stays at the lowest indentation level.

```csharp
public void Process(List<int> values)
{
    if (values.Count < 10) return;

    ProcessCore(values);
}
```

Do not wrap the main path in the inverse condition:

```csharp
if (values.Count >= 10)
{
    ProcessCore(values);
}
```

When a simple condition is followed only by `return`, `continue`, or `break`, keep it on one line and omit braces:

```csharp
if (candidate is null) return;
if (!candidate.IsSupported) continue;
if (matchFound) break;
```

Use braces when a branch has multiple statements, contains explanatory comments, or placing it on one line would reduce readability. Do not compress ordinary business logic into one-line conditionals.
```

**Step 2: Verify the rules file is trackable**

Run:

```powershell
git check-ignore .agents/rules/csharp-style.md
```

Expected: no output and exit code `1`.

### Task 3: Add the repository entry guide and commit

**Files:**
- Create: `AGENTS.md`
- Modify: `.gitignore` (maintainer-owned change; preserve as-is)

**Step 1: Write the concise root guide**

```markdown
# Repository Guidelines

## Project Structure & Module Organization

AspectN is being rebuilt as a backend-neutral expression parser and matcher. The approved target layout is defined in `docs/plans/2026-07-31-aspectn-industrial-redesign.md`. Core syntax, semantics, signatures, and matching belong in `src/AspectN`; backend types must not enter its public API. Mono.Cecil, Roslyn, and Reflection mappings live in `src/AspectN.Cecil`, `src/AspectN.Roslyn`, and `src/AspectN.Reflection`.

Mirror production projects under `tests/`. Keep cross-adapter conformance, Legacy oracle, .NET Framework compatibility, and fixture assemblies in their dedicated test projects. Place benchmarks in `benchmarks/`, engineering scripts in `eng/`, and normative architecture/language documents in `docs/superpowers/specs/`.

## Build, Test, and Development Commands

After the solution bootstrap task has landed, use:

- `dotnet restore AspectN.sln` — restore centrally managed packages.
- `dotnet build AspectN.sln -c Release --no-restore` — compile all libraries with release warnings.
- `dotnet test AspectN.sln -c Release --no-build` — run the complete test matrix.
- `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release` — run one focused suite.
- `dotnet format AspectN.sln --verify-no-changes` — verify formatting before committing.

## Coding Style & Naming Conventions

Use C# with four-space indentation and file-scoped namespaces. Private instance fields are `_camelCase`, private static fields are `_PascalCase`, and constants are `UPPER_SNAKE_CASE`. Prefer guard clauses and early exits to deep nesting. See `.agents/rules/csharp-style.md`; `.editorconfig` is authoritative where automation is possible.

## Testing Guidelines

Use xUnit for examples and FsCheck for bounded property tests. Name test classes after the subject and tests as `Method_State_ExpectedResult`. Keep fixtures deterministic. Legacy behavior changes require an explicit entry in `approved-differences.json`; never update compatibility baselines silently.

## Commit & Pull Request Guidelines

Follow the existing Conventional Commit style, such as `feat(core): add lexer`, `test: prove Cecil parity`, or `docs: clarify syntax`. Keep commits task-scoped. PRs must explain behavior and architecture impact, link relevant issues/specs, and list exact verification commands. Include compatibility, public API, package, or benchmark evidence when those surfaces change.

## Agent-Specific Instructions

Work in the existing checkout; do not create Git worktrees. Do not publish packages, push branches, remove Rougamo submodules, or modify the frozen Legacy oracle without the explicit checkpoints in the implementation plan.
```

**Step 2: Verify length, links, formatting, and scope**

Run:

```powershell
$words = ((Get-Content -Raw AGENTS.md) -split '\s+' | Where-Object { $_ }).Count
if ($words -lt 200 -or $words -gt 400) { throw "AGENTS.md has $words words." }
Test-Path .agents/rules/csharp-style.md
Test-Path docs/plans/2026-07-31-aspectn-industrial-redesign.md
git diff --check
git status --short
```

Expected: word count is 200–400, both paths return `True`, no whitespace errors, and only the four intended implementation files are dirty.

**Step 3: Stage exact files and commit**

```powershell
git add -- .gitignore .editorconfig .agents/rules/csharp-style.md AGENTS.md
git diff --cached --check
git commit -m "docs: add repository contributor guidelines"
```
