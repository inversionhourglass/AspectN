#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$verifier = Join-Path $PSScriptRoot '../verify-documentation.ps1'
$fixtureBase = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/documentation-tests'))
[IO.Directory]::CreateDirectory($fixtureBase) | Out-Null

function Write-FixtureFile($root, $path, $content) {
    $target = Join-Path $root $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::WriteAllText($target, $content)
}
function Save-Capability($root, $cap) {
    Write-FixtureFile $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md' ("# Fixture`n`n<!-- aspectn-capability:v1`n" + (ConvertTo-Json -InputObject $cap -Depth 30) + "`n-->`n`n## Summary`n`nFixture.")
}
function Update-Capability($root, [scriptblock]$change) {
    $text = [IO.File]::ReadAllText((Join-Path $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md'))
    $cap = ([regex]::Match($text, '(?s)<!-- aspectn-capability:v1\s*(.*?)\s*-->').Groups[1].Value | ConvertFrom-Json -AsHashtable)
    & $change $cap
    Save-Capability $root $cap
}
function New-Fixture($root) {
    Write-FixtureFile $root 'contract.md' '# Approved fixture contract'
    $reference = @{ path = 'contract.md'; section = 'fixture' }
    $source = @{schema_version=1; sources=@(@{id='SRC-TEST'; path='contract.md'; role='normative'; scope='fixture'; target='working-tree'; approval='approved'; delegated_by=@(); supersedes=@(); approval_refs=@($reference)})}
    $profile = @{schema_version=1; profiles=@(@{id='feature-v1'; kind='feature'; required_sections=@('Summary'); invariants=@(@{id='INVLOGIC'; condition='fixture condition'})})}
    $coverage = @{schema_version=1; items=@(@{id='INV-0001';subject='fixture';origin_refs=@($reference);assessment_status='documented';capability_ids=@('USE-TEST-001');notes='fixture'})}
    foreach ($pair in @(@('sources.json',$source),@('profiles.json',$profile),@('coverage.json',$coverage))) {
        Write-FixtureFile $root ('docs/knowledge/' + $pair[0]) (ConvertTo-Json -InputObject $pair[1] -Depth 30)
    }
    $cap = @{schema_version=1;id='USE-TEST-001';title='fixture';category='type-uses';kind='feature';profile='feature-v1';norm_refs=@($reference);
        scenarios=@(@{id='S1';adapter='core';input_mode='neutral';entry_point='fixture';conditions='valid input';target='working-tree';expected_behavior='fixture expected result';contract_status='supported';verification_status='unverified';verification_methods=@('automated');evidence_ids=@();unverified_notes='not executed'});
        invariant_checks=@(@{invariant_id='INVLOGIC';applicability='applicable';condition_ref=$reference;reason='fixture';review_status='pending';reviewer=$null;reviewed_on=$null});
        relations=@();open_findings=@();related_findings=@()}
    Save-Capability $root $cap
    Write-FixtureFile $root 'docs/usage/index.md' ("# Usage`n<!-- aspectn-usage:v1`n" + (ConvertTo-Json -InputObject @{schema_version=1;capability_ids=@('USE-TEST-001');inventory_refs=@()} -Depth 10) + "`n-->`nFixture.")
}
function Add-Finding($root, $status) {
    $f = @{schema_version=1;id='FND-20261012-001';kind='robustness_risk';status=$status;capability_ids=@('USE-TEST-001');inventory_refs=@('INV-0001');input_class='fixture';channel='none';severity='unrated';confidence='limited';blocking='no';expected='fixture';actual='fixture';impact='fixture';reproduction='fixture';countercheck='fixture';decision_refs=@(@{path='contract.md';section='fixture'});related_evidence=@();revalidation_conditions='changed input'}
    Write-FixtureFile $root 'docs/reviews/findings/FND-20261012-001.md' ("# Finding`n<!-- aspectn-finding:v1`n" + (ConvertTo-Json -InputObject $f -Depth 20) + "`n-->`nFixture.")
}
function Update-Finding($root, [scriptblock]$change) {
    $path = 'docs/reviews/findings/FND-20261012-001.md'
    $text = [IO.File]::ReadAllText((Join-Path $root $path))
    $finding = ([regex]::Match($text, '(?s)<!-- aspectn-finding:v1\s*(.*?)\s*-->').Groups[1].Value | ConvertFrom-Json -AsHashtable)
    & $change $finding
    Write-FixtureFile $root $path ("# Finding`n<!-- aspectn-finding:v1`n" + (ConvertTo-Json -InputObject $finding -Depth 20) + "`n-->`nFixture.")
}
function Add-Evidence($root) {
    $reference = @{path='contract.md';section='fixture'}
    $e = @{schema_version=1;id='EV-20261012-001';method='automated';scenario_refs=@(@{capability_id='USE-TEST-001';scenario_id='S1'});oracle_refs=@($reference);procedure='fixture procedure';result='passed';recorded_on='2026-10-12T09:00:00Z';environment=@{tfm='fixture'};code_state=@{head=('a'*40);dirty=$true;files=@(@{path='contract.md';sha256=(Get-FileHash -LiteralPath (Join-Path $root 'contract.md')).Hash})};dependency_scope=@{paths=@('contract.md');environment_keys=@('tfm')};revalidation_conditions='fixture changed'}
    Write-FixtureFile $root 'docs/reviews/evidence/EV-20261012-001.json' (ConvertTo-Json -InputObject $e -Depth 20)
}
$cases = @(
    @{Name='Valid minimal repository';Exit=0;Warning=$false;Change={param($r)}},
    @{Name='Valid dated evidence verifies its own scenario';Exit=0;Warning=$false;Change={param($r) Add-Evidence $r;Update-Capability $r {param($c) $c.scenarios[0].verification_status='verified';$c.scenarios[0].evidence_ids=@('EV-20261012-001');$c.scenarios[0].unverified_notes=''}}},
    @{Name='Duplicate capability ID';Exit=1;Warning=$false;Change={param($r) Copy-Item -LiteralPath (Join-Path $r 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md') -Destination (Join-Path $r 'docs/knowledge/capabilities/type-uses/duplicate.md')}},
    @{Name='Invalid contract status';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].contract_status='green'}}},
    @{Name='Missing scenario field';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].Remove('conditions') | Out-Null}}},
    @{Name='Scenario list must be array';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios='S1'}}},
    @{Name='Prefix/category mismatch';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.category='types'}}},
    @{Name='Unknown profile';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.profile='unknown'}}},
    @{Name='Missing required section';Exit=1;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md'; [IO.File]::WriteAllText($p,([IO.File]::ReadAllText($p).Replace('## Summary','## Other')))}},
    @{Name='Invalid adapter';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].adapter='all'}}},
    @{Name='Invalid method';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].verification_methods=@('guess')}}},
    @{Name='Unknown norm file';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.norm_refs[0].path='missing.md'}}},
    @{Name='Repository escape';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.norm_refs[0].path='../outside.md'}}},
    @{Name='Unregistered normative source';Exit=1;Warning=$false;Change={param($r) Write-FixtureFile $r 'other.md' '# Other'; Update-Capability $r {param($c) $c.norm_refs[0].path='other.md'}}},
    @{Name='Verified requires evidence';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].verification_status='verified'; $c.scenarios[0].unverified_notes=''}}},
    @{Name='Unverified requires notes';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].unverified_notes=''}}},
    @{Name='Unverified notes must be a string';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].unverified_notes=123}}},
    @{Name='Pending contract requires reason';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].contract_status='pending'}}},
    @{Name='Pending reason must be a string';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].contract_status='pending';$c.scenarios[0].pending_reason=123}}},
    @{Name='Excluded requires rationale';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].contract_status='excluded'}}},
    @{Name='Unknown evidence ID';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.scenarios[0].evidence_ids=@('EV-20261012-999')}}},
    @{Name='Confirmed N/A requires reviewer';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.invariant_checks[0].applicability='not_applicable';$c.invariant_checks[0].review_status='confirmed'}}},
    @{Name='Confirmed reviewer must be a string';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.invariant_checks[0].review_status='confirmed';$c.invariant_checks[0].reviewer=123;$c.invariant_checks[0].reviewed_on='2026-10-12'}}},
    @{Name='Profile invariants cannot be omitted';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.invariant_checks=@()}}},
    @{Name='Unknown relation ID';Exit=1;Warning=$false;Change={param($r) Update-Capability $r {param($c) $c.relations=@(@{type='depends_on';capability_id='USE-NONE-001'})}}},
    @{Name='Broken Markdown link';Exit=1;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/usage/index.md'; [IO.File]::AppendAllText($p,"`n[broken](missing.md)")}},
    @{Name='Unknown usage capability';Exit=1;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/usage/index.md';[IO.File]::WriteAllText($p,([IO.File]::ReadAllText($p).Replace('USE-TEST-001','USE-NONE-001')))}},
    @{Name='Usage declaration mandatory';Exit=1;Warning=$false;Change={param($r) Write-FixtureFile $r 'docs/usage/index.md' '# Usage without metadata'}},
    @{Name='Malformed registry JSON';Exit=1;Warning=$false;Change={param($r) Write-FixtureFile $r 'docs/knowledge/sources.json' '{ invalid'}},
    @{Name='Duplicate metadata block';Exit=1;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md';[IO.File]::AppendAllText($p,[IO.File]::ReadAllText($p))}},
    @{Name='Missing Finding only warns';Exit=0;Warning=$true;Change={param($r) Update-Capability $r {param($c) $c.open_findings=@('FND-20261012-999')}}},
    @{Name='Closed open_finding only warns';Exit=0;Warning=$true;Change={param($r) Add-Finding $r 'accepted_risk';Update-Capability $r {param($c) $c.open_findings=@('FND-20261012-001')}}},
    @{Name='Accepted risk needs decision';Exit=1;Warning=$false;Change={param($r) Add-Finding $r 'accepted_risk';$p=Join-Path $r 'docs/reviews/findings/FND-20261012-001.md';$t=[IO.File]::ReadAllText($p);$f=([regex]::Match($t,'(?s)<!-- aspectn-finding:v1\s*(.*?)\s*-->').Groups[1].Value|ConvertFrom-Json -AsHashtable);$f.decision_refs=@();Write-FixtureFile $r 'docs/reviews/findings/FND-20261012-001.md' ("# Finding`n<!-- aspectn-finding:v1`n"+(ConvertTo-Json -InputObject $f -Depth 20)+"`n-->")}},
    @{Name='Risk revalidation conditions must be a string';Exit=1;Warning=$false;Change={param($r) Add-Finding $r 'accepted_risk';Update-Finding $r {param($f) $f.revalidation_conditions=123}}},
    @{Name='Blocking reason must be a string';Exit=1;Warning=$false;Change={param($r) Add-Finding $r 'open';Update-Finding $r {param($f) $f.blocking='pending';$f.blocking_reason=123}}},
    @{Name='Historical discussion exempt';Exit=0;Warning=$false;Change={param($r) Write-FixtureFile $r 'DOCUMENTATION-KNOWLEDGE-BASE-DESIGN.md' 'old [link](does-not-exist.md)'}},
    @{Name='Fenced example links ignored';Exit=0;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/usage/index.md';[IO.File]::AppendAllText($p,"`n~~~md`n[example](absent.md)`n~~~")}},
    @{Name='Capability directory move preserves ID';Exit=0;Warning=$false;Change={param($r) $p=Join-Path $r 'docs/knowledge/capabilities/other';[IO.Directory]::CreateDirectory($p)|Out-Null;Move-Item -LiteralPath (Join-Path $r 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md') -Destination (Join-Path $p 'USE-TEST-001.md')}},
    @{Name='Strict verification rejects missing generated link';Exit=1;Warning=$false;Change={param($r) [IO.File]::AppendAllText((Join-Path $r 'docs/usage/index.md'),"`n[矩阵](../knowledge/generated/support-matrix.md)")}},
    @{Name='Generation permits known missing output links';Exit=0;Warning=$false;Generation=$true;Change={param($r) [IO.File]::AppendAllText((Join-Path $r 'docs/usage/index.md'),"`n[矩阵](../knowledge/generated/support-matrix.md) [目录](../knowledge/generated/catalog.md)")}},
    @{Name='Strict verification rejects stale generated links';Exit=1;Warning=$false;Change={param($r) Write-FixtureFile $r 'docs/knowledge/generated/support-matrix.md' '[旧链接](missing.md)'}},
    @{Name='Generation permits stale output body';Exit=0;Warning=$false;Generation=$true;Change={param($r) Write-FixtureFile $r 'docs/knowledge/generated/support-matrix.md' '[旧链接](missing.md)'}},
    @{Name='Generation rejects other broken links';Exit=1;Warning=$false;Generation=$true;Change={param($r) [IO.File]::AppendAllText((Join-Path $r 'docs/usage/index.md'),"`n[其他](../knowledge/generated/other.md)")}},
    @{Name='Generation still checks metadata references';Exit=1;Warning=$false;Generation=$true;Change={param($r) Update-Capability $r {param($c) $c.norm_refs[0].path='docs/knowledge/generated/support-matrix.md'}}},
    @{Name='Generation preserves Finding warnings';Exit=0;Warning=$true;Generation=$true;Change={param($r) Update-Capability $r {param($c) $c.open_findings=@('FND-20261012-999')}}}
)
$failures = [Collections.Generic.List[string]]::new()
foreach ($case in $cases) {
    $root = Join-Path $fixtureBase ([guid]::NewGuid().ToString('N'))
    try {
        New-Fixture $root
        & $case.Change $root
        $arguments = @('-NoProfile','-File',$verifier,'-RepositoryRoot',$root,'-AsJson')
        if ($case.Generation) { $arguments += '-ForGeneration' }
        $output = & pwsh @arguments 2>&1
        $code = $LASTEXITCODE
        $result = $null
        try { $result = ($output -join "`n") | ConvertFrom-Json } catch { }
        $unexpectedFailure = $null -ne $result -and @($result.errors | Where-Object { $_ -match 'Unexpected validator failure' }).Count -gt 0
        $errorCount = if ($null -eq $result) { 0 } else { @($result.errors).Count }
        $warningCount = if ($null -eq $result) { 0 } else { @($result.warnings).Count }
        if ($code -ne $case.Exit -or $null -eq $result -or $unexpectedFailure -or
            ($case.Exit -eq 1 -and $errorCount -eq 0) -or
            ($case.Exit -eq 0 -and $errorCount -ne 0) -or
            ($case.Warning -and $warningCount -eq 0) -or
            (!$case.Warning -and $warningCount -ne 0)) {
            $failures.Add("$($case.Name): expected exit $($case.Exit), got $code. $($output -join ' ')")
        } else { Write-Host "PASS $($case.Name)" }
    } finally {
        $absolute = [IO.Path]::GetFullPath($root)
        if (!$absolute.StartsWith($fixtureBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Fixture cleanup escaped its dedicated directory.'
        }
        if (Test-Path -LiteralPath $absolute) { Remove-Item -LiteralPath $absolute -Recurse -Force }
    }
}
if ($failures.Count) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    Write-Host "$($failures.Count) / $($cases.Count) fixture cases failed."
    exit 1
}
Write-Host "$($cases.Count) fixture cases passed."
exit 0
