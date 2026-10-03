# AspectN

AspectN is a backend-neutral parser and matcher for .NET type and callable
expressions. Parse an expression once, map metadata from the backend you
already use, and evaluate it with explicit `Matched`, `NotMatched`, or
`Unknown` results.

## Packages

| Package | Purpose |
| --- | --- |
| `AspectN` | Syntax, binding, backend-neutral signatures, and matching |
| `AspectN.Cecil` | Mono.Cecil metadata adapter |
| `AspectN.Cecil.Fody` | Cecil adapter for projects that consume FodyHelpers |
| `AspectN.Roslyn` | Microsoft.CodeAnalysis symbol adapter |
| `AspectN.Reflection` | Runtime reflection adapter |

All packages target `netstandard2.0` and `net10.0`. The `netstandard2.0`
assets support consumers targeting .NET Framework 4.7.2.

`AspectN.Cecil` and `AspectN.Cecil.Fody` expose the same adapter API against
different strong-named Cecil assemblies and must not be referenced together.
Use `AspectN.Cecil.Fody` inside Fody weavers that reference `FodyHelpers`.

## Quick start

```csharp
using AspectN;
using AspectN.Evaluation;
using AspectN.Reflection;

var compilation = new AspectNEngine().CompileMemberExpression(
    "method(public string Example.Service.GetName())");

if (!compilation.TryGetMemberPattern(out var pattern))
{
    throw new InvalidOperationException(
        string.Join(Environment.NewLine, compilation.Diagnostics));
}

var method = typeof(Example.Service).GetMethod("GetName")!;
var typeSystem = new ReflectionTypeSystem();
var candidate = typeSystem.Mapper.MapMethod(method);
var result = pattern.Match(candidate, typeSystem);

if (result.Status == MatchStatus.Unknown)
{
    // Inspect result.Diagnostics and choose an application policy.
}
```

## Language

AspectN implements one case-sensitive language, using the existing Standard
grammar and matching semantics. Language selection and the Legacy compatibility
layer have been removed. Migrating from Shared.Cecil.AspectN is a breaking
change that requires reviewing expressions and their intended match ranges.

See the [language guide](docs/language/README.md), the
[normative language specification](docs/superpowers/specs/2026-07-31-aspectn-language-spec.md),
and the [Shared.Cecil.AspectN migration guide](docs/migration/shared-cecil-to-aspectn.md).

## Build and test

```powershell
dotnet restore AspectN.sln
dotnet build AspectN.sln -c Release --no-restore
dotnet test AspectN.sln -c Release --no-build --no-restore
dotnet format AspectN.sln --verify-no-changes --no-restore
powershell -NoProfile -File eng/verify-packages.ps1
```

The repository treats warnings as errors and verifies package dependency
boundaries with temporary consumers.

## License

AspectN is licensed under the MIT License.
