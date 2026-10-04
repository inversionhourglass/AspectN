using System.Reflection;
using System.Text.Json;
using AspectN.Evaluation;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class CrossAdapterConformanceTests :
    IClassFixture<FixtureCompiler>
{
    private const string FIXTURE_TYPE =
        "AspectN.TestAssembly.Conformance.ConformanceFixture";
    private const string OUTER_TYPE =
        "AspectN.TestAssembly.Conformance.ConformanceOuter`1";

    private static readonly JsonSerializerOptions _JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FixtureCompiler _fixture;

    public CrossAdapterConformanceTests(FixtureCompiler fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<ConformanceCase> Cases
    {
        get
        {
            var json = File.ReadAllText(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "conformance-cases.json"));
            var cases = JsonSerializer.Deserialize<List<ConformanceCase>>(
                json,
                _JsonOptions) ?? throw new InvalidOperationException(
                    "Conformance cases could not be loaded.");
            var data = new TheoryData<ConformanceCase>();
            foreach (var @case in cases)
            {
                data.Add(@case);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void FixtureCase_ThreeBackends_ProduceEquivalentResults(
        ConformanceCase @case)
    {
        if (@case.Kind == "nested-type")
        {
            AssertTypeCase(@case);
            return;
        }

        AssertCallableCase(@case);
    }

    [Fact]
    public void UnresolvedExternalBase_MissingBackends_ReturnUnknown()
    {
        using var fixture = FixtureCompiler.CreateUnresolved();
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern("External.Base+"));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var roslynType = Assert.IsAssignableFrom<INamedTypeSymbol>(
            fixture.Compilation.GetTypeByMetadataName(FIXTURE_TYPE));
        var roslynMethod = Assert.Single(
            roslynType.GetMembers("Missing").OfType<IMethodSymbol>());
        var roslynCandidate = fixture.Roslyn.Mapper.MapType(
            Assert.Single(roslynMethod.Parameters).Type);
        var roslynResult = pattern!.Match(roslynCandidate, fixture.Roslyn);

        var cecilType = fixture.Cecil.Module.GetType(FIXTURE_TYPE) ??
            throw new InvalidOperationException(
                $"Cecil type '{FIXTURE_TYPE}' was not found.");
        var cecilMethod = Assert.Single(
            cecilType.Methods,
            method => method.Name == "Missing");
        var cecilCandidate = fixture.Cecil.Mapper.MapType(
            Assert.Single(cecilMethod.Parameters).ParameterType);
        var cecilResult = pattern.Match(cecilCandidate, fixture.Cecil);

        var reflectionCandidate = TryMapMissingReflectionParameter(
            fixture,
            cecilCandidate);
        var reflectionResult = pattern.Match(
            reflectionCandidate,
            fixture.Reflection);

        Assert.Equal(MatchStatus.Matched, roslynResult.Status);
        Assert.Equal(MatchStatus.Unknown, cecilResult.Status);
        Assert.NotEmpty(cecilResult.Diagnostics);
        Assert.Equal(MatchStatus.Unknown, reflectionResult.Status);
        Assert.NotEmpty(reflectionResult.Diagnostics);
    }

    [Theory]
    [InlineData("op_Fake")]
    [InlineData("op_Addition")]
    [InlineData("op_Implicit")]
    [InlineData("op_Unexpected")]
    [InlineData("op_Subtraction")]
    public void OrdinaryMethod_WithOperatorLikeName_RemainsInMethodDomain(string name)
    {
        AssertCallableCase(new ConformanceCase
        {
            Member = name,
            ParameterCount = 0,
            Expression = $"method(int {name}())",
            Expected = nameof(MatchStatus.Matched)
        });
    }

    [Theory]
    [InlineData("op_AdditionAssignment", 1)]
    [InlineData("op_IncrementAssignment", 0)]
    public void OrdinaryInstanceMethod_WithAssignmentOperatorName_RemainsInMethodDomain(string name, int parameterCount)
    {
        AssertCallableCase(new ConformanceCase
        {
            Member = name,
            ParameterCount = parameterCount,
            Expression = $"method(void {name}(..))",
            Expected = nameof(MatchStatus.Matched)
        });
    }

    [Fact]
    public void OrdinaryGenericMethod_WithSpecialOperatorName_RemainsInMethodDomain()
    {
        AssertCallableCase(new ConformanceCase
        {
            Member = "op_CheckedAddition",
            ParameterCount = 2,
            Expression = "method(int op_CheckedAddition<>(..))",
            Expected = nameof(MatchStatus.Matched)
        });
    }

    [Theory]
    [InlineData("method(* (ConformanceFixture || Other).(Read || Flush)(..))", "Read", "Matched")]
    [InlineData("method(* !ConformanceFixture.Read(..))", "Read", "NotMatched")]
    [InlineData("getter(* !Name)", "get_Label", "Matched")]
    [InlineData("getter(* !*)", "get_Label", "NotMatched")]
    [InlineData("property(* (ConformanceFixture || Other).(Name || Label))", "get_Name", "Matched")]
    [InlineData("property(* ConformanceFixture.(Name && !Label))", "get_Name", "Matched")]
    public void MemberPath_ThreeBackends_ProduceEquivalentResults(string expression, string member, string expected)
    {
        AssertCallableCase(new ConformanceCase
        {
            Member = member,
            ParameterCount = 0,
            Expression = expression,
            Expected = expected
        });
    }

    private static TypeSignature TryMapMissingReflectionParameter(
        UnresolvedFixture fixture,
        TypeSignature fallback)
    {
        try
        {
            var type = fixture.ReflectionAssembly.GetType(
                FIXTURE_TYPE,
                throwOnError: true)!;
            var method = Assert.IsAssignableFrom<MethodInfo>(
                type.GetMethod("Missing"));
            return fixture.Reflection.Mapper.MapMethod(method)
                .Parameters[0]
                .Type
                .Type;
        }
        catch (FileNotFoundException)
        {
            return fallback;
        }
        catch (FileLoadException)
        {
            return fallback;
        }
        catch (TypeLoadException)
        {
            return fallback;
        }
    }

    private void AssertCallableCase(ConformanceCase @case)
    {
        var roslyn = MapRoslynCallable(@case);
        var cecil = MapCecilCallable(@case);
        var reflection = MapReflectionCallable(@case);
        var roslynSnapshot = SignatureSnapshot.Callable(roslyn);

        Assert.Equal(roslynSnapshot, SignatureSnapshot.Callable(cecil));
        Assert.Equal(roslynSnapshot, SignatureSnapshot.Callable(reflection));

        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(@case.Expression));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        var roslynResult = pattern!.Match(roslyn, _fixture.Roslyn);
        var cecilResult = pattern.Match(cecil, _fixture.Cecil);
        var reflectionResult = pattern.Match(
            reflection,
            _fixture.Reflection);
        var expected = Enum.Parse<MatchStatus>(@case.Expected);

        Assert.Equal(expected, roslynResult.Status);
        Assert.Equal(roslynResult.Status, cecilResult.Status);
        Assert.Equal(roslynResult.Status, reflectionResult.Status);
    }

    private void AssertTypeCase(ConformanceCase @case)
    {
        var roslyn = MapRoslynNestedType();
        var cecil = MapCecilNestedType();
        var reflection = MapReflectionNestedType();
        var roslynSnapshot = SignatureSnapshot.Type(roslyn);

        Assert.Equal(roslynSnapshot, SignatureSnapshot.Type(cecil));
        Assert.Equal(roslynSnapshot, SignatureSnapshot.Type(reflection));

        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(@case.Expression));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        var roslynResult = pattern!.Match(roslyn, _fixture.Roslyn);
        var cecilResult = pattern.Match(cecil, _fixture.Cecil);
        var reflectionResult = pattern.Match(
            reflection,
            _fixture.Reflection);
        var expected = Enum.Parse<MatchStatus>(@case.Expected);

        Assert.Equal(expected, roslynResult.Status);
        Assert.Equal(roslynResult.Status, cecilResult.Status);
        Assert.Equal(roslynResult.Status, reflectionResult.Status);
    }

    private MethodSignature MapRoslynCallable(ConformanceCase @case)
    {
        var type = RequiredRoslynType(FIXTURE_TYPE);
        var method = FindRoslynMethod(
            type,
            @case.Member!,
            @case.ParameterCount);
        if (@case.GenericArguments.Count > 0)
        {
            method = method.Construct(
                @case.GenericArguments
                    .Select(RequiredRoslynType)
                    .Cast<ITypeSymbol>()
                    .ToArray());
        }

        return _fixture.Roslyn.Mapper.MapMethod(method);
    }

    private MethodSignature MapCecilCallable(ConformanceCase @case)
    {
        var type = _fixture.Cecil.Module.GetType(FIXTURE_TYPE) ??
            throw new InvalidOperationException(
                $"Cecil type '{FIXTURE_TYPE}' was not found.");
        MethodReference method = Assert.Single(
            type.Methods,
            candidate => candidate.Name == @case.Member &&
                candidate.Parameters.Count == @case.ParameterCount);
        if (@case.GenericArguments.Count > 0)
        {
            var constructed = new GenericInstanceMethod(method);
            foreach (var argument in @case.GenericArguments)
            {
                constructed.GenericArguments.Add(
                    RequiredCecilType(argument));
            }

            method = constructed;
        }

        return _fixture.Cecil.Mapper.MapMethod(method);
    }

    private MethodSignature MapReflectionCallable(ConformanceCase @case)
    {
        var type = _fixture.ReflectionAssembly.GetType(
            FIXTURE_TYPE,
            throwOnError: true)!;
        MethodBase method = FindReflectionMethod(type, @case);
        if (@case.GenericArguments.Count > 0)
        {
            method = Assert.IsAssignableFrom<MethodInfo>(method).MakeGenericMethod(
                @case.GenericArguments
                    .Select(RequiredReflectionType)
                    .ToArray());
        }

        return _fixture.Reflection.Mapper.MapMethod(method);
    }

    private TypeSignature MapRoslynNestedType()
    {
        var outer = RequiredRoslynType(OUTER_TYPE).Construct(
            RequiredRoslynType("System.String"));
        var inner = Assert.Single(outer.GetTypeMembers("Inner"));
        return _fixture.Roslyn.Mapper.MapType(
            inner.Construct(
                RequiredRoslynType("System.Int32"),
                RequiredRoslynType("System.Double")));
    }

    private TypeSignature MapCecilNestedType()
    {
        var outer = _fixture.Cecil.Module.GetType(OUTER_TYPE) ??
            throw new InvalidOperationException(
                $"Cecil type '{OUTER_TYPE}' was not found.");
        var inner = Assert.Single(
            outer.NestedTypes,
            type => type.Name == "Inner`2");
        var constructed = new GenericInstanceType(inner);
        constructed.GenericArguments.Add(
            RequiredCecilType("System.String"));
        constructed.GenericArguments.Add(
            RequiredCecilType("System.Int32"));
        constructed.GenericArguments.Add(
            RequiredCecilType("System.Double"));
        return _fixture.Cecil.Mapper.MapType(constructed);
    }

    private TypeSignature MapReflectionNestedType()
    {
        var definition = _fixture.ReflectionAssembly.GetType(
            $"{OUTER_TYPE}+Inner`2",
            throwOnError: true)!;
        return _fixture.Reflection.Mapper.MapType(
            definition.MakeGenericType(
                typeof(string),
                typeof(int),
                typeof(double)));
    }

    private INamedTypeSymbol RequiredRoslynType(string metadataName)
    {
        return Assert.IsAssignableFrom<INamedTypeSymbol>(
            _fixture.Compilation.GetTypeByMetadataName(metadataName));
    }

    private TypeReference RequiredCecilType(string metadataName)
    {
        return metadataName switch
        {
            "System.String" => _fixture.Cecil.Module.TypeSystem.String,
            "System.Int32" => _fixture.Cecil.Module.TypeSystem.Int32,
            "System.Double" => _fixture.Cecil.Module.TypeSystem.Double,
            _ => throw new InvalidOperationException(
                $"Unsupported Cecil test type '{metadataName}'.")
        };
    }

    private static Type RequiredReflectionType(string metadataName)
    {
        return metadataName switch
        {
            "System.String" => typeof(string),
            "System.Int32" => typeof(int),
            "System.Double" => typeof(double),
            _ => throw new InvalidOperationException(
                $"Unsupported Reflection test type '{metadataName}'.")
        };
    }

    private static IMethodSymbol FindRoslynMethod(
        INamedTypeSymbol type,
        string metadataName,
        int parameterCount)
    {
        if (metadataName.StartsWith("get_", StringComparison.Ordinal))
        {
            var property = Assert.Single(
                type.GetMembers().OfType<IPropertySymbol>(),
                candidate => candidate.MetadataName ==
                    metadataName.Substring(4));
            return Assert.IsAssignableFrom<IMethodSymbol>(property.GetMethod);
        }

        if (metadataName.StartsWith("set_", StringComparison.Ordinal))
        {
            var property = Assert.Single(
                type.GetMembers().OfType<IPropertySymbol>(),
                candidate => candidate.MetadataName ==
                    metadataName.Substring(4));
            return Assert.IsAssignableFrom<IMethodSymbol>(property.SetMethod);
        }

        return Assert.Single(
            type.GetMembers(metadataName).OfType<IMethodSymbol>(),
            method => method.Parameters.Length == parameterCount);
    }

    private static MethodBase FindReflectionMethod(
        Type type,
        ConformanceCase @case)
    {
        const BindingFlags FLAGS =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static;
        if (@case.Member == ".cctor")
        {
            return Assert.IsAssignableFrom<ConstructorInfo>(
                type.TypeInitializer);
        }

        if (@case.Member == ".ctor")
        {
            return Assert.Single(
                type.GetConstructors(FLAGS),
                candidate => !candidate.IsStatic &&
                    candidate.GetParameters().Length ==
                    @case.ParameterCount);
        }

        return Assert.Single(
            type.GetMethods(FLAGS),
            candidate => candidate.Name == @case.Member &&
                candidate.GetParameters().Length == @case.ParameterCount);
    }
}

