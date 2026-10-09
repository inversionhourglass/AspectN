# Final Review Remediation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Repair the verified review findings and implement the maintainer-approved contracts, with regression evidence and separate local commits.

**Architecture:** Keep the Standard language and backend-neutral public model. Normalize resolved type identity, expose only real custom attributes, enforce ordered parameter positions, and map native type graphs iteratively without introducing a new depth or binding budget.

**Tech Stack:** C#, .NET 10/netstandard2.0/net472, xUnit/FsCheck, Mono.Cecil, Roslyn, Reflection, PowerShell.

## Approved design and scope

The maintainer approved the recommended directions on 2026-10-09 and authorized implementation and task-scoped local commits. Use the existing checkout. Preserve all existing untracked files; do not stage the four review reports or `nul`. Do not push, publish, modify adjacent repositories, or change release/environment approval policy.

- `attr` consumes actual CustomAttribute entries. Interop attributes encoded as flags and runtime pseudo attributes are excluded consistently, including Roslyn source symbols. Arrays do not inherit element attributes.
- Named type identity uses the resolved definition assembly when available; unresolved references retain their reference assembly identity.
- Method/property parameter lists use declaration order, with `Position == index`; malformed lists are rejected, never silently reordered.
- Consecutive nullable suffixes are invalid; valid nested nullable uses remain supported.
- Identifier categories follow the C# Unicode category profile, including Nl; do not expand to UAX #31 Other_ID_Start or add escaped-name syntax.
- Binding alternative budgets remain deferred. Preserve meaningful bounded regressions and explicitly document growth with distinct bindings.
- Adapter mapping uses explicit work stacks, retaining existing cache/native associations and lifetime contracts. Do not impose a new native shape limit.
- Reflection's previously approved open constraint context boundary remains unchanged.

## Batch 1: Core language and public contract

Files: `src/AspectN/Parsing/TypePatternParser.cs`, `Lexer.cs`, `SelectorParser.cs`, `src/AspectN/Binding/Binder.cs`, `src/AspectN/Evaluation/TypePatternEvaluator.cs`, `src/AspectN/Metadata/MethodSignature.cs`, `PropertySignature.cs`, `GenericParameterSignature.cs`, `src/AspectN/AspectNCompilation.cs`, relevant core tests/public API baselines, language specification.

1. Add regression tests for standalone `!ref`, repeated nullable suffixes, Nl identifiers, open Nullable equivalence, invalid parameter positions, and nullable TryGet consumption. Run focused tests and record the intended failures.
2. Add the missing parser guard, reject nested direct nullable suffix nodes with AN2010, recognize the C# identifier category profile, use effective Nullable arguments, validate copied parameter lists, and annotate successful TryGet results. Clarify the flattened generic parameter slot contract.
3. Run core tests, solution build, and formatting. Request independent review and resolve actionable findings.
4. Commit the batch as `fix(core): enforce language and signature contracts`.

## Batch 2: Cecil identities, metadata failures and relationship queries

Files: `src/AspectN.Cecil/Internal/CecilTypeMapper.cs`, `CecilResolver.cs`, `src/AspectN.Cecil/CecilSignatureMapper.cs`, `src/AspectN.Reflection/ReflectionTypeSystem.cs`, Cecil/Reflection/conformance tests, identity/availability documents.

1. Add ordinary SDK/ref-pack reference identity, damaged InterfaceImpl TypeSpec, array element attribute isolation, and legal `I<int*[]>`/`I<int[]>` relation regressions. Confirm each fails before implementation.
2. Normalize resolvable assembly identities, protect node expansion with recoverable-failure diagnostics, isolate array declaration attributes, and defer Unknown until all known relation branches are considered.
3. Run Cecil, linked Fody, Reflection and conformance tests; format and request independent review.
4. Commit as `fix(adapters): preserve neutral identities and relation evidence`.

## Batch 3: Real attributes and Reflection metadata

Files: Reflection/Roslyn signature mappers, shared adapter-only attribute policy if needed, Reflection type system, relevant mapper/conformance fixtures/tests, attribute/availability documents.

