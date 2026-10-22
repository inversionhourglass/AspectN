#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(10, 300)]
    [int]$OuterTimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture = Join-Path $repository ('artifacts/testhost-timeout/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
Write-Host "挂起保护探针：$fixture"

function Save-FixtureFile([string]$path, [string]$content) {
    $target = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::WriteAllText($target, $content, [Text.UTF8Encoding]::new($false))
}
function Find-ProbeProcess([string]$marker) {
    if (![IO.File]::Exists($marker)) { return $null }
    $identity = [IO.File]::ReadAllText($marker).Split('|')
    try { $probeProcess = [Diagnostics.Process]::GetProcessById([int]$identity[0]) }
    catch [ArgumentException] { return $null }
    # 用启动时间避免 PID 重用时误判或终止无关进程。
    try {
        if ($probeProcess.StartTime.ToUniversalTime().Ticks -eq [long]$identity[1]) { return $probeProcess }
    } catch [InvalidOperationException] { }
    $probeProcess.Dispose()
    return $null
}
function Invoke-Probe([string]$mode, [string]$project) {
    $caseDirectory = Join-Path $fixture $mode
    [IO.Directory]::CreateDirectory($caseDirectory) | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('test',$project,'-c','Release','--no-build','--no-restore','--logger','trx','--results-directory',$caseDirectory,'--diag',(Join-Path $caseDirectory 'vstest.log'),'-v:minimal')) {
        $start.ArgumentList.Add($argument)
    }
    $start.Environment['ASPECTN_TIMEOUT_PROBE_MODE'] = $mode
    $start.Environment['ASPECTN_TIMEOUT_PROBE_DIRECTORY'] = $caseDirectory
    $start.Environment['DOTNET_CLI_UI_LANGUAGE'] = 'en-US'
    $runner = [Diagnostics.Process]::Start($start)
    $standardOutput = $runner.StandardOutput.ReadToEndAsync()
    $standardError = $runner.StandardError.ReadToEndAsync()
    $deadlineExpired = $false
    $alive = [Collections.Generic.List[string]]::new()
    try {
        $deadlineExpired = !$runner.WaitForExit($OuterTimeoutSeconds * 1000)
        if ($deadlineExpired) {
            $runner.Kill($true)
            $runner.WaitForExit()
        }
        foreach ($name in @('host')) {
            $marker = Join-Path $caseDirectory ($name + '.pid')
            if (![IO.File]::Exists($marker)) {
                throw "$mode 未到达 $name 启动标记，不能证明挂起保护。"
            }
            $waitUntil = [DateTime]::UtcNow.AddSeconds(5)
            do {
                $probeProcess = Find-ProbeProcess $marker
                if (!$probeProcess) { break }
                $probeProcess.Dispose()
                Start-Sleep -Milliseconds 100
            } while ([DateTime]::UtcNow -lt $waitUntil)
            $probeProcess = Find-ProbeProcess $marker
            if ($probeProcess) { $alive.Add($name); $probeProcess.Dispose() }
        }
        $observation = [ordered]@{mode=$mode;runner_exit_code=$runner.ExitCode;outer_deadline_expired=$deadlineExpired;remaining_processes=@($alive.ToArray())}
        [IO.File]::WriteAllText((Join-Path $caseDirectory 'observation.json'), ($observation | ConvertTo-Json -Depth 4))
        if ($deadlineExpired) { throw "$mode 依赖外层救援期限终止，宿主挂起保护没有独立生效。" }
        if ($alive.Count) { throw "$mode 遗留进程：$($alive -join ', ')。" }
        # 异常退出时继承管道的子进程可能仍持有句柄，不能无限等待读取结束。
        if (!$standardOutput.Wait(5000) -or !$standardError.Wait(5000)) { throw "$mode 输出管道未关闭。" }
        $output = $standardOutput.GetAwaiter().GetResult()
        $errorOutput = $standardError.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $caseDirectory 'stdout.log'), $output)
        [IO.File]::WriteAllText((Join-Path $caseDirectory 'stderr.log'), $errorOutput)
        if ($mode -eq 'hang') {
            if ($runner.ExitCode -eq 0) { throw '同步死循环被错误报告为成功。' }
            $reports = @(Get-ChildItem -LiteralPath $caseDirectory -Filter '*.trx')
            if ($reports.Count -ne 1) { throw '缺少唯一的挂起运行报告。' }
            $report = [IO.File]::ReadAllText($reports[0].FullName)
            if ($report -notmatch 'The specified inactivity time of 60 seconds has elapsed') {
                throw '运行未记录预期的60秒宿主超时，普通崩溃不能算验证通过。'
            }
            $sequences = @(Get-ChildItem -LiteralPath $caseDirectory -Recurse -Filter 'Sequence_*.xml')
            if (!$sequences.Count) { throw '缺少活动测试序列。' }
            # VSTest 会在收集目录与 TRX 附件目录保留同一序列的副本。
            foreach ($sequenceFile in $sequences) {
                [xml]$sequence = [IO.File]::ReadAllText($sequenceFile.FullName)
                $active = @($sequence.TestSequence.Test | Where-Object { $_.Name -eq 'AspectN.TestHostTimeoutProbe.TimeoutProbe.RunSynchronousPropertyUsesHostProtection' -and $_.Completed -eq 'False' })
                if ($active.Count -ne 1) { throw '超时未发生在预期的同步挂起测试内。' }
            }
        } elseif ($runner.ExitCode -ne 0) { throw "$mode 正常属性失败：$output $errorOutput" }
        if (@(Get-ChildItem -LiteralPath $caseDirectory -Recurse -Filter '*.dmp').Count) { throw "$mode 意外生成内存转储。" }
        Write-Host "PASS $mode；退出码 $($runner.ExitCode)，没有遗留探针宿主。"
    } finally {
        if (!$runner.HasExited) { $runner.Kill($true); $runner.WaitForExit() }
        $runner.Dispose()
        # 仅清理本次标记且启动时间相符的进程；清理不会把失败转成通过。
        foreach ($name in @('host')) {
            $probeProcess = Find-ProbeProcess (Join-Path $caseDirectory ($name + '.pid'))
            if ($probeProcess) {
                try { $probeProcess.Kill($true); $probeProcess.WaitForExit() }
                finally { $probeProcess.Dispose() }
            }
        }
    }
}

