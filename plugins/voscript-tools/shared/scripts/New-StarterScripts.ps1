<#
.SYNOPSIS
    Scaffolds the base VOScript.Starter script set for a new digital pathology system: an IMS / slide viewer
    (-Kind Ims, the default) or an LIS where reporting is the point (-Kind Lis).

.EXAMPLE
    New-StarterScripts.ps1 -System Halo -Vendor "Indica Labs Halo AP" -WindowTitle "Halo AP" -OutDir . -Reporting -AiResulting

.EXAMPLE
    New-StarterScripts.ps1 -Kind Lis -System PowerPath -Vendor "Sunquest PowerPath" -WindowTitle "^PowerPath Client" -Editor Word -Surface Desktop -ReturnToName ReturnPowerPath -OutDir .

.NOTES
    Generated scripts compile as written (check with Test-Compile.ps1 -IncludePrerequisites), but every element name,
    keystroke and anchor is a placeholder marked TODO. Verify each against the live application before shipping.

    LIS templates use line markers the scaffolder resolves from the switches: //@if <Flag>, //@if !<Flag>, //@else,
    //@endif (nestable). Flags: Word, Page, Desktop, Browser, SaveToLis.
#>
[CmdletBinding()]
param(
    # PascalCase system name. Becomes the namespace segment: VOScript.Starter.<System>
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string]$System,

    # Ims (slide viewer, the default) or Lis (reporting system: CaseNumber, DictateSection, the returns, NextCase, CaseComplete).
    [ValidateSet('Ims', 'Lis')]
    [string]$Kind = 'Ims',

    # LIS only. Word = the report is edited in Microsoft Word (ReturnToWord, then usually ReturnTo<System> to save it back).
    # Page = the LIS has its own editor, so ReturnTo<System> writes the report fields directly.
    [ValidateSet('Word', 'Page')]
    [string]$Editor = 'Word',

    # LIS only. How the LIS client itself is driven: a desktop application (window handles, keystrokes) or a browser.
    [ValidateSet('Desktop', 'Browser')]
    [string]$Surface = 'Desktop',

    # LIS with -Editor Word only: leave out ReturnTo<System>, for an LIS that needs no separate save-back from Word.
    [switch]$NoSaveToLis,

    # LIS only. Functional tier the commands go in: VOScript.Starter.<System>.<Tier>.
    [string]$Tier = 'Core',

    # Display name used in log messages and help text, e.g. "Indica Labs Halo AP"
    [string]$Vendor,

    # Browser window title fragment the application shows, e.g. "Halo AP"
    [string]$WindowTitle,

    # Where the .cs files land. Defaults to the current directory.
    [string]$OutDir = '.',

    # Named list holding the system's case types. Defaults to <System>CaseType.
    [string]$CaseTypeList,

    # Report Builder command document / fallback template.
    [string]$ReportTemplate,

    # Class name for the return-to-IMS command. Defaults to ReturnTo<System>, but the command is
    # often named after the product rather than the namespace, e.g. ReturnToHaloAP in Halo.
    [string]$ReturnToName,

    [string]$Author = 'andrew.boutcher@voicebrook.com',

    # Include DictateSection and ReturnTo<System>.
    [switch]$Reporting,

    # Include PullBiomarkerResults, the label registry, a label overlay, and the adapter.
    [switch]$AiResulting,

    # Overwrite files that already exist.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$skillRoot    = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $skillRoot 'templates'

if (-not (Test-Path $templateRoot)) {
    throw "Template directory not found at $templateRoot"
}

if (-not $Vendor)         { $Vendor         = $System }
if (-not $WindowTitle)    { $WindowTitle    = $System }
if (-not $CaseTypeList)   { $CaseTypeList   = "${System}CaseType" }
if (-not $ReportTemplate) { $ReportTemplate = "Doc-$System-Surgical" }
if (-not $ReturnToName)   { $ReturnToName   = "ReturnTo$System" }

$namespace = "VOScript.Starter.$System"
$coreNamespace = "$namespace.$Tier"

if ($Kind -eq 'Lis' -and ($Reporting -or $AiResulting)) {
    Write-Warning "-Reporting and -AiResulting are IMS options; an LIS set always includes reporting. Ignoring them."
}
if ($Kind -eq 'Lis' -and $NoSaveToLis -and $Editor -ne 'Word') {
    Write-Warning "-NoSaveToLis only applies with -Editor Word. Ignoring it."
}

