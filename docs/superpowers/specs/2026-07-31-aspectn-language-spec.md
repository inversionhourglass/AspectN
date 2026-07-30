# AspectN Language Design

**Status:** Draft for written review
**Date:** 2026-07-31
**Applies to:** AspectN 1.0 language design
**Language versions:** `Legacy`, `Standard`

## 1. Purpose

AspectN is a .NET type and callable-member pattern language. It parses backend-neutral patterns and evaluates them against normalized metadata supplied by Mono.Cecil, Roslyn, Reflection, or future adapters.

The language has two entry points:

- **Type pattern:** matches one type use or type identity.
- **Member expression:** combines member selectors with boolean operators.

AspectN is a matcher, not a query projection language. Evaluation returns a three-state match result and does not return variable captures.

## 2. Language Versions

### 2.1 Legacy

Legacy preserves approved Shared.Cecil.AspectN syntax and matching behavior for Rougamo. It includes historical names and ambiguities such as:

- `execution(...)`
- `property(...)`
- `ctor(...)` and `cctor(...)`
- `attr(...)`
- `privateprotected` and `protectedinternal`
- Space-separated accessibilities as alternatives.
- Implicit generic variables.
- `(T1,T2)` matching both `Tuple` and `ValueTuple`.
- `async T` as Task/ValueTask return sugar.

Legacy is selected explicitly by the Rougamo compatibility facade.

### 2.2 Standard

Standard is the default for new AspectN APIs. It uses:

- `callable(...)` instead of `execution(...)`.
- `accessor(...)` instead of `property(...)`.
- `ctor(...)` and `cctor(...)`.
- Standard C# accessibility phrases.
- Explicit `$T` pattern variables.
- C# `ValueTuple` meaning for tuple syntax.
- Separate async-method and task-like-return concepts.
- Explicit `ref`, `out`, `in`, and `ref readonly`.

An expression is always compiled with a language version. The compiled cache key includes that version.

## 3. Lexical Rules

### 3.1 Case

- Standard keywords are lowercase and case-sensitive.
- Type, namespace, member, and variable identifiers are case-sensitive.
- Legacy preserves the old case-insensitive keyword behavior where the oracle does so.

### 3.2 Trivia

Whitespace may separate tokens unless it participates in a Standard composite phrase such as `private protected`, `protected internal`, or `ref readonly`.

Comments are not part of AspectN 1.0 expression syntax.

### 3.3 Core punctuation

| Token | Meaning |
|---|---|
| `!` | Logical negation |
| `&&` | Logical conjunction |
| `||` | Logical disjunction |
| `(` `)` | Grouping, selector body, parameter list, or tuple |
| `<` `>` | Generic pattern list |
| `[` `]` | Array rank or attribute parameter index |
| `,` | List separator or array-rank marker |
| `.` | Namespace/type/member separator |
| `/` | Nested-type separator |
| `..` | Zero-or-more namespace segments or zero-or-more list entries, depending on grammar position |
| `*` | Name or type wildcard |
| `+` | Assignability suffix |
| `?` | Nullable suffix |
| `$` | Pattern-variable prefix |

`/` is the canonical nested-type separator in both versions. Adapters normalize Cecil `/`, Reflection `+`, and Roslyn containing-type symbols to this form.

### 3.4 Identifiers, name patterns, and literals

`identifier` follows Unicode identifier start/part rules and is used for extension names and pattern variables. `name-pattern` is a non-empty metadata-name segment containing literal name characters and `*`; reserved punctuation and whitespace terminate the segment. Matching is ordinal.

```ebnf
name-pattern             = ( name-character | "*" ), { name-character | "*" } ;
qualified-extension-name = identifier, { ".", identifier } ;
boolean-literal         = "true" | "false" ;
```

`name-character`, `identifier`, `string-literal`, `integer-literal`, and `regex-body` are lexer primitives. Strings use C#-style escapes, integers are signed decimal values, and Boolean literals are the lowercase tokens `true` and `false`.

## 4. Boolean Expressions

The common expression grammar is:

```ebnf
expression          = or-expression ;
or-expression       = and-expression, { "||", and-expression } ;
and-expression      = unary-expression, { "&&", unary-expression } ;
unary-expression    = "!", unary-expression | primary-expression ;
primary-expression  = selector | "(", expression, ")" ;
```

