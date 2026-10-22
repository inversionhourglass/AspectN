using AspectN.TypeSystem;

namespace AspectN.Metadata;

internal sealed class BudgetedGenericParameterKeyComparer(TypeSystemQueryContext context) : IEqualityComparer<GenericParameterKey>
{
    public bool Equals(GenericParameterKey x, GenericParameterKey y) => x.Equals(y, context);
    public int GetHashCode(GenericParameterKey obj) => obj.GetHashCode(context);
}
