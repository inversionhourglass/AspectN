# AspectN Language Design

**Status:** Approved single-language contract
**Date:** 2026-07-31; updated 2026-10-03
**Applies to:** AspectN 1.0 language design
**Language:** Standard (the sole AspectN language)

## 1. Purpose

AspectN is a .NET type and callable-member pattern language. It parses backend-neutral patterns and evaluates them against normalized metadata supplied by Mono.Cecil, Roslyn, Reflection, or future adapters.

The language has two entry points:

- **Type pattern:** matches one type use or type identity.
- **Member expression:** combines member selectors with boolean operators.

AspectN is a matcher, not a query projection language. Evaluation returns a three-state match result and does not return variable captures.

## 2. Single Language Contract

AspectN directly implements the existing Standard grammar. It uses:

- `callable(...)` for non-constructor method-like members, or `execution(...)` to cover constructors as well.
- `property(...)` for property getters and setters together.
- `ctor(...)` and `cctor(...)`.
- Standard C# accessibility phrases, with `|` joining accessibility alternatives.
- Explicit `$T` pattern variables.
- C# `ValueTuple` meaning for tuple syntax.
- Task-like return matching via `tasklike` or `tasklike<T>`; no `async` modifier.
- Explicit `ref`, `out`, `in`, and `ref readonly`.

There is no language selection API or compatibility mode. `AspectNParser` exposes `ParseMemberExpression(string source)` and `ParseTypePattern(string source)`. `AspectNEngine` compilation methods accept source and optional compilation options. Cache keys contain source text, parse entry point, regex timeout. Migrating from Shared.Cecil.AspectN is a breaking change; Rougamo integration and its major release are separate tasks.

## 3. Lexical Rules

### 3.1 Case

- Standard keywords are lowercase and case-sensitive.
- Type, namespace, member, and variable identifiers are case-sensitive.

### 3.2 Trivia

Whitespace may separate tokens unless it participates in a Standard composite phrase such as `private protected`, `protected internal`, or `ref readonly`.

Within a name, the separators `.`/`..`/`/`, the generic opening `<`, and the assignability suffix `+` continue the name only when adjacent to it; whitespace ends the name and separates the surrounding slots. The array suffix `[` and the nullable suffix `?` must also be adjacent to the type pattern; whitespace inside a `<...>` generic pattern list and inside array rank brackets (`int[ , ]`) is free.

The result-clause opening `<` must also touch `tasklike`: `tasklike<int>` is valid, while `tasklike <int>` is an error. A member separator `.` must touch both its declaring type pattern and its member-name pattern, including their grouping parentheses.

Comments are not part of AspectN 1.0 expression syntax.

### 3.3 Core punctuation

| Token | Meaning |
|---|---|
| `!` | Logical negation |
| `&&` | Logical conjunction |
| `||` | Logical disjunction |
| `|` | Accessibility alternation separator (Standard modifiers) |
| `(` `)` | Grouping, selector body, parameter list, or tuple |
| `<` `>` | Generic pattern list |
| `[` `]` | Array rank or attribute parameter index |
| `,` | List separator or array-rank marker |
| `.` | Namespace/type/member separator |
| `/` | Nested-type separator |
| `..` | Namespace segment ellipsis in qualified names, any-positive arity in `<..>`, or any parameter count in `(..)` |
| `*` | Name or type wildcard |
| `+` | Assignability suffix |
| `?` | Nullable suffix; alone in a generic clause (`<?>`) it means any arity including zero |
| `$` | Pattern-variable prefix |

`/` is the canonical nested-type separator. Adapters normalize Cecil `/`, Reflection `+`, and Roslyn containing-type symbols to this form.

### 3.4 Identifiers, name patterns, and literals

`identifier` follows the C# Unicode category profile and is used for selector names and pattern variables. Starts accept `_` and Lu, Ll, Lt, Lm, Lo, Nl; subsequent characters additionally accept Nd, Pc, Mn, Mc, Cf. This profile does not include UAX #31 Other_ID_Start/Other_ID_Continue additions or escaped identifier syntax. `name-pattern` is a non-empty metadata-name segment containing literal name characters and `*`; reserved punctuation and whitespace terminate the segment. Matching is ordinal.

