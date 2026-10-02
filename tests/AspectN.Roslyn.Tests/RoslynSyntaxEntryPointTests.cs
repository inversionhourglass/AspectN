using System.Reflection;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AspectN.Roslyn.Tests;

public sealed class RoslynSyntaxEntryPointTests
{
    [Fact]
    public void MapTypeAndCallable_SyntaxWithSemanticContext_MapSymbols()
    {
        var compilation = RoslynTestCompilation.Create();
        var tree = Assert.Single(compilation.SyntaxTrees);
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();
        var declaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(node => node.Identifier.ValueText == "Sample");
        var method = declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Single(node => node.Identifier.ValueText == "EchoAsync");
        var typeSystem = new RoslynTypeSystem(compilation);

        var fromModel = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(declaration, model));
        var fromCompilation = typeSystem.Mapper.MapType(
            declaration,
            compilation);
        var callableFromModel = typeSystem.Mapper.MapMethod(method, model);
        var callableFromCompilation = typeSystem.Mapper.MapMethod(
            method,
            compilation);

        Assert.Equal("Fixtures.Sample`1", fromModel.Identity.FullMetadataName);
        Assert.Same(fromModel, fromCompilation);
        Assert.Equal("EchoAsync", callableFromModel.Name);
        Assert.Same(callableFromModel, callableFromCompilation);
    }

    [Fact]
    public void MapSyntax_ForeignSemanticModel_ThrowsArgumentException()
    {
        var compilation = RoslynTestCompilation.Create();
        var foreignCompilation = RoslynTestCompilation.Create(
            RoslynTestCompilation.SOURCE.Replace(
                "namespace Fixtures;",
                "namespace Foreign;",
                StringComparison.Ordinal));
        var foreignTree = Assert.Single(foreignCompilation.SyntaxTrees);
        var declaration = foreignTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(node => node.Identifier.ValueText == "Sample");
        var typeSystem = new RoslynTypeSystem(compilation);

        Assert.Throws<ArgumentException>(() =>
            typeSystem.Mapper.MapType(
                declaration,
                foreignCompilation.GetSemanticModel(foreignTree)));
    }

    [Fact]
    public void PublicApi_SyntaxOnlyOverload_DoesNotExist()
    {
        var syntaxOnly = typeof(RoslynTypeSystem).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static))
            .Where(method => method.GetParameters().Any(parameter =>
                typeof(SyntaxNode).IsAssignableFrom(parameter.ParameterType)))
            .Where(method => method.GetParameters().All(parameter =>
                parameter.ParameterType != typeof(SemanticModel) &&
                parameter.ParameterType != typeof(Compilation)))
            .ToArray();

        Assert.Empty(syntaxOnly);
    }
}