# Flags the //@if markers test. Only LIS templates use them.
$flags = @{
    Word      = ($Editor -eq 'Word')
    Page      = ($Editor -eq 'Page')
    Desktop   = ($Surface -eq 'Desktop')
    Browser   = ($Surface -eq 'Browser')
    SaveToLis = ($Editor -eq 'Word' -and -not $NoSaveToLis)
}

function Resolve-Conditionals {
    param([string]$Text, [hashtable]$Flags, [string]$Name)
    $out = New-Object System.Collections.Generic.List[string]
    $stack = New-Object System.Collections.Generic.Stack[object]
    foreach ($line in ($Text -split "`r?`n")) {
        if ($line -match '^\s*//@if\s+(!?)(\w+)\s*$') {
            if (-not $Flags.ContainsKey($Matches[2])) { throw "$Name uses unknown flag '$($Matches[2])'" }
            $cond = [bool]$Flags[$Matches[2]]; if ($Matches[1]) { $cond = -not $cond }
            $parentOn = ($stack.Count -eq 0) -or $stack.Peek().On
            $stack.Push([pscustomobject]@{ Cond = $cond; ParentOn = $parentOn; On = ($parentOn -and $cond) })
            continue
        }
        if ($line -match '^\s*//@else\s*$') {
            if ($stack.Count -eq 0) { throw "$Name has //@else without //@if" }
            $top = $stack.Peek(); $top.On = ($top.ParentOn -and -not $top.Cond)
            continue
        }
        if ($line -match '^\s*//@endif\s*$') {
            if ($stack.Count -eq 0) { throw "$Name has //@endif without //@if" }
            [void]$stack.Pop()
            continue
        }
        if (($stack.Count -eq 0) -or $stack.Peek().On) { $out.Add($line) }
    }
    if ($stack.Count -gt 0) { throw "$Name has an unclosed //@if" }
    return ($out -join "`r`n")
}

$tokens = @{
    '{{System}}'         = $System
    '{{Namespace}}'      = $namespace
    '{{CoreNamespace}}'  = $coreNamespace
    '{{Tier}}'           = $Tier
    '{{Vendor}}'         = $Vendor
    '{{WindowTitle}}'    = $WindowTitle
    '{{CaseTypeList}}'   = $CaseTypeList
    '{{ReportTemplate}}' = $ReportTemplate
    '{{ReturnToName}}'   = $ReturnToName
    '{{Author}}'         = $Author
    '{{Date}}'           = (Get-Date -Format 'M/d/yyyy')
}

# template file -> generated class name (the file lands as <namespace>.<class>.cs)
$core = [ordered]@{
    '_CaseNumber.cs'           = '_CaseNumber'
    '_System.cs'               = "_$System"
    'CaseNumber.cs'            = 'CaseNumber'
    'NavigateCases.cs'         = 'NavigateCases'
    'NavigateSlides.cs'        = 'NavigateSlides'
    'RotateSlide.cs'           = 'RotateSlide'
    'SetMagnificationLevel.cs' = 'SetMagnificationLevel'
    'ShowTab.cs'               = 'ShowTab'
    'SlideMarkup.cs'           = 'SlideMarkup'
}

$reportingSet = [ordered]@{
    'DictateSection.cs'  = 'DictateSection'
    'ReturnToSystem.cs'  = $ReturnToName
}

$aiSet = [ordered]@{
    'ai/PullBiomarkerResults.cs' = 'PullBiomarkerResults'
    'ai/LabelRegistry.cs'        = "_${System}LabelRegistry"
    'ai/Adapter.cs'              = "_${System}Adapter"
    'ai/Labels.cs'               = "Labels._${System}BreastBmk169Labels"
}

# template file -> generated class name, for an LIS. Libraries sit at the namespace root; commands in the tier.
$lisLibraries = [ordered]@{
    '_CaseNumber.cs'   = '_CaseNumber'
    'lis/_System.cs'   = "_$System"
}
$lisCommands = [ordered]@{ 'lis/CaseNumber.cs' = 'CaseNumber'; 'lis/DictateSection.cs' = 'DictateSection' }
if ($flags.Word)                    { $lisCommands['lis/ReturnToWord.cs'] = 'ReturnToWord' }
if ($flags.SaveToLis -or $flags.Page) { $lisCommands['lis/ReturnToLis.cs'] = $ReturnToName }
$lisCommands['lis/NextCase.cs']     = 'NextCase'
$lisCommands['lis/CaseComplete.cs'] = 'CaseComplete'

