using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AspectN.Roslyn;

/// <summary>Represents roslyn syntax extensions.</summary>
public static class RoslynSyntaxExtensions
{
    /// <summary>Maps type metadata to an AspectN signature.</summary>
    /// <param name="mapper">Specifies the mapper.</param>
    /// <param name="declaration">Specifies the declaration.</param>
    /// <param name="semanticModel">Specifies the semantic model.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static TypeSignature MapType(
        this RoslynSignatureMapper mapper,
        TypeDeclarationSyntax declaration,
        SemanticModel semanticModel)
    {
        GuardContext(mapper, declaration, semanticModel);
        var symbol = semanticModel.GetDeclaredSymbol(declaration) as
            ITypeSymbol ?? throw new InvalidOperationException(
                "The declaration does not have a Roslyn type symbol.");
        return mapper.MapType(symbol);
    }

    /// <summary>Maps type metadata to an AspectN signature.</summary>
    /// <param name="mapper">Specifies the mapper.</param>
    /// <param name="declaration">Specifies the declaration.</param>
    /// <param name="compilation">Specifies the compilation.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static TypeSignature MapType(
        this RoslynSignatureMapper mapper,
        TypeDeclarationSyntax declaration,
        Compilation compilation)
    {
        GuardCompilation(mapper, declaration, compilation);
        return mapper.MapType(
            declaration,
            compilation.GetSemanticModel(declaration.SyntaxTree));
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="mapper">Specifies the mapper.</param>
    /// <param name="declaration">Specifies the declaration.</param>
    /// <param name="semanticModel">Specifies the semantic model.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static CallableSignature MapCallable(
        this RoslynSignatureMapper mapper,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel)
    {
        GuardContext(mapper, declaration, semanticModel);
        var symbol = semanticModel.GetDeclaredSymbol(declaration) as
            IMethodSymbol ?? throw new InvalidOperationException(
                "The declaration does not have a Roslyn method symbol.");
        return mapper.MapCallable(symbol);
    }

    /// <summary>Maps callable metadata to an AspectN signature.</summary>
    /// <param name="mapper">Specifies the mapper.</param>
    /// <param name="declaration">Specifies the declaration.</param>
    /// <param name="compilation">Specifies the compilation.</param>
    /// <returns>Returns the result of the operation.</returns>
    public static CallableSignature MapCallable(
        this RoslynSignatureMapper mapper,
        MethodDeclarationSyntax declaration,
        Compilation compilation)
    {
        GuardCompilation(mapper, declaration, compilation);
        return mapper.MapCallable(
            declaration,
            compilation.GetSemanticModel(declaration.SyntaxTree));
    }

    private static void GuardContext(
        RoslynSignatureMapper mapper,
        SyntaxNode declaration,
        SemanticModel semanticModel)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(semanticModel);
#else
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (declaration is null)
            throw new ArgumentNullException(nameof(declaration));
        if (semanticModel is null)
            throw new ArgumentNullException(nameof(semanticModel));
#endif

        if (!ReferenceEquals(mapper.Compilation, semanticModel.Compilation))
        {
            throw new ArgumentException(
                "The semantic model must belong to the adapter compilation.",
                nameof(semanticModel));
        }

        if (!ReferenceEquals(declaration.SyntaxTree, semanticModel.SyntaxTree))
        {
            throw new ArgumentException(
                "The declaration must belong to the semantic model syntax tree.",
                nameof(declaration));
        }
    }

    private static void GuardCompilation(
        RoslynSignatureMapper mapper,
        SyntaxNode declaration,
        Compilation compilation)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(compilation);
#else
        if (mapper is null) throw new ArgumentNullException(nameof(mapper));
        if (declaration is null)
            throw new ArgumentNullException(nameof(declaration));
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));
#endif

        if (!ReferenceEquals(mapper.Compilation, compilation))
        {
            throw new ArgumentException(
                "The compilation must be the adapter compilation.",
                nameof(compilation));
        }

        if (!compilation.SyntaxTrees.Contains(declaration.SyntaxTree))
        {
            throw new ArgumentException(
                "The declaration syntax tree is not part of the compilation.",
                nameof(declaration));
        }
    }
}
