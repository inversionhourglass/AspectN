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