Consecutive nullable suffixes, such as `int??` and `List<int??>`, produce AN2010. Nested or separated nullable uses remain valid, for example `List<int?>` and `int?[]?`. Ref-kind annotations, including adjacent `!ref`, require member parameter or return type slots and produce AN2005 at the standalone type-pattern entry point, including inside generic and Boolean type patterns. The `!` and `ref` tokens must touch: `! ref`, including a tab or newline between the tokens, produces AN2005 in member type slots.

```ebnf
name-pattern             = ( name-character | "*" ), { name-character | "*" } ;
boolean-literal         = "true" | "false" ;
```

`name-character`, `identifier`, `string-literal`, `integer-literal`, and `regex-body` are lexer primitives. Strings use C#-style escapes, integers are signed decimal values, and Boolean literals are the lowercase tokens `true` and `false`.

## 4. Boolean Expressions

The common expression grammar is:

```ebnf
expression          = or-expression ;
or-expression       = and-expression, { "||", and-expression } ;
and-expression      = unary-expression, { "&&", unary-expression } ;
unary-expression    = [ "!" ], primary-expression ;
primary-expression  = selector | "(", expression, ")" ;
```

Precedence from highest to lowest:

1. `!`
2. `&&`
3. `||`

Operators of the same precedence associate left to right. Parentheses override precedence.

Consecutive unary negations, including whitespace-separated `! !`, are errors (AN1001 at the second `!`). This applies to member expressions, type patterns and member-name patterns. Explicitly grouped negation such as `!(!int)` remains valid. Negating a full wildcard always yields the empty match set; there is no wildcard exception to Boolean complement.

## 5. Three-state Boolean Logic

Evaluation uses `Matched`, `NotMatched`, and `Unknown`.

Selectors query only the facts they consume. Unavailable metadata propagates `Unknown`, including through negation; known mismatches still dominate conjunctions and known matches dominate disjunctions. Empty collections whose availability is unknown do not establish absence. See [metadata availability](../../language/metadata-availability.md) for the signature contract and examples.

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
    | execution-selector
    | method-selector
    | property-selector
    | getter-selector
    | setter-selector
    | ctor-selector
    | cctor-selector
    | attribute-selector
    | regex-selector ;

callable-selector  = "callable",  "(", callable-signature, ")" ;
execution-selector = "execution", "(", callable-signature, ")" ;
method-selector    = "method",    "(", callable-signature, ")" ;
property-selector  = "property",  "(", property-signature, ")" ;
getter-selector    = "getter",    "(", property-signature, ")" ;
setter-selector    = "setter",    "(", property-signature, ")" ;
ctor-selector      = "ctor",      "(", ctor-signature, ")" ;
cctor-selector     = "cctor",     "(", type-pattern, ")" ;

```

### 6.1 Selector domains

- `callable(...)` matches non-constructor method-like members: ordinary methods, property/event accessors, operators, and conversions.
- `execution(...)` matches every method-like member, including instance and static constructors. Constructor candidates match a callable signature by their metadata name (`.ctor`/`.cctor`) and `void` return type.
- `method(...)` matches ordinary methods only.
- `property(...)` matches a property getter or setter.
- `getter(...)` matches only a property getter.
- `setter(...)` matches only a property setter.
- `ctor(...)` matches instance constructors.
- `cctor(...)` matches static constructors.

Whether a member has a body or can be woven is not implied by any selector. Rougamo applies that scenario-specific filter after AspectN matching.

## 7. Callable Signatures

```ebnf
callable-signature =
    { modifier },
    type-use-pattern,
    member-path-pattern,
    parameter-pattern-list ;

member-path-pattern =
    [ "!" ],
    [ declaring-type-pattern, "." ],
    member-name-pattern ;

declaring-type-pattern = type-pattern ;
member-name-pattern = member-name-and, { "||", member-name-and } ;
member-name-and = member-name-unary, { "&&", member-name-unary } ;
member-name-unary = [ "!" ], member-name-primary ;
member-name-primary =
      name-pattern, [ generic-pattern-list ]
    | "(", member-name-pattern, ")" ;

type-use-pattern =
    [ return-ref-kind ],
    type-pattern ;

return-ref-kind =
      "ref"
    | "ref", "readonly" ;
