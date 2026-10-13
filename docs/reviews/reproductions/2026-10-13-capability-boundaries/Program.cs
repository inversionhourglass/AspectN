using System.Runtime.Loader;
using System.Text.Json;
using AspectN;
using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;
using AssemblyIdentity = AspectN.Metadata.AssemblyIdentity;
using MethodKind = AspectN.Metadata.MethodKind;
using CoreAccessibility = AspectN.Metadata.Accessibility;
using CoreRefKind = AspectN.Metadata.RefKind;
using CoreTypeKind = AspectN.Metadata.TypeKind;

namespace AspectN.ReviewProbes;

internal static class Program
{
    private static readonly List<object> _Rows = [];
    private static readonly JsonSerializerOptions _JsonOptions = new() { WriteIndented = true };

    private static void Main(string[] args)
    {
        Run("cancellation", Cancellation);
        Run("partial-generics", PartialGenerics);
        Run("return-only-overloads", ReturnOnlyOverloads);
        Run("detached-attributes", DetachedAttributes);
        Run("readonly-marker-shadow", ReadonlyMarkerShadow);
        Run("partial-async", PartialAsync);
        var identity = typeof(ModuleDefinition).Assembly.GetName();
        var report = new
        {
            native_assembly = identity.FullName,
            native_token = Convert.ToHexString(identity.GetPublicKeyToken()!).ToLowerInvariant(),
            rows = _Rows
        };
        File.WriteAllText(args[0], JsonSerializer.Serialize(report, _JsonOptions));
    }

