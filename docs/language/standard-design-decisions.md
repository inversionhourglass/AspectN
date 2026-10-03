# AspectN language design decisions

AspectN has one language: the existing Standard syntax and semantics. These
decisions were extracted from the former compatibility difference inventory.
The [normative specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md)
defines the full contract; [migration guidance](../migration/shared-cecil-to-aspectn.md)
explains the breaking transition from Shared.Cecil.AspectN.

## Names and selector domains

Keywords are lowercase and case-sensitive. Bare identifiers always denote
literal type names; variables use `$T`. Numeric names are rejected.
`method` selects ordinary methods, `callable` adds accessors, operators and
conversions, and `execution` also includes instance and static constructors.
Whether a selected member can be woven is a caller decision.

Omitting a namespace allows any namespace, including the global namespace.
`..` joins namespace segments and permits zero or more intervening segments;
`*..*` still requires at least one namespace segment. Nested types require `/`.
The member joins its declaring type with `.`, so `*..M` is incomplete. Omitting
the declaring type matches global and nested types too; writing `*.*` or
`*..*.M` narrows that range. This keeps names independent of their position.

## Generic arity and variables

| Clause | Meaning |
| --- | --- |
| Omitted on a literal name | Exactly zero parameters |
| `<!>` | Exactly zero parameters |
| `<>`, `<,>`, `<,,>` | Exactly one, two, three parameters |
| `<..>` | Any positive arity |
| `<?>` | Any arity, including zero |
| `<$T,>`, `<,$T>`, `<$T,,$U>` | Exact slot count; empty slots are wildcard arguments |
| Omitted on the name `*` | Any arity |

These rules apply to type, declaring-type and method clauses. Explicit clauses
retain their constraints even on primitive aliases and assignable targets.
Every clause on `void` is invalid (AN2011). `$T` can bind a formal generic
parameter or a constructed type; repeated occurrences enforce equality.
Variables introduced under negation are invalid, and variables introduced in
isolated OR branches cannot escape. Bare `T` never becomes a variable.

## Wildcard family

Bare `*` is a full type wildcard. Adding a generic clause makes it a named-type
pattern with that arity constraint; it excludes arrays, generic parameter
candidates and nested types without an explicit `/` path.

| Candidate | `*` | `*<?>` | `*<!>` | `*<..>` |
| --- | --- | --- | --- | --- |
| `int`, `string`, ordinary class | Yes | Yes | Yes | No |
| `List<int>` or open `List<T>` | Yes | Yes | No | Yes |
| `Nullable<int>` or `ValueTuple<int,string>` | Yes | Yes | No | Yes |
| Non-generic `System.ValueTuple` | Yes | Yes | Yes | No |
| Array | Yes | No | No | No |
| Generic parameter `T` | Yes | No | No | No |
| Nested `Outer/Inner` | Yes | No | No | No |

`*[]` constrains the outer rank and leaves the element unconstrained, so it
matches `int[]`, `List<int>[]` and `int[][]`. Ordinary named patterns do not
strip array layers. `*?` retains its full wildcard inside nullable evaluation,
including reference types and `Nullable<GenericStruct<int>>`.

## Structural suffixes

`[` and `?` must touch their type pattern; spaces inside array rank brackets
are allowed. Tuple syntax means `System.ValueTuple`, including flattened Rest
shapes, and requires at least two elements. Write `System.Tuple<...>` explicitly
to match reference tuples. Parentheses also group Boolean type expressions.

For reference types, `T?` has the same CLR identity as `T`; for non-nullable
value types it means `Nullable<T>`. Unknown type category remains `Unknown`.
Arrays are reference types. `int?[]` means an array of nullable integers.

`+` uses a structural whitelist: a named target (wildcard names and generic
clauses included), or a single nullable wrapper over a named target. Arrays,
bare wildcards, tuples, Boolean expressions, variables, tasklike and nested or
interleaved nullable targets are invalid (AN2010). Generic arguments inside a
named target remain unrestricted (`List<int[]>+`); `int+?` remains valid.
Assignable matching preserves generic arguments and does not add covariance
or contravariance. A target that cannot be concretized uses a pattern walk
over the inheritance closure, preserving missing-metadata diagnostics.

## Modifiers, ref kinds and parameters

Unmentioned dimensions do not constrain matching. Accessibility uses the C#
phrases `private protected` and `protected internal`; alternatives use `|`.
There is at most one positive accessibility group. Duplicate modifiers and
`static` / `!static` conflicts are errors. Negation applies to the whole
compound phrase, and cannot be combined with alternation.

Unannotated type uses accept every ref kind. `ref`, `out`, `in` and
`ref readonly` match exactly; `!ref` matches by-value only. Returns cannot use
`out` or `in`. Empty parameter slots are wildcard parameters with exact count:
`(int,)` has two slots, while `(..)` accepts any parameter count.

## Return shapes, attributes and regex

`tasklike` matches non-generic Task/ValueTask; `tasklike<T>` matches the generic
result shape regardless of the compiler async flag. Historical async return
sugar is removed. `MethodSignature.IsAsync` remains general backend metadata.

Attribute targets are `type`, `call`, `prop`, `para[n]` / `para n`,
`para[*]` / `para *`, `ret`, `exec`, `any` and `*`. `exec` checks the current
method and its associated property; `call` checks the method alone. For `prop`,
ordinary methods return `NotMatched`, while missing accessor property metadata
returns `Unknown`. Fully specified names use exact direct queries; wildcard,
arity and other non-concrete arguments use type-pattern evaluation. Omitted
generic clauses require zero arity in both query paths.

Regex uses one culture-invariant signature format: spaced compound
accessibility, ref prefixes, `, ` separators, `/` nested types and positional
generic names. A sole formal parameter is `T`; multiple parameters are
`T1..TN`, outer type slots first, then nested type and method slots. The async
flag is omitted. A finite timeout is mandatory and yields `Unknown` on timeout.

Migration diagnostics AN4003 (compound accessibility), AN4004 (`tasklike<void>`)
and AN4005 (long attribute target names) remain. AN2002's retired selector-mode
entry point is removed; other diagnostic codes keep their numbers.
