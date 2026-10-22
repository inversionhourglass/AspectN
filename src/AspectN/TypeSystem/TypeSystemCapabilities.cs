namespace AspectN.TypeSystem;

/// <summary>Advertises backend operations; individual queries may still return Unknown for unavailable metadata.</summary>
[Flags]
public enum TypeSystemCapabilities
{
    /// <summary>No backend operations are advertised.</summary>
    None = 0,
    /// <summary>Comparison of type signatures for equality.</summary>
    TypeEquality = 1 << 0,
    /// <summary>Traversal of declared base-type relationships.</summary>
    BaseTypeTraversal = 1 << 1,
    /// <summary>Traversal of implemented-interface relationships.</summary>
    InterfaceTraversal = 1 << 2,
    /// <summary>Queries that determine whether one type is assignable to another.</summary>
    Assignability = 1 << 3,
    /// <summary>Substitution of type or method generic parameters in a constructed context.</summary>
    GenericSubstitution = 1 << 4,
    /// <summary>Lookup of custom attributes declared directly on a target.</summary>
    DirectAttributeLookup = 1 << 5,
    /// <summary>Mapping a constructed type or member to its original declaration.</summary>
    OriginalDefinitionMapping = 1 << 6,
    /// <summary>All operations currently defined by this enumeration.</summary>
    All = TypeEquality |
          BaseTypeTraversal |
          InterfaceTraversal |
          Assignability |
          GenericSubstitution |
          DirectAttributeLookup |
          OriginalDefinitionMapping
}
