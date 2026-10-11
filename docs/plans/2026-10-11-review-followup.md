# Review follow-up implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Repair the accepted REVIEW.md findings and record the maintainer's decisions in task-scoped local commits.

**Architecture:** Preserve the backend-neutral core and existing adapter boundaries. Fix each adapter's query behavior and diagnostics, keep constructor shape matching, require adjacent `!ref`, and remove test output-layout assumptions.

**Tech Stack:** C#, xUnit, FsCheck, Mono.Cecil, Roslyn, Reflection, MSBuild, GitHub Actions.

## Scope and decisions

- M1 is already addressed by `07fea5c`; L2 by `9309bcc`. Do not redo either change.
- H1: maintainer decides not to fix. Mapper inputs must contain required ReturnType/ElementType information. Keep exceptions for incomplete graphs; do not introduce Unsupported/Unknown fallback or improve exception messages in this task. Reassess only with an actual assembly reproduction; this is not a merge blocker.
- L7: retain constructor and static-constructor shape matching. Document its distinction from literal declaring-type member paths and add matching regressions.
- L8: `!ref` requires adjacency.
- M5 is a false positive: NuGet selects a symbol upload endpoint. Simplify redundant package enumeration without treating it as a publication blocker.
- L6 is a hash quality/allocation improvement; L15 is a generator invariant test, retained alongside stronger long-input parser fuzz coverage.
- Work in the existing checkout. Preserve untracked REVIEW.md and nul. Do not push, publish, or modify adjacent repositories.

## Batch 1: adapter query correctness (M2, M3, M4, L3, L4, L5)

Files: CecilTypeSystem.cs, CecilSignatureMapper.cs, Internal/CecilResolver.cs, RoslynTypeSystem.cs, ReflectionTypeSystem.cs and focused adapter regression tests.

1. Add regressions for wrapper attributes, detached generic constraints, unreadable Roslyn siblings and resolution diagnostics, cancelled closure fast paths, and preserved Reflection uncertainty.
2. Run the focused tests and record expected failures before production edits.
3. Return no direct type attributes for array/pointer/function-pointer/pinned/sentinel shapes, retaining named generic behavior.
4. Use the live Cecil adapter module for implicit generic roots when a parameter's module is absent; do not resolve detached reference owners, and preserve Unknown/AN3004 for unavailable declarations.
5. Catch recoverable Roslyn symbol failures per queued node, preserve known sibling branches, use AN3004, and check cancellation before closure fast paths.
6. Preserve `unresolved ?? MissingNativeReference(candidate)` in Reflection.
7. Run all affected adapter suites, including same-source Cecil.Fody, then commit.

## Batch 2: parsing and language contract (L1, L7, L8, L9)

Files: Lexer.cs, SelectorParser.cs, parser/constructor regression tests, language spec and design decisions.

1. Add failures proving invalid `\\c` must preserve the selector delimiter and subsequent OR, while valid control escapes stay legal; add spaced `! ref` rejection.
2. Add positive constructor/cctor tasklike shape tests plus literal declaring-type member path tests.
3. Consume a third regex escape character only for legal control characters; check span adjacency for member ref negation.
4. Document constructor shape matching, strict `!ref`, and Rougamo filtering after matching.
5. Run core and relevant conformance tests, then commit.

## Batch 3: identity hash (L6)

Files: Metadata/TypeIdentity.cs and identity regression tests.

1. Add regression for reversed containing chains and a warmed-up allocation check.
2. Run to demonstrate current order-insensitive hashes and allocations.
3. Fold local hashes in chain order with multiplication, without a Stack allocation.
4. Run identity/core tests and commit.

## Batch 4: conformance and fuzz coverage (L10, L11, L12, L13, L15)

Files: ReviewFixture.cs, CustomAttributeRecordTests.cs, MappingDepthTests.cs, conformance project file and focused fixture/fuzz tests.

1. Add private/internal method mapping regressions; verify current helper failures.
2. Enable all metadata imports and all relevant Reflection member visibility flags.
3. Extend return and declaring-type attribute comparisons to Roslyn PE.
4. Copy the probe's runnable output and dependencies into test output at build time; resolve from AppContext.BaseDirectory. Verify the existing isolated-process cases from both default and relocated outputs.
5. Retain the bounded generator invariant and add bounded-count long Unicode parser cases with termination/span assertions.
6. Run conformance/core tests, then commit.

## Batch 5: release flow and outcome record (M5, L14)

Files: .github/workflows/release.yml and docs/reviews/2026-10-11-review-followup.md.

1. Add per-tag concurrency with `cancel-in-progress: false` and enumerate nupkg only for publication, preserving symbol signing/verification.
2. Inspect the final YAML and record all dispositions, tests, and commit evidence.
3. Run `dotnet build AspectN.sln -c Release --no-restore`, `dotnet test AspectN.sln -c Release --no-build --no-restore`, and `dotnet format AspectN.sln --verify-no-changes --no-restore`.
4. Request independent code review under requesting-code-review; repair material findings and rerun affected checks.
5. Commit release changes and the completion record. Verify only original untracked files remain.

The user has approved implementation and batched commits in this chat; execute without further design approval or checkpoint pauses.
