# Legacy Differential Oracle

This project compares the backend-neutral AspectN implementation with the
historical Shared.Cecil.AspectN behavior. The historical sources are test-only:
production projects must not reference or import them.

## Prepare the pinned oracle

From the repository root, prepare a fresh checkout from the upstream repository:

```powershell
./eng/prepare-legacy-oracle.ps1
```

For a deterministic offline preparation, point the script at a Git repository
that contains the pinned commit:

```powershell
./eng/prepare-legacy-oracle.ps1 `
    -OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN
```

Both modes materialize `artifacts/legacy-oracle/Shared.Cecil.AspectN` from Git
object data at commit `03a9d1030f92a64449b6220eb45d7198d31653db`. The script
rejects dirty or incorrectly pinned existing artifacts and writes the exact
revision to `.aspectn-oracle-commit`. The artifacts directory is intentionally
ignored by Git.

## Run the oracle

```powershell
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj `
    -c Release
```

`legacy-patterns.json` contains deterministic cases against the local fixture
assembly. Candidate IDs have this stable form:

```text
<assembly-name>::<Cecil-declaring-type-full-name>::0x<metadata-token>
```

The corpus covers every legacy selector family, the deterministic local fixture,
Rougamo `SignatureUsage` and `PatternUsage`, and seven fixed public package
assemblies. Package restore is locked by `packages.lock.json`; the exact corpus,
versions, hashes, pair counts, and results are recorded in the migration report.

## Approved differences

Compatibility mismatches fail by default. A maintainer-approved exception must
be recorded in `approved-differences.json` using the full tuple of pattern,
candidate ID, composite-accessibility mode, legacy result, and new result, plus
a concrete rationale. Never update the file merely to make the test green. The
test also fails when an approval is no longer used so obsolete entries cannot
accumulate silently.
