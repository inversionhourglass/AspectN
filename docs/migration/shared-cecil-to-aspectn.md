# Migrating from Shared.Cecil.AspectN

AspectN replaces the Cecil-bound parser with a backend-neutral compile and
match pipeline implementing the existing Standard language. Legacy and the
language selection API are removed. This is a breaking migration: review
expressions and intended match ranges before switching callers. Rougamo's
actual integration and breaking major release belong to a later task.

## Package and namespace changes

Replace the Shared.Cecil.AspectN source dependency with:

- `AspectN` for parsing, compilation, signatures, and match results.
- `AspectN.Cecil` for applications using the official `Mono.Cecil` package.
- `AspectN.Cecil.Fody` for Fody weavers using the Cecil assembly bundled by
  `FodyHelpers`.

The two Cecil packages expose the same `AspectN.Cecil` namespace but target
different strong-named Cecil identities. Reference exactly one of them in a
consumer project. Rougamo uses `AspectN.Cecil.Fody`.

The solution, formatting and tests no longer depend on the Shared oracle.
Historical checkouts remain frozen and are not changed by this retirement.

## Compile expressions

```csharp
using AspectN;

var compilation = new AspectNEngine().CompileMemberExpression(
    expression,
    compilationOptions: new AspectNCompilationOptions(
        TimeSpan.FromMilliseconds(100)));

if (!compilation.TryGetMemberPattern(out var pattern))
{
    // Report compilation.Diagnostics.
}
```

`ParseMemberExpression(string source)` and `ParseTypePattern(string source)`
have no options parameter. Engine compilation methods accept `source`,
and optional `compilationOptions`. Remove references to
`AspectNLanguageVersion`, `AspectNParseOptions` and `LanguageVersion` on trees,
compilations and compiled patterns. Custom selectors, predicates and matchers
are not supported; remove extension registries and extension contexts. Unknown
selector names (including qualified names) report AN2001. Keywords are lowercase
and case-sensitive; no mode is inferred from input.

## Map Cecil metadata before matching

```csharp
using AspectN.Cecil;
using AspectN.Evaluation;
using Mono.Cecil;

using var module = ModuleDefinition.ReadModule(assemblyPath);
using var typeSystem = new CecilTypeSystem(
    module,
    new CecilMappingOptions(leaveOpen: true));

var method = module.Types
    .SelectMany(type => type.Methods)
    .Single(candidate => candidate.FullName == methodFullName);
var signature = typeSystem.Mapper.MapMethod(method);
var result = pattern!.Match(signature, typeSystem);

if (result.Status == MatchStatus.Unknown)
{
    // Preserve diagnostics and apply an explicit caller policy.
}
```

Keep one `CecilTypeSystem` for the lifetime of its `ModuleDefinition`, then
dispose it. The mapping cache is owned by that context. With `leaveOpen: true`,
the caller owns module disposal, as in this example; the default closes the
module when the type system is disposed. The deleted `CompositeAccessibility`
option has no replacement: accessibility reflects the member declaration,
without intersecting outer types. Property metadata still merges accessor
accessibilities, while property selectors check the current accessor.

## Behavioral migration checklist

1. Inventory expressions and the candidate members each is intended to select.
2. Rewrite historical forms using the table below, checking match range changes.
3. Compile every expression and address diagnostics; retain AN4003/4004/4005
   migration messages instead of suppressing them.
4. Map metadata into signatures and handle `Matched`, `NotMatched` and `Unknown`
   explicitly, preserving diagnostics for missing assemblies or facts.
5. Validate application scenarios and rerun cross-adapter conformance where
   the same rules are consumed through multiple metadata backends.

## Expression migration

The replacements express intended behavior; they do not guarantee identical
results to Shared.Cecil.AspectN.

