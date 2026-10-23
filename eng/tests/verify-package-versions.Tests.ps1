#requires -Version 7.0
[CmdletBinding()]
param([string] $VerifierHost = 'pwsh')
$ErrorActionPreference = 'Stop'
$verifier = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../verify-packages.ps1'))
$source = [IO.File]::ReadAllText($verifier)
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw '包验证脚本存在解析错误。' }
$boundary = @($ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.AssignmentStatementAst] -and
    $_.Left -is [Management.Automation.Language.VariableExpressionAst] -and
    $_.Left.VariablePath.UserPath -eq 'buildProperties'
})
if ($boundary.Count -ne 1) { throw '包验证初始化布局变化，请重新核对探针边界。' }
# 执行真实参数绑定与版本初始化，在打包和临时消费者操作前截断。
$probeSource = $source.Substring(0, $boundary[0].Extent.EndOffset) + @'

[ordered]@{ CecilVersion = $CecilVersion; RoslynVersion = $RoslynVersion; FodyHelpersVersion = $FodyHelpersVersion; buildProperties = $buildProperties } | ConvertTo-Json -Compress
'@
$hostPath = (Get-Command $VerifierHost -CommandType Application | Select-Object -First 1).Source
$runRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../../artifacts/package-version-tests/' + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$defaults = [ordered]@{ CecilVersion = '1.2.3'; RoslynVersion = '4.5.6'; FodyHelpersVersion = '7.8.9' }
function New-Document {
    [xml] $document = '<Project><PropertyGroup /></Project>'
    foreach ($name in $defaults.Keys) {
        $node = $document.CreateElement($name)
        $node.SetAttribute('Condition', 'true')
        $node.InnerText = $defaults[$name]
        $null = $document.DocumentElement.FirstChild.AppendChild($node)
    }
    return ,$document
}
function Invoke-Probe($path, $arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $hostPath
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    if ([IO.Path]::GetFileNameWithoutExtension($hostPath) -ieq 'powershell') {
        $null = $start.Environment.Remove('PSModulePath')
    }
    foreach ($argument in (@('-NoProfile', '-NonInteractive', '-File', $path, '-PackageVersion', '0.0.0-probe') + $arguments)) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw '无法启动版本初始化探针。' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) { $process.Kill($true); $process.WaitForExit(); throw '版本初始化探针超时。' }
        return @{ exit = $process.ExitCode; output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}
