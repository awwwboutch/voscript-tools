<#
.SYNOPSIS
    Scaffolds the base VOScript.Starter script set for a new digital pathology system.

.EXAMPLE
    New-StarterScripts.ps1 -System Halo -Vendor "Indica Labs Halo AP" -WindowTitle "Halo AP" -OutDir . -Reporting -AiResulting

.NOTES
    Generated scripts compile as written, but every element name, keystroke and anchor is a
    placeholder marked TODO. Verify each against the live application before shipping.
#>
[CmdletBinding()]
param(
    # PascalCase system name. Becomes the namespace segment: VOScript.Starter.<System>
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string]$System,

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

$tokens = @{
    '{{System}}'         = $System
    '{{Namespace}}'      = $namespace
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

$plan = [ordered]@{}
foreach ($k in $core.Keys)      { $plan[$k] = $core[$k] }
if ($Reporting)   { foreach ($k in $reportingSet.Keys) { $plan[$k] = $reportingSet[$k] } }
if ($AiResulting) { foreach ($k in $aiSet.Keys)        { $plan[$k] = $aiSet[$k] } }

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

    $className = $plan[$templateName]
    $outPath   = Join-Path $OutDir "$namespace.$className.cs"

    if ((Test-Path $outPath) -and -not $Force) {
        $skipped += $outPath
        continue
    }

    $content = Get-Content -Raw $templatePath

    foreach ($token in $tokens.Keys) {
        $content = $content.Replace($token, $tokens[$token])
    }

    Set-Content -Path $outPath -Value $content -Encoding UTF8
    $written += $outPath
}

# Named list worksheet. Not a .cs file, so it is generated outside the plan loop above.
$listTemplate = Join-Path $templateRoot 'NamedLists.txt'

if (Test-Path $listTemplate) {
    $listOut = Join-Path $OutDir "${System}NamedLists.txt"

    if ((Test-Path $listOut) -and -not $Force) {
        $skipped += $listOut
    }
    else {
        $listContent = Get-Content -Raw $listTemplate

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
Write-Host "Scaffolded $namespace" -ForegroundColor Cyan
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
