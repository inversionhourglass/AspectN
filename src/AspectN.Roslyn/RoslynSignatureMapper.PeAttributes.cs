using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using AspectN.Diagnostics;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn;

public sealed partial class RoslynSignatureMapper
{
    private AttributeSignature[] MapPeReturnAttributes(IMethodSymbol method, MetadataReader reader,
        AttributeSignature[] attributes, out MetadataAvailability availability)
    {
        availability = MetadataAvailability.Complete;
        var handle = MetadataTokens.Handle(method.MetadataToken);
        if (handle.Kind != HandleKind.MethodDefinition)
            throw new BadImageFormatException("A PE method must have a MethodDef token.");
        var result = attributes.ToList();
        foreach (var parameterHandle in reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetParameters())
        {
            var parameter = reader.GetParameter(parameterHandle);
            if (parameter.SequenceNumber != 0) continue;
            foreach (var attributeHandle in parameter.GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(attributeHandle);
                var (typeHandle, signature, name) = GetPeConstructor(reader, attribute.Constructor);
                if (reader.GetString(name) != ".ctor" || !IsParameterlessInstanceConstructor(reader, signature)) continue;
                var symbol = ResolvePeReadonlyMarker(method, reader, typeHandle, out var isMarker);
                if (!isMarker) continue;
                if (symbol is null)
                {
                    availability = ReturnAttributeUnavailable(DiagnosticCodes.MissingBackendCapability,
                        "The PE readonly return attribute declaration could not be resolved.");
                    continue;
                }

                var type = MapType(symbol);
                // 只按真实类型身份去重；读者或其他依赖的同名类型不能覆盖原始记录。
                if (result.Any(item => item.AttributeType is NamedTypeSignature existing && type is NamedTypeSignature marker &&
                    existing.Identity.FullMetadataName == marker.Identity.FullMetadataName &&
                    existing.Identity.Assembly.Equals(marker.Identity.Assembly))) continue;
                result.Add(new AttributeSignature(type, AttributeTargetKind.Return));
            }
        }
        return result.ToArray();
    }

    private static (EntityHandle Type, BlobHandle Signature, StringHandle Name) GetPeConstructor(
        MetadataReader reader, EntityHandle constructor)
    {
        if (constructor.Kind == HandleKind.MethodDefinition)
        {
            var method = reader.GetMethodDefinition((MethodDefinitionHandle)constructor);
            return (method.GetDeclaringType(), method.Signature, method.Name);
        }
        if (constructor.Kind == HandleKind.MemberReference)
        {
            var member = reader.GetMemberReference((MemberReferenceHandle)constructor);
            return (member.Parent, member.Signature, member.Name);
        }
        throw new BadImageFormatException("A custom attribute constructor must be a method definition or reference.");
    }

    private static bool IsParameterlessInstanceConstructor(MetadataReader reader, BlobHandle signature)
    {
        var blob = reader.GetBlobReader(signature);
        var header = blob.ReadSignatureHeader();
        return header.Kind == SignatureKind.Method && header.IsInstance && !header.IsGeneric &&
            blob.ReadCompressedInteger() == 0;
    }

    private static INamedTypeSymbol? ResolvePeReadonlyMarker(IMethodSymbol method, MetadataReader reader,
        EntityHandle handle, out bool isMarker)
    {
        const string markerNamespace = "System.Runtime.CompilerServices";
        const string markerName = "IsReadOnlyAttribute";
        const string fullName = markerNamespace + "." + markerName;
        isMarker = false;
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            var type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            isMarker = type.GetDeclaringType().IsNil && reader.GetString(type.Namespace) == markerNamespace &&
                reader.GetString(type.Name) == markerName;
            return isMarker ? method.ContainingAssembly.GetTypeByMetadataName(fullName) : null;
        }
        if (handle.Kind != HandleKind.TypeReference) return null;
        var reference = reader.GetTypeReference((TypeReferenceHandle)handle);
        isMarker = reader.GetString(reference.Namespace) == markerNamespace &&
            reader.GetString(reference.Name) == markerName && reference.ResolutionScope.Kind != HandleKind.TypeReference;
        if (!isMarker) return null;
        if (reference.ResolutionScope.Kind is HandleKind.ModuleDefinition or HandleKind.ModuleReference)
            return method.ContainingAssembly.GetTypeByMetadataName(fullName);
        if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;

        // CA构造器的AssemblyRef选择准确依赖；不从读取Compilation按全名寻找候选。
        var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope);
        var identity = new Microsoft.CodeAnalysis.AssemblyIdentity(reader.GetString(assembly.Name), assembly.Version,
            assembly.Culture.IsNil ? null : reader.GetString(assembly.Culture),
            ImmutableArray.CreateRange(reader.GetBlobBytes(assembly.PublicKeyOrToken)),
            hasPublicKey: (assembly.Flags & AssemblyFlags.PublicKey) != 0,
            isRetargetable: (assembly.Flags & AssemblyFlags.Retargetable) != 0,
            contentType: (assembly.Flags & AssemblyFlags.WindowsRuntime) != 0
                ? AssemblyContentType.WindowsRuntime : AssemblyContentType.Default);
        var module = method.ContainingModule;
        for (var index = 0; index < module.ReferencedAssemblies.Length; index++)
        {
            if (!module.ReferencedAssemblies[index].Equals(identity)) continue;
            var declaration = module.ReferencedAssemblySymbols[index];
            return declaration.GetTypeByMetadataName(fullName) ?? declaration.ResolveForwardedType(fullName);
        }
        return null;
    }

    private static MetadataAvailability ReturnAttributeUnavailable(string code, string message) =>
        new(MetadataFacts.ReturnAttributes, [new AspectNDiagnostic(code, AspectN.Diagnostics.DiagnosticSeverity.Warning, message)]);
}
