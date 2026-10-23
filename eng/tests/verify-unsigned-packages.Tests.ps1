#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $VerifierHost = 'pwsh'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$verifier = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../verify-unsigned-packages.ps1'))
$hostPath = (Get-Command $VerifierHost -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$runRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ("../../artifacts/unsigned-package-tests/" + [Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($runRoot) | Out-Null

function Save-Manifest($directory, [string[]] $lines, [string] $newline = "`n") {
    [IO.File]::WriteAllText((Join-Path $directory 'SHA256SUMS.unsigned'),
        (($lines -join $newline) + $newline), [Text.UTF8Encoding]::new($false))
}

function New-Fixture($directory) {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    foreach ($id in @('AspectN', 'AspectN.Cecil', 'AspectN.Cecil.Fody', 'AspectN.Roslyn', 'AspectN.Reflection')) {
        foreach ($extension in @('nupkg', 'snupkg')) {
            $name = "$id.1.2.3.$extension"
            [IO.File]::WriteAllText((Join-Path $directory $name), "合成包内容：$name", [Text.UTF8Encoding]::new($false))
        }
    }
    $lines = @(Get-ChildItem -LiteralPath $directory -File | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash + ' *' + $_.Name
    })
    Save-Manifest $directory $lines
}

function Read-Manifest($directory) {
    return [IO.File]::ReadAllLines((Join-Path $directory 'SHA256SUMS.unsigned'))
}

function Snapshot($directory) {
    return (@(Get-ChildItem -LiteralPath $directory -Force -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($directory, $_.FullName)
        if ($_.PSIsContainer) { "$relative/" }
        else { $relative + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }) -join "`n")
}

function Invoke-Verifier($directory) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $hostPath
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    if ([IO.Path]::GetFileNameWithoutExtension($hostPath) -ieq 'powershell') {
        # 旧宿主使用自己的默认模块路径，避免继承 pwsh 7 的模块路径。
        $null = $start.Environment.Remove('PSModulePath')
    }
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-File', $verifier, '-PackageDirectory', $directory)) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw '无法启动校验子进程。' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw '校验子进程超过15秒。'
        }
        return @{ exit = $process.ExitCode; output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}

$cases = @(
    @{ name = 'valid-ten-packages'; message = ''; change = {} },
    @{ name = 'lowercase-hashes'; message = ''; change = { param($dir)
        Save-Manifest $dir @((Read-Manifest $dir) | ForEach-Object { $_.Substring(0, 64).ToLowerInvariant() + $_.Substring(64) })
    } },
    @{ name = 'crlf-manifest'; message = ''; change = { param($dir) Save-Manifest $dir (Read-Manifest $dir) "`r`n" } },
    @{ name = 'reordered-manifest'; message = ''; change = { param($dir)
        $lines = @(Read-Manifest $dir); [Array]::Reverse($lines); Save-Manifest $dir $lines
    } },
    @{ name = 'missing-manifest'; message = 'Missing unsigned checksum manifest'; change = { param($dir)
        [IO.File]::Delete((Join-Path $dir 'SHA256SUMS.unsigned'))
    } },
    @{ name = 'empty-manifest'; message = 'Unsigned checksum manifest is empty'; change = { param($dir)
        [IO.File]::WriteAllText((Join-Path $dir 'SHA256SUMS.unsigned'), '')
    } },
    @{ name = 'blank-record'; message = 'Invalid unsigned checksum entry'; change = { param($dir) Save-Manifest $dir @('') } },
    @{ name = 'trailing-blank-record'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        Save-Manifest $dir (@(Read-Manifest $dir) + '')
    } },
    @{ name = 'invalid-hex'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = 'G' + $lines[0].Substring(1); Save-Manifest $dir $lines
    } },
    @{ name = 'short-hash'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = $lines[0].Substring(1); Save-Manifest $dir $lines
    } },
    @{ name = 'missing-binary-marker'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        Save-Manifest $dir @((Read-Manifest $dir) | ForEach-Object { $_.Replace(' *', '  ') })
    } },
    @{ name = 'garbage-record'; message = 'Invalid unsigned checksum entry'; change = { param($dir) Save-Manifest $dir @('invalid') } },
    @{ name = 'duplicate-record'; message = 'Duplicate unsigned package entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); Save-Manifest $dir ($lines + $lines[0])
    } },
    @{ name = 'case-alias-duplicate'; message = 'Duplicate unsigned package entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); Save-Manifest $dir ($lines + $lines[0].ToUpperInvariant())
    } },
    @{ name = 'extra-package'; message = 'Unexpected release entry'; change = { param($dir)
        [IO.File]::WriteAllText((Join-Path $dir 'Extra.1.2.3.nupkg'), 'extra')
    } },
    @{ name = 'extra-symbol-package'; message = 'Unexpected release entry'; change = { param($dir)
        [IO.File]::WriteAllText((Join-Path $dir 'Extra.1.2.3.snupkg'), 'extra')
    } },
    @{ name = 'extra-file'; message = 'Unexpected release entry'; change = { param($dir)
        [IO.File]::WriteAllText((Join-Path $dir 'extra.txt'), 'extra')
    } },
    @{ name = 'hidden-extra-file'; message = 'Unexpected release entry'; change = { param($dir)
        $path = Join-Path $dir '.extra'; [IO.File]::WriteAllText($path, 'extra')
        if ($IsWindows) { [IO.File]::SetAttributes($path, [IO.FileAttributes]::Hidden) }
    } },
    @{ name = 'extra-directory'; message = 'Unexpected release entry'; change = { param($dir)
        [IO.Directory]::CreateDirectory((Join-Path $dir 'extra')) | Out-Null
    } },
    @{ name = 'missing-package'; message = 'Missing release package'; change = { param($dir)
        [IO.File]::Delete((Join-Path $dir 'AspectN.1.2.3.nupkg'))
    } },
    @{ name = 'missing-symbol-package'; message = 'Missing release package'; change = { param($dir)
        [IO.File]::Delete((Join-Path $dir 'AspectN.1.2.3.snupkg'))
    } },
    @{ name = 'tampered-package'; message = 'SHA256 mismatch'; change = { param($dir)
        [IO.File]::AppendAllText((Join-Path $dir 'AspectN.1.2.3.nupkg'), 'changed')
    } },
    @{ name = 'tampered-symbol-package'; message = 'SHA256 mismatch'; change = { param($dir)
        [IO.File]::WriteAllText((Join-Path $dir 'AspectN.1.2.3.snupkg'), '')
    } },
    @{ name = 'parent-path'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = $lines[0].Substring(0, 66) + '../outside.nupkg'; Save-Manifest $dir $lines
    } },
    @{ name = 'windows-parent-path'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = $lines[0].Substring(0, 66) + '..\outside.nupkg'; Save-Manifest $dir $lines
    } },
    @{ name = 'absolute-path'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = $lines[0].Substring(0, 66) + '/outside.nupkg'; Save-Manifest $dir $lines
    } },
    @{ name = 'drive-path'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); $lines[0] = $lines[0].Substring(0, 66) + 'C:\outside.nupkg'; Save-Manifest $dir $lines
    } },
    @{ name = 'missing-manifest-record'; message = 'Unexpected release entry'; change = { param($dir)
        $lines = @(Read-Manifest $dir); Save-Manifest $dir $lines[1..($lines.Count - 1)]
    } },
    @{ name = 'nonexistent-manifest-package'; message = 'Missing release package'; change = { param($dir)
        Save-Manifest $dir (@(Read-Manifest $dir) + (('0' * 64) + ' *Missing.1.2.3.nupkg'))
    } },
    @{ name = 'manifest-listed-as-package'; message = 'Invalid unsigned checksum entry'; change = { param($dir)
        Save-Manifest $dir (@(Read-Manifest $dir) + (('0' * 64) + ' *SHA256SUMS.unsigned'))
    } },
    @{ name = 'manifest-is-directory'; message = 'Unsigned checksum manifest must be a regular file'; change = { param($dir)
        $path = Join-Path $dir 'SHA256SUMS.unsigned'; [IO.File]::Delete($path); [IO.Directory]::CreateDirectory($path) | Out-Null
    } },
    @{ name = 'package-is-directory'; message = 'Unexpected release entry'; change = { param($dir)
        $path = Join-Path $dir 'AspectN.1.2.3.nupkg'; [IO.File]::Delete($path); [IO.Directory]::CreateDirectory($path) | Out-Null
    } }
)

$results = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $directory = Join-Path $runRoot ($case.name + ' with space')
    New-Fixture $directory
    & $case.change $directory
    $before = Snapshot $directory
    $result = Invoke-Verifier $directory
    $expectedSuccess = $case.message.Length -eq 0
    $passed = (($result.exit -eq 0) -eq $expectedSuccess) -and
        ($expectedSuccess -or $result.output.Contains($case.message)) -and
        ($before -ceq (Snapshot $directory))
    $results.Add([ordered]@{ name = $case.name; passed = $passed; exit = $result.exit; expected_message = $case.message; output = $result.output })
    Write-Host "$($case.name): $passed"
}

$summary = [ordered]@{
    verifier_host = $hostPath
    verifier_sha256 = $(if (Test-Path -LiteralPath $verifier -PathType Leaf) { (Get-FileHash -LiteralPath $verifier -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null })
    cases = @($results.ToArray())
    passed = @($results | Where-Object passed).Count
    failed = @($results | Where-Object { -not $_.passed }).Count
}
[IO.File]::WriteAllText((Join-Path $runRoot 'results.json'), ($summary | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Host "校验器：$($summary.passed) 项通过，$($summary.failed) 项失败；结果：$runRoot"
if ($summary.failed -ne 0) { exit 1 }