Precedence from highest to lowest:

1. `!`
2. `&&`
3. `||`

Operators of the same precedence associate left to right. Parentheses override precedence.

## 5. Three-state Boolean Logic

Evaluation uses `Matched`, `NotMatched`, and `Unknown`.

### 5.1 Negation

| Input | Result |
|---|---|
| Matched | NotMatched |
| NotMatched | Matched |
| Unknown | Unknown |

### 5.2 Conjunction

| Left | Right | Result |
|---|---|---|
| Matched | Matched | Matched |
| Matched | NotMatched | NotMatched |
| Matched | Unknown | Unknown |
| NotMatched | Matched | NotMatched |
| NotMatched | NotMatched | NotMatched |
| NotMatched | Unknown | NotMatched |
| Unknown | Matched | Unknown |
| Unknown | NotMatched | NotMatched |
| Unknown | Unknown | Unknown |

### 5.3 Disjunction

| Left | Right | Result |
|---|---|---|
| Matched | Matched | Matched |
| Matched | NotMatched | Matched |
| Matched | Unknown | Matched |
| NotMatched | Matched | Matched |
| NotMatched | NotMatched | NotMatched |
| NotMatched | Unknown | Unknown |
| Unknown | Matched | Matched |
| Unknown | NotMatched | Unknown |
| Unknown | Unknown | Unknown |

Implementations may short-circuit only when doing so preserves diagnostics required by the selected evaluation policy.

## 6. Standard Member Selectors

```ebnf
selector =
      callable-selector
    | method-selector
    | accessor-selector
    | getter-selector
    | setter-selector
    | ctor-selector
    | cctor-selector
    | attribute-selector
    | regex-selector
    | extension-selector ;

callable-selector = "callable", "(", callable-signature, ")" ;
method-selector   = "method",   "(", callable-signature, ")" ;
accessor-selector = "accessor", "(", property-signature, ")" ;
getter-selector   = "getter",   "(", property-signature, ")" ;
setter-selector   = "setter",   "(", property-signature, ")" ;
ctor-selector     = "ctor",     "(", ctor-signature, ")" ;
cctor-selector    = "cctor",    "(", type-pattern, ")" ;

extension-selector =
    qualified-extension-name,
    "(", [ extension-argument, { ",", extension-argument } ], ")" ;

extension-argument =
      string-literal
    | integer-literal
    | boolean-literal
    | type-pattern ;
```

### 6.1 Selector domains

- `callable(...)` matches non-constructor method-like members: ordinary methods, property/event accessors, operators, and conversions.
- `method(...)` matches ordinary methods only.
- `accessor(...)` matches a property getter or setter.
- `getter(...)` matches only a property getter.
- `setter(...)` matches only a property setter.
- `ctor(...)` matches instance constructors.
- `cctor(...)` matches static constructors.

Whether a member has a body or can be woven is not implied by any selector. Rougamo applies that scenario-specific filter after AspectN matching or through a registered predicate such as `hasBody`.

### 6.2 Legacy selector mapping

| Legacy | Standard migration |
|---|---|
| `execution(...)` | `callable(...)`, plus explicit `ctor(...)`/`cctor(...)` where constructors are intended |
| `property(...)` | `accessor(...)` |
| `method(...)` | `method(...)` |
| `getter(...)` | `getter(...)` |
| `setter(...)` | `setter(...)` |
| `ctor(...)` | `ctor(...)` |
| `cctor(...)` | `cctor(...)` |

Standard reports a migration diagnostic for `execution` and `property` rather than accepting them as aliases.

## 7. Callable Signatures

```ebnf
callable-signature =
    { modifier },
    [ "async" ],
    type-use-pattern,
    member-path-pattern,
    [ generic-pattern-list ],
    parameter-pattern-list ;

member-path-pattern =
    [ type-pattern, "." ],
    name-pattern ;

type-use-pattern =
    [ return-ref-kind ],
    type-pattern ;

return-ref-kind =
      "ref"
    | "ref", "readonly" ;
```

`async` in Standard means the callable is compiler-recognized as asynchronous. It does not rewrite the return type. For example:

```text
method(public async System.Threading.Tasks.Task<string> Service.Get(..))
```

Task-like return matching is expressed as a type pattern:

```text
method(tasklike<string> Service.Get(..))
method(tasklike<void> Service.Flush(..))
```

`tasklike<T>` matches `Task<T>` or `ValueTask<T>` regardless of whether the method was compiled with `async`. `tasklike<void>` matches non-generic `Task` or `ValueTask`. Custom awaitables are outside 1.0 task-like syntax.

Legacy `async T` retains its historical Task/ValueTask return-shape behavior.

## 8. Property Signatures

```ebnf
property-signature =
    { modifier },
    type-use-pattern,
    [ type-pattern, "." ],
    name-pattern ;
```

The type position is the property type, not the setter method's CLR return type.

`accessor(...)` evaluates the associated property metadata and then selects both getter and setter methods. The unified model represents the property and its accessors separately.

## 9. Constructor Signatures

```ebnf
ctor-signature =
    type-pattern,
    parameter-pattern-list ;
```

`ctor(...)` never matches a static constructor. `cctor(...)` never matches an instance constructor.

## 10. Modifiers

Standard recognizes:

- `public`
- `protected`
- `internal`
- `private`
- `protected internal`
- `private protected`
- `static`
- `!static`
- Negated accessibility such as `!public`

```ebnf
modifier =
      accessibility
    | "!", accessibility
    | "static"
    | "!", "static" ;

accessibility =
      "public"
    | "protected"
    | "internal"
    | "private"
    | "protected", "internal"
    | "private", "protected" ;
```

At most one positive accessibility appears in one Standard signature. To match alternatives, use expression-level OR:

```text
method(private * *(..)) || method(protected * *(..))
```

This rule removes the Legacy ambiguity where:

```text
private protected
```

means “private or protected,” while `privateprotected` means the C# composite accessibility.

Legacy preserves `privateprotected`, `protectedinternal`, and its existing modifier-set behavior.

## 11. Parameter Patterns and Ref Kinds

```ebnf
parameter-pattern-list =
    "(", [ ".." | parameter-pattern, { ",", parameter-pattern } ], ")" ;

parameter-pattern =
    [ ref-kind ],
    type-pattern ;

ref-kind =
      "ref"
    | "out"
    | "in"
    | "ref", "readonly" ;
```

Rules:

- `(..)` matches any parameter count and any parameter types.
- `()` matches zero parameters.
- An explicit list matches exact count and order.
- Omitting `ref-kind` matches by-value only.
- A full wildcard parameter pattern may ignore ref kind.
- Return type uses support `ref` and `ref readonly`; `out` and `in` are invalid for returns.

`params`, `scoped`, pointers, and function pointers are not expressible in Standard 1.0, although adapters may preserve those facts for future predicates.

## 12. Type Patterns

### 12.1 Structure

```ebnf
type-pattern =
    type-or-pattern ;

type-or-pattern =
    type-and-pattern, { "||", type-and-pattern } ;

type-and-pattern =
    type-unary-pattern, { "&&", type-unary-pattern } ;

type-unary-pattern =
    "!", type-unary-pattern | type-primary-pattern ;

type-primary-pattern =
      type-atom-pattern, { type-suffix }
    | "(", type-pattern, ")" ;

type-atom-pattern =
      named-type-pattern
    | tuple-type-pattern
    | tasklike-type-pattern
    | pattern-variable
    | "*" ;

named-type-pattern =
    type-name-segment,
    { namespace-separator, type-name-segment },
    { "/", type-name-segment } ;

type-name-segment =
    name-pattern, [ generic-pattern-list ] ;

namespace-separator = "." | ".." ;

tuple-type-pattern =
    "(", type-pattern, ",", type-pattern,
    { ",", type-pattern }, ")" ;

tasklike-type-pattern =
    "tasklike", "<", type-pattern, ">" ;

pattern-variable =
    "$", identifier ;

type-suffix =
      array-rank-specifier
    | "?"
    | "+" ;

array-rank-specifier =
    "[", { "," }, "]" ;
```

Type-level boolean operators use the same precedence and three-state logic as member expressions.

The binder validates suffix ordering and applicability. Each comma in an array rank specifier increases the rank by one; `[]` is rank one.

