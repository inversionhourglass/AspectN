using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

/// <summary>Queries native metadata using isolated budgets; compiled matching shares its own match budget across internal queries.</summary>
/// <remarks>Immutable options can be reused concurrently; instance use also requires the native metadata objects to satisfy their concurrency requirements. Each direct query starts fresh. Mapping performed inside a query shares one mapping sub-budget and consumes the query budget. Third-party wrappers use the ordinary public boundary.</remarks>
public sealed class RoslynTypeSystem : ITypeSystem, IBudgetAwareTypeSystem
{
    /// <summary>Creates a reusable backend with independent mapping and standalone-query limits.</summary>
    /// <param name="compilation">The compilation supplying native symbols.</param>
    /// <param name="mappingOptions">Native mapping limits; null uses mapping defaults.</param>
    /// <param name="queryOptions">Limits for each direct public query; null uses match defaults. Compiled matching uses the compiled plan options instead.</param>
    /// <exception cref="ArgumentNullException"><paramref name="compilation"/> is null.</exception>
    public RoslynTypeSystem(Compilation compilation, AspectNMappingOptions? mappingOptions = null, AspectNMatchOptions? queryOptions = null)
    {
        Compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
        Mapper = new RoslynSignatureMapper(compilation, mappingOptions);
        QueryOptions = queryOptions ?? new AspectNMatchOptions();
    }

    /// <summary>Gets the compilation providing native metadata.</summary>
    public Compilation Compilation { get; }

    /// <inheritdoc cref="ITypeSystem.Capabilities"/>
    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    /// <summary>Gets the mapper retaining native associations and immutable mapping limits.</summary>
    public RoslynSignatureMapper Mapper { get; }

    /// <summary>Gets immutable defaults for independent public queries, including 250,000 work units, 1,024 relation nodes, and 4,096 relation edges.</summary>
    /// <remarks>These defaults do not constrain internal queries made by a compiled match. Different public calls have separate counters; nested mapping consumes both its own limits and the active query limits.</remarks>
    public AspectNMatchOptions QueryOptions { get; }

    /// <inheritdoc cref="ITypeSystem.AreEqual"/>
    public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).AreEqual(left, right, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.AreEqual(TypeSignature left, TypeSignature right, TypeSystemQueryContext context)
    {
        try
        {
            context.ThrowIfExhausted();
            var result = new RoslynQueryOperation(this, context).AreEqual(left, right, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }

    /// <inheritdoc cref="ITypeSystem.IsAssignableTo"/>
    public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).IsAssignableTo(candidate, target, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.IsAssignableTo(TypeSignature candidate, TypeSignature target, TypeSystemQueryContext context)
    {
        try
        {
            context.ThrowIfExhausted();
            var result = new RoslynQueryOperation(this, context).IsAssignableTo(candidate, target, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }

    /// <inheritdoc cref="ITypeSystem.GetInheritanceClosure"/>
    public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).GetInheritanceClosure(candidate, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    InheritanceClosure IBudgetAwareTypeSystem.GetInheritanceClosure(TypeSignature candidate, TypeSystemQueryContext context)
    {
        try
        {
            context.ThrowIfExhausted();
            var result = new RoslynQueryOperation(this, context).GetInheritanceClosure(candidate, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return new InheritanceClosure(Array.Empty<TypeSignature>(), TypeRelationResult.Unknown(diagnostic));
        }
    }

    /// <inheritdoc cref="ITypeSystem.HasDirectAttribute"/>
    public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).HasDirectAttribute(target, attributeType, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, TypeSystemQueryContext context)
    {
        try
        {
            context.ThrowIfExhausted();
            var result = new RoslynQueryOperation(this, context).HasDirectAttribute(target, attributeType, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }

}
