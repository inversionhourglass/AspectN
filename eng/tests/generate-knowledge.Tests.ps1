#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot '../generate-knowledge.ps1'
& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'generate-knowledge-paths.Tests.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$base = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/knowledge-generation-tests'))
[IO.Directory]::CreateDirectory($base) | Out-Null

function Save-File($root, $path, $body) {
    $target = Join-Path $root $path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::WriteAllText($target, $body, [Text.UTF8Encoding]::new($false))
}
function Save-Capability($root, $cap) {
    $sections = @('摘要','契约','输入条件','分层能力','阶段失败','例子与边界','规范依据','实现与证据','关联与缺口')
    $body = '# 合成测试能力' + "`n`n<!-- aspectn-capability:v1`n" + ($cap | ConvertTo-Json -Depth 30) + "`n-->`n"
    foreach ($section in $sections) { $body += "`n## $section`n`n合成测试资料。`n" }
    Save-File $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md' $body
}
function Load-Capability($root) {
    $text = [IO.File]::ReadAllText((Join-Path $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md'))
    return ([regex]::Match($text, '(?s)<!-- aspectn-capability:v1\s*(.*?)\s*-->').Groups[1].Value | ConvertFrom-Json -AsHashtable)
}
function New-Fixture {
    $root = Join-Path $base ([Guid]::NewGuid().ToString('N'))
    Save-File $root 'contract.md' '# 合成测试契约'
    $reference = @{path='contract.md';section='合成条款'}
    $sections = @('摘要','契约','输入条件','分层能力','阶段失败','例子与边界','规范依据','实现与证据','关联与缺口')
    $documents = @{
        'sources.json'=@{schema_version=1;sources=@(@{id='SRC-TEST';path='contract.md';role='normative';scope='合成范围';target='working-tree';approval='approved';delegated_by=@();supersedes=@();approval_refs=@($reference)})}
        'profiles.json'=@{schema_version=1;profiles=@(@{id='feature-v1';kind='feature';required_sections=$sections;invariants=@(@{id='INVLOGIC';condition='合成适用条件'})})}
        'coverage.json'=@{schema_version=1;items=@(@{id='INV-0001';subject='尚未盘点的合成主题';origin_refs=@($reference);assessment_status='unassessed';capability_ids=@();notes='保持未知，不推断排除'})}
    }
    foreach ($name in $documents.Keys) { Save-File $root ('docs/knowledge/'+$name) ($documents[$name]|ConvertTo-Json -Depth 20) }
    $scenarios = @()
    foreach ($row in @(@('S1','supported','verified'),@('S2','excluded','unverified'),@('S3','pending','needs_reverification'),@('S4','supported','failed'))) {
        $scenario = @{id=$row[0];adapter='reflection';input_mode='bare_type';entry_point='Mapper.MapTypeUse';conditions='明确输入';target='working-tree';expected_behavior='独立预期';contract_status=$row[1];verification_status=$row[2];verification_methods=@('automated');evidence_ids=@();unverified_notes='合成覆盖缺口'}
        if($row[0] -eq 'S1'){$scenario.evidence_ids=@('EV-20261013-999');$scenario.unverified_notes=''}
        if($row[0] -eq 'S2'){$scenario.exclusion=@{reason_code='dsl_not_expressible';reason='合成语法排除';decision_refs=@($reference);failure_behavior='编译拒绝'}}
        if($row[0] -eq 'S3'){$scenario.pending_reason='等待合成裁决'}
        if($row[0] -eq 'S4'){$scenario.evidence_ids=@('EV-20261013-998')}
        $scenarios += $scenario
    }
    $cap=@{schema_version=1;id='USE-TEST-001';title='测试能力';category='type-uses';kind='feature';profile='feature-v1';norm_refs=@($reference);scenarios=$scenarios;invariant_checks=@(@{invariant_id='INVLOGIC';applicability='applicable';condition_ref=$reference;reason='合成条件';review_status='pending';reviewer=$null;reviewed_on=$null});relations=@();open_findings=@();related_findings=@()}
    Save-Capability $root $cap
    foreach($pair in @(@('999','passed','S1'),@('998','failed','S4'))) {
        $report=@{schema_version=1;id=('EV-20261013-'+$pair[0]);method='automated';scenario_refs=@(@{capability_id=$cap.id;scenario_id=$pair[2]});oracle_refs=@($reference);procedure='合成测试，不是产品证据';result=$pair[1];recorded_on='2026-10-13T00:00:00Z';environment=@{tfm='fixture'};code_state=@{head=('a'*40);dirty=$true;files=@(@{path='contract.md';sha256=(Get-FileHash -LiteralPath (Join-Path $root 'contract.md')).Hash})};dependency_scope=@{paths=@('contract.md');environment_keys=@('tfm')};revalidation_conditions='合成条件变化'}
        Save-File $root ('docs/reviews/evidence/'+$report.id+'.json') ($report|ConvertTo-Json -Depth 20)
    }
    return $root
}
function Invoke-Generator($root, $check) {
    $arguments=@('-NoProfile','-File',$generator,'-RepositoryRoot',$root)
    if($check){$arguments+='-Check'}
    $output=& pwsh @arguments 2>&1
    return @{exit=$LASTEXITCODE;output=($output -join "`n")}
}
function Assert-Equal($expected, $actual, $name) {
    if($expected -cne $actual){throw "$name：预期 $expected，实际 $actual"}
}
function Read-Generated($root, $name) {
    return [IO.File]::ReadAllText((Join-Path $root ('docs/knowledge/generated/'+$name+'.md')))
}
$passed=0
$root=New-Fixture
$result=Invoke-Generator $root $false
Assert-Equal 0 $result.exit '有效资料生成'
$matrix=Read-Generated $root 'support-matrix'
foreach($label in @('支持','排除','待裁决','已验证','未验证','需重验','验证失败','裸类型','尚未盘点的合成主题','EV-20261013-999')) {
    if(!$matrix.Contains($label)){throw "矩阵缺少 $label"}
}
$passed++
Assert-Equal 0 (Invoke-Generator $root $true).exit '已同步输出'
$before=Read-Generated $root 'support-matrix'
Assert-Equal 0 (Invoke-Generator $root $false).exit '重复生成'
Assert-Equal $before (Read-Generated $root 'support-matrix') '确定性'
$passed++
$cap=Load-Capability $root
$cap.scenarios[0].conditions='新的输入条件'
Save-Capability $root $cap
Assert-Equal 1 (Invoke-Generator $root $true).exit '发现陈旧矩阵'
Assert-Equal $before (Read-Generated $root 'support-matrix') 'Check 不修改输出'
Assert-Equal 0 (Invoke-Generator $root $false).exit '更新陈旧矩阵'
$passed++
[IO.File]::Delete((Join-Path $root 'docs/knowledge/generated/catalog.md'))
Assert-Equal 1 (Invoke-Generator $root $true).exit '发现缺失目录'
Assert-Equal 0 (Invoke-Generator $root $false).exit '恢复目录'
$passed++
$before=Read-Generated $root 'support-matrix'
Save-File $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md' '<!-- aspectn-capability:v1 {坏 JSON} -->'
Assert-Equal 1 (Invoke-Generator $root $false).exit '坏资料拒绝'
Assert-Equal $before (Read-Generated $root 'support-matrix') '坏资料不覆盖输出'
$passed++
$root=New-Fixture
Assert-Equal 0 (Invoke-Generator $root $false).exit '重复编号基线'
$before=Read-Generated $root 'support-matrix'
Copy-Item -LiteralPath (Join-Path $root 'docs/knowledge/capabilities/type-uses/USE-TEST-001.md') -Destination (Join-Path $root 'docs/knowledge/capabilities/type-uses/duplicate.md')
Assert-Equal 1 (Invoke-Generator $root $false).exit '重复编号拒绝'
Assert-Equal $before (Read-Generated $root 'support-matrix') '重复编号不覆盖输出'
$passed++
$root=New-Fixture
$cap=Load-Capability $root
$cap.scenarios[0].conditions="a|b <T>`n``x`` *T* _T_ a\b ~~Run~~"
Save-Capability $root $cap
Assert-Equal 0 (Invoke-Generator $root $false).exit '特殊字符生成'
$matrix=Read-Generated $root 'support-matrix'
foreach($encoded in @('a&#124;b','&lt;T&gt;','<br>','&#96;x&#96;','&#42;T&#42;','&#95;T&#95;','a&#92;b','&#126;&#126;Run&#126;&#126;')) {
    if(!$matrix.Contains($encoded)){throw "未正确转义 $encoded"}
}
$passed++
$root=New-Fixture
Assert-Equal 0 (Invoke-Generator $root $false).exit '越界引用基线'
$before=Read-Generated $root 'support-matrix'
$cap=Load-Capability $root
$cap.norm_refs[0].path='../../escape.md'
Save-Capability $root $cap
Assert-Equal 1 (Invoke-Generator $root $false).exit '越界引用拒绝'
Assert-Equal $before (Read-Generated $root 'support-matrix') '越界引用不覆盖输出'
$passed++
foreach ($invalid in @('profile','invariants','exclusion','report','duplicate_json_key')) {
    $root=New-Fixture
    Assert-Equal 0 (Invoke-Generator $root $false).exit "$invalid 基线"
    $before=Read-Generated $root 'support-matrix'
    $cap=Load-Capability $root
    switch ($invalid) {
        'profile' { $cap.profile='unknown-v1'; Save-Capability $root $cap }
        'invariants' { $cap.invariant_checks=@(); Save-Capability $root $cap }
        'exclusion' { $cap.scenarios[1].exclusion.reason_code='invalid'; Save-Capability $root $cap }
        'report' {
            $path='docs/reviews/evidence/EV-20261013-999.json'
            $report=[IO.File]::ReadAllText((Join-Path $root $path))|ConvertFrom-Json -AsHashtable
            $report.code_state.dirty='invalid'
            Save-File $root $path ($report|ConvertTo-Json -Depth 30)
        }
        'duplicate_json_key' {
            $path='docs/knowledge/capabilities/type-uses/USE-TEST-001.md'
            $body=[IO.File]::ReadAllText((Join-Path $root $path))
            Save-File $root $path ($body.Replace('"schema_version": 1','"schema_version": 1, "schema_version": 1'))
        }
    }
    Assert-Equal 1 (Invoke-Generator $root $false).exit "$invalid 拒绝"
    Assert-Equal $before (Read-Generated $root 'support-matrix') "$invalid 不覆盖输出"
    $passed++
}
$root=New-Fixture
$reportPath='docs/reviews/evidence/EV-20261013-999.json'
$report=[IO.File]::ReadAllText((Join-Path $root $reportPath))|ConvertFrom-Json -AsHashtable
Save-File $root 'src/Before.cs' '// 历史快照中的合成源码。'
$report.code_state.files += @{path='src/Before.cs';sha256=(Get-FileHash -LiteralPath (Join-Path $root 'src/Before.cs')).Hash}
$report.dependency_scope.paths += 'src/Before.cs'
Save-File $root $reportPath ($report|ConvertTo-Json -Depth 20)
[IO.File]::Move((Join-Path $root 'src/Before.cs'), (Join-Path $root 'src/After.cs'))
$historicalReport=[IO.File]::ReadAllText((Join-Path $root $reportPath))
Assert-Equal 0 (Invoke-Generator $root $false).exit '历史源码重命名后仍可生成'
Assert-Equal 0 (Invoke-Generator $root $true).exit '历史源码重命名后的派生检查'
Assert-Equal $historicalReport ([IO.File]::ReadAllText((Join-Path $root $reportPath))) '生成不改写历史证据'
$passed++
Write-Output "知识库生成器：$passed 项黑盒检查通过。"