# template -> file stem the output lands as (<stem>.cs)
$plan = [ordered]@{}
if ($Kind -eq 'Lis') {
    foreach ($k in $lisLibraries.Keys) { $plan[$k] = "$namespace.$($lisLibraries[$k])" }
    foreach ($k in $lisCommands.Keys)  { $plan[$k] = "$coreNamespace.$($lisCommands[$k])" }
}
else {
    foreach ($k in $core.Keys)      { $plan[$k] = "$namespace.$($core[$k])" }
    if ($Reporting)   { foreach ($k in $reportingSet.Keys) { $plan[$k] = "$namespace.$($reportingSet[$k])" } }
    if ($AiResulting) { foreach ($k in $aiSet.Keys)        { $plan[$k] = "$namespace.$($aiSet[$k])" } }
}

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

$OutDir = (Resolve-Path $OutDir).Path

$written = @()
$skipped = @()

foreach ($templateName in $plan.Keys) {
    $templatePath = Join-Path $templateRoot $templateName

    if (-not (Test-Path $templatePath)) {
        Write-Warning "Template missing: $templateName"
        continue
    }

    $outPath = Join-Path $OutDir "$($plan[$templateName]).cs"

    if ((Test-Path $outPath) -and -not $Force) {
        $skipped += $outPath
        continue
    }

    $content = Resolve-Conditionals -Text (Get-Content -Raw $templatePath) -Flags $flags -Name $templateName

    foreach ($token in $tokens.Keys) {
        $content = $content.Replace($token, $tokens[$token])
    }

    Set-Content -Path $outPath -Value $content -Encoding UTF8
    $written += $outPath
}

# Named list worksheet. Not a .cs file, so it is generated outside the plan loop above.
$listTemplate = Join-Path $templateRoot $(if ($Kind -eq 'Lis') { 'lis/NamedLists.txt' } else { 'NamedLists.txt' })

if (Test-Path $listTemplate) {
    $listOut = Join-Path $OutDir "${System}NamedLists.txt"

    if ((Test-Path $listOut) -and -not $Force) {
        $skipped += $listOut
    }
    else {
        $listContent = Resolve-Conditionals -Text (Get-Content -Raw $listTemplate) -Flags $flags -Name 'NamedLists.txt'

        foreach ($token in $tokens.Keys) {
            $listContent = $listContent.Replace($token, $tokens[$token])
        }

        Set-Content -Path $listOut -Value $listContent -Encoding UTF8
        $written += $listOut
    }
}
else {
    Write-Warning "Template missing: NamedLists.txt"
}

Write-Host ""
Write-Host "Scaffolded $namespace ($Kind)" -ForegroundColor Cyan
if ($Kind -eq 'Lis') {
    Write-Host "  Editor        : $Editor$(if ($flags.SaveToLis) { " (with $ReturnToName save-back)" })"
    Write-Host "  Surface       : $Surface"
    Write-Host "  Commands in   : $coreNamespace"
}
Write-Host "  Vendor        : $Vendor"
Write-Host "  Window title  : $WindowTitle"
Write-Host "  Case types    : $CaseTypeList"
Write-Host "  Template      : $ReportTemplate"
Write-Host "  Output        : $OutDir"
Write-Host ""

foreach ($f in $written) { Write-Host "  created  $(Split-Path -Leaf $f)" -ForegroundColor Green }
foreach ($f in $skipped) { Write-Host "  exists   $(Split-Path -Leaf $f) (use -Force to overwrite)" -ForegroundColor Yellow }

Write-Host ""
Write-Host "Next: resolve every TODO against the live application, then build the named lists" -ForegroundColor Cyan
Write-Host "from ${System}NamedLists.txt and create the palette entries." -ForegroundColor Cyan
Write-Host ""

$scriptFiles = $written | Where-Object { $_ -like '*.cs' }
$todoCount = 0
foreach ($f in $scriptFiles) {
    $todoCount += (Select-String -Path $f -Pattern 'TODO:' -AllMatches | Measure-Object).Count
}

$listFile  = $written | Where-Object { $_ -like '*NamedLists.txt' }
$listTodos = 0
if ($listFile) {
    $listTodos = (Select-String -Path $listFile -Pattern '^TODO ' -AllMatches | Measure-Object).Count
}

Write-Host "$todoCount TODO markers across $($scriptFiles.Count) scripts, $listTodos placeholder rows in the named list worksheet."
