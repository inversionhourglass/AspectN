[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Release','Preview')]
    [string]$Mode,
    [string]$Tag,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
$configPath = Join-Path ([IO.Path]::GetFullPath($RepositoryRoot)) 'Directory.Build.props'
try {
    $configuration = New-Object System.Xml.XmlDocument
    $configuration.XmlResolver = $null
    $configuration.Load($configPath)
} catch {
    throw "Cannot read Directory.Build.props: $($_.Exception.Message)"
}
$nodes = $configuration.SelectNodes('/Project/PropertyGroup/VersionPrefix')
if ($nodes.Count -ne 1) { throw 'Directory.Build.props must define exactly one VersionPrefix.' }
$node = $nodes.Item(0)
if ($node.HasAttribute('Condition') -or $node.ParentNode.HasAttribute('Condition')) {
    throw 'VersionPrefix and its PropertyGroup must be unconditional.'
}
$prefix = $node.InnerText.Trim()
[version]$parsed = $null
if ($prefix -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' -or
    -not ([version]::TryParse($prefix, [ref]$parsed))) {
    throw 'Directory.Build.props must define a three-part numeric VersionPrefix without leading zeros, suffixes, or metadata.'
}
$suffix = ''
if ($Mode -eq 'Release') {
    if ($Tag -cne ('v'+$prefix)) { throw "Release tag must match VersionPrefix exactly: expected 'v$prefix', received '$Tag'." }
} else {
    if ($Tag) { throw 'Preview does not accept a tag; its version is derived from VersionPrefix.' }
    # 一次操作只读取一次时钟；下游复用完整版本，不在打包各项目时重新生成。
    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString([Globalization.CultureInfo]::InvariantCulture)
    $suffix = 'preview-'+$timestamp
}
$fullVersion = if ($suffix) { $prefix+'-'+$suffix } else { $prefix }
[ordered]@{ VersionPrefix=$prefix; VersionSuffix=$suffix; Version=$fullVersion } | ConvertTo-Json -Compress
