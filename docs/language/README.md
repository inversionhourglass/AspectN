# AspectN language guide

AspectN expressions select .NET types and callables without exposing a
specific metadata backend in the language or compiled pattern API.

For the confirmed differences from Shared, examples and rationale, see the
[中文设计差异说明](shared-standard-differences.md).

## Selectors

- `method(...)` selects ordinary methods.
- `callable(...)` also covers property and event accessors, operators, and
  conversions.
- `execution(...)` covers everything `callable(...)` does, plus instance and
  static constructors.
- `property(...)`, `getter(...)`, and `setter(...)` select property accessors.
- `ctor(...)` and `cctor(...)` select instance and static constructors.
- `attr(target Type)` checks a direct attribute on a type, callable (`call`),
  property (`prop`), return value (`ret`), parameter (`para[n]`), the execution
  family (`exec`), or any location (`any`, `*`). Parameter positions accept both
  brackets (`para[0]`, `para[*]`) and spaces (`para 0`, `para *`).

Selectors can be combined with `&&`, `||`, `!`, and parentheses.
Declaring types and member names can also be combined inside a path:
`method(* (A || B).(Run || Stop)(..))`. A path prefix `!` negates declaring
type, name and method generic constraints together. Consecutive `!` tokens
are rejected. Property names and namespace segments cannot have generic clauses.

```text
method(tasklike<string> Service.Load(..))
method($T Converter.Convert<$T>($T))
getter(string Example.Model.Name) && attr(prop Example.MarkerAttribute)
ctor(Example.Service(string, ref int))
```

## Type patterns

Type patterns support qualified and unqualified names, nested types, generic
definitions and constructed types, arrays, nullable value/reference syntax,
tuples, wildcards, and assignability with `+`.

```text
System.Collections.Generic.IEnumerable<string>+
Example.Outer<string>/Inner<int,double>
(string, int)
int[,][]
```

## Results and missing metadata

`regex(...)` searches the canonical member signature using a restricted regex
subset: character escapes/classes, ordinary and noncapturing groups,
alternation, anchors and greedy/lazy quantifiers. Comments, inline options,
lookarounds, backreferences and other special groups, and nested/subtracted
classes are rejected with AN2012. Escape literal brackets inside classes.
Invalid regex syntax produces a compile-time AN3002 error. Regex matching has
a finite timeout; timing out produces `Unknown` with AN3003. See section 15 of
the specification for the complete rules.

Matching is three-valued:

- `Matched` means the candidate satisfies the expression.
- `NotMatched` means the available metadata proves it does not.
- `Unknown` means a backend capability or referenced definition is missing.

Do not silently treat `Unknown` as `NotMatched` unless that is an explicit
application policy. Diagnostics on the result explain the missing fact.

## One language contract

The existing Standard grammar is the sole AspectN language. Keywords are
lowercase and case-sensitive; pattern variables require `$`, and a named type
or method without a generic clause requires arity zero. Use `<?>` for any
arity. The independent [design decisions](standard-design-decisions.md)
explain these choices and the wildcard family.

Legacy and the language selection API have been removed. The
[migration guide](../migration/shared-cecil-to-aspectn.md) describes the
breaking transition from Shared.Cecil.AspectN. Rougamo integration and its
breaking major release are separate work.

The complete grammar, precedence, modifier rules, generic-variable semantics,
canonical regex form, and diagnostic contract are defined by the
[normative language specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md).
