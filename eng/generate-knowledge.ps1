#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
$rootPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
# 与文档预检保持同一平台规则，避免区分大小写的平台接受相邻目录别名。
$isWindowsHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
$pathComparison = if ($isWindowsHost) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$utf8 = [Text.UTF8Encoding]::new($false)
$categories = [ordered]@{
    'types'='类型与身份'; 'generics'='泛型'; 'type-uses'='类型使用位置'; 'members'='成员'
    'csharp-metadata'='C# 与元数据表示'; 'attributes'='特性记录'; 'syntax'='解析与词法'
    'semantics'='绑定与匹配'; 'cross-cutting'='横向契约'
}
$contracts = @{supported='支持';excluded='排除';pending='待裁决'}
$verifications = @{unverified='未验证';verified='已验证';needs_reverification='需重验';failed='验证失败'}
$adapters = @{core='核心';cecil='Cecil';cecil_fody='Cecil（Fody）';roslyn='Roslyn';reflection='Reflection'}
$inputs = @{neutral='中立输入';native_member='原生成员';bare_type='裸类型';source_symbol='源码符号';pe_symbol='PE 符号';reference='引用';detached='脱离原生关联'}
$assessmentLabels = @{unassessed='未盘点';documented='已建档';needs_evidence='待补证据';reviewed='已审查'}

