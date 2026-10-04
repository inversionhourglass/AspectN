using AspectN.Diagnostics;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal static class CecilMetadataReader
{
    public static MetadataAvailability Merge(params MetadataAvailability[] sources)
    {
        var facts = sources.Aggregate(MetadataFacts.None, (current, source) => current | source.UnavailableFacts);
        return facts == MetadataFacts.None ? MetadataAvailability.Complete :
            new MetadataAvailability(facts, sources.SelectMany(source => source.Diagnostics).Distinct());
    }

    public static T ReadValue<T>(Func<T> read, T fallback, string subject, MetadataFacts facts,
        out MetadataAvailability availability)
    {
        try
        {
            var value = read();
            availability = MetadataAvailability.Complete;
            return value;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            availability = Failure(subject, facts, exception.Message);
            return fallback;
        }
    }

    public static bool IsRecoverable(Exception exception) => exception is
        AssemblyResolutionException or IOException or BadImageFormatException or
        InvalidOperationException or NotSupportedException or ArgumentException;

    public static T? Read<T>(Func<T?> read, string subject, MetadataFacts facts,
        out MetadataAvailability availability) where T : class
    {
        string message;
        try
        {
            var value = read();
            if (value is not null)
            {
                availability = MetadataAvailability.Complete;
                return value;
            }

            message = "The definition could not be resolved.";
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            message = exception.Message;
        }

        availability = Failure(subject, facts, message);
        return null;
    }

    private static MetadataAvailability Failure(string subject, MetadataFacts facts, string message)
    {
        return new MetadataAvailability(facts, new[]
        {
            new AspectNDiagnostic(DiagnosticCodes.ResolutionFailure, DiagnosticSeverity.Warning,
                $"Metadata for '{subject}' is unavailable: {message}")
        });
    }
}
