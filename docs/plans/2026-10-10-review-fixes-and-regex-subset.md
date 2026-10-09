# Review fixes and regular-expression subset

**Goal:** Fix the five verified review findings and implement the regex subset approved on 2026-10-10.

**Architecture:** Keep the backend-neutral public signatures unchanged. Reflection retains weak native associations and separates constraint query contexts from declaration identity. Regex bodies retain the existing delimiter syntax, with a small feature whitelist and compile-time diagnostics; .NET performs regex syntax validation and matching with the existing finite timeout.

**Tech stack:** C#, .NET Standard 2.0 / .NET 10, xUnit, Mono.Cecil, Roslyn, Reflection.

Execute in the existing checkout, preserving untracked files and adjacent repositories. No worktree, push, package publication or integration into Rougamo is part of this task.

## 1. Adapter ref classifications

- Add conformance cases for `[In] ref`, `[In, Out] ref`, ordinary `ref`, `out`, `in` and `ref readonly`.
- Observe failing matching and mapped ref classifications before modifying `CecilRefKindDetector` and `ReflectionSignatureMapper`.
- Distinguish interop direction flags from readonly markers, retaining ordinary out-only metadata classification.
- Run the focused conformance suite and the Cecil/Reflection/Fody suites.

## 2. Reflection constraint context and inheritance branches

- Extend `MethodGenericOwnerIdentityTests` to verify Reflection relationships for methods mapped from both `Owner<string>` and `Owner<int>`, including cache reuse and transitive constraints.
- Preserve a context-specific native association for method generic parameter uses without changing neutral declaration identity or retaining collectible assemblies strongly.
- Add a missing-interface-dependency PE scenario for inheritance closure using `MetadataLoadContext`; prove the known base matches and the failure remains visible when no known branch matches.
- Change relationship traversal to retain and process known branches after individual metadata failures.
- Run the focused conformance cases and collectible-lifetime tests.

## 3. Positional names in open constructed signatures

- Add regex tests using real Reflection inputs for `Container<List<T>>`, repeated parameters and nested/array arguments.
- Observe AN3001 before changing `GenericParameterNamer`.
- Collect nested generic parameter uses and name distinct declarations deterministically, preserving existing formal-slot naming.
- Run regex, identity and adapter matching tests.

## 4. Restricted regex language

- Add compile-time rejection cases for comments, all inline options, named/balancing/conditional/atomic groups, lookarounds, backreferences, nested/subtracted character classes and unescaped literal brackets inside classes.
- Add accepted cases for literals/escapes, basic classes and ranges, groups/noncapturing groups, alternation, ordinary quantifiers (including existing lazy quantifiers), anchors, and literal text resembling forbidden constructs.
- Simplify body scanning to escape, ordinary character-class and group-depth states. Report unsupported features before matching and use .NET for full syntax validation.
- Replace superseded positive tests for comments and class subtraction with rejection coverage; retain timeout, cancellation, recovery and source-preservation tests.
- Update diagnostic API baseline, language specification, design decisions, migration guidance and current comparison documentation.

## 5. Verification and independent review

Run focused red/green tests for each change, then:

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test AspectN.sln -c Release --no-build --no-restore -m:1 -nr:false
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

Use independent code-review subagents after implementation. Confirm the five original probes and relevant negative controls, resolve any verified findings, and summarize the final diff and evidence.

## Execution result — 2026-10-10

All five findings are fixed. The regressions were observed before the corresponding production changes. Independent adapter, core and release/documentation reviews passed after correcting the nested-arity budget and foreign-generic-owner boundary cases identified during review.

Final verification:

- Restore succeeded; Release solution build succeeded with zero warnings and errors, including `netstandard2.0`, `net10.0` and the `net472` test host.
- Complete solution tests: 1,442 passed, zero failures or skips. Core 860; Cecil 130; Reflection 97; Roslyn 22; conformance 188; compatibility `net472` 6 / `net10.0` 7; Cecil.Fody 132.
- `dotnet format AspectN.sln --verify-no-changes --no-restore` and `git diff --check` passed.
- `eng/verify-packages.ps1` validated all five local packages, both dependency groups and ten consumer builds/runs. Validation version: `0.0.0-validation-e7d058e64459`.
- Collectible-lifetime tests retain both the mapper and neutral signatures while verifying unload and subsequent Unknown/AN3001 relationships.

The accepted regex subset is normative in language-spec section 15. Unsupported features produce compile-time AN2012; invalid .NET regex syntax produces compile-time AN3002; matching timeouts remain Unknown/AN3003. Production package dependencies are unchanged; MetadataLoadContext is a private test dependency.
