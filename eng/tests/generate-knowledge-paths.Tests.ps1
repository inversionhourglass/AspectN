#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot '../generate-knowledge.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($generator, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw '生成器源码存在解析错误。' }
$main = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
if ($main.Count -ne 1) { throw '生成器主流程布局变化，请重新核对边界测试入口。' }
# 直接执行真实初始化和函数，隔离主流程的预检，防止预检掩盖边界函数退化。
$setup = [scriptblock]::Create($ast.Extent.Text.Substring(0, $main[0].Extent.StartOffset))
$hostIgnoresCase = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
$fixture = Join-Path $PSScriptRoot ('../../artifacts/knowledge-path-tests/' + [Guid]::NewGuid().ToString('N'))
$fixture = [IO.Path]::GetFullPath($fixture)
foreach ($name in @('Repo', 'repo', 'Sibling', 'RepoSuffix')) {
    $directory = Join-Path $fixture $name
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'contract.md'), '# 合成路径测试', [Text.UTF8Encoding]::new($false))
}
$repository = Join-Path $fixture 'Repo'
$cases = @(
    @{ name = '仓库内文件'; path = 'contract.md'; error = '' },
    @{ name = '仓库内归一化'; path = 'docs/../contract.md'; error = '' },
    @{ name = '大小写别名'; path = '../repo/contract.md'; error = '资料路径越界'; alias = $true },
    @{ name = '相邻目录'; path = '../Sibling/contract.md'; error = '资料路径越界' },
    @{ name = '相同前缀目录'; path = '../RepoSuffix/contract.md'; error = '资料路径越界' },
    @{ name = '缺失文件'; path = 'missing.md'; error = '资料文件不存在' },
    @{ name = '绝对路径'; path = (Join-Path $repository 'contract.md'); error = '资料路径必须是仓库相对路径' },
    @{ name = '空路径'; path = ''; error = '资料路径必须是仓库相对路径' }
)
$results = [Collections.Generic.List[object]]::new()
foreach ($mode in @('Host', 'Ordinal', 'OrdinalIgnoreCase')) {
    foreach ($case in $cases) {
        $expectedError = $case.error
        if ($case.alias -and ($mode -eq 'OrdinalIgnoreCase' -or ($mode -eq 'Host' -and $hostIgnoresCase))) {
            $expectedError = ''
        }
        $result = & {
            param($initialization, $root, $candidate, $policy)
            . $initialization -RepositoryRoot $root
            # 显式策略只用于模拟两种比较语义；Host 保留生成器自己的平台选择。
            if ($policy -ne 'Host') { $pathComparison = [StringComparison]$policy }
            try { @{ resolved = (Resolve-RepositoryFile $candidate); error = '' } }
            catch { @{ resolved = $null; error = $_.Exception.Message } }
        } $setup $repository $case.path $mode
        $passed = if ($expectedError) {
            $result.error.StartsWith($expectedError + '：', [StringComparison]::Ordinal)
        } else {
            $result.error -eq '' -and [string]::Equals($result.resolved,
                [IO.Path]::GetFullPath((Join-Path $repository $case.path)), [StringComparison]::Ordinal)
        }
        $results.Add([ordered]@{ policy = $mode; case = $case.name; passed = $passed; expected_error = $expectedError; actual_error = $result.error })
    }
}
$failed = @($results | Where-Object { -not $_.passed })
$summary = [ordered]@{
    generator_sha256 = (Get-FileHash -LiteralPath $generator -Algorithm SHA256).Hash.ToLowerInvariant()
    host_os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    cases = $results.ToArray(); passed = $results.Count - $failed.Count; failed = $failed.Count
}
[IO.File]::WriteAllText((Join-Path $fixture 'results.json'), ($summary | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
foreach ($failure in $failed) { Write-Host ($failure | ConvertTo-Json -Compress) }
Write-Output "生成器路径边界：$($summary.passed) 项通过，$($summary.failed) 项失败；结果：$fixture"
if ($failed.Count) { exit 1 }
