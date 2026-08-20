[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot ".."))
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts\packages"))
$artifactRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts"))
$validationId = [System.Guid]::NewGuid().ToString("N").Substring(0, 12)
$packageVersion = "0.0.0-validation-$validationId"
$projectUrl = "https://github.com/AspectN/AspectN"
$nugetSource = "https://api.nuget.org/v3/index.json"

if (-not $packageDirectory.StartsWith(
        $artifactRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Package output escaped the repository artifact directory."
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-EntryText {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory = $true)]
        [string] $EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    Assert-True ($null -ne $entry) "Package entry '$EntryName' was not found."
    $reader = New-Object System.IO.StreamReader($entry.Open())
    try {
        return $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}

function Get-MetadataNode {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode] $Metadata,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    return $Metadata.SelectSingleNode(
        "./*[local-name()='$Name']")
}

function Assert-DependencySet {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackageId,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Actual,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]] $Expected
    )

    $unexpected = @($Actual | Where-Object { $_ -notin $Expected })
    $missing = @($Expected | Where-Object { $_ -notin $Actual })
    Assert-True (
        $unexpected.Count -eq 0 -and $missing.Count -eq 0) (
        "$PackageId dependency mismatch. Actual: " +
        "[$($Actual -join ', ')]; expected: [$($Expected -join ', ')].")
}

function Get-ConsumerSource {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackageId
    )

    switch ($PackageId) {
        "AspectN" {
            return @"
using AspectN;

var compilation = new AspectNEngine().CompileMemberExpression(
    "method(* Example.Service.Run(..))");
if (!compilation.TryGetMemberPattern(out _))
{
    throw new InvalidOperationException("AspectN compilation failed.");
}
"@
        }
        "AspectN.Cecil" {
            return @"
using AspectN.Cecil;
using Mono.Cecil;

using var module = ModuleDefinition.CreateModule("Consumer", ModuleKind.Dll);
using var typeSystem = new CecilTypeSystem(module);
_ = typeSystem.Mapper.MapType(module.TypeSystem.String);
"@
        }
        "AspectN.Cecil.Fody" {
            return @"
using AspectN.Cecil;
using Mono.Cecil;

using var module = ModuleDefinition.CreateModule("Consumer", ModuleKind.Dll);
using var typeSystem = new CecilTypeSystem(module);
_ = typeSystem.Mapper.MapType(module.TypeSystem.String);
"@
        }
        "AspectN.Roslyn" {
            return @"
using AspectN.Roslyn;
using Microsoft.CodeAnalysis.CSharp;

var compilation = CSharpCompilation.Create("Consumer");
var typeSystem = new RoslynTypeSystem(compilation);
_ = typeSystem.Capabilities;
"@
        }
        "AspectN.Reflection" {
            return @"
using AspectN.Reflection;

var typeSystem = new ReflectionTypeSystem();
_ = typeSystem.Mapper.MapType(typeof(string));
"@
        }
        default {
            throw "No consumer source is defined for '$PackageId'."
        }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packages = @(
    @{
        Id = "AspectN"
        Project = "src\AspectN\AspectN.csproj"
        Dependencies = @()
    },
    @{
        Id = "AspectN.Cecil"
        Project = "src\AspectN.Cecil\AspectN.Cecil.csproj"
        Dependencies = @("AspectN", "Mono.Cecil")
    },
    @{
        Id = "AspectN.Cecil.Fody"
        Project = "src\AspectN.Cecil.Fody\AspectN.Cecil.Fody.csproj"
        Dependencies = @("AspectN", "FodyHelpers")
    },
    @{
        Id = "AspectN.Roslyn"
        Project = "src\AspectN.Roslyn\AspectN.Roslyn.csproj"
        Dependencies = @("AspectN", "Microsoft.CodeAnalysis.CSharp")
    },
    @{
        Id = "AspectN.Reflection"
        Project = "src\AspectN.Reflection\AspectN.Reflection.csproj"
        Dependencies = @("AspectN")
    }
)

if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDirectory | Out-Null

foreach ($package in $packages) {
    $projectPath = Join-Path $repositoryRoot $package.Project
    Invoke-DotNet @(
        "pack",
        $projectPath,
        "-c",
        "Release",
        "--output",
        $packageDirectory,
        "-p:Version=$packageVersion",
        "-p:PackageVersion=$packageVersion"
    )
}

$descriptions = New-Object "System.Collections.Generic.HashSet[string]" (
    [System.StringComparer]::Ordinal)
$tags = New-Object "System.Collections.Generic.HashSet[string]" (
    [System.StringComparer]::Ordinal)

foreach ($package in $packages) {
    $packageId = $package.Id
    $packagePath = Join-Path $packageDirectory (
        "$packageId.$packageVersion.nupkg")
    $symbolPath = Join-Path $packageDirectory (
        "$packageId.$packageVersion.snupkg")
    Assert-True (Test-Path -LiteralPath $packagePath) (
        "Package '$packagePath' was not created.")
    Assert-True (Test-Path -LiteralPath $symbolPath) (
        "Symbol package '$symbolPath' was not created.")

    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName })
        foreach ($framework in @("netstandard2.0", "net10.0")) {
            Assert-True ($entries -contains "lib/$framework/$packageId.dll") (
                "$packageId is missing its $framework assembly.")
            Assert-True ($entries -contains "lib/$framework/$packageId.xml") (
                "$packageId is missing its $framework XML documentation.")
        }
        Assert-True ($entries -contains "README.md") (
            "$packageId is missing README.md.")

        $nuspecName = "$packageId.nuspec"
        [xml] $nuspec = Get-EntryText $archive $nuspecName
        $metadata = $nuspec.SelectSingleNode(
            "/*[local-name()='package']/*[local-name()='metadata']")
        Assert-True ($null -ne $metadata) (
            "$packageId has no nuspec metadata element.")

        $description = (Get-MetadataNode $metadata "description").InnerText
        $packageTags = (Get-MetadataNode $metadata "tags").InnerText
        $license = Get-MetadataNode $metadata "license"
        $readme = (Get-MetadataNode $metadata "readme").InnerText
        $project = (Get-MetadataNode $metadata "projectUrl").InnerText
        $repository = Get-MetadataNode $metadata "repository"
        Assert-True (-not [string]::IsNullOrWhiteSpace($description)) (
            "$packageId has no description.")
        Assert-True (-not [string]::IsNullOrWhiteSpace($packageTags)) (
            "$packageId has no tags.")
        Assert-True ($descriptions.Add($description)) (
            "$packageId does not have a distinct description.")
        Assert-True ($tags.Add($packageTags)) (
            "$packageId does not have distinct tags.")
        Assert-True (
            $license.GetAttribute("type") -eq "expression" -and
            $license.InnerText -eq "MIT") (
            "$packageId must use the MIT license expression.")
        Assert-True ($readme -eq "README.md") (
            "$packageId has an invalid package readme path.")
        Assert-True ($project -eq $projectUrl) (
            "$packageId has an invalid project URL.")
        Assert-True ($repository.GetAttribute("type") -eq "git") (
            "$packageId has an invalid repository type.")
        Assert-True ($repository.GetAttribute("url") -eq $projectUrl) (
            "$packageId has an invalid repository URL.")

        $dependencyIds = @(
            $metadata.SelectNodes(".//*[local-name()='dependency']") |
                ForEach-Object { $_.GetAttribute("id") } |
                Sort-Object -Unique)
        Assert-DependencySet $packageId $dependencyIds $package.Dependencies
    }
    finally {
        $archive.Dispose()
    }

    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($symbolPath)
    try {
        $symbolEntries = @(
            $symbolArchive.Entries | ForEach-Object { $_.FullName })
        foreach ($framework in @("netstandard2.0", "net10.0")) {
            Assert-True (
                $symbolEntries -contains "lib/$framework/$packageId.pdb") (
                "$packageId is missing its $framework symbols.")
        }
    }
    finally {
        $symbolArchive.Dispose()
    }
}

$consumerRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    "AspectN.PackageValidation." + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
try {
    $nugetConfigPath = Join-Path $consumerRoot "NuGet.Config"
    $consumerPackageDirectory = Join-Path $consumerRoot "packages"
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="AspectN validation packages" value="$packageDirectory" />
    <add key="nuget.org" value="$nugetSource" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig)

    foreach ($package in $packages) {
        $packageId = $package.Id
        $consumerDirectory = Join-Path $consumerRoot $packageId
        New-Item -ItemType Directory -Path $consumerDirectory | Out-Null
        $projectPath = Join-Path $consumerDirectory "Consumer.csproj"
        $sourcePath = Join-Path $consumerDirectory "Program.cs"
        $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$packageId" Version="[$packageVersion]" />
  </ItemGroup>
</Project>
"@
        [System.IO.File]::WriteAllText($projectPath, $project)
        [System.IO.File]::WriteAllText(
            $sourcePath,
            (Get-ConsumerSource $packageId))
        Invoke-DotNet @(
            "restore",
            $projectPath,
            "--configfile",
            $nugetConfigPath,
            "--packages",
            $consumerPackageDirectory)
        Invoke-DotNet @(
            "build",
            $projectPath,
            "-c",
            "Release",
            "--no-restore",
            "-warnaserror")
    }
}
finally {
    if (Test-Path -LiteralPath $consumerRoot) {
        Remove-Item -LiteralPath $consumerRoot -Recurse -Force
    }
}

Write-Host "Validated five AspectN packages and temporary consumers."