1. Add source/PE/Cecil/Reflection parity for out/in/optional and explicit pseudo attributes, ordinary marker attributes, nested pointers, arity names without suffixes, and detached by-ref availability. Verify red outcomes.
2. Filter metadata-flag pseudo attributes consistently without removing true attribute blobs, preserve by-reference facts without guessing classification, canonicalize pointers, recover own generic arity, and report read failures as AN3004.
3. Run adapter/conformance tests, netstandard build, format and independent review.
4. Commit as `fix(adapters): align custom attributes and reflection metadata`.

## Batch 4: Iterative native type mapping

Files: all three signature mappers, native type caches/keys as necessary, `src/Shared` adapter-only work-stack support if warranted, depth and lifetime tests.

1. Add deep array/generic/containing-type mapping regressions using isolated processes where stack overflow would kill the test host. Preserve ordinary nested generic identity, shared graph cache reuse, and collectible/module lifetime tests.
2. Replace recursive shape and containing identity mapping with explicit dependency stacks; retain backend normalization, stable references, weak Reflection cache semantics and locking. Check that cache-key generation does not reintroduce native recursion.
3. Run mapping, lifetime, Fody and conformance suites and independent review. Validate that deep mapping succeeds and the existing Match guard still returns AN3005.
4. Commit as `refactor(adapters): map native type graphs iteratively`.

## Batch 5: Coverage and engineering follow-up

Files: conformance missing-ancestor fixture/helper and attribute snapshot filter, binding alternative tests, `eng/verify-packages.ps1`, `.github/workflows/ci.yml`, README, language/resource documentation, small verified test/spec naming fixes.

1. Repair misleading Reflection fallback coverage and empty Review attribute snapshots with tests that distinguish native input from detached input and verify actual attribute fields.
2. Fix the package consumer's literal backtick, assert the intended owner ID, and exercise TryGet guarded consumption. Main CI default dependency versions come from CPM; preserve explicit alternative matrices. Do not change release approval/gates.
3. Document Windows full-matrix requirements, distinct-binding resource growth, mapping guard scope and approved contracts; fix duplicate specification numbering and clearly inaccurate test names.
4. Run full restore/build/test/format, both supported adapter dependency lanes, local package/consumer validation and independent final review.
5. Commit as `test: close review coverage and consumer validation gaps` (split docs/CI into an additional scoped commit if clearer).

## Validation and execution evidence

Save red/green and review evidence under ignored `artifacts/final-review-remediation/`. Keep this plan's completion section current. Use exact path staging for every commit.

Final commands:

```text
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
dotnet test tests/AspectN.Cecil.Tests/AspectN.Cecil.Tests.csproj -c Release -p:CecilVersion=0.11.6
dotnet test tests/AspectN.Roslyn.Tests/AspectN.Roslyn.Tests.csproj -c Release -p:RoslynVersion=5.6.0
eng/verify-packages.ps1 -PackageDirectory artifacts/final-review-remediation/packages
```

Restore/build the default solution after alternative-version lanes before package validation, so their shared bin/obj outputs cannot create misleading version conflicts.

## Completion

- [x] Batch 1: core contracts — 21 initial failures plus 3 nested `!ref` failures confirmed; full solution 1,301 tests passed, Release build 0 warnings/errors, format passed (workspace-load warning only). Independent read-only review passed, including netstandard nullable consumer compilation.
- [x] Batch 2: identities / metadata / relations — five intended regressions reproduced; added unresolved-scope fallback coverage. Full solution 1,312 tests passed, Release build 0 warnings/errors; independent read-only review passed.
- [x] Batch 3: attributes / Reflection metadata — initial six failures reproduced; eight CA/pointer conformance cases cover true blobs alongside flags and embedded old-framework markers. Full solution 1,322 tests passed; Release build and format passed. Independent review's two P2 findings were fixed and re-reviewed successfully.
- [x] Batch 4: iterative mapping — five deep-shape failures and two deep-pointer failures reproduced in isolated small-stack processes. Thirteen initial deep mapping / legacy ambiguity cases plus no-suffix generic pointer regression pass; full solution 1,337 tests passed. Independent review's two P2 findings and weak-cache keep-alive suggestion were resolved and re-reviewed successfully. Follow-up review reproduced native Scope/FullName recursion at 8,192 reference-owner levels (1,024/2,048 passed): iterative metadata scope and native-format owner text remove those paths while preserving reference-local module fallback. Fifteen depth probes and byte-for-byte owner text/context checks cover the supplement.
- [ ] Batch 5: coverage / engineering / final gates
