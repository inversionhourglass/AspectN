# Single-language AspectN

The user approved removal of the Legacy compatibility layer on 2026-10-03.
Implementation is complete in the existing checkout.
This decision supersedes all dual-language, oracle parity, compatibility facade,
and approved-differences requirements in the earlier implementation plans.

AspectN directly implements the existing Standard syntax, binding and matching
contract. Language selection APIs and Cecil `CompositeAccessibility` are
removed as deliberate public API breaks. Existing Standard inputs and
diagnostics remain, including empty slots, `para 0` / `para *`, AN4003, AN4004
and AN4005. General `MethodSignature.IsAsync` metadata remains available.

Implementation order:

1. Preserve shared generic capabilities and valuable Standard regression tests.
2. Remove mode parameters and Legacy branches from parsing, binding, evaluation,
   extension contexts and compilation caches; update PublicAPI baselines.
3. Map Cecil declaration accessibility directly, preserving property accessor
   merging and `LeaveOpen` lifetimes; update the linked Fody adapter.
4. Rename shared fixtures to `MatchingFixture`; retire Legacy tests and oracle
   project, corpus, differences, lock file, preparation script and CI job.
5. Update the language and architecture specifications, design decisions,
   migration guide and contributor guidance.
6. Validate the complete retained test matrix, formatting and local packages.

Verification:

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

Work stays in the existing checkout. Existing untracked files, the frozen
oracle cache and adjacent repositories are preserved. Rougamo integration,
submodule removal, pushes and a breaking major release remain future work.

Validation completed: Release build has zero warnings and errors; all 837
retained tests pass (core 551, Cecil 71, Fody 73, Reflection 62, Roslyn 19,
conformance 59, .NET Framework 2). Restore, formatting verification and the
five-package / temporary-consumer validation succeed. Independent review
found no production semantic regression; its recommendation to preserve
empty generic slots with position-sensitive variable binding was implemented
with both matching and mismatching formal-parameter cases.