### 12.2 Namespaces and names

- `*` matches zero or more characters inside one name segment.
- `..` matches zero or more namespace segments.
- Omitted namespace matches any namespace.
- `/` separates nested types.
- Matching is ordinal and case-sensitive in Standard.

Examples:

```text
Service
MyCompany.Services.*Service
MyCompany..Internal..*Service
Outer/Inner
```

### 12.3 Primitive aliases

Standard recognizes:

```text
bool byte sbyte short ushort int uint long ulong
char float double decimal string object void
```

Each alias maps to its corresponding `System` type.

### 12.4 Assignability

The postfix `+` matches the named type itself or any assignable subtype/implementation:

```text
System.IDisposable+
*Provider+
```

Failure to resolve required base or interface information produces `Unknown`.

### 12.5 Arrays

```text
int[]
int[,]
string[][]
```

Ranks and nesting must match exactly unless a wildcard pattern explicitly removes that constraint.

### 12.6 Nullability

Standard rules:

- If `T` resolves to a non-nullable value type, `T?` matches `System.Nullable<T>`.
- If `T` resolves to a reference type, `T?` is equivalent to `T`.
- Nullable reference annotations are not matched.
- `System.Nullable<T>` can always be written explicitly.

Legacy preserves its historical `Nullable<T>` interpretation.

### 12.7 Tuples

Standard:

```text
(int, string)
($T, decimal)
```

matches `System.ValueTuple` only. Long tuples are normalized structurally so callers do not need to express the runtime `TRest` nesting.

To match `System.Tuple`, write it explicitly:

```text
System.Tuple<int, string>
```

Legacy tuple syntax continues to match both `System.Tuple` and `System.ValueTuple`.

## 13. Generics

### 13.1 Arity

Standard uses:

| Pattern | Meaning |
|---|---|
| `Name` | Non-generic type or member named `Name` |
| `Name<>` | Arity 1 with any argument |
| `Name<,>` | Arity 2 with any arguments |
| `Name<,,>` | Arity 3 with any arguments |
| `Name<..>` | Any positive arity |
| `Name<T1,T2>` | Exact argument patterns |

The special name wildcard `*` without a generic clause may match any arity.

Legacy preserves omission-as-any-arity and `<!>` for non-generic matching.

```ebnf
generic-pattern-list =
    "<", ( ".." | [ ",", { "," } ] | type-pattern, { ",", type-pattern } ), ">" ;
```

An empty wildcard list `<>` represents arity one; otherwise, an all-comma list has arity equal to its comma count plus one.

### 13.2 Pattern variables

Standard variables begin with `$`:

```text
$T
$Result
$TKey
```

Example:

```text
method($T Service.Convert<$T>($T))
```

The first positive occurrence binds the variable for one evaluation. Later occurrences must be type-equal under the active `ITypeSystem`.

Rules:

- Ordinary `T` or `Result` is a type name, never an implicit variable.
- Conjunction shares a binding environment.
- Each OR branch evaluates with an isolated copy.
- Bindings created inside negation do not escape it.
- A variable may not be introduced for the first time under negation.
- Bindings are not returned in public `MatchResult`.

Variables may bind formal generic parameters or constructed concrete types.

Legacy preserves implicit generic-variable naming and virtual-position behavior.

## 14. Attributes

Legacy preserves:

```text
attr(type AttributeType)
attr(exec AttributeType)
attr(para 0 AttributeType)
attr(para * AttributeType)
attr(ret AttributeType)
attr(* AttributeType)
```

Standard uses explicit positions:

```ebnf
attribute-selector =
    "attr", "(", attribute-position, type-pattern, ")" ;

attribute-position =
      "type"
    | "callable"
    | "property"
    | "parameter", "[", ( integer-literal | "*" ), "]"
    | "return"
    | "any" ;
```

```text
attr(type AttributeType)
attr(callable AttributeType)
attr(property AttributeType)
attr(parameter[0] AttributeType)
attr(parameter[*] AttributeType)
attr(return AttributeType)
attr(any AttributeType)
```

Semantics:

