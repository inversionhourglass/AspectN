# Legacy / Shared compatibility review fixes

The user authorized these repairs on 2026-10-01. Standard / Legacy language differences are deferred. The frozen Shared oracle stays unchanged.

1. Add regression tests before production changes: malformed signature compilation returns diagnostics; unbound T is a literal name; declared generic names still bind; !int return patterns; wildcard declaring paths; mixed omitted generic arguments; tuple arity and generic adjacency.
2. Repair the parser and binder in src/AspectN. Invalid syntax must not enter binding. Keep valid syntax trees and meaningful diagnostic codes stable. Preserve explicit variables, ref returns and Unicode names.
3. Extend tests/AspectN.LegacyOracle.Tests with acceptance and matching comparisons against Shared. Record expressly approved corrections: repeated generic names, Boolean scope, invalid leading paths, one-element / empty tuples, and strict token consumption. Restore Shared's existing generic adjacency rule. Retain return sugar using the user's clarification.
4. Document the examples, historical behavior and compatibility policy in docs/language. Update approved-differences.json only for real corpus cases expressly approved by the user.
5. Run focused regression tests, the full Release solution test matrix, formatting verification and an independent code review. Fix actionable findings and record final evidence in artifacts/reviews.

Verification commands:

```powershell
dotnet test tests/AspectN.Tests/AspectN.Tests.csproj -c Release --no-restore
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj -c Release --no-restore
dotnet test AspectN.sln -c Release --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
```

No package publishing, branch pushes, worktrees, or frozen-oracle edits are included.

Completed: all five steps. The final Release matrix passed 969 tests, formatting verification exited 0, and the independent reviewer passed 213 boundary probes with no reproducible finding. Evidence: `artifacts/reviews/aspectn-legacy-shared-fixes-2026-10-01.md`.