    private static void Run(string name, Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            _Rows.Add(new { probe = name, probe_error = exception.GetType().FullName, detail = exception.ToString() });
        }
    }

    private static NamedTypeSignature Named(string name, int arity = 0, TypeSignature[]? arguments = null)
        => new(new TypeIdentity(new AssemblyIdentity("Review"), "Review", name, arity, CoreTypeKind.Class), arguments);

    private static MethodSignature Method(TypeSignature result, GenericParameterSignature[]? parameters = null,
        TypeSignature[]? arguments = null)
        => new("M", MethodKind.OrdinaryMethod, CoreAccessibility.Public, true, false, Named("Owner"),
            new TypeUseSignature(result, CoreRefKind.None, TypeUseKind.Return), [], parameters ?? [],
            arguments ?? [], [], []);

    private static object Match(string source, MethodSignature candidate, ITypeSystem system)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        if (!compilation.TryGetMemberPattern(out var pattern)) throw new InvalidOperationException(source);
        var result = pattern.Match(candidate, system);
        return new { expression = source, status = result.Status.ToString(), diagnostics = result.Diagnostics.Select(d => d.Code).ToArray() };
    }

    private static object Closure(ITypeSystem system, TypeSignature type, CancellationToken token)
    {
        try
        {
            var closure = system.GetInheritanceClosure(type, token);
            return new
            {
                outcome = "returned",
                failure = closure.Failure?.Status.ToString(),
                diagnostics = closure.Failure?.Diagnostics.Select(d => d.Code).ToArray()
            };
        }
        catch (OperationCanceledException) { return new { outcome = "OperationCanceledException" }; }
    }

    private static void Cancellation()
    {
        using var cecil = new CecilTypeSystem(ModuleDefinition.CreateModule("Cancellation", ModuleKind.Dll));
        var reflection = new ReflectionTypeSystem();
        var roslyn = new RoslynTypeSystem(CSharpCompilation.Create("CancellationReader", references: References()));
        var detached = Named("Detached");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var (backend, system, mapped) in new (string, ITypeSystem, TypeSignature)[]
                 {
                     ("Cecil", cecil, cecil.Mapper.MapType(cecil.Module.TypeSystem.Object)),
                     ("Reflection", reflection, reflection.Mapper.MapType(typeof(object))),
                     ("Roslyn", roslyn, roslyn.Mapper.MapType(roslyn.Compilation.GetSpecialType(SpecialType.System_Object)))
                 })
        {
            _Rows.Add(new
            {
                probe = "cancellation",
                backend,
                detached_canceled = Closure(system, detached, cancellation.Token),
                detached_not_canceled = Closure(system, detached, default),
                associated_canceled = Closure(system, mapped, cancellation.Token)
            });
        }
    }

    private static void PartialGenerics()
    {
        var system = new ReflectionTypeSystem();
        var intType = system.Mapper.MapType(typeof(int));
        var stringType = system.Mapper.MapType(typeof(string));
        foreach (var (name, type) in new[]
                 {
                     ("partial", Named("Box", 2, [intType])),
                     ("complete", Named("Box", 2, [intType, stringType])),
                     ("definition", Named("Box", 2))
                 })
        {
            var compiled = new AspectNEngine().CompileTypePattern("Review.Box<int, int>");
            if (!compiled.TryGetTypePattern(out var pattern)) throw new InvalidOperationException("Box pattern");
            var structural = pattern.Match(type, system);
            _Rows.Add(new
            {
                probe = "partial-type-arguments",
                input = name,
                arity = type.Identity.GenericArity,
                arguments = type.TypeArguments.Count,
                regex = Match("regex(.*)", Method(type), system),
                structural = new { status = structural.Status.ToString(), diagnostics = structural.Diagnostics.Select(d => d.Code).ToArray() }
            });
        }
        var assembly = new AssemblyIdentity("Review");
        GenericParameterSignature[] parameters =
        [
            new("T", GenericParameterOwnerKind.Method, "Review.Owner::M", assembly, 0),
            new("U", GenericParameterOwnerKind.Method, "Review.Owner::M", assembly, 1)
        ];
        foreach (var (name, arguments) in new (string, TypeSignature[])[]
                 { ("partial", [intType]), ("complete", [intType, stringType]), ("definition", []) })
        {
            var method = Method(intType, parameters, arguments);
            _Rows.Add(new
            {
                probe = "partial-method-arguments",
                input = name,
                declared_arity = method.GenericParameters.Count,
                arguments = method.GenericArguments.Count,
                one_slot = Match("method(* M<int>(..))", method, system),
                two_slots = Match("method(* M<int,string>(..))", method, system),
                regex = Match("regex(.*)", method, system)
            });
        }
    }

    private static void ReturnOnlyOverloads()
    {
        using var module = ModuleDefinition.CreateModule("ReturnOnlyOverloads", ModuleKind.Dll);
        var owner = new TypeDefinition("Review", "Owner", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(owner);
        foreach (var isInt in new[] { true, false })
        {
            var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                isInt ? module.TypeSystem.Int32 : module.TypeSystem.String);
            owner.Methods.Add(method);
            var parameter = new GenericParameter("T", method);
            method.GenericParameters.Add(parameter);
            parameter.Constraints.Add(new GenericParameterConstraint(
                module.ImportReference(isInt ? typeof(IDisposable) : typeof(IComparable))));
            method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, parameter));
            method.Body.Instructions.Add(Instruction.Create(isInt ? OpCodes.Ldc_I4_0 : OpCodes.Ldnull));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
        using var stream = new MemoryStream();
        module.Write(stream);
        var image = stream.ToArray();
        using var fixture = new Fixture(image);
        var runtime = fixture.Assembly.GetType("Review.Owner")!.GetMethods().Where(m => m.Name == "M").ToArray();
        if (runtime.Length != 2) throw new InvalidOperationException("CLR overload enumeration");
        using var disposable = new MemoryStream();
        var executions = runtime.Select(m => m.ReturnType == typeof(int)
            ? m.MakeGenericMethod(typeof(MemoryStream)).Invoke(null, [disposable])
            : m.MakeGenericMethod(typeof(int)).Invoke(null, [0])).ToArray();
        _Rows.Add(new { probe = "return-only-overloads-clr", declarations = runtime.Length, executions });
        foreach (var (backend, system, methods) in new (string, ITypeSystem, MethodSignature[])[]
                 {
                     ("Cecil", fixture.Cecil, fixture.Cecil.Module.GetType("Review.Owner").Methods.Select(fixture.Cecil.Mapper.MapMethod).ToArray()),
                     ("Reflection", fixture.Reflection, runtime.Select(fixture.Reflection.Mapper.MapMethod).ToArray()),
                     ("Roslyn PE", fixture.Metadata, fixture.Metadata.Compilation.GetTypeByMetadataName("Review.Owner")!
                         .GetMembers("M").OfType<IMethodSymbol>().Select(fixture.Metadata.Mapper.MapMethod).ToArray())
                 })
        {
            if (methods.Length != 2) throw new InvalidOperationException(backend + " overload enumeration");
            var first = methods[0].GenericParameters[0];
            var second = methods[1].GenericParameters[0];
            _Rows.Add(new
            {
                probe = "return-only-overloads",
                backend,
                owners = new[] { first.OwnerId, second.OwnerId },
                parameter_equality = system.AreEqual(new GenericParameterTypeSignature(first), new GenericParameterTypeSignature(second)).Status.ToString()
            });
        }
    }

    private static void DetachedAttributes()
    {
        using var fixture = new Fixture("""
            using System;
            namespace Review;
            [AttributeUsage(AttributeTargets.Class, Inherited=true)]
            public sealed class InheritedMarkAttribute : Attribute {}
            [InheritedMark] public class BaseFixture { public virtual void M() {} }
            public class Fixture : BaseFixture { public override void M() {} }
            """);
        foreach (var name in new[] { "BaseFixture", "Fixture" })
        {
            var method = fixture.Reflection.Mapper.MapMethod(fixture.Assembly.GetType("Review." + name)!.GetMethod("M")!);
            foreach (var (context, system) in new[]
                     { ("origin", fixture.Reflection), ("new", new ReflectionTypeSystem()) })
                foreach (var expression in new[]
                         {
                             "attr(type Review.InheritedMarkAttribute)", "attr(type Review.InheritedMark*)",
                             "attr(type (Review.InheritedMarkAttribute || Review.MissingAttribute))"
                         })
                    _Rows.Add(new
                    {
                        probe = "detached-attributes",
                        input = name,
                        context,
                        type_attributes_known = method.Availability.IsKnown(MetadataFacts.TypeAttributes),
                        embedded_type_attributes = method.TypeAttributes.Select(a =>
                            ((NamedTypeSignature)a.AttributeType).Identity.FullMetadataName).ToArray(),
                        result = Match(expression, method, system)
                    });
        }
    }

    private static void ReadonlyMarkerShadow()
    {
        using var fixture = new Fixture("""
            namespace Review;
            public class Fixture
            {
                private int _value;
                public ref readonly int M() => ref _value;
            }
            """);
        var reader = CSharpCompilation.Create("ShadowReader",
            [CSharpSyntaxTree.ParseText("""
                namespace System.Runtime.CompilerServices;
                public sealed class IsReadOnlyAttribute : System.Attribute {}
                """)],
            References().Append(MetadataReference.CreateFromImage(fixture.Image)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = reader.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join(";", errors.Select(e => e.ToString())));
        var shadow = new RoslynTypeSystem(reader);
        var actualMarker = fixture.Reflection.Mapper.MapType(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
        var nativeReflectionAttributes = fixture.Assembly.GetType("Review.Fixture")!.GetMethod("M")!
            .ReturnParameter.CustomAttributes.Select(a => new
            {
                name = a.AttributeType.FullName,
                assembly = a.AttributeType.Assembly.GetName().Name
            }).ToArray();
        var nativeCecilAttributes = fixture.Cecil.Module.GetType("Review.Fixture").Methods.Single(m => m.Name == "M")
            .MethodReturnType.CustomAttributes.Select(a => new
            {
                name = a.Constructor.DeclaringType.FullName,
                assembly = ((AssemblyNameReference)a.Constructor.DeclaringType.Scope).Name
            }).ToArray();
        foreach (var (backend, system, method) in fixture.Methods("M").Append(
                     ("Roslyn shadow PE", shadow, shadow.Mapper.MapMethod(reader.GetTypeByMetadataName("Review.Fixture")!
                         .GetMembers("M").OfType<IMethodSymbol>().Single()))))
            _Rows.Add(new
            {
                probe = "readonly-marker-shadow",
                backend,
                native_reflection_return_attributes = nativeReflectionAttributes,
                native_cecil_return_attributes = nativeCecilAttributes,
                ref_kind = method.ReturnType.RefKind.ToString(),
                attributes = method.ReturnAttributes.Select(a => new
                {
                    name = ((NamedTypeSignature)a.AttributeType).Identity.FullMetadataName,
                    assembly = ((NamedTypeSignature)a.AttributeType).Identity.Assembly.Name,
                    equals_actual_marker = system.AreEqual(a.AttributeType, actualMarker).Status.ToString()
                }).ToArray()
            });
    }

    private static void PartialAsync()
    {
        using var fixture = new Fixture("""
            using System.Collections.Generic;
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            namespace Review;
            public partial class Fixture
            {
                public partial Task<int> Async();
                public async partial Task<int> Async() { await Task.Yield(); return 1; }
                public partial IAsyncEnumerable<int> AsyncIterator();
                public async partial IAsyncEnumerable<int> AsyncIterator() { await Task.Yield(); yield return 1; }
                [AsyncStateMachine(typeof(Fixture))]
                public partial Task<int> MarkedAsync();
                public async partial Task<int> MarkedAsync() { await Task.Yield(); return 1; }
            }
            """);
        foreach (var name in new[] { "Async", "AsyncIterator", "MarkedAsync" })
        {
            var definition = fixture.Source.Compilation.GetTypeByMetadataName("Review.Fixture")!
                .GetMembers(name).OfType<IMethodSymbol>().Single();
            var implementation = definition.PartialImplementationPart!;
            var methods = fixture.Methods(name).Append(
                ("Roslyn implementation", fixture.Source, fixture.Source.Mapper.MapMethod(implementation)));
            foreach (var (backend, system, method) in methods)
            {
                var nativeSymbol = backend == "Roslyn implementation" ? implementation : definition;
                var runtime = fixture.Assembly.GetType("Review.Fixture")!.GetMethod(name)!;
                var nativeMarker = runtime.CustomAttributes.Any(a =>
                    a.AttributeType.Name is "AsyncStateMachineAttribute" or "AsyncIteratorStateMachineAttribute");
                var expectedAsync = backend.StartsWith("Roslyn", StringComparison.Ordinal) && backend != "Roslyn PE"
                    ? nativeSymbol.IsAsync || nativeSymbol.GetAttributes().Any(a =>
                        a.AttributeClass?.Name is "AsyncStateMachineAttribute" or "AsyncIteratorStateMachineAttribute")
                    : nativeMarker;
                _Rows.Add(new
                {
                    probe = "partial-async",
                    input = name,
                    backend,
                    is_async = method.IsAsync,
                    native_expected_is_async = expectedAsync,
                    matches_native_is_async = expectedAsync == method.IsAsync,
                    attributes = method.Attributes.Select(a => ((NamedTypeSignature)a.AttributeType).Identity.FullMetadataName).ToArray(),
                    tasklike = Match($"method(tasklike<int> {name}())", method, system),
                    marker = Match("attr(call System.Runtime.CompilerServices." +
                        (name == "AsyncIterator" ? "AsyncIteratorStateMachineAttribute" : "AsyncStateMachineAttribute") + ")", method, system)
                });
            }
        }
    }

    private static IEnumerable<MetadataReference> References()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

    private sealed class Fixture : IDisposable
    {
        private readonly DefaultAssemblyResolver _resolver = new();
        private readonly AssemblyLoadContext _context = new("CapabilityBoundary", isCollectible: true);
        internal Fixture(string source)
        {
            var compilation = CSharpCompilation.Create("Boundary" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source)], References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emitted = compilation.Emit(stream);
            if (!emitted.Success) throw new InvalidOperationException(string.Join(";", emitted.Diagnostics));
            Image = stream.ToArray();
            Source = new RoslynTypeSystem(compilation);
            Assembly = _context.LoadFromStream(new MemoryStream(Image));
            _resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
            Cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(Image), new ReaderParameters { AssemblyResolver = _resolver }));
            Metadata = new RoslynTypeSystem(CSharpCompilation.Create("BoundaryReader",
                references: References().Append(MetadataReference.CreateFromImage(Image)),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All)));
        }

        internal Fixture(byte[] image)
        {
            Image = image;
            Assembly = _context.LoadFromStream(new MemoryStream(image));
            _resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
            Cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image), new ReaderParameters { AssemblyResolver = _resolver }));
            Source = new RoslynTypeSystem(CSharpCompilation.Create("NoSource"));
            Metadata = new RoslynTypeSystem(CSharpCompilation.Create("BoundaryReader",
                references: References().Append(MetadataReference.CreateFromImage(image)),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All)));
        }

        internal byte[] Image { get; }
        internal System.Reflection.Assembly Assembly { get; }
        internal CecilTypeSystem Cecil { get; }
        internal ReflectionTypeSystem Reflection { get; } = new();
        internal RoslynTypeSystem Source { get; }
        internal RoslynTypeSystem Metadata { get; }

        internal IEnumerable<(string Backend, ITypeSystem System, MethodSignature Method)> Methods(string name)
        {
            var runtime = Assembly.GetType("Review.Fixture")!.GetMethod(name)!;
            yield return ("Reflection", Reflection, Reflection.Mapper.MapMethod(runtime));
            yield return ("Cecil", Cecil, Cecil.Mapper.MapMethod(Cecil.Module.GetType("Review.Fixture").Methods.Single(m => m.Name == name)));
            foreach (var (backend, system) in new[] { ("Roslyn source", Source), ("Roslyn PE", Metadata) })
                yield return (backend, system, system.Mapper.MapMethod(system.Compilation.GetTypeByMetadataName("Review.Fixture")!
                    .GetMembers(name).OfType<IMethodSymbol>().Single()));
        }

        public void Dispose()
        {
            Cecil.Dispose();
            _resolver.Dispose();
            _context.Unload();
        }
    }
}
