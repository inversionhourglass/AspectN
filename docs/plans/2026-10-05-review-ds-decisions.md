# REVIEW-DS decisions implementation

## Approved scope

The user approved these decisions on 2026-10-05:

1. Preserve unavailable member metadata explicitly and propagate `Unknown` whenever a query needs it.
2. Retain vector (SZ) versus general array shape in the neutral public model.
3. Remove custom selector/matcher extensions, in a dedicated commit.
4. Defer reproducible performance benchmarks; do not claim the performance gate is complete.

These decisions supersede the extension and unresolved-metadata limitations in the earlier design and shared Standard differences document. Other independent REVIEW-DS findings remain separate work.

## Goal

Make the approved public contracts accurate across the core, Cecil/Fody, Reflection and Roslyn adapters, with regression tests and current documentation.

## Architecture

The core remains backend neutral. Signatures expose immutable metadata availability, with known values remaining usable independently of unavailable values. Queries combine known matches, known mismatches and unknown facts using the existing three-state logic. Adapters mark facts unavailable when member definitions cannot be read and report recoverable resolution failures under a distinct diagnostic code. Unknown selector names produce parser diagnostics and recovery nodes, without an extension registry, extension AST or custom evaluator.

Array signatures retain explicit SZ/general shape. Rank-based Standard array patterns continue to accept either one-dimensional shape. Structural equality distinguishes shapes; nominal assignability respects the directional vector-to-general relation and recursively checks element shape.

## Tech stack

C#, .NET 10/netstandard2.0, Mono.Cecil, Roslyn, Reflection, xUnit, existing conformance and net472 suites, PublicApiAnalyzers.

## Execution and validation

1. Add failing tests for unsupported custom selectors and absence of extension APIs. Remove the registry, compiler/cache dimensions, parser/binder/evaluator support and extension-only tests. Update public API baselines and normative documentation. Run core and full solution tests, review, then commit this change independently.
2. Add failing unresolved-member tests (positive/negative visibility, attributes, member domain, return/parameter ref-kind and regex), plus known-mismatch dominance. Add immutable availability to signatures and consume it in the relevant evaluators. Unify recoverable Cecil resolution failures and use `AN3004` for resolution failures. Verify core, Cecil and same-source Fody tests.
3. Add failing neutral and adapter array tests, including nested arrays and both assignability directions. Add public shape, map all backends, update structural/relationship comparison and snapshots. Verify all adapters and conformance suites.
4. Remove the unused BenchmarkDotNet pin and document the deferred performance gate. Record actual build/test/format evidence, including supported dependency alternatives when affected.
5. Review the resulting commits and remaining changes, fix material findings, run the complete Release build and test matrix and formatting verification. Preserve all pre-existing untracked files; do not push or publish packages.

## Completed changes

| Decision | Implementation |
| --- | --- |
| Remove semantic extensions | Dedicated commit `70e9628`; unknown selectors produce `AN2001` with recovery, and extension APIs, registries and cache dimensions are removed. |
| Complete availability repair | Commit `b9b0d95`; immutable facts and diagnostics, fact-specific three-state evaluation, by-reference marker/classification separation, detached attribute guards, recoverable Cecil reads under `AN3004`. Real malformed lazy-property and resolved-reference parameter-name regressions are included. |
| Improve array model | Commit `33b3181`; explicit SZ/general shape, recursive identity, neutral directional array relations and distinct generic owner IDs. Rank-one Standard patterns remain rank based, including attribute and generic relationship optimizations. |
| Defer performance | The normative design explicitly leaves the performance gate open. The unused BenchmarkDotNet pin is removed. No benchmark or performance claim is included. |

Independent reviews passed for the extension removal, metadata repair and array model. Review findings were reproduced and repaired before final validation, including lazy diagnostic-name reads and rank-pattern optimization regressions.

## Final verification (2026-10-05)

Both library targets (`netstandard2.0` and `net10.0`) build with public API analyzers enabled. Default and alternative dependency builds completed with zero warnings and errors. The default dependency versions were restored after compatibility checks.

| Test project | Default passed | Alternative passed |
| --- | ---: | ---: |
| AspectN.Tests | 673 | 673 |
| AspectN.Cecil.Tests | 112 | 112 |
| AspectN.Cecil.Fody.Tests | 114 | 114 |
| AspectN.Reflection.Tests | 81 | 81 |
| AspectN.Roslyn.Tests | 19 | 19 |
| AspectN.Conformance.Tests | 103 | 103 |
| AspectN.NetFramework.Tests (net472) | 2 | 2 |
| Total | **1104** | **1104** |

The default dependencies are Cecil 0.11.5 / Roslyn 4.12.0; alternatives are Cecil 0.11.6 / Roslyn 5.6.0. FodyHelpers stays 6.8.1. No tests were excluded in these final matrices.

Verification commands:

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore --logger trx --results-directory artifacts/review-ds-results/final-default-v3
dotnet restore AspectN.sln -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0
dotnet build AspectN.sln -c Release --no-restore -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0
dotnet test AspectN.sln -c Release --no-build --no-restore -p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0 --logger trx --results-directory artifacts/review-ds-results/final-alternatives-v2
dotnet format AspectN.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -File eng/verify-packages.ps1
```

Formatting verification passed. The existing package script validated all five packages, both target frameworks, symbol packages and five temporary consumer builds. During this session it ran through a local artifact wrapper that checks every resolved cleanup target before calling the unchanged package validation script. Raw final package output is in `artifacts/review-ds-verification/packages-final-v2.log`; TRX files are in the two result directories above.

All pre-existing untracked review reports and `nul` were preserved. No adjacent repository, frozen oracle cache, package publication or branch push was involved. Independent REVIEW-DS findings outside these four approved decisions remain separate work.
