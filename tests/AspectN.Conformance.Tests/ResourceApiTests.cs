using AspectN.Cecil;
using AspectN.Reflection;
using AspectN.Roslyn;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class ResourceApiTests
{
    [Theory]
    [InlineData(typeof(ReflectionSignatureMapper))]
    [InlineData(typeof(RoslynSignatureMapper))]
    public void Mapper_Constructor_ExposesImmutableMappingLimits(Type mapper)
    {
        Assert.Contains(mapper.GetConstructors(), constructor =>
            constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(AspectNMappingOptions)));
    }

    [Theory]
    [InlineData(typeof(ReflectionSignatureMapper))]
    [InlineData(typeof(RoslynSignatureMapper))]
    [InlineData(typeof(CecilSignatureMapper))]
    public void Mapper_PublicEntries_ExposeOrdinaryCancellation(Type mapper)
    {
        var methods = mapper.GetMethods().Where(method => method.Name is "MapType" or "MapTypeUse" or "MapMethod" or "MapProperty").ToArray();
        Assert.NotEmpty(methods);
        Assert.All(methods, method => Assert.Equal(typeof(CancellationToken), method.GetParameters().Last().ParameterType));
    }

    [Fact]
    public void CecilOptions_Resources_ExposeMappingOptions()
    {
        Assert.Equal(typeof(AspectNMappingOptions), typeof(CecilMappingOptions).GetProperty("Resources")?.PropertyType);
    }
}
