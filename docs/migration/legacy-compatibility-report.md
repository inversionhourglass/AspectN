# Cecil Legacy compatibility report

Date: 2026-08-10

## Result

The pinned Shared.Cecil.AspectN oracle and the new `AspectN.Cecil` adapter
agree for the complete deterministic corpus.

| Metric | Result |
|---|---:|
| Oracle revision | `03a9d1030f92a64449b6220eb45d7198d31653db` |
| Unique patterns | 36 |
| Candidate assemblies | 10 |
| Pattern/candidate pairs | 291,877 |
| Unapproved differences | 0 |
| Approved differences | 0 |
| `Unknown` results | 0 |
| Differential test body runtime | 1,821 ms |

The corpus file is
`tests/AspectN.LegacyOracle.Tests/legacy-patterns.json`, SHA-256
`31719bbc351117df77f415d81b788dadfc0cf31c50a63b5691eb989586e2c2f4`.
Candidate IDs use
`<assembly>::<Cecil-declaring-type-full-name>::<metadata-token>`.

## Corpus composition

The deterministic local and Rougamo sweeps use
`CompositeAccessibility=false`. The public package sweep reproduces the
historical AspectN.Tests configuration with `CompositeAccessibility=true`.

| Assembly | Source/version | Pairs |
|---|---|---:|
| `AspectN.TestAssembly` | current project fixture | 307 |
| `SignatureUsage` | Rougamo `c4071b5e3ded79ee30c047cf45685637c542fa26` | 2,550 |
| `PatternUsage` | Rougamo `c4071b5e3ded79ee30c047cf45685637c542fa26` | 2,140 |
| `Abp` | NuGet `9.4.2` | 61,900 |
| `Autofac` | NuGet `8.1.1` | 27,050 |
| `FreeSql` | NuGet `3.2.833` | 70,850 |
| `Newtonsoft.Json` | NuGet `13.0.3` | 42,080 |
| `Polly` | NuGet `8.4.2` | 23,250 |
| `Serilog` | NuGet `4.0.2` | 10,160 |
| `StackExchange.Redis` | NuGet `2.8.16` | 51,590 |

The Rougamo fixture binaries were built for `net8.0` in Release. Their
SHA-256 values are:

- `SignatureUsage.dll`:
  `d7f7361d598f171bc476f9f7d69bbc4f44dab31a21b20b6364f50ee29fb23a08`
- `PatternUsage.dll`:
  `9ad220d3b11c2aa9cebe9dbc1cf5cf0a81f7bcd915404f3fe6bc45898fadb9b5`

The public package graph is locked by
`tests/AspectN.LegacyOracle.Tests/packages.lock.json`, SHA-256
`6bd6f122ceeb8212c94b263ab6c41b85026af4578d455d49e870ab9533c5a869`.
The historical primary package versions remain unchanged. Two transitive
dependencies are explicitly pinned to patched versions rather than disabling
NuGet audit: `Microsoft.Extensions.Caching.Memory 8.0.1` and
`System.Linq.Dynamic.Core 1.6.0`.

## Compatibility fixes found by the oracle

Every initial mismatch was minimized into a focused unit test before changing
production code. The fixes were:

- preserve concrete generic arguments when a type pattern is converted into an
  assignability target;
- treat an empty target assembly or namespace as unqualified only inside Cecil
  relationship and direct-attribute lookup, while retaining exact public
  structural equality;
- parse Legacy wildcard declaring-type generic forms such as `*<!>`, `*<>`,
  and `*<,>` inside member selectors;
- reject generic member paths that omit a member name or repeat a generic list;
- synthesize Oracle-compatible virtual type parameter names for open generic
  declaring types, including nested definitions;
- preserve a definitive generic-argument mismatch even when an earlier
  argument comparison returned `Unknown`.

No entry was added to `approved-differences.json`.

## Verification environment

- SDK: `.NET SDK 10.0.302`
- Test runtime: `.NET 10.0.10`
- Platform: `Microsoft Windows 10.0.26200`, x64 process on x64 OS
- Mono.Cecil: `0.11.5`

The following commands passed:

```powershell
powershell -NoProfile -File eng/prepare-legacy-oracle.ps1 `
    -OfflineSource D:\Code\Hourglass\rougamo-eco\Shared.Cecil.AspectN
dotnet restore tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj `
    --locked-mode
dotnet test tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj `
    -c Release
dotnet list tests/AspectN.LegacyOracle.Tests/AspectN.LegacyOracle.Tests.csproj `
    package --vulnerable --include-transitive
```

The vulnerability audit reported no vulnerable direct or transitive packages
from the configured sources.
