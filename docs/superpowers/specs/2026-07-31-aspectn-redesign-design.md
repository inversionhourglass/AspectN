# AspectN Industrial Redesign

**Status:** Approved architecture, updated for the single-language decision
**Date:** 2026-07-31; updated 2026-10-03
**Decision owners:** AspectN maintainers
**Scope:** AspectN core and metadata adapters; Rougamo migration is follow-up work

## 1. Summary

AspectN is a backend-neutral .NET pattern-matching engine developed directly in the `AspectN` repository. It implements one language contract: the existing Standard grammar and matching semantics. Shared.Cecil.AspectN remains historical source outside the build and test graph.

The redesign separates:

1. Source text, tokens, and immutable syntax trees.
2. Semantic binding for the single language contract.
3. Backend-neutral metadata and type-system contracts.
4. Immutable compiled match plans.
5. Mono.Cecil, Roslyn, and Reflection adapters.

The Legacy compatibility layer and language selection APIs are removed. Its partial repairs already departed from Shared syntax; retaining them would preserve design problems and burden the common implementation. Rougamo will migrate deliberately and publish a breaking major version in a separate task.

## 2. Goals

- Preserve the existing Standard input, diagnostic and matching contract.
- Define the language with explicit grammar, names, precedence, diagnostics, and semantic rules.
- Match equivalent type and member information consistently across Mono.Cecil, Roslyn, and Reflection.
- Keep `AspectN` independent from all native backend object models.
- Support both definitions and constructed references.
- Expose incomplete backend knowledge as `Unknown` instead of silently treating it as a negative match.
- Provide deterministic, thread-safe compilation and matching without global mutable state.
- Publish normal NuGet packages instead of consuming a shared-project submodule.
- Make third-party semantic extensions possible without allowing grammar mutation.

## 3. Non-goals for 1.0

- Binary or source compatibility with `PatternParser`, `SignatureParser`, `IMatcher`, or other old CLR APIs.
- A fully serialized offline metadata universe.
- Returning `$T` capture values or multiple query solutions.
- Field and event declaration selectors.
- DSL syntax for `params`, `scoped`, pointers, or function pointers.
- Nullable reference annotation matching.
- Arbitrary third-party lexer, token, or grammar extensions.
- A general AOP weaving model. AspectN matches metadata; Rougamo decides whether and how a match can be woven.

## 4. Language and Migration Contract

The language is governed by its EBNF, semantic specification and Standard
regression and conformance tests. Existing Standard inputs, empty slots,
parameter attribute forms and migration diagnostics remain supported.

There is no compatibility mode. Language selection API removal and deletion
of Cecil's outer-type accessibility intersection are intentional API breaks.
Old Shared CLR APIs and parser accidents are not part of the contract.
Migration must review expressions and match ranges, preserve `Unknown` policy
and validate real Rougamo scenarios before a future breaking major release.

## 5. Package and Dependency Layout

The first stable release contains five packages:

| Package | Responsibility | External dependency |
|---|---|---|
| `AspectN` | Parser, syntax model, binder, compiled plans, unified metadata contracts, matching | None beyond the BCL |
| `AspectN.Cecil` | Mono.Cecil mapping, resolution, equality, and relationship queries | Mono.Cecil |
| `AspectN.Cecil.Fody` | The same Cecil adapter API compiled for Fody's strong-named Cecil identity | FodyHelpers |
| `AspectN.Roslyn` | `ISymbol` mapping and `SyntaxNode + SemanticModel` convenience entry points | Microsoft.CodeAnalysis |
| `AspectN.Reflection` | `Type`, `MethodBase`, `PropertyInfo`, and related reflection mapping | BCL Reflection |

Dependency direction is strict:

```text
AspectN.Cecil ──────┐
AspectN.Cecil.Fody ─┤
AspectN.Roslyn ─────┼──> AspectN
AspectN.Reflection ─┘
```

Adapters never reference one another. `AspectN` never references an adapter.

`AspectN.Cecil` and `AspectN.Cecil.Fody` share adapter source but are mutually
exclusive consumer dependencies because their Cecil assembly public key tokens
differ. Fody upgrades are compatibility events and must re-run the shared Cecil
contract and Rougamo integration lanes.

