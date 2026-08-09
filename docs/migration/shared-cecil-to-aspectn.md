# Migrating from Shared.Cecil.AspectN

AspectN replaces the Cecil-bound parser with a backend-neutral compile and
match pipeline. Existing expressions can move first; applications can adopt
the Standard language separately.

## Package and namespace changes

Replace the Shared.Cecil.AspectN source dependency with:

- `AspectN` for parsing, compilation, signatures, and match results.
- `AspectN.Cecil` for applications using the official `Mono.Cecil` package.
- `AspectN.Cecil.Fody` for Fody weavers using the Cecil assembly bundled by
  `FodyHelpers`.

The two Cecil packages expose the same `AspectN.Cecil` namespace but target
different strong-named Cecil identities. Reference exactly one of them in a
consumer project. Rougamo uses `AspectN.Cecil.Fody`.

The frozen Legacy oracle remains a test-only compatibility reference. Do not
copy or modify its production source during migration.

## Compile Legacy expressions explicitly

```csharp
using AspectN;

var parseOptions = new AspectNParseOptions(
    AspectNLanguageVersion.Legacy);
var compilation = new AspectNEngine().CompileMemberExpression(
    expression,
    parseOptions);

if (!compilation.TryGetMemberPattern(out var pattern))
{
    // Report compilation.Diagnostics.
}
```

Language selection is immutable on the compiled pattern. Do not infer Legacy
mode from the shape or casing of an expression.

## Map Cecil metadata before matching

```csharp
using AspectN.Cecil;
using AspectN.Evaluation;
using Mono.Cecil;

using var module = ModuleDefinition.ReadModule(assemblyPath);
using var typeSystem = new CecilTypeSystem(
    module,
    new CecilMappingOptions(compositeAccessibility: false));

var method = module.Types
    .SelectMany(type => type.Methods)
    .Single(candidate => candidate.FullName == methodFullName);
var signature = typeSystem.Mapper.MapCallable(method);
var result = pattern!.Match(signature, typeSystem);

if (result.Status == MatchStatus.Unknown)
{
    // Preserve diagnostics and apply an explicit caller policy.
}
```

Keep one `CecilTypeSystem` for the lifetime of its `ModuleDefinition`, then
dispose it. The mapping cache is owned by that context.

## Behavioral migration checklist

1. Run existing expressions with `AspectNLanguageVersion.Legacy`.
2. Compare outcomes against the pinned Legacy oracle.
3. Record intentional corrections in `approved-differences.json`; never update
   the baseline silently.
4. Decide how each caller handles `Unknown` and retain its diagnostics.
5. Move expressions to Standard syntax deliberately, using migration
   diagnostics for renamed selectors and modifiers.
6. Re-run the cross-adapter conformance suite if the same rules are consumed
   through Cecil, Roslyn, or Reflection.

## Important differences

- The new matcher returns a tri-state result rather than a bare Boolean.
- Backend objects are mapped into backend-neutral signatures before matching.
- `Standard` is case-sensitive and does not accept Legacy aliases silently.
- Missing assemblies and unsupported metadata shapes produce `Unknown` with a
  diagnostic instead of an implicit negative match.
- Composite accessibility is a Cecil mapping option retained only for Legacy
  compatibility scenarios.

See the [language guide](../language/README.md) and the
[normative language specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md)
for Standard replacements and full semantics.