```

A member path has an optional prefix negation, an optional declaring type pattern and a Boolean member-name pattern. The declaring type is omitted by writing nothing — `method(* Get(..))` matches members named `Get` on any declaring type; there is no leading-separator spelling. Separators (`.`, `..`, `/`) must remain adjacent (§3.2).

Group Boolean declaring types before the member separator: `(A || B).Run`. Names can be grouped independently: `A.(Run || Stop)`, or combined as `(A || B).(Run && !Stop)`. Arrays and nullable types use the same structural matching rules as other type positions: `int[,].Get`, `int?.GetValueOrDefault`. Name atoms are literal metadata names with wildcard characters and optional method generic clauses, not type aliases or structural type patterns.

A prefix `!` negates the entire conjunction of declaring type, member name and that name's generic clause. It does not negate modifiers, return/property type, parameters or the selector domain. Thus `!Run` complements zero-arity `Run`; `!Run<?>` complements `Run` at every arity. To negate only the name, write `A.!Run`; to negate only the declaring type, write `(!A).Run`. `!*(..)` matches no members. Negation isolates tentative variable bindings, and Boolean branches use the scope rules in §13.

`tasklike` remains a literal name in member-path declaring-type name positions, including `tasklike<int>+.Get`. In return/parameter type patterns, generic argument patterns, and the declaring-type patterns of `ctor`/`cctor`, it denotes the Task/ValueTask shape. Thus `ctor(tasklike(..))` and `cctor(tasklike<int>)` use shape matching. A qualified literal type name such as `Example.tasklike<int>` is unambiguous in any position, including constructors.

`..` is a namespace ellipsis: it may only appear between namespace segments, never immediately before the member name. The member name is always joined to its declaring type with `.` — `method(* *..StaticMethod(..))` and `method(* System..*(..))` are errors because the declaring type's type-name segment is missing; write `method(* *..*.StaticMethod(..))`, or omit the declaring type entirely with `method(* StaticMethod(..))`. These replacements have different ranges: a written namespace/type path excludes global or nested types unless explicitly expressed, while an omitted declaring type includes them.

`*` as the whole member path matches any namespace, declaring type, member name and generic arity. With the structured declaring-type grammar, `*.*` also uses the full type wildcard and includes arrays, generic parameters and nested declaring types. Adding a declaring-type generic clause changes it to a named pattern: `*<?>.*` excludes arrays and generic parameters, and nested types require `/`. Other written names continue to follow the namespace and nesting rules.

Standard has no `async` modifier: writing `async` in a signature is an error. Asynchrony is matched through the return type instead.

Task-like return matching is expressed as a type pattern:

```text
method(tasklike<string> Service.Get(..))
method(tasklike Service.Flush(..))
```

`tasklike<T>` matches `Task<T>` or `ValueTask<T>` regardless of whether the method was compiled with `async`. Bare `tasklike` matches non-generic `Task` or `ValueTask`; `tasklike<void>` and `tasklike<System.Void>` are rejected with a migration diagnostic. Custom awaitables are outside 1.0 task-like syntax.

The historical `async` return sugar is unsupported. General `MethodSignature.IsAsync` metadata remains available to consumers.

## 8. Property Signatures

```ebnf
property-signature =
    { modifier },
    type-use-pattern,
    member-path-pattern ;
```

The type position is the property type, not the setter method's CLR return type.

Property names use the same Boolean and path negation syntax as methods. Generic clauses on any property-name atom are errors (AN2011), even `<!>` or a clause inside a Boolean branch; generic clauses on its declaring type remain valid.

`property(...)` evaluates the associated property metadata and then selects both getter and setter methods. The unified model represents the property and its accessors separately.

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
    | accessibility-alternation
    | "!", accessibility
    | "static"
    | "!", "static" ;

accessibility =
      single-accessibility
    | compound-accessibility ;

accessibility-alternation =
      alternation-atom, "|", alternation-atom, { "|", alternation-atom } ;

alternation-atom =
      single-accessibility
    | "(", compound-accessibility, ")" ;

single-accessibility =
      "public"
    | "protected"
    | "internal"
    | "private" ;

compound-accessibility =
      "protected", "internal"
    | "private", "protected" ;
```