public sealed class ConformanceCase
{
    public string Expected { get; init; } = string.Empty;

    public string Expression { get; init; } = string.Empty;

    public IReadOnlyList<string> GenericArguments { get; init; } =
        Array.Empty<string>();

    public string Id { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string? Member { get; init; }

    public int ParameterCount { get; init; }

    public override string ToString()
    {
        return Id;
    }
}

internal static class SignatureSnapshot
{
    public static string Callable(MethodSignature signature)
    {
        return string.Join(
            "|",
            signature.Name,
            signature.Kind,
            signature.Accessibility,
            signature.IsStatic,
            signature.IsAsync,
            Type(signature.DeclaringType),
            TypeUse(signature.ReturnType),
            string.Join(";", signature.Parameters.Select(Parameter)),
            string.Join(";", signature.GenericParameters.Select(Generic)),
            string.Join(";", signature.GenericArguments.Select(Type)),
            Attributes(signature.Attributes),
            Attributes(signature.ReturnAttributes),
            signature.OriginalDefinition is null
                ? string.Empty
                : CallableDefinition(signature.OriginalDefinition),
            signature.AssociatedProperty is null
                ? string.Empty
                : Property(signature.AssociatedProperty));
    }

    public static string Type(TypeSignature signature)
    {
        return signature switch
        {
            NamedTypeSignature named =>
                $"named:{Identity(named.Identity)}" +
                $"<{string.Join(",", named.TypeArguments.Select(Type))}>" +
                $"~{(named.OriginalDefinition is null ? string.Empty : Identity(named.OriginalDefinition.Identity))}",
            ArrayTypeSignature array =>
                $"array:{array.Rank}:{array.Shape}:{Type(array.ElementType)}",
            GenericParameterTypeSignature generic =>
                $"generic:{Generic(generic.Parameter)}",
            UnsupportedTypeSignature unsupported =>
                $"unsupported:{unsupported.DisplayName}",
            _ => throw new InvalidOperationException(
                $"Unsupported signature kind '{signature.Kind}'.")
        };
    }