| Historical form or intent | AspectN form and effect |
| --- | --- |
| Any arity for named type or method | Write `Service<?>.Get<?>`; omitted clauses now require zero arity |
| Implicit variable `T` | Write `$T` consistently, for example `method($T Converter.Convert<$T>($T))`; bare `T` is a literal name |
| `privateprotected`, `protectedinternal` | `private protected`, `protected internal` (AN4003) |
| Space-separated alternative permissions | Join alternatives with `|`; compounds such as `(private protected)|public` are one alternative |
| `async T`, `async null` | `tasklike<T>`, bare `tasklike`, based on return shape, independent of compiler async metadata |
| Historical `async void` intention | Write `void`, rather than converting it to a Task shape |
| `*..M` member path | Write `*..*.M` for a nonempty namespace and non-nested type; omit declaring type with `M` to include global and nested types |
| `*..*` used as a universal declaring type | Omit declaring type or use the whole member path `*`; written paths follow namespace and nesting rules |
| `int` intended to select arrays | Write the required `[]` ranks; ordinary named patterns do not strip array layers |
| `int[]+` | Use `int+[]` when element assignability is intended; array targets of `+` are invalid |
| Tuple shorthand for reference Tuple | Write `System.Tuple<...>`; `(int,string)` matches ValueTuple only |
| `T?` | Reference type annotations keep the same CLR identity; value types require Nullable<T> |
| Default by-value-only parameter intention | Add `!ref`; unannotated types match every ref kind |
| Attribute long target names | `call`, `prop`, `para`, `ret` (AN4005); both `para[0]` / `para 0` and `para[*]` / `para *` remain accepted |
| `tasklike<void>` | Bare `tasklike` (AN4004) |
| Path prefix `!Run` | Supported; it negates name plus its generic clause, so use `!Run<?>` to exclude every arity |
| Boolean declaring/member paths | Use `(A || B).(Run || Stop)`; each position has ordinary Boolean semantics |
| Array/nullable declaring patterns | `int[,].Get` and `int?.GetValueOrDefault` use structural type matching |
| Consecutive `!!` or `! !` | Remove the redundant negation; AN1001 points to the second `!` |
| Property name `Value<>` | Remove the clause; property names cannot be generic (AN2011) |
| Namespace `Ns<int>.Owner` | Remove the namespace clause, or use `Ns.Outer<int>/Inner` for a nested type (AN2011) |
| `tasklike <int>` | Join the clause to the keyword: `tasklike<int>` |

`execution` includes constructors, `callable` excludes them, and `method`
selects ordinary methods. `attr(exec ...)` checks the current method plus its
associated property; `attr(call ...)` checks only the method. A wildcard name
with a generic clause is a named-type pattern: `*<?>` excludes arrays, formal
generic parameters and nested types unless a nesting path is written.

Review regex expressions against the current canonical signature: ref prefixes,
spaced compound permissions, comma-space separators, `/` nesting and positional
generic names. The compiler async flag is omitted.

Regex expressions must also conform to the restricted subset in specification
section 15. Remove comments and all inline options, including `(?x)` and
`(?i)`; spaces and `#` are literal. Rewrite lookarounds, backreferences,
named/balancing/conditional/atomic groups and nested/subtracted classes using
ordinary regex forms or structured selectors combined with Boolean operators.
Escape literal `[` and `]` inside classes, replacing forms such as `[[]` and
`[]abc]`. Unsupported features produce AN2012 and invalid .NET regex syntax
produces AN3002 during compilation; no pattern is returned. Finite matching
timeouts still produce Unknown/AN3003.

With structured declaring-type patterns, `*.Run` includes array and nested
declaring types. Write `*<?>.Run` to retain a non-nested named-type constraint.
Generic clauses within member-name Boolean branches retain their constraints;
variables first introduced in `||` branches cannot escape, and negation cannot
introduce a new variable. Shared's historical Boolean path splitting and
negated-wildcard exceptions are not reproduced.

The [Shared/Standard audit](../reviews/2026-10-03-shared-standard-audit.md)
retains its fixed-version evidence. The
[confirmed follow-up decisions](../reviews/2026-10-03-shared-standard-decisions.md)
record which differences were retained and which were fixed after review.

## Important differences

- The new matcher returns a tri-state result rather than a bare Boolean.
- Backend objects are mapped into backend-neutral signatures before matching.
- AspectN implements one case-sensitive grammar without a compatibility layer.
- Missing assemblies and unsupported metadata shapes produce `Unknown` with a
  diagnostic instead of an implicit negative match.
- Declaration accessibility is independent of containing-type accessibility.

See the [language guide](../language/README.md) and the
[normative language specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md)
and the [design decisions](../language/standard-design-decisions.md) for full
semantics and rationale.

## Constructing neutral signatures

Current public names are `MethodSignature`, `MethodKind`, `MapMethod`, and `AttributeTarget.ForMethod`; historical intermediate Callable names are not compatibility aliases. A generic parameter declaration now includes its assembly identity:

```csharp
using AspectN.Metadata;

var parameter = new GenericParameterSignature(
    "T", GenericParameterOwnerKind.Type, "Example.Container`1",
    new AssemblyIdentity("Example.Assembly"), position: 0);
var type = new GenericParameterTypeSignature(parameter);
```

Use mapper-produced signatures where possible. Required collection arguments cannot be null; optional attribute collections default to empty. Parameter attributes require a zero-based ParameterIndex, and non-parameter attributes require null. InheritanceClosure copies its ancestor list and only accepts an Unknown failure. Runtime type-system capabilities advertise operations; unavailable metadata can still produce Unknown. Standalone type patterns do not carry ref-kind information—use member return/parameter positions for ref annotations.