At most one positive accessibility (or one `|` alternation group) appears in one Standard signature. `|` joins positive alternatives; a candidate matches when its accessibility equals any alternative. A compound accessibility combined with `|` must be parenthesized. Parentheses around a lone compound accessibility are also allowed for readability; a parenthesized single accessibility such as `(public)`, or a pair that is not a compound such as `(internal public)`, is an error — whitespace never joins accessibility alternatives, use `|`:

```text
method(private|internal * *(..))
method((private protected)|(protected internal)|public * *(..))
```

Negation applies to a single accessibility phrase only; `!` cannot be combined with `|`:

```text
method(!protected internal * *(..))
```

`private protected` and `protected internal` each denote one composite accessibility. Use `|` to express alternatives. Concatenated historical spellings report AN4003 with the corresponding spaced phrase.

## 11. Parameter Patterns and Ref Kinds

```ebnf
parameter-pattern-list =
    "(", [ ".." | parameter-slot, { ",", parameter-slot } ], ")" ;

parameter-slot =
    [ ref-annotation ],
    type-pattern ;

ref-annotation =
      ref-kind
    | "!ref" ;

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
- An empty slot between or after commas is an implicit `*` parameter: `(int,)` matches two parameters (`int` plus any), and `(,int)` matches (any, `int`).
- Omitting the ref annotation does not constrain ref kind: the parameter or return matches by-value and by-ref candidates alike. `ref`, `out`, `in`, and `ref readonly` match that exact kind, and `!ref` matches only non-by-ref (by-value) candidates. `!ref` cannot be combined with an explicit ref kind, and no `!out`/`!in` negation exists — negation applies to the by-ref concept as a whole.
- Return type uses support `ref`, `ref readonly`, and `!ref`; `out` and `in` are invalid for returns.

`params`, `scoped`, pointers, and function pointers are not expressible in Standard 1.0, although adapters may preserve those facts for future predicates.

## 12. Type Patterns

### 12.1 Structure

Parentheses group Boolean type expressions such as `(A || B)`. A lone `(int)` is rejected; write `int`. Tuple syntax requires at least two elements, so `()` and `(int,)` are invalid. Standalone `ParseTypePattern` accepts pure type patterns; `ref`, `out`, `in`, `ref readonly`, and `!ref` belong to member return/parameter type-use positions.


```ebnf
type-pattern =
    type-or-pattern ;

type-or-pattern =
    type-and-pattern, { "||", type-and-pattern } ;

type-and-pattern =
    type-unary-pattern, { "&&", type-unary-pattern } ;

type-unary-pattern =
    [ "!" ], type-primary-pattern ;

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
    [ namespace-pattern, namespace-separator ],
    type-name-segment,
    { "/", type-name-segment } ;

namespace-pattern =
    name-pattern, { namespace-separator, name-pattern } ;

type-name-segment =
    name-pattern, [ generic-pattern-list ] ;

namespace-separator = "." | ".." ;

tuple-type-pattern =
    "(", type-pattern, ",", type-pattern,
    { ",", type-pattern }, ")" ;

tasklike-type-pattern =
    "tasklike", [ "<", type-pattern, ">" ] ;

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

Namespace segments never have generic clauses (AN2011 at the clause). `Ns<int>.Owner` is invalid; `Ns.Outer<int>/Inner<string>` is valid. Use `/` for nested types rather than placing a generic type in a namespace segment.

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

The postfix `+` matches the named type itself or any declared subtype/implementation, including runtime-synthesized interface implementations such as the generic collection interfaces of SZ arrays:

```text
System.IDisposable+
*Provider+
```

The target of `+` is validated structurally: after unwrapping parentheses it must be a named type pattern (wildcard names and explicit generic clauses allowed) or a single-level nullable of a named type pattern (`X?+`). Any other target structure — arrays, nested or interleaved nullable wrappers, bare wildcards, tuples, Boolean combinations, pattern variables, `tasklike`, or a second `+` separated by another suffix — is rejected with a binding diagnostic. The rule constrains the target node itself only: array, wildcard, and tuple patterns inside the generic arguments of a named target (`List<int[]>+`) remain legal, and `int+?` (a nullable wrapping an assignable pattern, not an assignability target) is unaffected.

Variance conversions — array covariance and generic co/contravariance — are not part of `+`. Express them compositionally by placing `+` inside type arguments or array elements:

```text
Base+[]                     matches Derived[]
IEnumerable<object+>        matches IEnumerable<string>
```

Failure to resolve required base or interface information produces `Unknown`.

### 12.5 Arrays

```text
int[]
int[,]
string[][]
```

Ranks and nesting must match exactly unless a wildcard pattern explicitly removes that constraint.

Standard array patterns constrain rank and element patterns. `int[]` accepts both a vector (`int[]`, SZ) and a general rank-one array (`int[*]`, non-SZ); `[*]` is metadata notation, not added DSL syntax. The public `ArrayTypeSignature.Shape` preserves this distinction for identity and nominal relationship queries. Shape is compared recursively in nested arrays and generic arguments. Nominal array-to-array queries permit vector-to-general shape conversion with identical elements, but not the reverse; they do not introduce CLR element covariance. See [array shape](../../language/array-shape.md).

### 12.6 Nullability

Standard rules:

- If `T` resolves to a non-nullable value type, `T?` matches `System.Nullable<T>`.
- If `T` resolves to a reference type, `T?` is equivalent to `T`.
- Nullable reference annotations are not matched.
- `System.Nullable<T>` can always be written explicitly.

### 12.7 Tuples

Standard:

```text
(int, string)
method(($T, decimal) Service.M<$T>())
```

matches `System.ValueTuple` only. Long tuples are normalized structurally so callers do not need to express the runtime `TRest` nesting.

To match `System.Tuple`, write it explicitly:

```text
System.Tuple<int, string>
```

Match `System.Tuple` explicitly with a named generic type pattern.

## 13. Generics

### 13.1 Arity

Standard uses:

| Pattern | Meaning |
|---|---|
| `Name` | Non-generic type or member named `Name` (omission means arity zero) |
| `Name<!>` | Explicit arity zero |
| `Name<>` | Arity 1 with any argument |
| `Name<,>` | Arity 2 with any arguments |
| `Name<,,>` | Arity 3 with any arguments |
| `Name<..>` | Any positive arity (at least one) |
| `Name<?>` | Any arity including zero — generic and non-generic alike |
| `Name<T1,T2>` | Exact argument patterns |
| `Name<T,>` / `Name<,T>` | Arity 2 with one exact and one any-argument slot |
| `Name<T,,U>` | Arity 3 with a middle any-argument slot |

`<!>` and `<?>` are available in every generic-clause position (type patterns, declaring types, member generic clauses) and on wildcard names. A suffixed `*` clause binds as a named wildcard pattern — it is not the full type wildcard: arrays and generic-parameter candidates are never matched, and a single-segment pattern does not cross nested types (§7). `ValueTuple<...>` is an ordinary named generic type from the CLR's perspective and is matched by `*<?>`/`*<..>` like any constructed generic. `*` without a clause remains the only full wildcard and the only abbreviation that matches arrays and nesting.

The special name wildcard `*` without a generic clause matches any arity — the single abbreviation exception. For every other name, omission means arity zero; write `Name<?>` to include generic candidates.

`void` never has generic parameters: any generic clause on `void` (`void<>`, `void<..>`, `void<!>`, `void<?>`) is rejected with a binding diagnostic.

```ebnf
generic-pattern-list =
    "<", ( "!" | ".." | "?" | generic-slot, { ",", generic-slot } ), ">" ;

generic-slot =
    [ type-pattern ] ;
```

An empty wildcard list `<>` represents arity one; an all-comma list has arity equal to its comma count plus one; a list with at least one typed argument has arity equal to its slot count, and each empty slot is an any-type argument occupying one position.

The zero-arity form is recognized only when `!` is followed immediately by the closing token (ignoring whitespace inside the clause). `Box<!>` is zero arity; `Box<!int>` is one generic argument with the Boolean pattern `!int`. First and later argument slots have the same type-pattern grammar.

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

A variable is declared by a positive occurrence in a type or method generic clause. Its first evaluation binds it; later references must be type-equal under the active `ITypeSystem`. Within a member signature, the declaring-type and method generic clauses are bound and evaluated before return/property types and parameters, regardless of textual order.

Rules:

- Ordinary `T` or `Result` is a type name, never an implicit variable.
- Return/property types and method/constructor parameter types can only reference variables already declared in an enclosing type or method generic clause or the visible conjunction scope. Nested generic clauses in those type uses do not declare variables.
- A standalone `$T` cannot declare a variable. A generic declaration argument cannot negate a variable, even when that name is already visible: `method($T Service.M<!$T>())` and `method($T Service<$T>.M<!$T>())` are invalid. Concrete negated arguments such as `Box<!int>` remain valid.
- A return or parameter reference may be negated: `method(!$T Service.M<$T>())` is valid. Negating a whole type pattern can reference an existing variable, as in `Box<$T> && !Box<$T>`.
- Conjunction shares a binding environment.
- Each OR branch evaluates with an isolated copy.
- Matched and unknown binding alternatives survive OR and inheritance/attribute enumeration; unknown alternatives retain their uncertainty through later conjunctions. Equivalent alternatives may be coalesced without adding a resource budget or changing match status.
- Bindings created inside negation do not escape it.
- A variable may not be introduced for the first time under negation.
- Bindings are not returned in public `MatchResult`.

Variables may bind formal generic parameters or constructed concrete types.

## 14. Attributes

Attribute selectors use short position names:

```ebnf
attribute-selector =
    "attr", "(", attribute-position, type-pattern, ")" ;

attribute-position =
      "type"
    | "call"
    | "prop"
    | "para", "[", ( integer-literal | "*" ), "]"
    | "para", ( integer-literal | "*" )
    | "ret"
    | "exec"
    | "any"
    | "*" ;
```

```text
attr(type AttributeType)
attr(call AttributeType)
attr(prop AttributeType)
attr(para[0] AttributeType)
attr(para[*] AttributeType)
attr(ret AttributeType)
attr(exec AttributeType)
attr(any AttributeType)
```

Semantics:

- `type`: direct attributes on the declaring type.
- `call`: direct attributes on the current method — for an accessor candidate this is the accessor method itself.
- `prop`: direct attributes on the associated property when the candidate is a property accessor. A candidate that is not an accessor definitively has no property location and returns `NotMatched`; an accessor whose property link is missing from backend metadata returns `Unknown`.
- `para[n]` or `para n`: direct attributes on parameter `n`.
- `para[*]` or `para *`: direct attributes on any parameter.
- `ret`: direct attributes on the return location.
- `exec`: direct attributes on the current callable, plus direct attributes on the associated property when the candidate is a property accessor.
- `any` or `*`: any supported direct location.

There is no `get`/`set` attribute position: accessor-scoped attributes are expressed compositionally, because `call` on an accessor candidate already denotes that accessor's own attributes:

```text
getter(string Name) && attr(call ObsoleteAttribute)
```

The earlier long position names `callable`, `property`, `parameter`, and `return` are rejected with a migration diagnostic (AN4005) naming the short replacement.

Attribute matching does not instantiate attribute objects. Inherited-attribute semantics are outside Standard 1.0.

`attr` uses CustomAttribute records. Metadata flags and other metadata table encodings are not attribute records, even when Reflection exposes them as pseudo attributes or source uses attribute notation. This excludes `Serializable`, `ComImport`, `StructLayout`, `DllImport`, `PreserveSig`, `MethodImpl`, `In`, `Out`, `Optional`, `MarshalAs` and `DefaultParameterValue` encodings. A real PE CustomAttribute blob of one of these types remains visible, including when the same metadata also causes a pseudo attribute to be synthesized. Ordinary marker attributes and real readonly marker records remain visible. Array declaring types do not inherit attributes from their element type.

## 15. Regex

```ebnf
regex-selector = "regex", "(", regex-body, ")" ;
```

`regex-body` preserves its original characters. The closing delimiter is the unmatched outer `)` after honoring escape atoms, ordinary character classes and nested groups. The supported regex subset is:

- Literals, character escapes, `.`, character categories such as `\d`, `\w`, `\s` and `\p{L}`, and their complements.
- Ordinary character classes, ranges and negation, such as `[a-z]` and `[^\s]`. Literal `[` and `]` inside a class must be escaped as `\[` and `\]`.
- Ordinary groups `(...)`, noncapturing groups `(?:...)`, alternation `|`, anchors, greedy and lazy quantifiers `*`, `+`, `?`, `{m}`, `{m,}`, `{m,n}`.

