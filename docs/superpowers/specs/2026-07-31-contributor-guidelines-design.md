# Contributor Guidelines Design

## Goal

Add concise, repository-specific contributor guidance for the planned AspectN implementation without turning the root guide into a complete style manual.

## Deliverables

- `AGENTS.md` is the entry point. It uses the title “Repository Guidelines”, stays within roughly 200–400 English words, and summarizes the target project layout, architecture boundaries, development commands, testing expectations, commit/PR conventions, and links to detailed rules.
- `.editorconfig` contains machine-checkable C# formatting and naming rules: four-space indentation, UTF-8, file-scoped namespaces, `_camelCase` private instance fields, `_PascalCase` private static fields, and `UPPER_SNAKE_CASE` constants.
- `.agents/rules/csharp-style.md` is the detailed human-readable C# guide. It explains early exits, reduced nesting, and same-line unbraced `return`, `continue`, and `break` statements, with compact examples.
- `.gitignore` retains the maintainer’s current AI-tool exclusions while leaving `.agents/rules/**` trackable.

## Repository Model

The guide describes the approved target structure from the industrial redesign plan rather than only the current documentation-only skeleton:

- `src/AspectN` contains the backend-neutral parser, semantic model, signatures, and matcher.
- `src/AspectN.Cecil`, `src/AspectN.Roslyn`, and `src/AspectN.Reflection` contain adapter-specific mapping and type-system logic.
- Matching test projects mirror production projects under `tests/`; conformance, Legacy oracle, .NET Framework, and fixture assemblies remain separate.
- `benchmarks/`, `eng/`, and `docs/` contain performance suites, engineering scripts, and specifications.

## Verification and Commit

Validate Markdown word count and links, inspect `.editorconfig` naming-rule keys, and confirm Git tracks `.agents/rules/csharp-style.md`. Because production projects do not exist yet, no build or test command is expected to run during this documentation task. Commit `AGENTS.md`, `.editorconfig`, `.agents/rules/csharp-style.md`, and the maintainer’s `.gitignore` change together in one scoped commit.
