using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Reflection;

/// <summary>Queries native metadata using isolated budgets; compiled matching shares its own match budget across internal queries.</summary>
/// <remarks>Immutable options can be reused concurrently; instance use also requires the native metadata objects to satisfy their concurrency requirements. Each direct query starts fresh. Mapping performed inside a query shares one mapping sub-budget and consumes the query budget. Third-party wrappers use the ordinary public boundary.</remarks>
/// <example><code>
/// var backend = new ReflectionTypeSystem(queryOptions:
///     new AspectNMatchOptions(maximumWork: ResourceLimit.Finite(1)));
/// var candidate = backend.Mapper.MapType(typeof(string));
/// var direct = backend.AreEqual(candidate, candidate); // Unknown, AN3008
/// var compilation = new AspectNEngine().CompileTypePattern("IComparable+");
/// if (compilation.TryGetTypePattern(out var pattern))
/// {
///     var result = pattern.Match(candidate, backend); // Matched, compiled defaults
/// }
/// </code>Repeated direct queries start fresh; multiple internal queries within a single Match share one budget. Changing standalone query defaults does not override a compiled plan.</example>
public sealed class ReflectionTypeSystem : ITypeSystem, IBudgetAwareTypeSystem
{
    /// <summary>Creates a reusable backend with independent mapping and standalone-query limits.</summary>
    /// <param name="mappingOptions">Native mapping limits; null uses mapping defaults.</param>
    /// <param name="queryOptions">Limits for each direct public query; null uses match defaults. Compiled matching uses the compiled plan options instead.</param>
    public ReflectionTypeSystem(AspectNMappingOptions? mappingOptions = null, AspectNMatchOptions? queryOptions = null)
    {
        Mapper = new ReflectionSignatureMapper(mappingOptions);
        QueryOptions = queryOptions ?? new AspectNMatchOptions();
    }

    /// <inheritdoc cref="ITypeSystem.Capabilities"/>
    public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;

    /// <summary>Gets the mapper retaining native associations and immutable mapping limits.</summary>
    public ReflectionSignatureMapper Mapper { get; }

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
            var result = new ReflectionQueryOperation(this, context).AreEqual(left, right, context.CancellationToken);
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
            var result = new ReflectionQueryOperation(this, context).IsAssignableTo(candidate, target, context.CancellationToken);
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
            var result = new ReflectionQueryOperation(this, context).GetInheritanceClosure(candidate, context.CancellationToken);
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
            var result = new ReflectionQueryOperation(this, context).HasDirectAttribute(target, attributeType, context.CancellationToken);
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
