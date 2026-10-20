using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.TypeSystem;

public sealed class QueryTargetContractTests
{
    [Theory]
    [InlineData("MarkerAttribute", "")]
    [InlineData("QueryContracts.MarkerAttribute", "QueryContracts")]
    [InlineData("QueryContracts.MarkerAttribute<int>", "QueryContracts")]
    [InlineData("QueryContracts.Outer<int>/Inner<string>", "QueryContracts")]
    [InlineData("QueryContracts.MarkerAttribute<int[,]>", "QueryContracts")]
    public void Match_ConcreteQueries_PassUnqualifiedIdentityFields(string source, string expectedNamespace)
    {
        foreach (var attribute in new[] { false, true })
        {
            var backend = new RecordingTypeSystem();
            if (attribute)
            {
                Assert.True(new AspectNEngine().CompileMemberExpression("attr(call " + source + ")")
                    .TryGetMemberPattern(out var pattern));
                var owner = Named("Owner");
                var method = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
                    owner, new TypeUseSignature(owner, RefKind.None, TypeUseKind.Return), [], [], [], [], []);
                Assert.Equal(MatchStatus.NotMatched, pattern.Match(method, backend).Status);
            }
            else
            {
                Assert.True(new AspectNEngine().CompileTypePattern(source + "+").TryGetTypePattern(out var pattern));
                Assert.Equal(MatchStatus.NotMatched, pattern.Match(Named("Candidate"), backend).Status);
            }

            var query = Assert.IsType<NamedTypeSignature>(backend.Query);
            var outer = query.Identity;
            while (outer.ContainingType is not null) outer = outer.ContainingType;
            Assert.Equal(expectedNamespace, outer.Namespace);
            AssertQueryFields(query);
        }
    }

    private static void AssertQueryFields(TypeSignature type)
    {
        if (type is ArrayTypeSignature array)
        {
            Assert.Equal(2, array.Rank);
            AssertQueryFields(array.ElementType);
            return;
        }
        var named = Assert.IsType<NamedTypeSignature>(type);
        for (var identity = named.Identity; identity is not null; identity = identity.ContainingType)
        {
            Assert.Empty(identity.Assembly.Name);
            Assert.Equal(TypeKind.Unknown, identity.Kind);
        }
        foreach (var argument in named.TypeArguments) AssertQueryFields(argument);
    }

    private static NamedTypeSignature Named(string name) => new(
        new TypeIdentity(new AssemblyIdentity("Actual"), "Actual", name, 0, TypeKind.Class));

    private sealed class RecordingTypeSystem : ITypeSystem
    {
        public TypeSignature? Query { get; private set; }
        public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;
        public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right) => StructuralTypeComparer.AreEqual(left, right);
        public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default)
        {
            Query = target;
            return TypeRelationResult.NotMatched;
        }
        public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default)
        {
            Query = attributeType;
            return TypeRelationResult.NotMatched;
        }
        public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("这些具体查询应直接调用后端关系接口。");
    }
}