All other `(?...)` group forms are rejected: comments, inline or scoped options (including `(?x)` and `(?i)`), named or balancing groups, conditionals, lookarounds and atomic groups. Backreferences (`\1` through `\9` and longer numeric forms, or `\k<name>` / `\k'name'`) and nested or subtracted character classes are also rejected. Numeric escapes beginning with `1` through `9` outside a class are always rejected, including ambiguous octal forms; use `\xNN` or `\uNNNN` for literal character codes. Initial unescaped `]` in a class and the `[[]` literal-bracket convention are not supported. Whitespace and `#` always remain literal characters.

Unsupported constructs produce a compile-time `AN2012` error (`UnsupportedRegexFeature`); escaped or class-contained literal text resembling a construct is accepted. For example, `regex(\(\?x\))` matches the literal text `(?x)`. After subset validation, .NET validates regex syntax: an invalid pattern produces a compile-time `AN3002` error (`InvalidRegex`). Neither error produces a compiled member pattern. Authors should express complex selection logic with structured selectors and `&&`, `||`, `!`.

Regex uses culture-invariant `Regex.IsMatch` substring search on one canonical member signature. Use `^...$` for a full-signature match:

```text
<accessibility> [static] <return-type> <declaring-type>.<member-name><generic-part>(<parameters>)
```

The compiler `async` flag is not part of the canonical signature; return shapes are matched with `tasklike`. Ref kinds are rendered before affected parameter or return types. Nested types use `/`. Type names are fully qualified except generic formal parameters in definition signatures, which are renamed positionally to `T1..TN`: declaring-type parameters first (outermost type, left to right, then nested levels), followed by method parameters. Every occurrence of one parameter renders the same name, in the declaring-type clause, the method generic clause, and use positions alike. Constructed signatures render the actual type arguments.

For an open constructed declaring type, recursively visit its actual arguments from left to right, including named generic arguments and array elements. Assign one position to each distinct type-parameter declaration in first-occurrence order, then append method parameters. For example, `Container<List<T>>` retains `T` inside `List<T>`, while `Pair<T,T>` uses the same name for both occurrences. With an additional method parameter the names become `T1` and `T2`. Closed arguments do not consume parameter positions.

Positional numbering is deliberate: patterns stay valid when source renames a generic parameter, and authors only need the arity of each type and the method. When the total parameter count (type plus method) is exactly one, the sole parameter renders as bare `T`; otherwise parameters are numbered `T1..TN`. Bare `T` and `T` followed by digits are reserved for this encoding — a concrete type whose fully qualified name is exactly such a token (only possible in the global namespace) is not distinguishable from a positional parameter by regex; use a structured selector for such members.

Regex evaluation has a mandatory finite timeout. Timeout produces `Unknown` with `AN3003`. Ordinary quantifiers can still cause expensive backtracking; restricting syntax does not remove the timeout requirement.

## 16. Closed Semantic Contract

Custom selectors, predicates and matching semantics are not supported. Selector names are the built-in names in section 6 and are case-sensitive. Both unqualified unknown names and qualified names such as `vendor.feature(...)` produce `AN2001` (`UnknownSelector`) during parsing. Recovery retains consumed tokens and continues at the next outer expression boundary; it does not interpret custom arguments.

The engine and compilation APIs have no extension registry parameter. The former extension diagnostic `AN2008` is retired and is not reassigned.

## 17. Diagnostics

Standard diagnostics include stable categories for:

- Lexical errors.
- Missing or unexpected tokens.
- Unknown selector or predicate.
- Invalid target domain.
- Invalid modifier combinations.
- Invalid ref kind.
- Invalid generic clause (including any generic clause on `void`).
- Duplicate or illegally scoped variables.
- Invalid parameter index outside the Int32 range.
- Missing backend capability.
- Resolution failure.
- Unsupported regex features (AN2012) and invalid regex syntax (AN3002), both compile-time errors.
- Regex timeout.

Migration diagnostics include an actionable replacement where one exists:

```text
privateprotected -> private protected
protectedinternal -> protected internal
tasklike<void> -> tasklike
attr(callable ...) -> attr(call ...)
attr(property ...) -> attr(prop ...)
attr(parameter ...) -> attr(para ...)
attr(return ...) -> attr(ret ...)
```