`AspectN.Abstractions` is not a separate package in 1.0. The core `AspectN` package is the abstraction boundary.

All packages target:

```text
netstandard2.0;net10.0
```

The officially supported .NET Framework floor is 4.7.2. Rougamo retains a .NET Framework 4.6.1 compatibility smoke test without turning 4.6.1 into an AspectN support commitment.

## 6. Architectural Pipeline

```text
SourceText
  -> Lexer
  -> SyntaxTree
  -> Semantic Binder
  -> Compiled Pattern
  -> Evaluator
  -> MatchResult
```

Native metadata flows through a separate path:

```text
Mono.Cecil object ──> CecilTypeSystem ──────┐
Roslyn ISymbol ─────> RoslynTypeSystem ─────┼──> unified semantic view
Reflection object ──> ReflectionTypeSystem ─┘
```

The evaluator sees only the unified semantic view and `ITypeSystem` operations.

## 7. Parser Architecture

### 7.1 Lexer

The lexer produces immutable tokens with:

- Token kind.
- Exact source span.
- Original text.
- Normalized value where appropriate.
- Lexical diagnostics.

Whitespace is trivia. Invalid characters produce bad tokens and diagnostics rather than uncontrolled exceptions.

### 7.2 Syntax tree

Syntax nodes describe only source structure. Every concrete node name ends in `Syntax`. Syntax trees:

- Preserve spans required for diagnostics.
- Contain no Cecil, Roslyn, or Reflection objects.
- Are immutable and thread-safe.
- Can represent malformed input sufficiently for multiple diagnostics.

Member paths have a dedicated `MemberPathSyntax` node containing optional joint negation,
a declaring `TypePatternSyntax`, and a `MemberNamePatternSyntax` tree. Each name leaf owns
its generic clause; Boolean combinations preserve this structure instead of flattening
tokens or extracting a name from a type branch.

### 7.3 Parser

The parser is handwritten:

- Pratt or precedence-climbing parsing for `!`, `&&`, `||`, and grouping.
- Recursive descent for selectors, signatures, types, generics, attributes, and parameter lists.
- Explicit parse entry points for member expressions and type patterns.
- No parser-generator runtime dependency.

The parser returns a syntax tree and diagnostics. Invalid user text is an expected result and is not reported through `ArgumentException`.

### 7.4 Semantic binder

The binder:

- Applies the language specification directly, without a mode parameter.
- Resolves aliases and syntax sugar.
- Creates and scopes `$T` variables.
- Validates selector target domains.
- Binds registered semantic extensions.
- Emits semantic diagnostics.
- Produces immutable bound nodes.

Syntax nodes and bound nodes are separate types. Bound nodes are not exposed as a mutable plugin surface.

The binder validates property-name and namespace generic clauses and scopes variables
through declaring-type and member-name Boolean branches. Evaluation negates the joint
declaring type, name, and generic constraints in an isolated binding environment;
selector domain, modifiers, return type, and parameters remain outside that negation.

### 7.5 Compiled patterns

A successful bind produces an immutable, thread-safe compiled plan. A plan contains no mutable per-match state. Each evaluation allocates or rents an isolated binding environment.

## 8. Unified Metadata Model

The core model distinguishes identity, type use, member signature, and relationship queries.

### 8.1 Type identity

Type identity contains backend-neutral facts such as:

- Namespace.
- Containing type chain.
- Metadata name and generic arity.
- Type kind.
- Definition versus constructed form.
- Generic arguments.
- Array shape and other supported wrappers.

Native backend objects never appear in public core model properties.

### 8.2 Type use

A type use records how a type occurs in a signature. It includes:

- The referenced type identity.
- `RefKind`: none, `ref`, `out`, `in`, or `ref readonly`.
- Array ranks.
- Generic substitutions.
- Whether the use is a formal parameter, return, declaring type, or generic argument where that distinction affects semantics.

Nullable reference annotations are not part of core type identity. Standard treats `T?` on reference types as the same CLR type identity as `T`.

