#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$AsJson,
    [switch]$ForGeneration
)
$ErrorActionPreference = 'Stop'
$errors = [Collections.Generic.List[string]]::new()
$warnings = [Collections.Generic.List[string]]::new()
$counts = @{ capabilities = 0; scenarios = 0; findings = 0; evidence = 0; usage = 0 }
$root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
$isWindowsHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
$pathComparison = if ($isWindowsHost) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }

function Convert-JsonElement([Text.Json.JsonElement]$element) {
    switch ($element.ValueKind.ToString()) {
        'Object' {
            $value = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
            foreach ($property in $element.EnumerateObject()) {
                if ($value.Contains($property.Name)) { throw "Duplicate JSON key: $($property.Name)" }
                $value.Add($property.Name, (Convert-JsonElement $property.Value))
            }
            return $value
        }
        'Array' {
            $values = @($element.EnumerateArray() | ForEach-Object { Convert-JsonElement $_ })
            return ,$values
        }
        'String' { return $element.GetString() }
        'Number' {
            $integer = [long]0
            if ($element.TryGetInt64([ref]$integer)) { return $integer }
            return $element.GetDecimal()
        }
        'True' { return $true }
        'False' { return $false }
        'Null' { return $null }
        default { throw 'Unsupported JSON value.' }
    }
}
function Convert-DocumentJson([string]$text) {
    $document = [Text.Json.JsonDocument]::Parse($text)
    try { return Convert-JsonElement $document.RootElement }
    finally { $document.Dispose() }
}

