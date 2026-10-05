# REVIEW-DS remaining remediation

**Goal:** Close every verified, deterministic issue in REVIEW-DS without changing the Standard language contract or reopening the four maintainer decisions.

**Status:** Completed and verified at `18483b7`; see [the item-by-item remediation and validation record](../reviews/2026-10-05-review-ds-remediation.md). Performance work remains deferred.

**Architecture:** Preserve backend-neutral signatures and the existing nominal relation contract. Reject excessive syntax nesting with diagnostics, make neutral structural comparison stack-safe, and preserve unavailable metadata as Unknown. Backend-specific gaps must be documented and tested rather than inferred from names.

**Technology:** C#, xUnit/FsCheck, Cecil 0.11.5/0.11.6, Roslyn 4.12.0/5.6.0, net10.0/netstandard2.0/net472, PowerShell/GitHub Actions. Work in this checkout and preserve the original untracked reports.

## Execution and evidence

1. **Stack safety (C1, M30).** Add failing depth-limit tests in `tests/AspectN.Tests`; reproduce process-fatal deep comparisons in the isolated verification probe. Guard recursive parser entry and resulting syntax depth in `src/AspectN/Parsing` and `Syntax`; replace recursive neutral comparison in `TypeSystem/StructuralTypeComparer.cs`. Protect matching of excessively deep neutral inputs. Verify ordinary expressions and isolated adversarial cases.
2. **Relations and metadata (I2, I3, I9, M11–M13, M15–M18).** Add source/PE/Reflection/Cecil conformance examples for generic constraints, zero/long/invalid tuples, dynamic generic owner identities, async iterators and function-pointer conventions. Repair the corresponding type systems and mappers. Preserve unsupported pointer matching and document PE event-raiser/foreign-language boundaries.
3. **Parser correctness (M1, M3–M10).** Add focused failure/recovery/span tests for selector tuples, nested-type separators, ref-kind entry points, numeric characters, C# Unicode escapes and escaped line breaks. Share small recovery helpers where useful; keep existing Boolean grouping and substring-regex semantics.
4. **Neutral public contracts (M18, M20, M22–M25, M28, M31–M32, M37–M38).** Test owner assembly identity, argument guards, attribute ownership, immutable closures, stable deduplicated diagnostics and unavailable generic positions. Rewrite identified public documentation with concrete semantics; remove the unused Reflection member cache.
5. **Verification quality (I7–I8, M35, M40–M44, M48, M50, M52, M54).** Strengthen assertions that previously could pass without exercising behavior, include type attributes in snapshots, reject unknown corpus kinds, extend API/Framework/netstandard runtime coverage, and clarify architecture/migration/history evidence. Keep performance gates deferred.
6. **Build/release correctness (M39, M45–M47).** Consolidate evaluated pack properties, require the net472 release gate, emit LF checksums, propagate dependency/version properties through final artifact validation, and validate the actual release packages with temporary consumers. Do not publish or push.
7. **Independent review and closure.** Request a read-only review after substantive batches, fix actionable findings, then run restore/build/test/format plus package and alternate dependency validation. Record each report item as fixed, previously fixed, not substantiated, an explicit backend boundary, or deferred. Commit task-scoped changes locally.

## Verification commands

- `dotnet restore AspectN.sln`
- `dotnet build AspectN.sln -c Release --no-restore`
- `dotnet test AspectN.sln -c Release --no-build --no-restore`
- `dotnet format AspectN.sln --verify-no-changes --no-restore`
- Repeat the matrix with `-p:CecilVersion=0.11.6 -p:RoslynVersion=5.6.0` (confirm the repository's actual property names before execution).
- Run `eng/verify-packages.ps1` against both the normal and final-version artifacts, including each backend consumer.
- Run isolated stack probes and modern-runtime tests consuming the netstandard2.0 assemblies.

## Scope exclusions already settled

- Custom matching extensions were removed in their own commit.
- Performance/benchmark acceptance is deferred by the maintainer.
- Do not renumber SyntaxKind, amend historical commits, impose whole-string regex matching, broaden nominal assignment to arbitrary CLR conversions, or delete the untracked report/nul files.
