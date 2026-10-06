# Review Remediation Implementation Plan

> Execute the approved repairs in the existing checkout, with failing regressions before production changes.

**Goal:** Repair R1, R2, R4, R5 and R6, and enforce the maintainer's clarification of generic variable declarations in R3. R1 equivalent-state deduplication was approved on 2026-10-06; resource budgets are outside this change.

**Architecture:** Preserve backend-neutral signatures and three-state matching. Keep generic declaration identity separate from constructed backend context. Expose immutable syntax snapshots. Variables are declared by type/method generic clauses; return and parameter types only reference declared variables, and generic variable declarations cannot be negated.

**Tech Stack:** C#, xUnit, Cecil/Fody, Reflection, Roslyn, net10.0/netstandard2.0/net472.

## 1. Immutable syntax (R6)

- Add cached AST mutation and caller-owned ref-token collection regressions in `tests/AspectN.Tests/Syntax`.
- Run the focused suite and confirm the regressions fail.
- Wrap syntax collection snapshots in read-only collections, including `TypeUseSyntax.NegatedRefTokens`; preserve node child consistency and public signatures.
- Rerun focused syntax and core tests.

## 2. Three-state alternatives (R2/R1)

- Add partial-closure OR regressions where a later constraint removes the known matching branch; include member and attribute alternatives.
- Confirm the tests fail before changing `EvaluationContext`, `BindingEnvironment` and evaluators.
- Preserve possible unknown alternatives and their unknown state through conjunction and negation.
- Coalesce equivalent environments without collapsing distinct bindings or losing unknown states, as approved. Verify a short repeated-wildcard expression uses bounded allocation; introduce no new budget policy.
- Run evaluation and core suites.

## 3. Declaration identity (R4/R5)

- Add conformance regressions for extern-alias overloads and closed-owner method generic parameters, including context-specific constraints.
- Confirm failures before changing the three owner identity encoders and Cecil cache context handling.
- Include recursive named parameter assembly identity while preserving canonical structural shapes. Normalize Cecil method owners to declarations, retaining constructed native query context separately.
- Run all adapter, same-source Fody and conformance suites.

## 4. Generic variable contract (R3)

- Add rejection tests for undeclared return/parameter variables and negated generic variable definitions, plus positive method/type declaration references.
- Retain declaration-first signature binding; add explicit declaration versus reference context.
- Update affected regressions and normative/conformance documentation to the maintainer's clarified contract. Preserve existing arity, negation and branch-scope rules that do not conflict with that clarification.
- Run binding and full core suites.

## 5. Final verification

- `dotnet restore AspectN.sln`
- `dotnet build AspectN.sln -c Release --no-restore`
- `dotnet test AspectN.sln -c Release --no-build --no-restore`
- `dotnet format AspectN.sln --verify-no-changes --no-restore`
- Validate five local packages and temporary consumers under a new artifact directory.
- Request an independent read-only review, fix material defects, and report remaining decisions. Preserve existing untracked files; do not push or publish.

## Completion evidence — 2026-10-06

The repairs were verified in the working tree on `codex/aspectn-redesign-spec`
before committing. Existing untracked review documents and `nul` were
preserved. No adjacent repository, worktree, push or publication was involved.

- Failing regressions preceded their production fixes: mutable syntax snapshots,
  discarded unknown alternatives, repeated equivalent OR allocation, invalid
  generic variable declarations, cross-assembly overload identities and closed
  declaring-type contexts.
- Independent review also exposed closed `T`/`int` overloads, inherited method
  references, and vararg call sites. Real C#/IL regressions were added and their
  declaration-resolution failures were repaired. All seven method-owner identity
  tests pass, including recursive array/generic parameter assembly identities and
  function-pointer display preservation.
- The 212-character chain of eighteen equivalent `(* || *)` terms allocated
  125,848,488 bytes before deduplication. The regression now passes its less-than-
  5 MiB allocation bound with a `Matched` result. No stack overflow was reproduced;
  the observed issue was allocation growth and the resulting OOM risk. No resource
  budget, limit or new failure policy was introduced.
- R3's proposed source-order binding change was withdrawn. Declaration-first
  binding is retained. Negated declaration arguments and undeclared return or
  parameter variables are rejected, while references to declared variables remain
  valid regardless of textual order. The language specification and conformance
  cases reflect the maintainer's clarification.
- Independent read-only reviews of evaluation, parser/binding/syntax and adapters
  all finished with no remaining actionable findings.

Final validation:

| Command | Result |
| --- | --- |
| `dotnet restore AspectN.sln` | Passed |
| `dotnet build AspectN.sln -c Release --no-restore` | Passed, 0 warnings and 0 errors |
| `dotnet test AspectN.sln -c Release --no-build --no-restore` | 1,252 passed, 0 failed, 0 skipped |
| `dotnet format AspectN.sln --verify-no-changes --no-restore` | Passed |
| `git diff --check` | Passed |
| `./eng/verify-packages.ps1 -PackageDirectory artifacts/review-remediation-20261006-final2-packages` | Five packages and symbol packages, both dependency groups, ten consumer builds/runs passed |

The test matrix includes 752 core, 116 Cecil, 118 Fody, 82 Reflection, 19 Roslyn,
152 conformance, 7 net10.0/netstandard consumption and 6 net472 tests. Local package
validation used version `0.0.0-validation-3eb31011785d` and did not publish packages.