## 18. Conformance

The existing Standard regression suites and cross-adapter cases define conformance. The solution has no oracle dependency. AN4003, AN4004 and AN4005 remain migration diagnostics; the removed selector-mode diagnostic AN2002 is not reassigned.

## 19. Standard Examples

```text
method(public static string MyCompany.Services.UserService.Get*(..))

callable(!public * *..*.*(..))

execution(public string MyCompany.Services.*.*(..))

property(public string MyCompany.Models.User.Name)

getter(* *..*.CreatedAt)

setter(* *..*.UpdatedAt)

ctor(MyCompany.Services.UserService(..))

cctor(MyCompany.Services.UserService)

method(tasklike<string> *..*.*Async(..))

method($T Converter.Convert<$T>($T))

method(* Service.Copy(ref int, out string))

method((int, string) Service.GetPair())

attr(para[*] System.ComponentModel.DataAnnotations.RequiredAttribute)

method(* Service.*(..)) && !attr(call ObsoleteAttribute)
```

## 20. Design Rationale and Migration

The [design decisions](../../language/standard-design-decisions.md) explain the name model, generic arity, wildcard family, ref kinds and structural suffix rules. The [migration guide](../../migration/shared-cecil-to-aspectn.md) describes the breaking transition from Shared.Cecil.AspectN and the removed public APIs.

## 21. Implementation limits and diagnostic presentation

`AspectNLimits` exposes a maximum type-structure depth of 64 and a maximum generic arity of 64. The root type counts as one level; each array, pointer, native wrapper, constructed argument or containing type adds a structural level. Generic-parameter declaration owners, original definitions and custom-modifier type identities are checked independently as identity contexts, without adding a structural level or following identity cycles repeatedly. Type patterns deeper than 64 levels report AN1002 before binding. Parsing also retains safeguards of 128 recursive entries and 256 syntax-tree nodes in depth; exceeding either reports AN1002.

A named type's effective generic arity includes all parameters across its containing-type chain. A method's own generic arity is limited independently: a type with 64 parameters may declare a method with another 64. Both declared arity and supplied argument counts are checked. Explicit generic clauses with more than 64 slots, including the sum across a nested type path, report AN2011.

Adapters reject native signatures exceeding these limits with `AspectNLimitException`; its `Diagnostic` reports AN3005 for depth or AN3006 for generic arity. Checks precede adapter cache keys, recursive ref classification, canonical owner text and positional generic-argument synthesis. Arity checks use the remaining capacity before addition, preventing integer overflow and excessive allocation. A manually constructed neutral signature exceeding either limit produces Unknown with the same diagnostic before matching, including wildcard and regex matching.

Relationship queries that encounter an over-limit ancestor retain that diagnostic and continue other branches. A known matching branch produces Matched; when no known branch matches, the incomplete relationship produces Unknown/AN3005 or Unknown/AN3006.

Adapters map native array, constructed-generic and containing-type graphs with explicit work stacks. Canonical native type text and legacy Reflection function-pointer ambiguity checks also traverse shapes iteratively. Reflection caches keep native dependencies alive during one mapping operation without retaining them permanently. These are implementation robustness limits and apply to every adapter, including Cecil.Fody.

Public diagnostic collections are immutable snapshots. Exact duplicates with the same code, severity, span and message are removed. Ordering is by span start (unlocated diagnostics last), ordinal code, severity, span length, then ordinal message. Distinct messages at the same code/span remain distinct.

Regex generic names require declaration identity and positional slots. An incomplete externally constructed signature that cannot supply them produces Unknown/AN3001; the formatter never substitutes source parameter names.

Canonical regex signatures are limited to 65,536 UTF-16 characters. The formatter checks this cumulative budget before appending, including repeated shared-type subgraphs and bulk array-rank punctuation. Exceeding the budget produces Unknown/AN3001 with a formatting-limit diagnostic. Formatting observes the match cancellation token; the regex timeout starts only after formatting.

The generic-arity limit is checked before synthesizing positional arguments. The cumulative canonical-text budget remains separate: a supported signature can still produce Unknown/AN3001 when its formatted text exceeds 65,536 characters.
