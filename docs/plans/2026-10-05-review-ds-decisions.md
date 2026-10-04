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
