#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('pwsh','powershell')][string]$PowerShellHost = 'pwsh',
    [switch]$KeepArtifacts
)
$ErrorActionPreference = 'Stop'
$versionScript = Join-Path $PSScriptRoot '../get-release-version.ps1'
if (!(Test-Path -LiteralPath $versionScript)) { throw '尚未实现发布版本策略：缺少get-release-version.ps1。' }
. (Join-Path $PSScriptRoot 'test-artifacts.ps1')
$scope = New-TestArtifactScope (Join-Path $PSScriptRoot '../../artifacts/release-version-tests')
$fixture = New-TestArtifactDirectory $scope
$completed = $false
$results = [Collections.Generic.List[object]]::new()
function Invoke-Version([string]$root, [string]$mode, [string]$tag) {
    $arguments = @('-NoLogo','-NoProfile','-NonInteractive')
    if ($PowerShellHost -eq 'powershell') { $arguments += @('-ExecutionPolicy','Bypass') }
    $arguments += @('-File',$versionScript,'-RepositoryRoot',$root,'-Mode',$mode)
    if ($tag) { $arguments += @('-Tag',$tag) }
    $output = & $PowerShellHost @arguments 2>&1
    return @{ exit=$LASTEXITCODE; output=($output -join "`n") }
}
function Assert-Case([bool]$passed, [string]$name, $details) {
    $results.Add([ordered]@{name=$name;passed=$passed;details=$details})
    if (!$passed) { throw "$name：$($details | ConvertTo-Json -Depth 5 -Compress)" }
}
try {
    $valid = '<Project><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix></PropertyGroup></Project>'
    $validRoot = Join-Path $fixture 'valid'
    [IO.Directory]::CreateDirectory($validRoot) | Out-Null
    $config = Join-Path $validRoot 'Directory.Build.props'
    [IO.File]::WriteAllText($config,$valid,[Text.UTF8Encoding]::new($false))
    $beforeHash = (Get-FileHash -LiteralPath $config).Hash
    $result = Invoke-Version $validRoot 'Release' 'v1.2.3'
    $version = if ($result.exit -eq 0) { $result.output | ConvertFrom-Json }
    Assert-Case ($result.exit -eq 0 -and $version.VersionPrefix -ceq '1.2.3' -and $version.VersionSuffix -ceq '' -and $version.Version -ceq '1.2.3') '正式tag与基础版本一致' $result

    foreach ($tag in @('v1.2.4','1.2.3','V1.2.3','v1.2.3-preview-123','v1.2.3+build.42','v1.2.3-rc.1+build.42','')) {
        $result = Invoke-Version $validRoot 'Release' $tag
        Assert-Case ($result.exit -ne 0 -and $result.output.Contains('Release tag must match')) ('拒绝正式tag：'+$tag) $result
    }

    $result = Invoke-Version $validRoot 'Preview' ''
    $version = if ($result.exit -eq 0) { $result.output | ConvertFrom-Json }
    Assert-Case ($result.exit -eq 0 -and $version.VersionPrefix -ceq '1.2.3' -and $version.VersionSuffix -cmatch '^preview-[0-9]{13}$' -and $version.Version -ceq ('1.2.3-'+$version.VersionSuffix)) '预发布完整版本复用同一后缀' $result
    $result = Invoke-Version $validRoot 'Preview' 'v1.2.3'
    Assert-Case ($result.exit -ne 0 -and $result.output.Contains('Preview does not accept a tag')) '预发布不混入tag版本' $result
    Assert-Case ((Get-FileHash -LiteralPath $config).Hash -ceq $beforeHash) '版本解析不修改基础配置' @{sha256=$beforeHash.ToLowerInvariant()}

    $invalid = @(
        @{name='缺少基础版本';body='<Project><PropertyGroup /></Project>';error='exactly one VersionPrefix'},
        @{name='重复基础版本';body='<Project><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix><VersionPrefix>1.2.3</VersionPrefix></PropertyGroup></Project>';error='exactly one VersionPrefix'},
        @{name='空基础版本';body='<Project><PropertyGroup><VersionPrefix> </VersionPrefix></PropertyGroup></Project>';error='three-part numeric VersionPrefix'},
        @{name='基础版本带预发布';body=$valid.Replace('1.2.3','1.2.3-rc.1');error='three-part numeric VersionPrefix'},
        @{name='基础版本带元数据';body=$valid.Replace('1.2.3','1.2.3+build.42');error='three-part numeric VersionPrefix'},
        @{name='基础版本含前导零';body=$valid.Replace('1.2.3','01.2.3');error='three-part numeric VersionPrefix'},
        @{name='基础版本含第四段';body=$valid.Replace('1.2.3','1.2.3.4');error='three-part numeric VersionPrefix'},
        @{name='基础版本数值溢出';body=$valid.Replace('1.2.3','2147483648.2.3');error='three-part numeric VersionPrefix'},
        @{name='条件属性';body=$valid.Replace('<VersionPrefix>','<VersionPrefix Condition="true">');error='unconditional'},
        @{name='条件属性组';body=$valid.Replace('<PropertyGroup>','<PropertyGroup Condition="true">');error='unconditional'},
        @{name='缺少配置文件';body=$null;error='Directory.Build.props'},
        @{name='无效XML';body='<Project>';error='Directory.Build.props'}
    )
    for ($i=0; $i -lt $invalid.Count; $i++) {
        $case = $invalid[$i]
        $root = Join-Path $fixture ('invalid-'+$i)
        [IO.Directory]::CreateDirectory($root) | Out-Null
        if ($null -ne $case.body) { [IO.File]::WriteAllText((Join-Path $root 'Directory.Build.props'),$case.body,[Text.UTF8Encoding]::new($false)) }
        $result = Invoke-Version $root 'Release' 'v1.2.3'
        Assert-Case ($result.exit -ne 0 -and $result.output.Contains($case.error)) $case.name $result
    }
    $summary = [ordered]@{host=$PowerShellHost;script_sha256=(Get-FileHash -LiteralPath $versionScript).Hash.ToLowerInvariant();passed=$results.Count;failed=0;results=$results.ToArray()}
    [IO.File]::WriteAllText((Join-Path $fixture 'results.json'),($summary | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    Write-Output "发布版本策略：$($results.Count) 项检查通过（$PowerShellHost）；结果：$fixture"
    $completed = $true
} finally {
    Complete-TestArtifactScope $scope -Succeeded:$completed -KeepArtifacts:$KeepArtifacts
}
