[CmdletBinding()]
param(
    [string] $PackageVersion,
    [string] $PackageDirectory = "artifacts/packages",
    [string] $CecilVersion,
    [string] $RoslynVersion,
    [string] $FodyHelpersVersion,
    [switch] $ValidateExisting
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot ".."))
$packageDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot $PackageDirectory))
$artifactRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts"))
$validationId = [System.Guid]::NewGuid().ToString("N").Substring(0, 12)
if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    if ($ValidateExisting) { throw "ValidateExisting requires PackageVersion." }
    $PackageVersion = "0.0.0-validation-$validationId"
}
[xml] $centralPackages = Get-Content -LiteralPath (Join-Path $repositoryRoot "Directory.Packages.props") -Raw
foreach ($propertyName in @("CecilVersion", "RoslynVersion", "FodyHelpersVersion")) {
    if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $propertyName -ValueOnly))) {
        $versionNodes = $centralPackages.SelectNodes("/Project/PropertyGroup/$propertyName")
        if ($versionNodes.Count -ne 1) {
            throw "Directory.Packages.props must define exactly one default '$propertyName'; found $($versionNodes.Count)."
        }
        $defaultVersion = $versionNodes.Item(0).InnerText
        if ([string]::IsNullOrWhiteSpace($defaultVersion)) {
            throw "Directory.Packages.props default '$propertyName' must not be empty."
        }
        Set-Variable -Name $propertyName -Value $defaultVersion
    }
}
$buildProperties = @("-p:CecilVersion=$CecilVersion", "-p:RoslynVersion=$RoslynVersion",
    "-p:FodyHelpersVersion=$FodyHelpersVersion")
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
            return @'
using AspectN;
using AspectN.Metadata;

var compilation = new AspectNEngine().CompileMemberExpression(
    "method(* Example.Service.Run(..))");
if (!compilation.TryGetMemberPattern(out var memberPattern))
{
    throw new InvalidOperationException("AspectN compilation failed.");
}
_ = memberPattern.Options;
var typeCompilation = new AspectNEngine().CompileTypePattern("Example.Container<>");
if (!typeCompilation.TryGetTypePattern(out var typePattern))
{
    throw new InvalidOperationException("AspectN type compilation failed.");
}
_ = typePattern.Options;
var parameter = new GenericParameterSignature(
    "T", GenericParameterOwnerKind.Type, "Example.Container`1",
    new AssemblyIdentity("Example.Assembly"), position: 0);
_ = new GenericParameterTypeSignature(parameter);
if (parameter.OwnerId != "Example.Container" + (char)96 + "1")
{
    throw new InvalidOperationException("The generic owner metadata arity was lost.");
}
'@
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

if (-not $ValidateExisting) {
    if (Test-Path -LiteralPath $packageDirectory) {
        Remove-Item -LiteralPath $packageDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    foreach ($package in $packages) {
        $projectPath = Join-Path $repositoryRoot $package.Project
        Invoke-DotNet (@("pack", $projectPath, "-c", "Release", "--output", $packageDirectory,
            "-p:Version=$PackageVersion", "-p:PackageVersion=$PackageVersion") + $buildProperties)
    }
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

        Assert-True ((Get-MetadataNode $metadata "version").InnerText -eq $PackageVersion) (
            "$packageId has an unexpected package version.")
        foreach ($dependency in $metadata.SelectNodes(".//*[local-name()='dependency']")) {
            $expectedVersion = switch ($dependency.GetAttribute("id")) {
                "AspectN" { $PackageVersion }
                "Mono.Cecil" { $CecilVersion }
                "Microsoft.CodeAnalysis.CSharp" { $RoslynVersion }
                "FodyHelpers" { $FodyHelpersVersion }
            }
            Assert-True ($dependency.GetAttribute("version").TrimStart('[', '(').Split(',')[0].TrimEnd(']') -eq $expectedVersion) (
                "$packageId has an unexpected dependency version: $($dependency.OuterXml)")
        }

        $dependencyIds = @(
            $metadata.SelectNodes(".//*[local-name()='dependency']") |
                ForEach-Object { $_.GetAttribute("id") } |
                Sort-Object -Unique)
        Assert-DependencySet $packageId $dependencyIds $package.Dependencies
        $dependencyGroups = @($metadata.SelectNodes(".//*[local-name()='dependencies']/*[local-name()='group']"))
        Assert-True ($dependencyGroups.Count -eq 2) "$packageId must have exactly two dependency groups."
        foreach ($framework in @(".NETStandard2.0", "net10.0")) {
            $group = @($dependencyGroups | Where-Object { $_.GetAttribute("targetFramework") -eq $framework })
            Assert-True ($group.Count -eq 1) "$packageId has no unique dependency group for $framework."
            $groupIds = @($group[0].SelectNodes("./*[local-name()='dependency']") |
                ForEach-Object { $_.GetAttribute("id") })
            Assert-DependencySet "$packageId/$framework" $groupIds $package.Dependencies
        }
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
$consumerRoot = [System.IO.Path]::GetFullPath($consumerRoot)
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
if (-not $consumerRoot.StartsWith($temporaryRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase) -or
    [System.IO.Path]::GetFileName($consumerRoot) -notmatch '^AspectN\.PackageValidation\.[a-f0-9]{32}$') {
    throw "Consumer directory escaped the validation temporary directory."
}
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
    <TargetFrameworks>net472;net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
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
        foreach ($framework in @("net472", "net10.0")) {
            Invoke-DotNet @("run", "--project", $projectPath, "-c", "Release", "-f", $framework,
                "--no-build", "--no-restore")
        }
    }
}
finally {
    if (Test-Path -LiteralPath $consumerRoot) {
        Remove-Item -LiteralPath $consumerRoot -Recurse -Force
    }
}

Write-Host "Validated five AspectN packages, both dependency groups, and ten consumer builds/runs."