function Add-Error([string]$where, [string]$message) { $errors.Add("${where}: $message") }
function Test-Object($value, [string]$where) {
    if ($value -isnot [Collections.IDictionary]) { Add-Error $where 'Expected a JSON object.'; return $false }
    return $true
}
function Test-Fields($record, $fields, $strings, $arrays, [string]$where) {
    if (!(Test-Object $record $where)) { return $false }
    $valid = $true
    foreach ($field in $fields) {
        if (!$record.Contains($field)) { Add-Error $where "Missing $field."; $valid = $false }
    }
    foreach ($field in $strings) {
        if ($record[$field] -isnot [string] -or [string]::IsNullOrWhiteSpace($record[$field])) {
            Add-Error $where "$field must be a non-empty string."; $valid = $false
        }
    }
    foreach ($field in $arrays) {
        if ($record[$field] -isnot [array]) { Add-Error $where "$field must be an array."; $valid = $false }
    }
    return $valid
}
function Test-Enum($value, $allowed, [string]$where) {
    if ($value -isnot [string] -or $allowed -cnotcontains $value) { Add-Error $where "Expected one of: $($allowed -join ', ')." }
}
function Test-Version($record, [string]$where) {
    if ($record.schema_version -isnot [long] -and $record.schema_version -isnot [int]) { Add-Error $where 'schema_version must be the integer 1.' }
    elseif ($record.schema_version -ne 1) { Add-Error $where 'Unsupported schema_version.' }
}
function Resolve-RepoPath([string]$path, [string]$where) {
    if ([string]::IsNullOrWhiteSpace($path) -or $path -match '^[\\/]|^[a-zA-Z]:|\\') { Add-Error $where 'Expected a repository-relative path with forward slashes.'; return $null }
    $full = [IO.Path]::GetFullPath((Join-Path $root $path))
    if (!$full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, $pathComparison)) { Add-Error $where "Path escapes repository: $path"; return $null }
    return $full
}
function Resolve-RepoFile([string]$path, [string]$where) {
    $full = Resolve-RepoPath $path $where
    if (!$full) { return $null }
    if (![IO.File]::Exists($full)) { Add-Error $where "File not found: $path"; return $null }
    return $full
}
function Read-JsonFile($path) {
    $full = Resolve-RepoFile $path $path
    if (!$full) { return $null }
    try {
        $record = Convert-DocumentJson ([IO.File]::ReadAllText($full))
        if (!(Test-Object $record $path)) { return $null }
        Test-Version $record $path
        return $record
    } catch { Add-Error $path "Invalid JSON: $($_.Exception.Message)"; return $null }
}
function Read-Registry($path, $key) {
    $record = Read-JsonFile $path
    if (!$record) { return @() }
    if ($record[$key] -isnot [array]) { Add-Error $path "$key must be an array."; return @() }
    return $record[$key]
}
function Find-Files($relative, $filter) {
    $full = Join-Path $root $relative
    if (Test-Path -LiteralPath $full -PathType Container) {
        Get-ChildItem -LiteralPath $full -Filter $filter -Recurse -File | ForEach-Object {
            [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        }
    }
}
function Read-Metadata($path, $kind) {
    $full = Resolve-RepoFile $path $path
    if (!$full) { return $null }
    $text = [IO.File]::ReadAllText($full)
    $blocks = [regex]::Matches($text, "(?s)<!--\s*aspectn-${kind}:v([0-9]+)\s+(.*?)-->")
    if ($blocks.Count -ne 1) { Add-Error $path "Expected exactly one aspectn-${kind} metadata block."; return $null }
    if ($blocks[0].Groups[1].Value -ne '1') { Add-Error $path 'Unsupported metadata block version.' }
    try {
        $record = Convert-DocumentJson $blocks[0].Groups[2].Value
        if (!(Test-Object $record $path)) { return $null }
        Test-Version $record $path
        return @{ metadata = $record; text = $text; path = $path }
    } catch { Add-Error $path "Invalid metadata JSON: $($_.Exception.Message)"; return $null }
}
function Test-Refs($refs, [string]$where, [switch]$Normative) {
    foreach ($reference in $refs) {
        if (!(Test-Fields $reference @('path','section') @('path','section') @() $where)) { continue }
        Resolve-RepoFile $reference.path $where | Out-Null
        if ($Normative) {
            $source = $sourcesByPath[$reference.path]
            if (!$source -or $source.role -ne 'normative' -or $source.approval -ne 'approved') {
                Add-Error $where "Normative reference is not registered as approved normative: $($reference.path)"
            }
        }
    }
}
function Register-ID($table, $record, $where, $pattern) {
    if ($record.id -isnot [string] -or $record.id -cnotmatch $pattern) { Add-Error $where 'Invalid identifier.'; return }
    if ($table.Contains($record.id)) { Add-Error $where "Duplicate identifier $($record.id)." }
    else { $table[$record.id] = $record }
}
function Test-IDRefs($ids, $table, $where) {
    foreach ($id in $ids) {
        if ($id -isnot [string] -or !$table.Contains($id)) { Add-Error $where "Unknown referenced ID: $id" }
    }
}
function Test-Date($value, $where) {
    $parsed = [DateTimeOffset]::MinValue
    if ($value -isnot [string] -or $value -notmatch '^\d{4}-\d{2}-\d{2}(?:T.*)?$' -or ![DateTimeOffset]::TryParse($value,[ref]$parsed)) {
        Add-Error $where 'Expected an ISO 8601 date/time.'
    }
}
function Test-MarkdownLinks($path) {
    $text = [IO.File]::ReadAllText((Join-Path $root $path))
    $text = [regex]::Replace($text, '(?s)<!--.*?-->', '')
    $lines = [Collections.Generic.List[string]]::new()
    $fence = ''
    foreach ($line in ($text -split '\r?\n')) {
        $match = [regex]::Match($line, '^\s{0,3}([`~])\1{2,}')
        if ($match.Success) {
            if (!$fence) { $fence = $match.Groups[1].Value }
            elseif ($fence -eq $match.Groups[1].Value) { $fence = '' }
            continue
        }
        if (!$fence) { $lines.Add($line) }
    }
    $prose = [regex]::Replace(($lines -join "`n"), '`[^`]*`', '')
    foreach ($match in [regex]::Matches($prose, '\[[^\]\r\n]*\]\(([^)\r\n]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(https?://|mailto:|#)') { continue }
        $target = ($target -split '#',2)[0].Trim('<','>')
        if (!$target) { continue }
        $relative = [IO.Path]::GetRelativePath($root, [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName((Join-Path $root $path))) ([Uri]::UnescapeDataString($target))))).Replace('\','/')
        # 生成前仅允许两个已知派生目标尚不存在；元数据引用仍按完整规则校验。
        if ($ForGeneration -and $relative -cin @('docs/knowledge/generated/catalog.md','docs/knowledge/generated/support-matrix.md')) { continue }
        Resolve-RepoFile $relative $path | Out-Null
    }
}

