using AspectN;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.TypeSystem;

internal static class FrontendProbe
{
    public static void Run(string shape, int depth)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new AspectNParseOptions(ResourceLimit.Unlimited, ResourceLimit.Unlimited,
                    ResourceLimit.Unlimited, ResourceLimit.Unlimited, ResourceLimit.Unlimited,
                    ResourceLimit.Unlimited, ResourceLimit.Unlimited, ResourceLimit.Unlimited);
                var type = string.Concat(Enumerable.Repeat("Box<", depth)) + "int" + new string('>', depth);
                var source = shape switch
                {
                    "parse-generic" or "compile-generic" => type,
                    "parse-member-type" or "compile-member-type" => "method(" + type + " M())",
                    "match-array" => "int" + string.Concat(Enumerable.Repeat("[]", depth)),
                    "match-expression" => string.Join(" && ", Enumerable.Repeat("method(* *(..))", depth)),
                    "match-concrete" => type + "+",
                    "match-attribute" => "attr(call " + type + ")",
                    _ => new string('(', depth) + "method(* *(..))" + new string(')', depth)
                };
                var tree = shape is "parse-generic" or "compile-generic" or "match-array" or "match-concrete" ? AspectNParser.ParseTypePattern(source, options) :
                    AspectNParser.ParseMemberExpression(source, options);
                if (tree.Diagnostics.Count != 0) throw new InvalidOperationException(string.Join("; ", tree.Diagnostics.Select(item => item.Message)));
                if (shape.StartsWith("compile", StringComparison.Ordinal) || shape.StartsWith("match", StringComparison.Ordinal))
                {
                    var result = AspectNCompilation.Create(tree, new AspectNCompilationOptions(matchOptions:
                        new AspectNMatchOptions(maximumTypeDepth: ResourceLimit.Unlimited, maximumWork: ResourceLimit.Unlimited)));
                    if (!result.TryGetMemberPattern(out _) && !result.TryGetTypePattern(out _))
                        throw new InvalidOperationException("Deep syntax could not be bound.");
                    if (shape.StartsWith("match", StringComparison.Ordinal))
                    {
                        TypeSignature candidate = new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Core"), "System", "Int32", 0, TypeKind.Struct));
                        ITypeSystem system = shape is "match-concrete" or "match-attribute" ? new NeutralSystem() : new ReflectionTypeSystem();
                        MatchResult match;
                        if (shape is "match-array" or "match-concrete")
                        {
                            if (shape == "match-array")
                                for (var index = 0; index < depth; index++) candidate = new ArrayTypeSignature(candidate, 1);
                            result.TryGetTypePattern(out var pattern);
                            match = pattern!.Match(candidate, system);
                        }
                        else
                        {
                            var method = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
                                candidate, new TypeUseSignature(candidate, RefKind.None, TypeUseKind.Return), [], [], [], [], []);
                            result.TryGetMemberPattern(out var pattern);
                            match = pattern!.Match(method, system);
                        }
                        if (match.Status != MatchStatus.Matched) throw new InvalidOperationException("Deep pattern did not match.");
                    }
                }
            }
            catch (Exception exception) { failure = exception; }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private sealed class NeutralSystem : ITypeSystem
    {
        public TypeSystemCapabilities Capabilities => TypeSystemCapabilities.All;
        public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default) => TypeRelationResult.Matched;
        public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default) => TypeRelationResult.Matched;
        public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default) => new([], null);
        public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default) => TypeRelationResult.Matched;
    }
}
