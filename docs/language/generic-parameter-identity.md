# Generic parameter identity

A generic parameter's neutral identity is its owner kind, declaration owner ID,
owner assembly and position. Closing a declaring type does not change the
declaration identity of a method's generic parameter.

For type parameters, position is the flattened CLI slot across the containing
type chain, while owner ID identifies the level that declares the parameter.
For `Outer<T1,T2>.Inner<U>`, `U` belongs to `Inner` and has position 2.
Method parameter positions are local to the method and start at zero.

Method owner IDs include the declaration type, method name, generic arity and
recursive CLI parameter shapes. Each named parameter type includes its assembly
name, version, culture and public key token, including names inside arrays,
constructed generic arguments and function pointers. The shared adapter encoder
uses length-prefixed assembly fields to avoid delimiter collisions. Cecil uses
resolved declarations when available, including forwarded named types, and
retains the available reference identity when resolution fails.

Cecil verifies the complete parameter identity of a resolved method before using
its declaration. Declaration generic slots remain distinct even when closing a
type makes two overloads' parameter types identical. References through derived
types can resolve inherited declarations. For vararg call sites, only the fixed
parameters before the sentinel participate in declaration lookup; optional call
arguments do not appear in the declaration signature.

Cecil's cache separately retains the closed declaring-type context. Parameters
from `Owner<string>.M<V>` and `Owner<int>.M<V>` compare as the same declaration,
while `where V : T` still resolves constraints using the corresponding actual
type. The owner ID is metadata identity; it does not change regex or diagnostic
type rendering.

The cross-adapter regression scenarios are in
`tests/AspectN.Conformance.Tests/MethodGenericOwnerIdentityTests.cs`.
