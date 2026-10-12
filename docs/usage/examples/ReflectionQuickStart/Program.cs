using AspectN;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;

var typeSystem = new ReflectionTypeSystem();
var engine = new AspectNEngine();

Check(Match("method(public string Example.Service.GetName())", "GetName") == MatchStatus.Matched);
Check(Match("method(* Example.Service.TryRun(out int))", "TryRun") == MatchStatus.Matched);

var arrayCompilation = engine.CompileTypePattern("int[]");
Check(arrayCompilation.TryGetTypePattern(out var arrayPattern));
var vector = typeSystem.Mapper.MapType(typeof(int[]));
var general = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));
Check(arrayPattern!.Match(vector, typeSystem).Status == MatchStatus.Matched);
Check(arrayPattern.Match(general, typeSystem).Status == MatchStatus.Matched);
Check(typeSystem.AreEqual(vector, general).Status == MatchStatus.NotMatched);

var first = typeof(Example.Owner<string>).GetMethod("Echo")!.GetGenericArguments()[0];
var second = typeof(Example.Owner<int>).GetMethod("Echo")!.GetGenericArguments()[0];
Check(typeSystem.AreEqual(typeSystem.Mapper.MapType(first), typeSystem.Mapper.MapType(second)).Status == MatchStatus.Matched);

var mapped = typeSystem.Mapper.MapMethod(typeof(Example.Service).GetMethod("Run")!);
var unknownVisibility = new MethodSignature(mapped.Name, mapped.Kind, Accessibility.Unknown,
    mapped.IsStatic, mapped.IsAsync, mapped.DeclaringType, mapped.ReturnType, mapped.Parameters,
    mapped.GenericParameters, mapped.GenericArguments, mapped.Attributes, mapped.ReturnAttributes);
Check(MatchUnknown("method(public * Example.Service.Run(..))").Status == MatchStatus.Unknown);
Check(MatchUnknown("method(!public * Example.Service.Run(..))").Status == MatchStatus.Unknown);
Check(MatchUnknown("method(public string Example.Service.Run(..))").Status == MatchStatus.NotMatched);
Check(MatchLogic.Not(MatchStatus.Unknown) == MatchStatus.Unknown);
Check(MatchLogic.And(MatchStatus.NotMatched, MatchStatus.Unknown) == MatchStatus.NotMatched);
Check(!MatchUnknown("method(public * Example.Service.Run(..))").ToBoolean(UnknownMatchBehavior.False));
Console.WriteLine("Documentation examples passed.");

MatchStatus Match(string source, string name)
{
    var compilation = engine.CompileMemberExpression(source);
    Check(compilation.TryGetMemberPattern(out var pattern));
    return pattern!.Match(typeSystem.Mapper.MapMethod(typeof(Example.Service).GetMethod(name)!), typeSystem).Status;
}

MatchResult MatchUnknown(string source)
{
    var compilation = engine.CompileMemberExpression(source);
    Check(compilation.TryGetMemberPattern(out var pattern));
    return pattern!.Match(unknownVisibility, typeSystem);
}

static void Check(bool value)
{
    if (!value) throw new InvalidOperationException("Documentation example did not satisfy its expected contract.");
}
