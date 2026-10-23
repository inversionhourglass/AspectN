#requires -Version 7.0
[CmdletBinding()]
param([switch]$KeepArtifacts)
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'test-artifacts.ps1'
if (!(Test-Path -LiteralPath $helper)) { throw '尚未实现测试夹具生命周期：缺少test-artifacts.ps1。' }
. $helper
$suite = New-TestArtifactScope (Join-Path $PSScriptRoot '../../artifacts/test-artifact-tests')
$suiteRoot = New-TestArtifactDirectory $suite
$completed = $false
$passed = 0
function Assert-State([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
}
function Assert-CleanupRejected($scope, [string]$name) {
    $rejected = $false
    try { Complete-TestArtifactScope $scope -Succeeded }
    catch { $rejected = $true }
    Assert-State $rejected "$name 未拒绝清理。"
    foreach ($path in $scope.CreatedPaths) { Assert-State (Test-Path -LiteralPath $path) "$name 删除了未完成预检的目标。" }
}
try {
    $scope = New-TestArtifactScope (Join-Path $suiteRoot 'success')
    $first = New-TestArtifactDirectory $scope
    $second = New-TestArtifactDirectory $scope
    [IO.File]::WriteAllText((Join-Path $first 'file.txt'), '本次夹具')
    Complete-TestArtifactScope $scope -Succeeded
    Assert-State (!(Test-Path -LiteralPath $first) -and !(Test-Path -LiteralPath $second)) '成功运行未清理全部已登记夹具。'
    $passed++

    $scope = New-TestArtifactScope (Join-Path $suiteRoot 'keep')
    $path = New-TestArtifactDirectory $scope
    Complete-TestArtifactScope $scope -Succeeded -KeepArtifacts
    Assert-State (Test-Path -LiteralPath $path) 'KeepArtifacts未保留成功夹具。'
    $passed++

    $scope = New-TestArtifactScope (Join-Path $suiteRoot 'failure')
    $path = New-TestArtifactDirectory $scope
    Complete-TestArtifactScope $scope
    Assert-State (Test-Path -LiteralPath $path) '失败运行未保留夹具。'
    $passed++

    $scope = New-TestArtifactScope (Join-Path $suiteRoot 'throw')
    $path = New-TestArtifactDirectory $scope
    $originalError = ''
    try {
        try { throw '合成原始失败' }
        finally { Complete-TestArtifactScope $scope }
    } catch { $originalError = $_.Exception.Message }
    Assert-State ($originalError -ceq '合成原始失败' -and (Test-Path -LiteralPath $path)) '异常或失败现场被完成处理覆盖。'
    $passed++

    $driver = Join-Path $suiteRoot 'exit-driver.ps1'
    [IO.File]::WriteAllText($driver, @'
param($Helper, $BasePath)
. $Helper
$scope = New-TestArtifactScope $BasePath
try { $null = New-TestArtifactDirectory $scope; exit 7 }
finally { Complete-TestArtifactScope $scope }
'@)
    $exitBase = Join-Path $suiteRoot 'exit'
    $exitOutput = & pwsh -NoProfile -File $driver -Helper $helper -BasePath $exitBase 2>&1
    Assert-State ($LASTEXITCODE -eq 7 -and @(Get-ChildItem -LiteralPath $exitBase -Directory).Count -eq 1 -and ($exitOutput -join "`n").Contains('保留测试夹具')) 'exit未保留原退出码和目录位置。'
    $passed++

    $scope = New-TestArtifactScope (Join-Path $suiteRoot 'history')
    $historical = Join-Path $scope.BasePath ([Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($historical) | Out-Null
    $sentinel = Join-Path $historical 'sentinel.txt'
    [IO.File]::WriteAllText($sentinel, '历史资料')
    $current = New-TestArtifactDirectory $scope
    Complete-TestArtifactScope $scope -Succeeded
    Assert-State (([IO.File]::ReadAllText($sentinel) -ceq '历史资料') -and !(Test-Path -LiteralPath $current)) '清理影响了同级历史夹具。'
    $passed++

    foreach ($invalid in @('parent', 'prefix', 'traversal', 'nonguid', 'file')) {
        $scope = New-TestArtifactScope (Join-Path $suiteRoot $invalid)
        $current = New-TestArtifactDirectory $scope
        $target = switch ($invalid) {
            'parent' { $scope.BasePath }
            'prefix' { Join-Path ($scope.BasePath+'Suffix') ([Guid]::NewGuid().ToString('N')) }
            'traversal' { Join-Path $scope.BasePath ('../outside/'+[Guid]::NewGuid().ToString('N')) }
            'nonguid' { Join-Path $scope.BasePath 'unowned' }
            'file' { Join-Path $scope.BasePath ([Guid]::NewGuid().ToString('N')) }
        }
        if ($invalid -eq 'file') { [IO.File]::WriteAllText($target, '普通文件') }
        else { [IO.Directory]::CreateDirectory($target) | Out-Null }
        $scope.CreatedPaths.Add($target)
        Assert-CleanupRejected $scope $invalid
        $passed++
    }

    $outsideRejected = $false
    try { $null = New-TestArtifactScope ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) }
    catch { $outsideRejected = $true }
    Assert-State $outsideRejected '允许在artifacts范围以外建立清理范围。'
    $passed++

    $linkType = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
    foreach ($placement in @('descendant', 'target', 'base')) {
        $scope = New-TestArtifactScope (Join-Path $suiteRoot ('link-'+$placement))
        $current = New-TestArtifactDirectory $scope
        $destination = Join-Path $suiteRoot ('destination-'+$placement)
        [IO.Directory]::CreateDirectory($destination) | Out-Null
        $sentinel = Join-Path $destination 'sentinel.txt'
        [IO.File]::WriteAllText($sentinel, '链接目标资料')
        $link = switch ($placement) {
            'descendant' { Join-Path $current 'link' }
            'target' { Join-Path $scope.BasePath ([Guid]::NewGuid().ToString('N')) }
            'base' { Join-Path $suiteRoot 'linked-base' }
        }
        New-Item -ItemType $linkType -Path $link -Target $destination | Out-Null
        try {
            if ($placement -eq 'target') { $scope.CreatedPaths.Add($link) }
            if ($placement -eq 'base') {
                $rejected = $false
                try { $null = New-TestArtifactScope (Join-Path $link 'nested') }
                catch { $rejected = $true }
                Assert-State $rejected '未拒绝链接祖先。'
            } else { Assert-CleanupRejected $scope $placement }
            Assert-State ([IO.File]::ReadAllText($sentinel) -ceq '链接目标资料') '链接目标被修改。'
        } finally {
            # 仅解除本用例刚创建的链接；不递归删除其指向的目录。
            Remove-Item -LiteralPath $link -Force
        }
        $passed++
    }
    Write-Output "测试夹具生命周期：$passed 项检查通过。"
    $completed = $true
} finally {
    Complete-TestArtifactScope $suite -Succeeded:$completed -KeepArtifacts:$KeepArtifacts
}
