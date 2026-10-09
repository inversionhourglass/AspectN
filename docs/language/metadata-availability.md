# Metadata availability

The neutral signatures expose immutable `MetadataAvailability`. `UnavailableFacts` identifies fields whose stored values are placeholders; `Diagnostics` explains reading failures. A manually constructed signature defaults to `MetadataAvailability.Complete`, meaning its supplied values and empty collections are known. Explicit `Accessibility.Unknown` remains unknown even with that default.

Method and property parameter collections are snapshots in declaration order. Each `ParameterSignature.Position` must equal its zero-based list index. Constructors reject null entries, reordered positions, duplicates and gaps with `ArgumentException`; they do not reorder supplied metadata.

| Signature | Applicable facts |
| --- | --- |
| MethodSignature | Name, Kind, Accessibility, IsStatic, IsAsync, Attributes, ReturnAttributes, TypeAttributes, AssociatedProperty |
| PropertySignature | Name, Accessibility, IsStatic, Attributes, Parameters |
| ParameterSignature | Name, IsOptional, Attributes |
| TypeUseSignature | RefKind, ByReference |

Type shape, names and parameter/generic arity present in a Cecil reference remain usable without resolving its definition. An unresolved method definition makes its method kind, visibility, async marker, direct attributes, return attributes and associated property unavailable. Declaring-type attributes are resolved independently. Parameter definition metadata is also marked unavailable. Consumers must check `Availability.IsKnown(...)` before interpreting placeholders; an empty unavailable attribute list does not prove absence.

By-reference shape is independent of ref classification: a Cecil signature can retain a known by-reference marker while the distinction between `ref`, `out`, `in` and `ref readonly` is unknown. `TypeUseSignature.IsByReference` is usable when `ByReference` is known. `!ref` uses this marker; exact ref modifiers require the classification as well. `CecilSignatureMapper.MapTypeUse` without member definition metadata preserves the marker but does not guess exact classification.

Evaluation asks for only the facts consumed by a selector. For an unresolved `External.Widget.Run` reference:

- `execution(* External.Widget.Run(..))` can match because the selector accepts every member kind.
- `method(* External.Widget.Run(..))` is unknown because the ordinary-method domain cannot be established.
- Positive and negated visibility constraints are both unknown when visibility is unavailable.
- A known different member name, declaring type, return type or parameter count still gives `NotMatched` in a conjunction with unknown facts.
- Exact and wildcard attribute selectors respect collection availability. For `any`/`exec`, a known matching attribute wins over another unknown target, while an unreadable property association cannot prove absence.
- Regex needs a complete canonical signature (name, visibility, static flag and return/parameter ref classification), so it reports unknown when those facts are unavailable. It does not require async state or attribute collections.

All adapters honor availability on detached signatures passed to direct attribute APIs. Cecil converts recoverable resolution/read failures (assembly resolution, IO, bad image, unsupported metadata, invalid operation and invalid metadata arguments) to `AN3004`. `AN3002` remains invalid regex and `AN3003` remains regex timeout. Cancellation propagates. Unknown facts without a supplied reading diagnostic use the existing `AN3001` missing-capability diagnostic.

`Parameters` applies to property signatures: Cecil derives index parameters from accessor metadata, so damaged lazy method semantics can leave that collection unavailable, with an empty list as its placeholder. Standard property selectors do not consume that list. The property name, type and direct attributes remain usable. Diagnostic subjects use simple metadata names so formatting a message cannot itself trigger an unprotected lazy read. Resolved method references obtain parameter names from their definitions.

Relationship queries retain unreadable branches as Unknown while continuing other branches. A known match wins over an earlier Unknown; when no branch matches, the reading failure remains visible. Cecil protects lazy node expansion and cache-key reads as well as `Resolve()`. Array owners have their own empty direct attribute collection and do not inherit element attributes.

This contract, approved on 2026-10-05, supersedes the earlier unresolved-method limitation in the shared Standard comparison document. It does not change backend support for valid metadata forms that were already explicitly outside the adapter's classification coverage.
