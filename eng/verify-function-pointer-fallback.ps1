[CmdletBinding()]
param([string] $RuntimeVersion = "7.0.20")

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$probeDirectory = Join-Path $repositoryRoot "artifacts/function-pointer-fallback"
New-Item -ItemType Directory -Path $probeDirectory -Force | Out-Null
$projectPath = Join-Path $probeDirectory "Probe.csproj"
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net7.0</TargetFramework>
    <CheckEolTargetFramework>false</CheckEolTargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Reference Include="AspectN"><HintPath>$repositoryRoot/src/AspectN/bin/Release/netstandard2.0/AspectN.dll</HintPath></Reference>
    <Reference Include="AspectN.Reflection"><HintPath>$repositoryRoot/src/AspectN.Reflection/bin/Release/netstandard2.0/AspectN.Reflection.dll</HintPath></Reference>
  </ItemGroup>
</Project>
"@
$source = @'
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using FunctionPointerProbe;

var firstSystem = new ReflectionTypeSystem();
var secondSystem = new ReflectionTypeSystem();
var methods = typeof(Fixture).GetMethods().Where(method => method.Name == "M").ToArray();
if (methods.Length != 2) throw new InvalidOperationException("Missing convention overloads.");
var types = methods.Select(method => method.GetGenericArguments()[0]).ToArray();
var signatures = types.Select(type => (GenericParameterTypeSignature)firstSystem.Mapper.MapType(type)).ToArray();
var unavailable = typeof(Type).GetProperty("IsUnmanagedFunctionPointer") is null ||
    typeof(System.Reflection.ParameterInfo).GetMethod("GetModifiedParameterType") is null;
foreach (var signature in signatures)
    if (signature.Parameter.OwnerId.Contains("#declaration:") != unavailable)
        throw new InvalidOperationException("Incorrect declaration fallback: " + signature.Parameter.OwnerId);
if (firstSystem.AreEqual(signatures[0], signatures[1]).Status != MatchStatus.NotMatched)
    throw new InvalidOperationException("Calling convention overloads collapsed.");
foreach (var type in types)
    if (firstSystem.AreEqual(firstSystem.Mapper.MapType(type), secondSystem.Mapper.MapType(type)).Status != MatchStatus.Matched)
        throw new InvalidOperationException("Same declaration identity changed across mapper instances.");
Console.WriteLine($"Runtime {Environment.Version}: calling convention identity checks passed (fallback={unavailable}).");

namespace FunctionPointerProbe
{
    public static unsafe class Fixture
    {
        public static void M<T>(delegate* unmanaged[Cdecl]<void> value) { }
        public static void M<T>(delegate* unmanaged[Stdcall]<void> value) { }
    }
}
'@
[System.IO.File]::WriteAllText($projectPath, $project)
[System.IO.File]::WriteAllText((Join-Path $probeDirectory "Program.cs"), $source)
& dotnet build $projectPath -c Release
if ($LASTEXITCODE -ne 0) { throw "Function pointer probe build failed." }
& dotnet --fx-version $RuntimeVersion (Join-Path $probeDirectory "bin/Release/net7.0/Probe.dll")
if ($LASTEXITCODE -ne 0) { throw "Function pointer identity probe failed on $RuntimeVersion." }
