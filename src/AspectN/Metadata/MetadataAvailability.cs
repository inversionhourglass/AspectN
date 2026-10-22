using System.Collections.ObjectModel;
using AspectN.Diagnostics;

namespace AspectN.Metadata;

/// <summary>Identifies independently readable facts in member and type-use signatures.</summary>
[Flags]
public enum MetadataFacts
{
    /// <summary>No metadata facts are selected.</summary>
    None = 0,
    /// <summary>The member's kind classification.</summary>
    Kind = 1 << 0,
    /// <summary>The member's accessibility.</summary>
    Accessibility = 1 << 1,
    /// <summary>Whether the member is static.</summary>
    IsStatic = 1 << 2,
    /// <summary>Whether the method is classified as async.</summary>
    IsAsync = 1 << 3,
    /// <summary>The direct custom-attribute records on the target.</summary>
    Attributes = 1 << 4,
    /// <summary>The direct custom-attribute records on the return value.</summary>
    ReturnAttributes = 1 << 5,
    /// <summary>The direct custom-attribute records on the declaring type.</summary>
    TypeAttributes = 1 << 6,
    /// <summary>The property associated with an accessor, including a known absence.</summary>
    AssociatedProperty = 1 << 7,
    /// <summary>The exact ref, out, in, or ref readonly classification.</summary>
    RefKind = 1 << 8,
    /// <summary>The name stored in the signature.</summary>
    Name = 1 << 9,
    /// <summary>Whether a parameter is optional.</summary>
    IsOptional = 1 << 10,
    /// <summary>Whether a by-reference marker is present, independently of the exact ref classification.</summary>
    ByReference = 1 << 11,
    /// <summary>The callable's complete parameter list.</summary>
    Parameters = 1 << 12
}

/// <summary>Records unavailable facts and the diagnostics explaining their absence.</summary>
public sealed class MetadataAvailability
{
    private readonly IReadOnlyList<AspectNDiagnostic> _diagnostics;

    /// <summary>Creates an immutable availability snapshot.</summary>
    /// <param name="unavailableFacts">Facts whose placeholder values must not be used for matching.</param>
    /// <param name="diagnostics">Diagnostics describing why those facts could not be read.</param>
    public MetadataAvailability(
        MetadataFacts unavailableFacts = MetadataFacts.None,
        IEnumerable<AspectNDiagnostic>? diagnostics = null)
    {
        const MetadataFacts ALL_FACTS = MetadataFacts.Kind | MetadataFacts.Accessibility |
            MetadataFacts.IsStatic | MetadataFacts.IsAsync | MetadataFacts.Attributes |
            MetadataFacts.ReturnAttributes | MetadataFacts.TypeAttributes | MetadataFacts.AssociatedProperty |
            MetadataFacts.RefKind | MetadataFacts.Name | MetadataFacts.IsOptional | MetadataFacts.ByReference |
            MetadataFacts.Parameters;
        if ((unavailableFacts & ~ALL_FACTS) != 0)
            throw new ArgumentOutOfRangeException(nameof(unavailableFacts));
        UnavailableFacts = unavailableFacts;
        _diagnostics = new ReadOnlyCollection<AspectNDiagnostic>(
            DiagnosticSnapshot.Create(diagnostics));
    }

    /// <summary>Gets the shared snapshot used when all signature facts are readable.</summary>
    public static MetadataAvailability Complete { get; } = new();

    /// <summary>Gets facts whose stored values are placeholders.</summary>
    public MetadataFacts UnavailableFacts { get; }

    /// <summary>Gets the diagnostics associated with unavailable metadata.</summary>
    public IReadOnlyList<AspectNDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Determines whether every requested fact is readable.</summary>
    /// <param name="facts">Facts required by a query.</param>
    /// <returns>True if all requested facts are readable.</returns>
    public bool IsKnown(MetadataFacts facts) => (UnavailableFacts & facts) == 0;
}
