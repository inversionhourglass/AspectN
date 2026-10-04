# Array shape

Approved on 2026-10-05, `ArrayTypeSignature` preserves array rank, element type and CLI `ArrayShape`: `Vector` is an SZ rank-one array; `General` includes non-SZ rank-one and multidimensional arrays. This does not model individual instance lengths or lower bounds. The existing `(elementType, rank)` constructor selects Vector for rank one and General otherwise. The explicit shape constructor rejects a vector with any other rank and undefined shape values.

Cecil maps `ArrayType.IsVector`, Reflection maps its runtime SZ fact, and Roslyn maps `IArrayTypeSymbol.IsSZArray`. Structural equality compares shape recursively, including array elements and generic arguments. Generic parameter owner identifiers retain `[*]` for general rank-one parameter arrays so overloads differing only by array shape do not collapse.

Array-to-array nominal queries work for detached signatures as well as mapped signatures:

| Candidate | Target | Result |
| --- | --- | --- |
| int[] | int[] | Matched |
| int[*] | int[*] | Matched |
| int[] | int[*] | Matched |
| int[*] | int[] | NotMatched |
| string[,] | object[,] | NotMatched |

Rank must agree and element identity must agree. Element identity includes nested shape; array element covariance and generic variance remain outside the nominal relationship contract. A known shape or rank mismatch wins without needing element resolution. Unknown element identity propagates diagnostics when it is needed. Relationships to named bases and interfaces still use the adapter's context: only vectors provide generic array collection interfaces.

Standard type patterns remain rank based: `int[]` matches both rank-one shapes. No `int[*]` syntax is added to the DSL. Canonical regex signature text retains the existing rank-based rendering, so regex is not a new shape selector. Metadata owner identity and conformance snapshots retain shape independently of that presentation.

This rank rule also applies inside named generic patterns, including attribute patterns and `+` relationship targets. A pattern containing rank-one `[]` cannot be optimized into one concrete Vector identity; these paths evaluate the pattern against embedded attributes or the declared inheritance closure. Public `AreEqual` and `IsAssignableTo` still use explicit metadata identity and shape.