### 8.3 Members

The core member model distinguishes:

- Ordinary methods.
- Property getters and setters.
- Event accessors.
- User-defined operators and conversions.
- Instance constructors.
- Static constructors.
- Original definitions and constructed references.

Property metadata is represented independently and linked to its accessors. `property(...)` selects property getters and setters. Modifiers match the current accessor's declaration accessibility; property metadata retains the merged accessor accessibility. Cecil does not intersect member accessibility with containing types. `CecilMappingOptions` only controls `LeaveOpen`.

### 8.4 Definitions and constructed references

Every constructed type or member can expose:

- Its original definition.
- Its current constructed identity.
- The generic substitution map.
- The effective return and parameter types after substitution.

Matching uses the effective signature by default. Explicit original-definition matching is an extension point, not the default.

### 8.5 Type-system service

`ITypeSystem` supplies environment-dependent operations:

- Type equality.
- Base type and interface traversal.
- Assignability.
- Generic substitution.
- Direct attribute lookup.
- Original-definition mapping.
- Capability discovery.

This avoids copying an entire metadata graph into detached DTOs.

## 9. Adapter Requirements

### 9.1 Mono.Cecil

The Cecil adapter accepts definitions and references, including constructed generic types and methods. Resolution failures return `Unknown` with diagnostics.

Its caches are scoped to a `ModuleDefinition` or an explicitly shorter lifetime. A cache must not keep an obsolete module graph alive.

### 9.2 Roslyn

Roslyn is Symbol-first:

- Primary inputs are `ITypeSymbol`, `IMethodSymbol`, `IPropertySymbol`, and related symbols.
- Convenience overloads accept a declaration syntax node together with `SemanticModel` or `Compilation`.
- SyntaxNode-only matching is not a complete semantic mode.

The adapter preserves original definitions and constructed symbols. It binds cache lifetime to the owning `Compilation`.

### 9.3 Reflection

The Reflection adapter accepts `Type`, `MethodBase`, `PropertyInfo`, and related objects without instantiating attributes merely to inspect their type.

Its caches must not prevent a collectible `AssemblyLoadContext` from unloading. Where reflection cannot recover required metadata, evaluation returns `Unknown`.

## 10. Match Semantics

The core result is:

```text
Matched
NotMatched
Unknown
```

`Unknown` carries one or more diagnostics describing missing resolution, unsupported capabilities, or incomplete metadata.

Convenience policies may map the result:

- Compatibility policy: `Unknown` becomes `false`.
- Strict policy: `Unknown` raises a controlled evaluation failure.
- Diagnostic policy: matching continues and reports warnings.

The core never silently rewrites `Unknown` to `NotMatched`.

`$T` variables are internal unification variables. They constrain equality across pattern positions but are not returned to callers in 1.0.

## 11. Extensibility

The grammar is closed. Third parties cannot:

- Add token kinds.
- Change lexical rules.
- Replace parser productions.
- Mutate a global parser registry.

Semantic extensions are open through an immutable registry owned by one `AspectNCompilation` or `AspectNEngine`. An extension:

- Has a qualified, collision-resistant name.
- Declares its valid target domain.
- Declares required type-system capabilities.
- Binds syntax arguments to an immutable semantic node.
- Returns `Unknown` when a backend cannot provide a required capability.

The registry identity participates in compiled-plan cache keys.

## 12. Caching and Lifetime

There are no unbounded global static caches.

- Syntax trees and compiled plans are immutable.
- Parser and binder do not hide global caches.
- An optional engine-scoped cache is explicit and bounded.
- Cache keys include source text, parse entry point, regex timeout and extension-registry identity.
- Adapter caches follow `ModuleDefinition`, `Compilation`, or collectible assembly lifetime.
- Rougamo may reuse one engine for a weave operation and dispose it when the operation completes.

## 13. Diagnostics and Safety

Diagnostics have:

- Stable code.
- Severity.
- Message.
- Source span when applicable.
- Optional backend context.

Parsing and binding attempt to report multiple useful diagnostics in one pass.

Regex matching:

- Uses one canonical member signature, never an arbitrary `ToString()`.
- Is culture-invariant.
- Has a mandatory finite timeout.
- Returns `Unknown` with a diagnostic on timeout.
- Is documented as an escape hatch rather than the primary matching mechanism.

Public long-running operations accept `CancellationToken`. Hot, already-compiled match operations remain synchronous.

## 14. Verification Strategy

### 14.1 Syntax and semantics

- Lexer golden tests for every token and malformed-token class.
- Parser precedence and grouping tests.
- Syntax round-trip tests where preservation is supported.
- Binder tests for every language-version difference.
- Diagnostic code and span tests.
- Property-based and fuzz tests proving arbitrary input does not hang or crash unexpectedly.

### 14.2 Regression and adapter compatibility

- Preserve Standard behavior for generic arity, formal parameter binding, Boolean scopes, suffix structure, attributes and migration diagnostics.
- Use neutral shared fixtures for open and nested generics and assembly unloading.
- Keep the Cecil/Roslyn version matrices, Fody Cecil identity and Rougamo compilation checks, .NET Framework and package validation.
- Build, tests and formatting have no oracle dependency.

### 14.3 Cross-adapter conformance

Build one source fixture and inspect it through:

- Roslyn before emission.
- Mono.Cecil after emission.
- Reflection after loading.

Equivalent facts must produce equivalent results. Missing information must produce `Unknown`, not an unexplained backend-specific negative.

### 14.4 Performance

Benchmark:

- Lex and parse.
- Bind and compile.
- Cached hot matching.
- Assignability-heavy matching.
- Adapter conversion and cache reuse.

Performance evidence for Rougamo integration belongs to the future migration task. The Legacy retirement only removes compatibility-driven overhead and redundancy.

## 15. Migration and Release Plan

1. Retire Legacy and make parsing, binding, evaluation and caches implement Standard directly.
2. Preserve shared capabilities and valuable regression tests; update APIs and documentation.
3. Validate cross-adapter behavior, package boundaries, formatting and the complete retained matrix.
4. In a future task, migrate Rougamo expressions and integration and prepare its breaking major release.

This work does not modify adjacent repositories, frozen cache checkouts or Rougamo submodules, and does not publish packages or push branches.

## 16. Public API Direction

Public roles and naming are fixed:

- `AspectNParser`: parse source into syntax trees and diagnostics.
- `AspectNSyntaxTree`: immutable parsed representation.
- `AspectNCompilation`: syntax tree, compilation options, extensions, and binding.
- `CompiledPattern`: immutable executable plan.
- `MatchResult` and `MatchStatus`: three-state evaluation.
- `ITypeSystem`: backend-neutral relationship contract.
- `CecilTypeSystem`, `RoslynTypeSystem`, `ReflectionTypeSystem`: adapter contexts.

A simple facade may combine parse, bind, and compile, but it must expose diagnostics and must not bypass the explicit compilation model.

## 17. Rejected Alternatives

### Refactor Shared.Cecil.AspectN first

Rejected because it would alter the historical implementation and require a second migration into AspectN.

### One self-contained metadata snapshot

Rejected for 1.0 because assignability and resolution depend on backend context, and copying complete metadata graphs is expensive and error-prone.

### Parser generator

Rejected because the grammar is compact, requires precise diagnostics, and does not justify generated artifacts or another runtime dependency.

### Backend-specific ASTs

Rejected because they would duplicate language semantics and make conformance unprovable.

### Global static caches and extension registries

Rejected because they cause memory retention, non-determinism, cache corruption, and test interference.

### Public capture/query results

Rejected for 1.0 because OR, negation, multiple solutions, and backend-object lifetime would expand the matcher into a query language.

## 18. Review Checklist

- Parsing and binding implement a single language without retired mode parameters.
- Standard regression coverage and stable diagnostics are preserved.
- Native backend objects cannot leak into the core model.
- `Unknown` is preserved through core evaluation.
- No global mutable cache or grammar extension exists.
- Rougamo integration and publication remain separate work.
- 1.0 non-goals are not pulled into implementation tasks.