- `type`: direct attributes on the declaring type.
- `callable`: direct attributes on the current callable.
- `property`: direct attributes on the associated property when the candidate is a property accessor.
- `parameter[n]`: direct attributes on parameter `n`.
- `parameter[*]`: direct attributes on any parameter.
- `return`: direct attributes on the return location.
- `any`: any supported direct location.

Attribute matching does not instantiate attribute objects. Inherited-attribute semantics are outside Standard 1.0.

## 15. Regex

```ebnf
regex-selector = "regex", "(", regex-body, ")" ;
```

`regex-body` preserves its original characters. The closing delimiter is the unmatched outer `)` after honoring escapes, character classes, and nested regex groups.

Regex applies to a versioned, culture-invariant canonical member signature:

```text
<accessibility> [static] [async] <return-type> <declaring-type>.<member-name><generic-part>(<parameters>)
```

Ref kinds are rendered before affected parameter or return types. Nested types use `/`. Type names are fully qualified except generic variables in definition signatures.

Regex evaluation has a mandatory finite timeout. Timeout produces `Unknown` with a diagnostic.

## 16. Extension Selectors and Predicates

Qualified extension names may participate in selector or predicate positions defined by the fixed grammar:

```text
vendor.feature(argument1, argument2)
```

An extension registry is supplied to `AspectNCompilation` before binding and becomes immutable. Arguments use the fixed scalar-or-type grammar from section 6; an extension cannot reinterpret an arbitrary token stream. Unknown extensions produce diagnostics, and extensions cannot add punctuation or alter parsing rules.

## 17. Diagnostics

Standard diagnostics include stable categories for:

- Lexical errors.
- Missing or unexpected tokens.
- Unknown selector or predicate.
- Selector unavailable in the selected language version.
- Invalid target domain.
- Invalid modifier combinations.
- Invalid ref kind.
- Duplicate or illegally scoped variables.
- Missing backend capability.
- Resolution failure.
- Regex timeout.

Migration diagnostics include an actionable replacement where one exists:

```text
execution(...) -> callable(...)
property(...)  -> accessor(...)
privateprotected -> private protected
protectedinternal -> protected internal
```

## 18. Legacy Conformance

Legacy conformance is behavioral, not architectural. The new implementation may produce a completely different syntax tree and execution plan, but it must preserve every approved Legacy match outcome.

The frozen Shared.Cecil.AspectN engine remains the differential oracle until AspectN 1.0. A behavior difference is acceptable only when recorded as an approved correction.

## 19. Standard Examples

```text
method(public static string MyCompany.Services.UserService.Get*(..))

callable(!public * *..*.*(..))

accessor(public string MyCompany.Models.User.Name)

getter(* *..*.CreatedAt)

setter(* *..*.UpdatedAt)

ctor(MyCompany.Services.UserService(..))

cctor(MyCompany.Services.UserService)

method(tasklike<string> *..*.*Async(..))

method(public async System.Threading.Tasks.Task<string> Service.Load(..))

method($T Converter.Convert<$T>($T))

method(* Service.Copy(ref int, out string))

method((int, string) Service.GetPair())

attr(parameter[*] System.ComponentModel.DataAnnotations.RequiredAttribute)

method(* Service.*(..)) && !attr(callable ObsoleteAttribute)
```

## 20. Standard/Legacy Difference Summary

| Concern | Legacy | Standard |
|---|---|---|
| Umbrella callable selector | `execution` | `callable` |
| All property accessors | `property` | `accessor` |
| Constructors | `ctor`, `cctor` | `ctor`, `cctor` |
| Composite accessibility | `privateprotected`, `protectedinternal` | `private protected`, `protected internal` |
| Space-separated accessibilities | Alternatives | Not allowed; use expression OR |
| Generic variables | Implicit identifiers | Explicit `$name` |
| Tuple syntax | Tuple or ValueTuple | ValueTuple only |
| `async T` | Task/ValueTask return sugar | Actual `async` modifier plus `tasklike<T>` return pattern |
| Reference `T?` | Historical Nullable behavior | Same CLR reference type as `T` |
| Ref kinds | Not formally modeled | `ref`, `out`, `in`, `ref readonly` |
| Generic name without clause | Any arity | Arity zero, except `*` |
| Attribute positions | `exec`, `para`, `ret`, `*` | Full position names |
