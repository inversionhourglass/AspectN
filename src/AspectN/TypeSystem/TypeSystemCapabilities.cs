namespace AspectN.TypeSystem;

/// <summary>Advertises backend operations; individual queries may still return Unknown for unavailable metadata.</summary>
[Flags]
public enum TypeSystemCapabilities
{
    None = 0,
    TypeEquality = 1 << 0,
    BaseTypeTraversal = 1 << 1,
    InterfaceTraversal = 1 << 2,
    Assignability = 1 << 3,
    GenericSubstitution = 1 << 4,
    DirectAttributeLookup = 1 << 5,
    OriginalDefinitionMapping = 1 << 6,
    All = TypeEquality |
          BaseTypeTraversal |
          InterfaceTraversal |
          Assignability |
          GenericSubstitution |
          DirectAttributeLookup |
          OriginalDefinitionMapping
}