function Resolve-RepositoryFile([string]$path) {
    if([string]::IsNullOrWhiteSpace($path) -or [IO.Path]::IsPathRooted($path)){throw "资料路径必须是仓库相对路径：$path"}
    $absolute = [IO.Path]::GetFullPath((Join-Path $rootPath $path))
    $prefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    if(!$absolute.StartsWith($prefix, $pathComparison)){throw "资料路径越界：$path"}
    if(!(Test-Path -LiteralPath $absolute -PathType Leaf)){throw "资料文件不存在：$path"}
    return $absolute
}
function Escape-Cell($value) {
    $encoded = [Net.WebUtility]::HtmlEncode([string]$value)
    return $encoded.Replace('|','&#124;').Replace('*','&#42;').Replace('_','&#95;').Replace('~','&#126;').Replace('\','&#92;').Replace([string][char]96,'&#96;').Replace('[','&#91;').Replace(']','&#93;').Replace("`r`n","<br>").Replace("`n","<br>").Replace("`r","<br>")
}
function Read-Json([string]$path) {
    return ([IO.File]::ReadAllText((Resolve-RepositoryFile $path)) | ConvertFrom-Json -AsHashtable)
}
function Link-To([string]$label, [string]$repositoryPath) {
    $basePath = Join-Path $rootPath 'docs/knowledge/generated'
    $target = Resolve-RepositoryFile $repositoryPath
    $relative = [IO.Path]::GetRelativePath($basePath, $target).Replace('\','/')
    return '[' + (Escape-Cell $label) + '](' + $relative.Replace(' ','%20') + ')'
}
function Append-Line($builder, [string]$line='') { [void]$builder.Append($line).Append("`n") }

try {
    # 复用唯一文档校验器，避免生成器维护第二套格式规则。
    $verifier = Join-Path $PSScriptRoot 'verify-documentation.ps1'
    $validationOutput = & pwsh -NoProfile -File $verifier -RepositoryRoot $rootPath -AsJson -ForGeneration
    if($LASTEXITCODE -ne 0){throw "文档预检失败：$($validationOutput -join [Environment]::NewLine)"}
    $validation = ($validationOutput -join [Environment]::NewLine) | ConvertFrom-Json -AsHashtable
    foreach($warning in $validation.warnings){Write-Warning $warning}
    $capabilities = [Collections.Generic.List[object]]::new()
    $capabilityPath = Join-Path $rootPath 'docs/knowledge/capabilities'
    $files = [IO.Directory]::GetFiles($capabilityPath, '*.md', [IO.SearchOption]::AllDirectories)
    [Array]::Sort($files, [StringComparer]::Ordinal)
    foreach($capabilityFile in $files) {
        $body = [IO.File]::ReadAllText($capabilityFile)
        $block = [regex]::Match($body, '(?s)<!--\s*aspectn-capability:v1\s+(.*?)-->')
        $cap = $block.Groups[1].Value | ConvertFrom-Json -AsHashtable
        $relative = [IO.Path]::GetRelativePath($rootPath, $capabilityFile).Replace('\','/')
        $capabilities.Add(@{data=$cap;path=$relative})
    }
    $coverage = Read-Json 'docs/knowledge/coverage.json'
    $matrix = [Text.StringBuilder]::new()
    $catalog = [Text.StringBuilder]::new()
    foreach($pair in @(@($matrix,'# 场景支持与验证矩阵'),@($catalog,'# 分类能力目录'))) {
        Append-Line $pair[0] $pair[1]; Append-Line $pair[0]
        Append-Line $pair[0] '<!-- 由 eng/generate-knowledge.ps1 生成；请修改能力条目或 coverage.json，勿手工编辑。 -->'
        Append-Line $pair[0]
        Append-Line $pair[0] '本页由唯一场景元数据派生。支持承诺与登记验证状态分开；已验证不代替当前证据适用性审查。没有某后端或版本的行不等于排除，也不代表全部输入组合均已验证。'
        Append-Line $pair[0]
    }
    foreach($category in $categories.Keys) {
        $records = @($capabilities | Where-Object {$_.data.category -ceq $category} | Sort-Object {$_.data.id})
        Append-Line $matrix ('## '+$categories[$category]); Append-Line $matrix
        Append-Line $catalog ('## '+$categories[$category]); Append-Line $catalog
        if(!$records.Count){Append-Line $matrix '当前分类尚无已建档能力。'; Append-Line $catalog '当前分类尚无已建档能力。'}else{
            Append-Line $matrix '| 能力/场景 | 后端/输入 | 目标 | 入口、条件与预期 | 契约 | 验证 | 证据与缺口 |'
            Append-Line $matrix '| --- | --- | --- | --- | --- | --- | --- |'
            Append-Line $catalog '| 能力 | 范围入口 | 场景数 |'
            Append-Line $catalog '| --- | --- | --- |'
            foreach($record in $records) {
                $cap = $record.data
                Append-Line $catalog ('| '+(Link-To $cap.id $record.path)+' | '+(Escape-Cell $cap.title)+' | '+$cap.scenarios.Count+' |')
                foreach($scenario in @($cap.scenarios|Sort-Object {[int]$_.id.Substring(1)})) {
                    $facts = '入口：'+$scenario.entry_point+"`n条件："+$scenario.conditions+"`n预期："+$scenario.expected_behavior
                    $contract = $contracts[$scenario.contract_status]
                    if($scenario.contract_status -eq 'excluded'){$contract+="`n原因："+$scenario.exclusion.reason+"`n阶段表现："+$scenario.exclusion.failure_behavior}
                    if($scenario.contract_status -eq 'pending'){$contract+="`n原因："+$scenario.pending_reason}
                    $evidenceLinks = @($scenario.evidence_ids | ForEach-Object {Link-To $_ ('docs/reviews/evidence/'+$_+'.json')}) -join '，'
                    $notes = Escape-Cell $scenario.unverified_notes
                    $evidenceCell = $evidenceLinks
                    if($notes){if($evidenceCell){$evidenceCell+='<br>'};$evidenceCell+=$notes}
                    $columns=@((Link-To ($cap.id+'/'+$scenario.id) $record.path),(Escape-Cell ($adapters[$scenario.adapter]+'/'+$inputs[$scenario.input_mode])),(Escape-Cell $scenario.target),(Escape-Cell $facts),(Escape-Cell $contract),(Escape-Cell $verifications[$scenario.verification_status]),$evidenceCell)
                    Append-Line $matrix ('| '+($columns -join ' | ')+' |')
                }
            }
        }
        Append-Line $matrix; Append-Line $catalog
    }
    Append-Line $matrix '## 盘点范围与剩余主题'; Append-Line $matrix
    Append-Line $matrix '| 编号 | 主题 | 盘点状态 | 范围及缺口 |'
    Append-Line $matrix '| --- | --- | --- | --- |'
    foreach($item in @($coverage.items|Sort-Object id)){
        Append-Line $matrix ('| '+(Escape-Cell $item.id)+' | '+(Escape-Cell $item.subject)+' | '+$assessmentLabels[$item.assessment_status]+' | '+(Escape-Cell $item.notes)+' |')
    }
    Append-Line $matrix
    Append-Line $catalog ('共 '+$capabilities.Count+' 条能力；具体承诺、输入条件、失败原因与证据见[逐场景矩阵](support-matrix.md)。')
    $outputs = [ordered]@{'support-matrix.md'=($matrix.ToString().TrimEnd([char]10)+"`n");'catalog.md'=($catalog.ToString().TrimEnd([char]10)+"`n")}
    $outputFolder = Join-Path $rootPath 'docs/knowledge/generated'
    if($Check){
        foreach($name in $outputs.Keys){
            $target = Join-Path $outputFolder $name
            if(!(Test-Path -LiteralPath $target -PathType Leaf) -or [IO.File]::ReadAllText($target).Replace("`r`n","`n") -cne $outputs[$name]){throw "派生资料缺失或陈旧：$name；请运行 eng/generate-knowledge.ps1。"}
        }
        Write-Output '知识库派生资料一致性检查通过。'
    }else{
        # 全部数据核验并在内存中生成后，才开始写入派生输出。
        [IO.Directory]::CreateDirectory($outputFolder) | Out-Null
        foreach($name in $outputs.Keys){[IO.File]::WriteAllText((Join-Path $outputFolder $name),$outputs[$name],$utf8)}
        Write-Output ('知识库已生成：'+$capabilities.Count+' 条能力。')
    }
    exit 0
}catch{
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
