#requires -Version 7.0

function Assert-TestArtifactBase([string]$BasePath) {
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $artifacts = Join-Path $repository 'artifacts'
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $resolved = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($BasePath))
    if (!$resolved.StartsWith($artifacts+[IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw "测试夹具基路径必须位于仓库artifacts子目录内：$resolved"
    }
    # 逐级检查，避免通过基路径或其祖先中的链接到达仓库外。
    for ($ancestor = $resolved; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
        if (Test-Path -LiteralPath $ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force
            if (!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "测试夹具基路径包含非普通目录：$ancestor"
            }
        }
        if ([string]::Equals($ancestor, $repository, $comparison)) { break }
    }
    return $resolved
}

function New-TestArtifactScope([string]$BasePath) {
    $resolved = Assert-TestArtifactBase $BasePath
    [IO.Directory]::CreateDirectory($resolved) | Out-Null
    return @{ BasePath = $resolved; CreatedPaths = [Collections.Generic.List[string]]::new() }
}

function New-TestArtifactDirectory($Scope) {
    $base = Assert-TestArtifactBase $Scope.BasePath
    $path = Join-Path $base ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path -ErrorAction Stop | Out-Null
    $Scope.CreatedPaths.Add($path)
    return $path
}

function Complete-TestArtifactScope($Scope, [switch]$Succeeded, [switch]$KeepArtifacts) {
    if (!$Succeeded -or $KeepArtifacts) {
        foreach ($path in $Scope.CreatedPaths) { Write-Host "保留测试夹具：$path" }
        return
    }
    try {
        $base = Assert-TestArtifactBase $Scope.BasePath
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        $targets = [Collections.Generic.List[string]]::new()
        # 全部目标先预检，任何目标非法时不开始删除其他目录。
        foreach ($path in $Scope.CreatedPaths) {
            $resolved = [IO.Path]::GetFullPath($path)
            if (![string]::Equals([IO.Path]::GetDirectoryName($resolved), $base, $comparison) -or
                [IO.Path]::GetFileName($resolved) -cnotmatch '^[0-9a-f]{32}$') {
                throw "清理目标必须是本次登记的直接GUID子目录：$resolved"
            }
            if (!(Test-Path -LiteralPath $resolved)) { continue }
            $pending = [Collections.Generic.Stack[string]]::new()
            $pending.Push($resolved)
            while ($pending.Count) {
                $directory = Get-Item -LiteralPath $pending.Pop() -Force
                if (!$directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    throw "拒绝清理非普通目录：$($directory.FullName)"
                }
                foreach ($child in Get-ChildItem -LiteralPath $directory.FullName -Force) {
                    if ($child.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                        throw "拒绝清理包含链接的夹具：$($child.FullName)"
                    }
                    if ($child.PSIsContainer) { $pending.Push($child.FullName) }
                }
            }
            $targets.Add($resolved)
        }
        foreach ($target in $targets) { Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop }
        Write-Host "已清理本次测试夹具：$($targets.Count) 个目录。"
    } catch {
        foreach ($path in $Scope.CreatedPaths) { Write-Host "清理未完成，保留测试夹具路径：$path" }
        throw
    }
}
