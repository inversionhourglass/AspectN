[CmdletBinding()]
param(
    [string] $OfflineSource
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PINNED_REVISION = '03a9d1030f92a64449b6220eb45d7198d31653db'
$ORACLE_URL = 'https://github.com/inversionhourglass/Shared.Cecil.AspectN.git'
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..'))
$oracleParent = Join-Path $repositoryRoot 'artifacts\legacy-oracle'
$oracleRoot = Join-Path $oracleParent 'Shared.Cecil.AspectN'
$markerName = '.aspectn-oracle-commit'

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $output = & git -C $WorkingDirectory @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }

    return $output
}

function Assert-PinnedMarker {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $markerPath = Join-Path $Root $markerName
    if (!(Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        throw "Legacy oracle marker is missing at '$markerPath'."
    }

    $actual = [System.IO.File]::ReadAllText($markerPath).Trim()
    if ($actual -ne $PINNED_REVISION) {
        throw "Legacy oracle marker is '$actual'; expected '$PINNED_REVISION'."
    }
}

function Write-PinnedMarker {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    [System.IO.File]::WriteAllText(
        (Join-Path $Root $markerName),
        $PINNED_REVISION,
        [System.Text.Encoding]::ASCII)
}

function Get-TreeHashes {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $rootPrefix = $Root.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $hashes = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        if (!$file.FullName.StartsWith(
                $rootPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "File '$($file.FullName)' escaped oracle root '$Root'."
        }

        $relativePath = $file.FullName.Substring($rootPrefix.Length)
        $relativePath = $relativePath.Replace('\', '/')
        $hashes[$relativePath] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }

    return $hashes
}

function Assert-IdenticalTrees {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ActualRoot,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedRoot
    )

    $actual = Get-TreeHashes -Root $ActualRoot
    $expected = Get-TreeHashes -Root $ExpectedRoot
    if ($actual.Count -ne $expected.Count) {
        throw "Legacy oracle directory '$ActualRoot' is dirty."
    }

    foreach ($path in $expected.Keys) {
        if (!$actual.ContainsKey($path) -or $actual[$path] -ne $expected[$path]) {
            throw "Legacy oracle directory '$ActualRoot' is dirty at '$path'."
        }
    }
}

function Assert-GitOracle {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $actualRevision = (Invoke-Git -WorkingDirectory $Root -Arguments @(
            'rev-parse',
            'HEAD'
        ) | Select-Object -Last 1).Trim()
    if ($actualRevision -ne $PINNED_REVISION) {
        throw "Legacy oracle is at '$actualRevision'; expected '$PINNED_REVISION'."
    }

    Assert-PinnedMarker -Root $Root
    # 干净 checkout 时 git 没有输出，@() 确保结果始终是数组，避免严格模式下读取 .Count 抛错。
    $status = @(Invoke-Git -WorkingDirectory $Root -Arguments @(
            'status',
            '--porcelain',
            '--untracked-files=all'
        ))
    if ($status.Count -ne 0) {
        throw "Legacy oracle directory '$Root' is dirty:`n$($status -join [Environment]::NewLine)"
    }
}

function Add-MarkerToGitExclude {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Root
    )

    $excludePath = Join-Path $Root '.git\info\exclude'
    $excludeEntry = "/$markerName"
    $entries = [System.IO.File]::ReadAllLines($excludePath)
    if ($entries -notcontains $excludeEntry) {
        [System.IO.File]::AppendAllText(
            $excludePath,
            [Environment]::NewLine + $excludeEntry + [Environment]::NewLine,
            [System.Text.Encoding]::UTF8)
    }
}

function Export-PinnedOracle {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination,

        [Parameter(Mandatory = $true)]
        [string] $ArchivePath
    )

    Invoke-Git -WorkingDirectory $Source -Arguments @(
        'cat-file',
        '-e',
        "$PINNED_REVISION^{commit}"
    ) | Out-Null
    Invoke-Git -WorkingDirectory $Source -Arguments @(
        'archive',
        '--format=tar',
        "--output=$ArchivePath",
        $PINNED_REVISION
    ) | Out-Null
    New-Item -ItemType Directory -Path $Destination | Out-Null
    & tar -xf $ArchivePath -C $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to extract Legacy oracle archive '$ArchivePath'."
    }

    Write-PinnedMarker -Root $Destination
}

New-Item -ItemType Directory -Path $oracleParent -Force | Out-Null
$tempRoot = Join-Path $oracleParent ('.prepare-' + [Guid]::NewGuid().ToString('N'))
$tempPrefix = $oracleParent.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (!$tempRoot.StartsWith(
        $tempPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary path '$tempRoot' escaped '$oracleParent'."
}

try {
    if ([string]::IsNullOrWhiteSpace($OfflineSource)) {
        if (Test-Path -LiteralPath $oracleRoot) {
            if (!(Test-Path -LiteralPath (Join-Path $oracleRoot '.git') -PathType Container)) {
                throw "Existing Legacy oracle '$oracleRoot' is not a Git checkout."
            }

            Assert-GitOracle -Root $oracleRoot
        }
        else {
            $cloneRoot = Join-Path $tempRoot 'Shared.Cecil.AspectN'
            New-Item -ItemType Directory -Path $tempRoot | Out-Null
            $output = & git clone --no-checkout $ORACLE_URL $cloneRoot 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to clone Legacy oracle:`n$($output -join [Environment]::NewLine)"
            }

            Invoke-Git -WorkingDirectory $cloneRoot -Arguments @(
                'checkout',
                '--detach',
                $PINNED_REVISION
            ) | Out-Null
            Add-MarkerToGitExclude -Root $cloneRoot
            Write-PinnedMarker -Root $cloneRoot
            Assert-GitOracle -Root $cloneRoot
            Move-Item -LiteralPath $cloneRoot -Destination $oracleRoot
        }
    }
    else {
        $sourceRoot = (Resolve-Path -LiteralPath $OfflineSource).Path
        $exportRoot = Join-Path $tempRoot 'Shared.Cecil.AspectN'
        $archivePath = Join-Path $tempRoot 'Shared.Cecil.AspectN.tar'
        New-Item -ItemType Directory -Path $tempRoot | Out-Null
        Export-PinnedOracle `
            -Source $sourceRoot `
            -Destination $exportRoot `
            -ArchivePath $archivePath

        if (Test-Path -LiteralPath $oracleRoot) {
            if (Test-Path -LiteralPath (Join-Path $oracleRoot '.git')) {
                throw "Existing Legacy oracle '$oracleRoot' is a Git checkout, not an offline export."
            }

            Assert-PinnedMarker -Root $oracleRoot
            Assert-IdenticalTrees -ActualRoot $oracleRoot -ExpectedRoot $exportRoot
        }
        else {
            Move-Item -LiteralPath $exportRoot -Destination $oracleRoot
        }
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
        if (!$resolvedTemp.StartsWith(
                $tempPrefix,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unsafe temporary path '$resolvedTemp'."
        }

        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}

Write-Host "Legacy oracle prepared at '$oracleRoot' ($PINNED_REVISION)."
