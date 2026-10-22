using AspectN.Metadata;

namespace AspectN.TypeSystem;

internal sealed class BudgetedTypeSystem(ITypeSystem backend, TypeSystemQueryContext context) : ITypeSystem
{
    public TypeSystemCapabilities Capabilities => backend.Capabilities;

    public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default)
    {
        context.SpendWork();
        return Complete(backend is IBudgetAwareTypeSystem builtIn ? builtIn.AreEqual(left, right, context) :
            backend.AreEqual(left, right, context.CancellationToken));
    }

    public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default)
    {
        context.SpendWork();
        return Complete(backend is IBudgetAwareTypeSystem builtIn ? builtIn.IsAssignableTo(candidate, target, context) :
            backend.IsAssignableTo(candidate, target, context.CancellationToken));
    }

    public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default)
    {
        context.SpendWork();
        var closure = backend is IBudgetAwareTypeSystem builtIn ? builtIn.GetInheritanceClosure(candidate, context) :
            backend.GetInheritanceClosure(candidate, context.CancellationToken);
        if (closure.Failure is not null) Complete(closure.Failure);
        context.ThrowIfExhausted();
        return closure;
    }

    public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default)
    {
        context.SpendWork();
        return Complete(backend is IBudgetAwareTypeSystem builtIn ? builtIn.HasDirectAttribute(target, attributeType, context) :
            backend.HasDirectAttribute(target, attributeType, context.CancellationToken));
    }

    private TypeRelationResult Complete(TypeRelationResult result)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        foreach (var diagnostic in result.Diagnostics) context.ReportResourceLimit(diagnostic);
        context.ThrowIfExhausted();
        return result;
    }
}