try {
    $sourcesByPath = @{}; $sourceIDs = @{}
    foreach ($s in @(Read-Registry 'docs/knowledge/sources.json' 'sources')) {
        if (!(Test-Fields $s @('id','path','role','scope','target','approval','delegated_by','supersedes') @('id','path','role','scope','target','approval') @('delegated_by','supersedes') 'sources')) { continue }
        Register-ID $sourceIDs $s 'sources' '^SRC-[A-Z0-9-]+$'
        Test-Enum $s.role @('normative','guide','history') $s.id
        Test-Enum $s.approval @('approved','pending','historical') $s.id
        Resolve-RepoFile $s.path $s.id | Out-Null
        if ($sourcesByPath.Contains($s.path)) { Add-Error $s.id 'Duplicate source path; use scoped references within one source record.' }
        $sourcesByPath[$s.path] = $s
        Test-Refs $s.delegated_by $s.id
        Test-Refs $s.supersedes $s.id
        if ($s.approval -eq 'approved') {
            if ($s.approval_refs -isnot [array] -or !$s.approval_refs.Count) { Add-Error $s.id 'Approved source needs approval_refs.' }
            else { Test-Refs $s.approval_refs $s.id }
        }
    }
    $profiles = @{}
    foreach ($p in @(Read-Registry 'docs/knowledge/profiles.json' 'profiles')) {
        if (!(Test-Fields $p @('id','kind','required_sections','invariants') @('id','kind') @('required_sections','invariants') 'profiles')) { continue }
        Register-ID $profiles $p 'profiles' '^[a-z][a-z0-9-]+-v1$'
        Test-Enum $p.kind @('feature','cross_cutting') $p.id
        $invariantIDs = @{}
        foreach ($i in $p.invariants) {
            if (Test-Fields $i @('id','condition') @('id','condition') @() $p.id) { Register-ID $invariantIDs $i $p.id '^INV[A-Z0-9]+$' }
        }
        $p.invariant_ids = $invariantIDs
    }
    $capabilities = @{}; $capDocs = [Collections.Generic.List[object]]::new()
    $categoryPrefixes = @{types='TYPE';generics='GEN';'type-uses'='USE';members='MEM';'csharp-metadata'='CSH';attributes='ATTR';syntax='SYN';semantics='SEM';'cross-cutting'='CROSS'}
    foreach ($path in @(Find-Files 'docs/knowledge/capabilities' '*.md')) {
        $doc = Read-Metadata $path 'capability'
        if (!$doc) { continue }; $c = $doc.metadata
        if (!(Test-Fields $c @('id','title','category','kind','profile','norm_refs','scenarios','invariant_checks','relations','open_findings','related_findings') @('id','title','category','kind','profile') @('norm_refs','scenarios','invariant_checks','relations','open_findings','related_findings') $path)) { continue }
        Register-ID $capabilities $c $path '^(TYPE|GEN|USE|MEM|CSH|ATTR|SYN|SEM|CROSS)-[A-Z0-9]+(?:-[A-Z0-9]+)*-[0-9]{3,}$'
        $prefix = $categoryPrefixes[$c.category]
        if (!$prefix -or !$c.id.StartsWith($prefix + '-', [StringComparison]::Ordinal)) { Add-Error $path 'Category/prefix mismatch.' }
        Test-Enum $c.kind @('feature','cross_cutting') $path
        if (!$c.norm_refs.Count) { Add-Error $path 'norm_refs must not be empty.' }
        Test-Refs $c.norm_refs $path -Normative
        $profile = $profiles[$c.profile]
        if (!$profile -or $profile.kind -ne $c.kind) { Add-Error $path 'Unknown or incompatible profile.' }
        else {
            foreach ($section in $profile.required_sections) {
                if ($section -isnot [string] -or $doc.text -notmatch ('(?m)^##\s+' + [regex]::Escape($section) + '\s*$')) { Add-Error $path "Missing profile section: $section" }
            }
        }
        $scenarioIDs = @{}
        if (!$c.scenarios.Count) { Add-Error $path 'At least one scenario is required.' }
        foreach ($s in $c.scenarios) {
            if (!(Test-Fields $s @('id','adapter','input_mode','entry_point','conditions','target','expected_behavior','contract_status','verification_status','verification_methods','evidence_ids','unverified_notes') @('id','adapter','input_mode','entry_point','conditions','target','expected_behavior','contract_status','verification_status') @('verification_methods','evidence_ids') $path)) { continue }
            Register-ID $scenarioIDs $s $path '^S[1-9][0-9]*$'
            Test-Enum $s.adapter @('core','cecil','cecil_fody','roslyn','reflection') $path
            Test-Enum $s.input_mode @('neutral','native_member','bare_type','source_symbol','pe_symbol','reference','detached') $path
            Test-Enum $s.contract_status @('supported','excluded','pending') $path
            Test-Enum $s.verification_status @('unverified','verified','needs_reverification','failed') $path
            foreach ($method in $s.verification_methods) { Test-Enum $method @('automated','manual') $path }
            if ($s.verification_status -eq 'verified' -and !$s.evidence_ids.Count) { Add-Error $path 'Verified scenario needs evidence.' }
            if ($s.unverified_notes -isnot [string]) { Add-Error $path 'unverified_notes must be a string; verified scenarios may use an empty string.' }
            if ($s.verification_status -ne 'verified' -and [string]::IsNullOrWhiteSpace($s.unverified_notes)) { Add-Error $path 'Unverified scenario needs unverified_notes.' }
            if ($s.contract_status -eq 'pending' -and ($s.pending_reason -isnot [string] -or [string]::IsNullOrWhiteSpace($s.pending_reason))) { Add-Error $path 'Pending contract needs a non-empty string pending_reason.' }
            if ($s.contract_status -eq 'excluded') {
                if (Test-Fields $s.exclusion @('reason_code','reason','decision_refs','failure_behavior') @('reason_code','reason','failure_behavior') @('decision_refs') $path) {
                    Test-Enum $s.exclusion.reason_code @('out_of_scope','dsl_not_expressible','explicit_exclusion','backend_or_version_limit','resource_limit') $path
                    if (!$s.exclusion.decision_refs.Count) { Add-Error $path 'Excluded contract needs decision_refs.' }
                    Test-Refs $s.exclusion.decision_refs $path
                }
            }
            if ($s.verification_status -eq 'failed' -and !$s.evidence_ids.Count -and !$c.open_findings.Count) { Add-Error $path 'Failed scenario needs evidence or Finding.' }
            $counts.scenarios++
        }
        $c.scenario_ids = $scenarioIDs
        $checkIDs = @{}
        foreach ($i in $c.invariant_checks) {
            if (!(Test-Fields $i @('invariant_id','applicability','condition_ref','reason','review_status','reviewer','reviewed_on') @('invariant_id','applicability','reason','review_status') @() $path)) { continue }
            if ($checkIDs.Contains($i.invariant_id)) { Add-Error $path 'Duplicate invariant check.' }; $checkIDs[$i.invariant_id] = $i
            Test-Enum $i.applicability @('applicable','not_applicable') $path
            Test-Enum $i.review_status @('pending','confirmed','rejected') $path
            Test-Refs @($i.condition_ref) $path
            if ($i.review_status -eq 'confirmed') {
                if ($i.reviewer -isnot [string] -or [string]::IsNullOrWhiteSpace($i.reviewer)) { Add-Error $path 'Confirmed check needs a non-empty string reviewer.' }
                Test-Date $i.reviewed_on $path
            }
            elseif ($i.review_status -eq 'pending' -and ($null -ne $i.reviewer -or $null -ne $i.reviewed_on)) { Add-Error $path 'Pending check reviewer/date must be null.' }
        }
        if ($profile) {
            foreach ($id in $profile.invariant_ids.Keys) { if (!$checkIDs.Contains($id)) { Add-Error $path "Missing invariant check $id." } }
            foreach ($id in $checkIDs.Keys) { if (!$profile.invariant_ids.Contains($id)) { Add-Error $path "Unknown profile invariant $id." } }
        }
        $capDocs.Add($doc); $counts.capabilities++
    }
    $inventory = @{}
    foreach ($i in @(Read-Registry 'docs/knowledge/coverage.json' 'items')) {
        if (!(Test-Fields $i @('id','subject','origin_refs','assessment_status','capability_ids','notes') @('id','subject','assessment_status','notes') @('origin_refs','capability_ids') 'coverage')) { continue }
        Register-ID $inventory $i 'coverage' '^INV-[0-9]{4,}$'
        Test-Enum $i.assessment_status @('unassessed','documented','needs_evidence','reviewed') $i.id
        Test-Refs $i.origin_refs $i.id
        Test-IDRefs $i.capability_ids $capabilities $i.id
        if ($i.assessment_status -ne 'unassessed' -and !$i.capability_ids.Count) { Add-Error $i.id 'Documented coverage needs capability IDs.' }
    }
    $evidence = @{}
    foreach ($path in @(Find-Files 'docs/reviews/evidence' '*.json')) {
        $e = Read-JsonFile $path
        if (!(Test-Fields $e @('id','method','scenario_refs','oracle_refs','procedure','result','recorded_on','environment','code_state','dependency_scope','revalidation_conditions') @('id','method','procedure','result','recorded_on','revalidation_conditions') @('scenario_refs','oracle_refs') $path)) { continue }
        Register-ID $evidence $e $path '^EV-[0-9]{8}-[0-9]{3,}$'
        Test-Enum $e.method @('automated','manual') $path
        Test-Enum $e.result @('passed','failed','inconclusive') $path
        Test-Date $e.recorded_on $path
        Test-Refs $e.oracle_refs $path
        Test-Object $e.environment $path | Out-Null
        # 快照记录取证时的路径与哈希，不要求重命名后的工作区保留同名文件。
        # 集合按报告隔离，缺失依赖只能引用本报告中结构有效的快照条目。
        $snapshotPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        if (Test-Fields $e.code_state @('head','dirty','files') @('head') @('files') $path) {
            if ($e.code_state.dirty -isnot [bool]) { Add-Error $path 'code_state.dirty must be boolean.' }
            if ($e.code_state.head -cnotmatch '^[a-f0-9]{40}$') { Add-Error $path 'Expected full HEAD SHA.' }
            foreach ($file in $e.code_state.files) {
                if (Test-Fields $file @('path','sha256') @('path','sha256') @() $path) {
                    $snapshotPath = Resolve-RepoPath $file.path $path
                    if ($file.sha256 -cnotmatch '^[a-fA-F0-9]{64}$') { Add-Error $path 'Invalid file SHA256.' }
                    elseif ($snapshotPath) { $snapshotPaths.Add($file.path) | Out-Null }
                }
            }
        }
        if (Test-Fields $e.dependency_scope @('paths','environment_keys') @() @('paths','environment_keys') $path) {
            foreach ($dependency in $e.dependency_scope.paths) {
                if ($dependency -is [string] -and $snapshotPaths.Contains($dependency)) { continue }
                Resolve-RepoFile $dependency $path | Out-Null
            }
        }
        foreach ($s in $e.scenario_refs) {
            if (!(Test-Fields $s @('capability_id','scenario_id') @('capability_id','scenario_id') @() $path)) { continue }
            if (!$capabilities.Contains($s.capability_id) -or !$capabilities[$s.capability_id].scenario_ids.Contains($s.scenario_id)) { Add-Error $path 'Unknown evidence scenario reference.' }
        }
        if ($e.result -eq 'passed' -and (!$e.scenario_refs.Count -or !$e.oracle_refs.Count -or !$e.code_state.files.Count)) { Add-Error $path 'Passed report needs scenarios, oracle and code snapshot.' }
        $counts.evidence++
    }
    $findings = @{}
    foreach ($path in @(Find-Files 'docs/reviews/findings' '*.md')) {
        $doc = Read-Metadata $path 'finding'; if (!$doc) { continue }; $f = $doc.metadata
        if (!(Test-Fields $f @('id','kind','status','capability_ids','inventory_refs','input_class','channel','severity','confidence','blocking','expected','actual','impact','reproduction','countercheck','decision_refs','related_evidence') @('id','kind','status','input_class','channel','severity','confidence','blocking','expected','actual','impact','reproduction','countercheck') @('capability_ids','inventory_refs','decision_refs','related_evidence') $path)) { continue }
        Register-ID $findings $f $path '^FND-[0-9]{8}-[0-9]{3,}$'
        Test-Enum $f.kind @('defect','contract_gap','evidence_gap','robustness_risk','improvement') $path
        Test-Enum $f.status @('open','pending_evidence','pending_decision','fixed','rejected','accepted_risk','documentation_resolved') $path
        Test-Enum $f.channel @('contract','robustness','none') $path
        Test-Enum $f.severity @('high','medium','low','unrated') $path
        Test-Enum $f.confidence @('confirmed','limited','unverified') $path
        Test-Enum $f.blocking @('pending','yes','no') $path
        Test-IDRefs $f.capability_ids $capabilities $path
        Test-IDRefs $f.inventory_refs $inventory $path
        Test-IDRefs $f.related_evidence $evidence $path
        Test-Refs $f.decision_refs $path
        if (!$f.capability_ids.Count -and !$f.inventory_refs.Count) { Add-Error $path 'Finding needs capability or inventory association.' }
        if ($f.status -eq 'fixed' -and !$f.related_evidence.Count) { Add-Error $path 'Fixed Finding needs regression evidence.' }
        if (@('rejected','accepted_risk','documentation_resolved') -contains $f.status -and !$f.decision_refs.Count) { Add-Error $path 'Closed Finding needs decision_refs.' }
        if ($f.status -eq 'accepted_risk' -and ($f.revalidation_conditions -isnot [string] -or [string]::IsNullOrWhiteSpace($f.revalidation_conditions))) { Add-Error $path 'Accepted risk needs a non-empty string revalidation_conditions.' }
        if ($f.blocking -ne 'no' -and ($f.blocking_reason -isnot [string] -or [string]::IsNullOrWhiteSpace($f.blocking_reason))) { Add-Error $path 'Blocking evaluation needs a non-empty string blocking_reason.' }
        $counts.findings++
    }
    foreach ($doc in $capDocs) {
        $c = $doc.metadata; $path = $doc.path
        foreach ($r in $c.relations) {
            if (Test-Fields $r @('type','capability_id') @('type','capability_id') @() $path) {
                Test-Enum $r.type @('depends_on','related_to','supersedes','superseded_by') $path
                Test-IDRefs @($r.capability_id) $capabilities $path
            }
        }
        foreach ($s in $c.scenarios) {
            if ($s -isnot [Collections.IDictionary]) { continue }
            Test-IDRefs $s.evidence_ids $evidence $path
            if ($s.verification_status -eq 'verified') {
                $matching = @($s.evidence_ids | Where-Object {
                    $report = $evidence[$_]
                    $report -and $report.result -eq 'passed' -and @($report.scenario_refs | Where-Object { $_.capability_id -eq $c.id -and $_.scenario_id -eq $s.id }).Count
                })
                if (!$matching.Count) { Add-Error $path 'Verified scenario lacks a passed report for that scenario.' }
            }
        }
        foreach ($field in @('open_findings','related_findings')) {
            foreach ($id in $c[$field]) {
                if ($id -isnot [string] -or !$findings.Contains($id)) { $warnings.Add("${path}: Finding reference missing: $id"); continue }
                if ($field -eq 'open_findings' -and @('fixed','rejected','accepted_risk','documentation_resolved') -contains $findings[$id].status) { $warnings.Add("${path}: open_findings points to closed $id") }
            }
        }
    }
    foreach ($path in @(Find-Files 'docs/usage' '*.md')) {
        $doc = Read-Metadata $path 'usage'; if (!$doc) { continue }; $u = $doc.metadata
        if (Test-Fields $u @('capability_ids','inventory_refs') @() @('capability_ids','inventory_refs') $path) {
            Test-IDRefs $u.capability_ids $capabilities $path
            Test-IDRefs $u.inventory_refs $inventory $path
        }
        $counts.usage++
    }
    $markdown = @(
        @(Find-Files 'docs/knowledge' '*.md' | Where-Object { $_ -notmatch '/templates/' })
        @(Find-Files 'docs/usage' '*.md')
        @(Find-Files 'docs/reviews/findings' '*.md')
        @($sourcesByPath.Values | Where-Object { $_.role -eq 'normative' -and $_.path.EndsWith('.md') } | ForEach-Object { $_.path })
    ) | Sort-Object -Unique
    foreach ($path in $markdown) {
        # 陈旧派生正文不能阻止重建；普通校验仍检查其全部链接。
        if ($ForGeneration -and $path -cin @('docs/knowledge/generated/catalog.md','docs/knowledge/generated/support-matrix.md')) { continue }
        if ([IO.File]::Exists((Join-Path $root $path))) { Test-MarkdownLinks $path }
    }
} catch { Add-Error 'validator' "Unexpected validator failure: $($_.Exception.Message) at line $($_.InvocationInfo.ScriptLineNumber)" }
$result = @{ errors = @($errors.ToArray()); warnings = @($warnings.ToArray()); counts = $counts }
if ($AsJson) { ConvertTo-Json -InputObject $result -Depth 10 }
else {
    foreach ($message in $errors) { Write-Host "ERROR $message" }
    foreach ($message in $warnings) { Write-Host "WARNING $message" }
    Write-Host "Documentation: $($counts.capabilities) capabilities, $($counts.scenarios) scenarios, $($counts.usage) usage chapters; $($errors.Count) errors, $($warnings.Count) warnings."
}
if ($errors.Count) { exit 1 }
exit 0
