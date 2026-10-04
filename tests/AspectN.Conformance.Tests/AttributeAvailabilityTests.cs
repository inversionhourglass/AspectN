using AspectN.Cecil;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class AttributeAvailabilityTests
{
    [Fact]
    public void HasDirectAttribute_DetachedUnavailableTargets_AllAdaptersReturnUnknown()
    {
        using var module = ModuleDefinition.CreateModule("Availability", ModuleKind.Dll);
        using var cecil = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        ITypeSystem[] systems = { cecil, new ReflectionTypeSystem(), new RoslynTypeSystem(CSharpCompilation.Create("Availability")) };
        var type = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Service", 0, TypeKind.Class));
        var availability = new MetadataAvailability(MetadataFacts.Attributes | MetadataFacts.ReturnAttributes,
            new[] { new AspectNDiagnostic(DiagnosticCodes.ResolutionFailure, AspectN.Diagnostics.DiagnosticSeverity.Warning, "Unavailable fixture.") });
        var parameter = new ParameterSignature("value", 0, new TypeUseSignature(type, RefKind.None, TypeUseKind.Parameter),
            availability: availability);
        var property = new PropertySignature("Value", Accessibility.Public, false, type,
            new TypeUseSignature(type, RefKind.None, TypeUseKind.Property), Array.Empty<ParameterSignature>(),
            Array.Empty<AttributeSignature>(), availability);
        var method = new MethodSignature("Run", AspectN.Metadata.MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            type, new TypeUseSignature(type, RefKind.None, TypeUseKind.Return), new[] { parameter },
            Array.Empty<GenericParameterSignature>(), Array.Empty<TypeSignature>(), Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(), availability: availability);
        AttributeTarget[] targets = { AttributeTarget.ForMethod(method), AttributeTarget.ForReturn(method),
            AttributeTarget.ForProperty(property), AttributeTarget.ForParameter(parameter) };

        foreach (var system in systems)
            foreach (var target in targets)
            {
                var result = system.HasDirectAttribute(target, type);
                Assert.Equal(MatchStatus.Unknown, result.Status);
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
            }
    }
}
