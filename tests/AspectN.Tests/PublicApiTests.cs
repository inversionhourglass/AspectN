using System.Reflection;

namespace AspectN.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void CoreAssembly_DoesNotReferenceAdapterAssemblies()
    {
        var references = typeof(AspectNParser)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(references, name => name == "Mono.Cecil");
        Assert.DoesNotContain(
            references,
            name => name is not null &&
                    name.StartsWith(
                        "Microsoft.CodeAnalysis",
                        StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            name => name is
                "AspectN.Cecil" or
                "AspectN.Roslyn" or
                "AspectN.Reflection");
    }

    [Fact]
    public void CoreAssembly_PublicTypesDoNotExposeAdapterNamespaces()
    {
        var adapterPrefixes = new[]
        {
            "Mono.Cecil",
            "Microsoft.CodeAnalysis",
            "System.Reflection"
        };
        var publicTypes = typeof(AspectNParser)
            .Assembly
            .GetExportedTypes();

        Assert.All(
            publicTypes,
            type =>
            {
                Assert.DoesNotContain(
                    adapterPrefixes,
                    prefix => UsesNamespace(type.BaseType, prefix));
                var additionalTypes = type.GetInterfaces()
                    .Concat(type.GetFields().Select(field => field.FieldType))
                    .Concat(type.GetEvents().Select(@event => @event.EventHandlerType!))
                    .Concat(type.GetGenericArguments().Where(argument => argument.IsGenericParameter)
                        .SelectMany(argument => argument.GetGenericParameterConstraints()))
                    .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                        .Select(parameter => parameter.ParameterType))
                    .Concat(type.GetMethods().Where(method => method.IsGenericMethodDefinition)
                        .SelectMany(method => method.GetGenericArguments())
                        .SelectMany(argument => argument.GetGenericParameterConstraints()));
                Assert.All(additionalTypes, exposed => Assert.DoesNotContain(adapterPrefixes,
                    prefix => UsesNamespace(exposed, prefix)));
                Assert.All(
                    type.GetProperties(),
                    property => Assert.DoesNotContain(
                        adapterPrefixes,
                        prefix => UsesNamespace(
                            property.PropertyType,
                            prefix)));
                Assert.All(
                    type.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.DeclaredOnly),
                    method =>
                    {
                        Assert.DoesNotContain(
                            adapterPrefixes,
                            prefix => UsesNamespace(
                                method.ReturnType,
                                prefix));
                        Assert.All(
                            method.GetParameters(),
                            parameter => Assert.DoesNotContain(
                                adapterPrefixes,
                                prefix => UsesNamespace(
                                    parameter.ParameterType,
                                    prefix)));
                    });
            });
    }

    private static bool UsesNamespace(Type? type, string namespacePrefix)
    {
        if (type is null) return false;
        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            return UsesNamespace(type.GetElementType(), namespacePrefix);
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(argument =>
                UsesNamespace(argument, namespacePrefix)))
        {
            return true;
        }

        return type.Namespace?.StartsWith(
            namespacePrefix,
            StringComparison.Ordinal) == true;
    }
}
