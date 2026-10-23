#requires -Version 7.0
[CmdletBinding()]
param([string] $VerifierHost = 'pwsh')
$ErrorActionPreference = 'Stop'
$verifier = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../verify-packages.ps1'))
$source = [IO.File]::ReadAllText($verifier)
$tokens = $null; $parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw '包验证脚本存在解析错误。' }
$boundary = @($ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.AssignmentStatementAst] -and
    $_.Left -is [Management.Automation.Language.VariableExpressionAst] -and $_.Left.VariablePath.UserPath -eq 'consumerRoot'
})
if ($boundary.Count -ne 2) { throw '消费者目录创建及归一化布局变化，请重新核对探针。' }
# 保留真实参数绑定、ZIP读取及全部包内容检查，在消费者构建前截断。
$probeSource = $source.Substring(0, $boundary[0].Extent.StartOffset) + "`nWrite-Output 'Package metadata probe passed.'`n"
$hostPath = (Get-Command $VerifierHost -CommandType Application | Select-Object -First 1).Source
$runRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../../artifacts/package-dependency-tests/' + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$version = '1.2.3-probe'
$versions = @{ AspectN = $version; 'Mono.Cecil' = '0.11.5'; 'FodyHelpers' = '6.8.1'; 'Microsoft.CodeAnalysis.CSharp' = '4.12.0' }
$packageDependencies = [ordered]@{ AspectN = @(); 'AspectN.Cecil' = @('AspectN', 'Mono.Cecil'); 'AspectN.Cecil.Fody' = @('AspectN', 'FodyHelpers'); 'AspectN.Roslyn' = @('AspectN', 'Microsoft.CodeAnalysis.CSharp'); 'AspectN.Reflection' = @('AspectN') }
function Save-Archive($path, $entries) {
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $entries.Keys) {
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open(), [Text.UTF8Encoding]::new($false))
            try { $writer.Write($entries[$name]) } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
}
function Invoke-Probe($probe) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $hostPath; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    if ([IO.Path]::GetFileNameWithoutExtension($hostPath) -ieq 'powershell') { $null = $start.Environment.Remove('PSModulePath') }
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-File', $probe, '-ValidateExisting', '-PackageVersion', $version)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw '无法启动包内容探针。' }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) { $process.Kill($true); $process.WaitForExit(); throw '包内容探针超时。' }
        return @{ exit = $process.ExitCode; output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult() }
    } finally { $process.Dispose() }
}
function Snapshot($directory) {
    return (@(Get-ChildItem -LiteralPath $directory -File | Sort-Object Name | ForEach-Object { $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }) -join "`n")
}
$cases = @(
    @{ name = 'valid-five-packages'; change = {} },
    @{ name = 'unknown-extra'; error = 'AspectN.Cecil dependency mismatch.'; detail = 'Newtonsoft.Json'; change = { param($doc) $node = $doc.CreateElement('dependency'); $node.SetAttribute('id', 'Newtonsoft.Json'); $node.SetAttribute('version', '13.0.3'); $null = $doc.SelectSingleNode('//group').AppendChild($node) } },
    @{ name = 'known-extra'; error = 'AspectN.Cecil dependency mismatch.'; detail = 'FodyHelpers'; change = { param($doc) $node = $doc.CreateElement('dependency'); $node.SetAttribute('id', 'FodyHelpers'); $node.SetAttribute('version', '6.8.1'); $null = $doc.SelectSingleNode('//group').AppendChild($node) } },
    @{ name = 'missing-dependency'; error = 'AspectN.Cecil dependency mismatch.'; detail = 'Mono.Cecil'; change = { param($doc) foreach ($node in @($doc.SelectNodes('//dependency[@id="Mono.Cecil"]'))) { $null = $node.ParentNode.RemoveChild($node) } } },
    @{ name = 'unknown-and-version-error'; error = 'AspectN.Cecil dependency mismatch.'; detail = 'Newtonsoft.Json'; change = { param($doc) $doc.SelectSingleNode('//dependency[@id="AspectN"]').SetAttribute('version', '99.0.0'); $node = $doc.CreateElement('dependency'); $node.SetAttribute('id', 'Newtonsoft.Json'); $node.SetAttribute('version', '13.0.3'); $null = $doc.SelectSingleNode('//group').AppendChild($node) } },
    @{ name = 'missing-and-version-error'; error = 'AspectN.Cecil dependency mismatch.'; detail = 'Mono.Cecil'; change = { param($doc) $doc.SelectSingleNode('//dependency[@id="AspectN"]').SetAttribute('version', '99.0.0'); foreach ($node in @($doc.SelectNodes('//dependency[@id="Mono.Cecil"]'))) { $null = $node.ParentNode.RemoveChild($node) } } },
    @{ name = 'cecil-version-error'; error = 'AspectN.Cecil has an unexpected dependency version:'; detail = '99.0.0'; change = { param($doc) $doc.SelectSingleNode('//dependency[@id="Mono.Cecil"]').SetAttribute('version', '99.0.0') } },
    @{ name = 'core-version-error'; error = 'AspectN.Cecil has an unexpected dependency version:'; detail = '99.0.0'; change = { param($doc) $doc.SelectSingleNode('//dependency[@id="AspectN"]').SetAttribute('version', '99.0.0') } },
    @{ name = 'framework-missing-dependency'; error = 'AspectN.Cecil/.NETStandard2.0 dependency mismatch.'; detail = 'Mono.Cecil'; change = { param($doc) $node = $doc.SelectSingleNode('//dependency[@id="Mono.Cecil"]'); $null = $node.ParentNode.RemoveChild($node) } },
    @{ name = 'framework-group-count'; error = 'AspectN.Cecil must have exactly two dependency groups.'; change = { param($doc) $node = $doc.SelectSingleNode('//group'); $null = $node.ParentNode.RemoveChild($node) } },
    @{ name = 'framework-group-identity'; error = 'AspectN.Cecil has no unique dependency group for .NETStandard2.0.'; change = { param($doc) $doc.SelectSingleNode('//group').SetAttribute('targetFramework', 'net10.0') } }
)
$results = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $directory = Join-Path $runRoot ($case.name + ' with space')
    $packages = Join-Path $directory 'artifacts/packages'
    [IO.Directory]::CreateDirectory($packages) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $directory 'eng')) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'Directory.Packages.props'), '<Project><PropertyGroup><CecilVersion>0.11.5</CecilVersion><RoslynVersion>4.12.0</RoslynVersion><FodyHelpersVersion>6.8.1</FodyHelpersVersion></PropertyGroup></Project>', [Text.UTF8Encoding]::new($false))
    $probe = Join-Path $directory 'eng/verify-packages.ps1'
    [IO.File]::WriteAllText($probe, $probeSource, [Text.UTF8Encoding]::new($true))
    foreach ($id in $packageDependencies.Keys) {
        [xml] $nuspec = "<package><metadata><id>$id</id><version>$version</version><description>Description $id</description><tags>Tags $id</tags><license type='expression'>MIT</license><readme>README.md</readme><projectUrl>https://github.com/AspectN/AspectN</projectUrl><repository type='git' url='https://github.com/AspectN/AspectN' /><dependencies /></metadata></package>"
        foreach ($framework in @('.NETStandard2.0', 'net10.0')) {
            $group = $nuspec.CreateElement('group'); $group.SetAttribute('targetFramework', $framework)
            foreach ($dependencyId in $packageDependencies[$id]) {
                $node = $nuspec.CreateElement('dependency'); $node.SetAttribute('id', $dependencyId); $node.SetAttribute('version', $versions[$dependencyId]); $null = $group.AppendChild($node)
            }
            $null = $nuspec.SelectSingleNode('//dependencies').AppendChild($group)
        }
        if ($id -eq 'AspectN.Cecil') { & $case.change $nuspec }
        $entries = @{ 'README.md' = '合成包内容校验输入，不含可执行程序集。' }; $symbols = @{}
        $entries["$id.nuspec"] = $nuspec.OuterXml
        foreach ($framework in @('netstandard2.0', 'net10.0')) { $entries["lib/$framework/$id.dll"] = 'synthetic'; $entries["lib/$framework/$id.xml"] = '<doc />'; $symbols["lib/$framework/$id.pdb"] = 'synthetic' }
        Save-Archive (Join-Path $packages "$id.$version.nupkg") $entries
        Save-Archive (Join-Path $packages "$id.$version.snupkg") $symbols
    }
    $before = Snapshot $packages
    $result = Invoke-Probe $probe
    $passed = if ($case.error) { $result.exit -ne 0 -and $result.output.Contains($case.error) -and (!$case.detail -or $result.output.Contains($case.detail)) }
        else { $result.exit -eq 0 -and $result.output.Contains('Package metadata probe passed.') }
    $passed = $passed -and ($before -ceq (Snapshot $packages))
    $results.Add([ordered]@{ name = $case.name; passed = $passed; exit = $result.exit; expected_error = $case.error; output = $result.output })
    Write-Host "$($case.name): $passed"
}
$summary = [ordered]@{ verifier_host = $hostPath; verifier_sha256 = (Get-FileHash -LiteralPath $verifier).Hash.ToLowerInvariant(); cases = $results.ToArray(); passed = @($results | Where-Object passed).Count; failed = @($results | Where-Object { -not $_.passed }).Count }
[IO.File]::WriteAllText((Join-Path $runRoot 'results.json'), ($summary | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Host "包依赖诊断：$($summary.passed) 项通过，$($summary.failed) 项失败；结果：$runRoot"
if ($summary.failed) { exit 1 }