    private static string CallableDefinition(MethodSignature signature)
    {
        return string.Join(
            ":",
            signature.Name,
            Type(signature.DeclaringType),
            TypeUse(signature.ReturnType),
            string.Join(",", signature.Parameters.Select(Parameter)),
            string.Join(",", signature.GenericParameters.Select(Generic)));
    }

    private static string Property(PropertySignature property)
    {
        return string.Join(
            ":",
            property.Name,
            property.Accessibility,
            property.IsStatic,
            Type(property.DeclaringType),
            TypeUse(property.PropertyType),
            string.Join(",", property.Parameters.Select(Parameter)),
            Attributes(property.Attributes));
    }

    private static string Parameter(ParameterSignature parameter)
    {
        return string.Join(
            ":",
            parameter.Name,
            parameter.Position,
            TypeUse(parameter.Type),
            parameter.IsOptional,
            Attributes(parameter.Attributes));
    }

    private static string TypeUse(TypeUseSignature typeUse)
    {
        return $"{typeUse.RefKind}:{typeUse.UseKind}:{Type(typeUse.Type)}";
    }

    private static string Generic(GenericParameterSignature parameter)
    {
        return string.Join(
            ":",
            parameter.Name,
            parameter.OwnerKind,
            parameter.OwnerId,
            parameter.OwnerAssembly.Name,
            parameter.OwnerAssembly.Version,
            parameter.OwnerAssembly.CultureName,
            parameter.OwnerAssembly.PublicKeyToken,
            parameter.Position);
    }

    private static string Identity(TypeIdentity identity)
    {
        return string.Join(
            ":",
            identity.Assembly.Name,
            identity.Assembly.Version,
            identity.Assembly.CultureName,
            identity.Assembly.PublicKeyToken,
            identity.FullMetadataName,
            identity.Kind);
    }

    private static string Attributes(
        IReadOnlyList<AttributeSignature> attributes)
    {
        return string.Join(
            ",",
            attributes
                .Where(attribute => attribute.AttributeType is
                    NamedTypeSignature named &&
                    named.Identity.FullMetadataName.StartsWith(
                        "AspectN.TestAssembly.Conformance.",
                        StringComparison.Ordinal))
                .Select(attribute =>
                    $"{attribute.Target}:{attribute.ParameterIndex}:" +
                    Type(attribute.AttributeType)));
    }
}
