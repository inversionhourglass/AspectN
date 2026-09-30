# AspectN language guide

AspectN expressions select .NET types and callables without exposing a
specific metadata backend in the language or compiled pattern API.

## Selectors

- `method(...)` selects ordinary methods.
- `callable(...)` also covers property and event accessors, operators, and
  conversions.
- `execution(...)` covers everything `callable(...)` does, plus instance and
  static constructors.
- `property(...)`, `getter(...)`, and `setter(...)` select property accessors.
- `ctor(...)` and `cctor(...)` select instance and static constructors.
- `attr(target Type)` checks a direct attribute on a type, callable, property,
  return value, or parameter.

Selectors can be combined with `&&`, `||`, `!`, and parentheses.

```text
method(tasklike<string> Service.Load(..))
method($T Converter.Convert<$T>($T))
getter(string Example.Model.Name) && attr(property Example.MarkerAttribute)
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

Matching is three-valued:

- `Matched` means the candidate satisfies the expression.
- `NotMatched` means the available metadata proves it does not.
- `Unknown` means a backend capability or referenced definition is missing.

Do not silently treat `Unknown` as `NotMatched` unless that is an explicit
application policy. Diagnostics on the result explain the missing fact.

## Standard and Legacy

`Standard` is case-sensitive and uses the current fixed grammar. `Legacy` is
an explicit compatibility language for migrating Shared.Cecil.AspectN
expressions. New applications should use `Standard`.

The complete grammar, precedence, modifier rules, generic-variable semantics,
canonical regex form, and diagnostic contract are defined by the
[normative language specification](../superpowers/specs/2026-07-31-aspectn-language-spec.md).
