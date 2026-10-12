# Repository Guidelines

## Project Structure & Module Organization

AspectN is a backend-neutral expression parser and matcher with one language contract: the existing Standard grammar. The target layout is defined in `docs/plans/2026-07-31-aspectn-industrial-redesign.md`; its Legacy compatibility requirements are superseded by `docs/plans/2026-10-03-single-language-aspectn.md`. Core syntax, semantics, signatures, and matching belong in `src/AspectN`; backend types must not enter its public API. Mono.Cecil, Roslyn, and Reflection mappings live in `src/AspectN.Cecil`, `src/AspectN.Roslyn`, and `src/AspectN.Reflection`; Fody consumes the Cecil sources through `src/AspectN.Cecil.Fody`.

`src/Shared` contains adapter-only sources compiled into each backend assembly under `AspectN.AdapterInternals`; they are not a separate assembly and must not enter the core public API.

Mirror production projects under `tests/`. Keep cross-adapter conformance, .NET Framework compatibility, and fixture assemblies in their dedicated test projects. Place benchmarks in `benchmarks/`, engineering scripts in `eng/`, and normative architecture/language documents in `docs/superpowers/specs/`.

## Build, Test, and Development Commands

Use:

- `dotnet restore AspectN.sln` — restore centrally managed packages.
- `dotnet build AspectN.sln -c Release --no-restore` — compile all libraries with release warnings.
- `dotnet test AspectN.sln -c Release --no-build --no-restore` — run the complete test matrix.
- `dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release` — run one focused suite.
- `dotnet format AspectN.sln --verify-no-changes --no-restore` — verify formatting before committing.

## Coding Style & Naming Conventions

Use C# with four-space indentation and file-scoped namespaces. Private instance fields are `_camelCase`, private static fields are `_PascalCase`, and constants are `UPPER_SNAKE_CASE`. Prefer guard clauses and early exits to deep nesting. See `.agents/rules/csharp-style.md`; `.editorconfig` is authoritative where automation is possible.

## Testing Guidelines

Use xUnit for examples and FsCheck for bounded property tests. Name test classes after the subject and tests as `Method_State_ExpectedResult`. Keep fixtures deterministic. Preserve Standard regression coverage, including generic arity, variable scope, attributes, diagnostics, and adapter lifetimes. Intentional language changes require specification and conformance updates. There is no Legacy oracle or approved-differences baseline in the solution.

## Commit & Pull Request Guidelines

Follow the existing Conventional Commit style, such as `feat(core): add lexer`, `test: prove Cecil parity`, or `docs: clarify syntax`. Keep commits task-scoped. PRs must explain behavior and architecture impact, link relevant issues/specs, and list exact verification commands. Include compatibility, public API, package, or benchmark evidence when those surfaces change.

## Agent-Specific Instructions

### Documentation and knowledge-base pilot

For user guidance, start at `docs/usage/index.md`. For capability changes or review,
start at `docs/knowledge/index.md`, load only affected entries and their declared
dependencies, and use `docs/knowledge/review/index.md` for the review process.
The approved workflow design is `docs/superpowers/specs/2026-10-12-aspectn-documentation-knowledge-base-design.md`.

For the documented pilot capabilities, declare affected capability/scenario IDs;
update the relevant knowledge entry, user-visible guidance, and evidence in the
same reviewable change, or state a concrete no-impact reason. For unassessed
surfaces, add/update `docs/knowledge/coverage.json` and follow existing specification
and conformance rules. Do not infer exclusion from missing documentation or mark
verification complete merely because test source exists. Reassess evidence when
its implementation, tests, contract or environment changes; preserve old reports.
Run `pwsh -NoProfile -File eng/verify-documentation.ps1` for these changes.
Finding references initially warn; semantic approval, N/A reasoning and current
evidence applicability require review. New/changed pilot N/A records need a
non-author reviewer and date. Full-repository mandatory synchronization awaits
the readiness gate and maintainer confirmation; the pilot is not a full support matrix.

Work in the existing checkout; do not create Git worktrees. Preserve existing untracked files. Do not publish packages, push branches, remove Rougamo submodules, or modify adjacent repositories or the frozen oracle cache without explicit authorization. Rougamo migration and major-version publication are separate tasks.
