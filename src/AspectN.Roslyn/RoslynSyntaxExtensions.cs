using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AspectN.Roslyn;

/// <summary>Maps C# type and method declarations using the compilation associated with a Roslyn mapper.</summary>
public static class RoslynSyntaxExtensions
{
    /// <summary>Resolves a C# type declaration and maps its declared symbol to a type signature.</summary>
    /// <param name="mapper">The mapper whose compilation owns the declaration.</param>
    /// <param name="declaration">A type declaration in the semantic model's syntax tree.</param>
    /// <param name="semanticModel">A model from the same compilation instance as the mapper.</param>
    /// <returns>The declared type's backend-neutral signature.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">The compilation instances differ, or the declaration and model refer to different syntax tree instances.</exception>
    /// <exception cref="InvalidOperationException">The declaration has no declared type symbol.</exception>
    /// <remarks>Identical source text does not make separately created compilations or syntax trees interchangeable. Mapping limitations and failures follow <see cref="RoslynSignatureMapper.MapType"/>.</remarks>
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

    /// <summary>Obtains a semantic model from a compilation and maps a C# type declaration.</summary>
    /// <param name="mapper">The mapper associated with the supplied compilation.</param>
    /// <param name="declaration">A type declaration whose syntax tree belongs to the compilation.</param>
    /// <param name="compilation">The same compilation instance used to create the mapper.</param>
    /// <returns>The declared type's backend-neutral signature.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">The compilation differs from the mapper's instance, or does not contain the declaration's syntax tree.</exception>
    /// <exception cref="InvalidOperationException">The declaration has no declared type symbol.</exception>
    /// <remarks>Mapping limitations and failures follow <see cref="RoslynSignatureMapper.MapType"/>.</remarks>
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

    /// <summary>Resolves a C# method declaration and maps its declared symbol to a callable signature.</summary>
    /// <param name="mapper">The mapper whose compilation owns the declaration.</param>
    /// <param name="declaration">A method declaration in the semantic model's syntax tree.</param>
    /// <param name="semanticModel">A model from the same compilation instance as the mapper.</param>
    /// <returns>The declared method's backend-neutral signature.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">The compilation instances differ, or the declaration and model refer to different syntax tree instances.</exception>
    /// <exception cref="InvalidOperationException">The declaration has no declared method symbol.</exception>
    /// <remarks>Identical source text does not make separately created compilations or syntax trees interchangeable. Mapping limitations and failures follow <see cref="RoslynSignatureMapper.MapMethod"/>.</remarks>
    public static MethodSignature MapMethod(
        this RoslynSignatureMapper mapper,
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel)
    {
        GuardContext(mapper, declaration, semanticModel);
        var symbol = semanticModel.GetDeclaredSymbol(declaration) as
            IMethodSymbol ?? throw new InvalidOperationException(
                "The declaration does not have a Roslyn method symbol.");
        return mapper.MapMethod(symbol);
    }

    /// <summary>Obtains a semantic model from a compilation and maps a C# method declaration.</summary>
    /// <param name="mapper">The mapper associated with the supplied compilation.</param>
    /// <param name="declaration">A method declaration whose syntax tree belongs to the compilation.</param>
    /// <param name="compilation">The same compilation instance used to create the mapper.</param>
    /// <returns>The declared method's backend-neutral signature.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">The compilation differs from the mapper's instance, or does not contain the declaration's syntax tree.</exception>
    /// <exception cref="InvalidOperationException">The declaration has no declared method symbol.</exception>
    /// <remarks>Mapping limitations and failures follow <see cref="RoslynSignatureMapper.MapMethod"/>.</remarks>
    public static MethodSignature MapMethod(
        this RoslynSignatureMapper mapper,
        MethodDeclarationSyntax declaration,
        Compilation compilation)
    {
        GuardCompilation(mapper, declaration, compilation);
        return mapper.MapMethod(
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