$cases = @(
    @{ name = 'original-layout'; change = {} },
    @{ name = 'nodes-without-attributes'; change = { param($doc) foreach ($node in $doc.SelectNodes('/Project/PropertyGroup/*')) { $node.RemoveAllAttributes() } } },
    @{ name = 'unrelated-property-group'; change = { param($doc)
        $group = $doc.CreateElement('PropertyGroup'); $node = $doc.CreateElement('Description'); $node.InnerText = 'synthetic'; $null = $group.AppendChild($node); $null = $doc.DocumentElement.AppendChild($group)
    } },
    @{ name = 'split-groups'; change = { param($doc)
        foreach ($name in @('RoslynVersion', 'FodyHelpersVersion')) { $group = $doc.CreateElement('PropertyGroup'); $null = $group.AppendChild($doc.SelectSingleNode('/Project/PropertyGroup/' + $name)); $null = $doc.DocumentElement.AppendChild($group) }
    } },
    @{ name = 'empty-group-before-defaults'; change = { param($doc) $null = $doc.DocumentElement.PrependChild($doc.CreateElement('PropertyGroup')) } },
    @{ name = 'explicit-partial'; arguments = @('-CecilVersion', '9.0.0'); expected = @{ CecilVersion = '9.0.0' }; change = {} },
    @{ name = 'explicit-blank-falls-back'; arguments = @('-CecilVersion', ' '); change = {} },
    @{ name = 'explicit-all-no-defaults'; arguments = @('-CecilVersion', '9.0.0', '-RoslynVersion', '9.1.0', '-FodyHelpersVersion', '9.2.0'); expected = @{ CecilVersion = '9.0.0'; RoslynVersion = '9.1.0'; FodyHelpersVersion = '9.2.0' }; change = { param($doc) $doc.DocumentElement.RemoveAll() } },
    @{ name = 'explicit-skips-invalid-corresponding-default'; arguments = @('-CecilVersion', '9.0.0'); expected = @{ CecilVersion = '9.0.0' }; change = { param($doc) $node = $doc.SelectSingleNode('/Project/PropertyGroup/CecilVersion'); $null = $node.ParentNode.AppendChild($node.CloneNode($true)) } },
    @{ name = 'explicit-does-not-skip-other-default'; arguments = @('-CecilVersion', '9.0.0'); error = "Directory.Packages.props must define exactly one default 'RoslynVersion'; found 0."; change = { param($doc) $node = $doc.SelectSingleNode('/Project/PropertyGroup/RoslynVersion'); $null = $node.ParentNode.RemoveChild($node) } },
    @{ name = 'wrong-location'; error = "Directory.Packages.props must define exactly one default 'CecilVersion'; found 0."; change = { param($doc) $group = $doc.CreateElement('ItemGroup'); $null = $group.AppendChild($doc.SelectSingleNode('/Project/PropertyGroup/CecilVersion')); $null = $doc.DocumentElement.AppendChild($group) } }
)
foreach ($name in $defaults.Keys) {
    foreach ($kind in @('missing', 'duplicate', 'duplicate-same-value', 'empty', 'whitespace')) {
        $cases += @{ name = "$kind-$name"; property = $name; kind = $kind }
    }
}
$results = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $directory = Join-Path $runRoot ($case.name + ' with space')
    [IO.Directory]::CreateDirectory((Join-Path $directory 'eng')) | Out-Null
    $document = New-Document
    $expectedError = [string]$case.error
    if ($case.kind) {
        $node = $document.SelectSingleNode('/Project/PropertyGroup/' + $case.property)
        switch ($case.kind) {
            'missing' { $null = $node.ParentNode.RemoveChild($node) }
            'duplicate' { $copy = $node.CloneNode($true); $copy.InnerText = '99.0.0'; $group = $document.CreateElement('PropertyGroup'); $null = $group.AppendChild($copy); $null = $document.DocumentElement.AppendChild($group) }
            'duplicate-same-value' { $null = $node.ParentNode.AppendChild($node.CloneNode($true)) }
            'empty' { $node.InnerText = '' }
            'whitespace' { $node.InnerText = '   ' }
        }
        $expectedError = if ($case.kind -in @('empty', 'whitespace')) { "Directory.Packages.props default '$($case.property)' must not be empty." }
            else { "Directory.Packages.props must define exactly one default '$($case.property)'; found $(if ($case.kind -eq 'missing') { 0 } else { 2 })." }
    } else { & $case.change $document }
    $props = Join-Path $directory 'Directory.Packages.props'
    $probe = Join-Path $directory 'eng/verify-packages.ps1'
    [IO.File]::WriteAllText($props, $document.OuterXml, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($probe, $probeSource, [Text.UTF8Encoding]::new($true))
    $before = (Get-FileHash -LiteralPath $props).Hash
    $result = Invoke-Probe $probe @($case.arguments)
    $passed = if ($expectedError) { $result.exit -ne 0 -and $result.output.Contains($expectedError) } else {
        try {
            $actual = $result.output | ConvertFrom-Json -AsHashtable
            $expected = @{}; foreach ($name in $defaults.Keys) { $expected[$name] = $defaults[$name] }
            if ($case.expected) { foreach ($name in $case.expected.Keys) { $expected[$name] = $case.expected[$name] } }
            $result.exit -eq 0 -and (@($defaults.Keys | Where-Object { $actual[$_] -cne $expected[$_] }).Count -eq 0) -and
                (($actual.buildProperties -join '|') -ceq ((@($defaults.Keys | ForEach-Object { "-p:$($_)=$($expected[$_])" })) -join '|'))
        } catch { $false }
    }
    $passed = $passed -and ((Get-FileHash -LiteralPath $props).Hash -ceq $before)
    $results.Add([ordered]@{ name = $case.name; passed = $passed; exit = $result.exit; expected_error = $expectedError; output = $result.output })
    Write-Host "$($case.name): $passed"
}
$summary = [ordered]@{ verifier_host = $hostPath; verifier_sha256 = (Get-FileHash -LiteralPath $verifier).Hash.ToLowerInvariant(); cases = $results.ToArray(); passed = @($results | Where-Object passed).Count; failed = @($results | Where-Object { -not $_.passed }).Count }
[IO.File]::WriteAllText((Join-Path $runRoot 'results.json'), ($summary | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Host "默认版本初始化：$($summary.passed) 项通过，$($summary.failed) 项失败；结果：$runRoot"
if ($summary.failed) { exit 1 }
