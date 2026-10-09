# AspectN language design decisions

AspectN has one language: the existing Standard syntax and semantics. These
decisions were extracted from the former compatibility difference inventory.
The [normative specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md)
defines the full contract; [migration guidance](../migration/shared-cecil-to-aspectn.md)
explains the breaking transition from Shared.Cecil.AspectN.

The [confirmed Shared/Standard comparison](shared-standard-differences.md)
provides Chinese explanations, examples and the reason for each difference.

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
the declaring type matches global and nested types too. Structured declaring
types now use the full type-pattern grammar: `*.*` includes all type shapes,
while `*<?>.*` and `*..*.M` remain named paths with explicit nesting rules.

Member paths support prefix negation and independent Boolean combinations:
`!(A || B).(Run || Stop)` negates the conjunction of declaring type and
member-name pattern. Each method-name atom retains its own generic clause.
`!Run` complements zero-arity Run; use `!Run<?>` for every arity. Return types,
parameters, modifiers and selector domains remain outside path negation.
Array and nullable declaring patterns such as `int[,].Get` and
`int?.GetValueOrDefault` follow ordinary type-pattern semantics.

Consecutive `!` tokens, even separated by whitespace, are rejected as likely
typing mistakes. Explicit parentheses may still group negation. Negation of
the full wildcard always selects an empty set, including in member paths.

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
Variables are declared in positive type or method generic clauses. Return,
property and parameter types only reference visible declarations, including
inside their nested generic clauses. The declaring-type/method clauses are
processed before those references regardless of textual order.
`method($T Service.M<!$T>())` is invalid; negated generic-variable declaration
arguments remain invalid even when the variable is already visible.
`method(!$T Service.M<$T>())` is a valid negated return reference.
Adapter declaration identity and closed query contexts are described in
[generic parameter identity](generic-parameter-identity.md).
Variables introduced under negation are invalid, and variables introduced in
isolated OR branches cannot escape. Bare `T` never becomes a variable.

Equivalent binding environments are coalesced only when variable keys, bound
signature instances and certainty agree. Distinct keys remain separate even
when they refer to the same type: a conjunction of `n` groups such as
`(List<$L_i> || List<$R_i>)` can retain up to `2^n` alternatives. Allocation and
matching work can therefore grow exponentially. Binding count, allocation and
time budgets remain deferred; existing parser and candidate-depth limits do
not bound this growth. Regression tests cover equivalent alternatives and a
bounded eight-group distinct-binding case; their allocation ceiling is a test
threshold, not a runtime resource guarantee.

Namespace segments and property-name atoms cannot have generic clauses
(AN2011); declaring types and method names retain them. `<!>` denotes zero
arity only when `!` is the entire clause. `Box<!int>` uses a negated first
argument pattern, just like later slots.

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

`async` is not a signature modifier in Standard, including property, getter and
setter signatures. In the initial modifier position, a separate `async` token
is diagnosed. Adjacent type-name continuations such as `async.Result`,
`async*`, `async<>`, `async[]` and `async?`, and ordinary member names such as
`method(int async())`, retain their normal identifier/pattern meanings.

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

The result clause must be adjacent: `tasklike<int>` is valid and
`tasklike <int>` is rejected. `tasklike` remains a literal declaring-type
name; its shape keyword is used in return/parameter and generic-argument
patterns. Operator/conversion classification validates public special-name
metadata without runtime-special, method-generic, vararg or params flags;
parameter ref kinds must be by value or `in`. It then checks recognized
names, static/instance, return shape and parameter count,
including C# 14 instance assignment/increment operators. Ordinary `op_`
methods remain in the `method` domain.

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

Regex is a small language subset for signature matching. It retains character
escapes/classes, ordinary and noncapturing groups, alternation, anchors and
greedy/lazy quantifiers. Comments, all inline options, other special groups,
backreferences and nested/subtracted classes are rejected during compilation
with AN2012. Literal brackets inside classes require escapes. .NET syntax
errors are compile-time AN3002 errors. This keeps delimiter scanning dependent
only on escapes, ordinary classes and group depth; the finite timeout remains
necessary for ordinary backtracking patterns. See the normative specification
section 15 for the full subset.

Open constructed declaring types name distinct remaining type parameters by
first occurrence inside their actual arguments, including nested generic and
array arguments, before numbering method parameters. Repeated uses share one
name; closed arguments consume no positions.

Migration diagnostics AN4003 (compound accessibility), AN4004 (`tasklike<void>`)
and AN4005 (long attribute target names) remain. AN2002's retired selector-mode
entry point is removed; other diagnostic codes keep their numbers.
