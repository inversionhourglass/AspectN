#requires -Version 5.1
<#
.SYNOPSIS
在签名前核对发布目录与未签名SHA256清单，失败时中止流程。
.PARAMETER PackageDirectory
下载得到的平面发布目录，相对路径基于当前工作目录。
.EXAMPLE
pwsh -NoProfile -File eng/verify-unsigned-packages.ps1 -PackageDirectory artifacts/release
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $PackageDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$directory = [IO.Path]::GetFullPath($PackageDirectory)
if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
    throw 'Release package directory does not exist.'
}

$manifestName = 'SHA256SUMS.unsigned'
$manifestPath = Join-Path $directory $manifestName
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Missing unsigned checksum manifest.'
}
$manifest = Get-Item -LiteralPath $manifestPath -Force
if ($manifest.PSIsContainer -or ($manifest.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Unsigned checksum manifest must be a regular file.'
}

$lines = [IO.File]::ReadAllLines($manifestPath)
if ($lines.Count -eq 0) {
    throw 'Unsigned checksum manifest is empty.'
}
$hashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$aliases = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
for ($index = 0; $index -lt $lines.Count; $index++) {
    # 仅接受本工作流生成的平面包文件名，清单不能指定目录或自身。
    $match = [regex]::Match($lines[$index],
        '\A(?<hash>[0-9A-Fa-f]{64}) \*(?<name>[A-Za-z0-9_][A-Za-z0-9_.+-]*\.(?i:nupkg|snupkg))\z',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Invalid unsigned checksum entry at line $($index + 1)."
    }
    $name = $match.Groups['name'].Value
    if (-not $aliases.Add($name)) {
        throw "Duplicate unsigned package entry '$name'."
    }
    $hashes.Add($name, $match.Groups['hash'].Value)
}

$actualNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
    if ([string]::Equals($entry.Name, $manifestName, [StringComparison]::Ordinal)) { continue }
    if ($entry.PSIsContainer -or ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        -not $hashes.ContainsKey($entry.Name)) {
        throw "Unexpected release entry '$($entry.Name)'."
    }
    $null = $actualNames.Add($entry.Name)
}

foreach ($record in $hashes.GetEnumerator()) {
    if (-not $actualNames.Contains($record.Key)) {
        throw "Missing release package '$($record.Key)'."
    }
    $actualHash = (Get-FileHash -LiteralPath (Join-Path $directory $record.Key) -Algorithm SHA256).Hash
    if (-not [string]::Equals($actualHash, $record.Value, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA256 mismatch for '$($record.Key)'."
    }
}

Write-Host "Verified $($hashes.Count) unsigned release package checksums."