$coreProject = Join-Path $repository 'tests/AspectN.Tests/AspectN.Tests.csproj'
$runSettings = (& dotnet msbuild $coreProject -nologo -getProperty:RunSettingsFilePath | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw '无法读取核心测试项目的默认 runsettings。' }
$escapedSettings = [Security.SecurityElement]::Escape($runSettings)
Save-FixtureFile 'probe/Probe.csproj' @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <RunSettingsFilePath>$escapedSettings</RunSettingsFilePath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
    <PackageReference Include="FsCheck.Xunit" />
  </ItemGroup>
</Project>
"@
Save-FixtureFile 'probe/TimeoutProbe.cs' @'
using System.Diagnostics;
using FsCheck.Xunit;

namespace AspectN.TestHostTimeoutProbe;

public sealed class TimeoutProbe
{
    [Property(MaxTest = 1, Replay = "20260731,1801")]
    public bool RunSynchronousPropertyUsesHostProtection()
    {
        var mode = Environment.GetEnvironmentVariable("ASPECTN_TIMEOUT_PROBE_MODE");
        var directory = Environment.GetEnvironmentVariable("ASPECTN_TIMEOUT_PROBE_DIRECTORY")!;
        using (var self = Process.GetCurrentProcess())
            File.WriteAllText(Path.Combine(directory, "host.pid"),
                $"{self.Id}|{self.StartTime.ToUniversalTime().Ticks}");
        if (mode == "return") return true;
        if (mode == "delay")
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(1200));
            return true;
        }
        // 同步、不可协作取消的循环；防线必须终止进程。
        while (true) Thread.Sleep(100);
    }
}
'@
$probeProject = Join-Path $fixture 'probe/Probe.csproj'
& dotnet build $probeProject -c Release -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal
if ($LASTEXITCODE -ne 0) { throw "合成探针构建失败：$probeProject" }
foreach ($mode in @('return','delay','hang')) {
    Invoke-Probe $mode $probeProject
}
Write-Host '测试宿主挂起保护：3 项合成场景通过。'
