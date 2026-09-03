<#
.SYNOPSIS
    Checks whether a tenant has the shared libraries every generated starter script depends on,
    and stages the missing ones for creation.

.DESCRIPTION
    Nothing the scaffolder produces compiles without _Browser and Browser.Manager.BrowserManager.
    Not every tenant has them — a fresh QA or customer tenant often has neither, and an older one
    can have a revision predating helpers the templates call.

    Run this BEFORE generating scripts. It reports what is present, what is missing, and what is
    older than the version the templates were written against.

.EXAMPLE
    Test-Prerequisites.ps1
    Resolves the tenant from the registry and reports on the core libraries.

.EXAMPLE
    Test-Prerequisites.ps1 -IncludeReporting -IncludeAi -OutDir .\prereqs
    Checks every tier and copies anything missing or stale into .\prereqs for creation in PRO.
#>
[CmdletBinding()]
param(
    # Path to a tenant's R_ScriptSource. Omit to resolve from the registry.
    [string]$CachePath,

    # Tenant name, when the registry lists several in AvailableTenants.
    [string]$Tenant,

    # Copy anything missing or stale here, ready to create in PRO.
    [string]$OutDir,

    # Include _Premium, needed by DictateSection and the return command.
    [switch]$IncludeReporting,

    # Include the CapResultParser libraries, needed by PullBiomarkerResults.
    [switch]$IncludeAi,

    # Stage stale libraries too, not just missing ones.
    [switch]$IncludeStale
)

$ErrorActionPreference = 'Stop'

$prereqRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'prerequisites'
$manifestPath = Join-Path $prereqRoot 'manifest.json'

if (-not (Test-Path $manifestPath)) {
    throw "Prerequisite manifest not found at $manifestPath"
}

$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json

# ---------------------------------------------------------------- resolve the tenant cache
if (-not $CachePath) {
    $key = 'HKLM:\SOFTWARE\Voicebrook\PRO_Client'

    if (-not (Test-Path $key)) {
        throw "No -CachePath given and $key is missing. Pass -CachePath explicitly."
    }

    $p = Get-ItemProperty $key
    # @() matters: a single-value split returns a string, not an array, and $tenants[0] would
    # then index the first CHARACTER of the tenant name rather than the name itself.
    $tenants = @($p.AvailableTenants -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ })

    if ($Tenant) {
        $chosen = $Tenant
    }
    elseif ($tenants.Count -eq 1) {
        $chosen = $tenants[0]
    }
    else {
        throw ("AvailableTenants lists several tenants: {0}. Re-run with -Tenant <name>." -f ($tenants -join ', '))
    }

    $CachePath = Join-Path $p.ProgramData (Join-Path $p.ClientEnvironment (Join-Path $chosen 'VO_LocalCache\R_ScriptSource'))
    Write-Host "Tenant  : $($p.ClientEnvironment)\$chosen" -ForegroundColor Cyan
}

Write-Host "Cache   : $CachePath" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $CachePath)) {
    Write-Warning "That cache path does not exist. Treating every prerequisite as MISSING."
}

# ---------------------------------------------------------------- which tiers to check
$tiers = @('core')
if ($IncludeReporting) { $tiers += 'reporting' }
if ($IncludeAi)        { $tiers += 'ai' }

$wanted = $manifest | Where-Object { $tiers -contains $_.Tier }

# highest published revision number of a given export key in the tenant
function Get-PublishedRevision {
    param([string]$Root, [string]$Key)

    if (-not (Test-Path $Root)) { return $null }

    $files = Get-ChildItem $Root -Filter "$Key@*.xml" -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -match '@(\d+)P-' }

    if (-not $files) { return $null }

    $best = $files | Sort-Object { [int]([regex]::Match($_.Name, '@(\d+)P-').Groups[1].Value) } | Select-Object -Last 1
    [pscustomobject]@{
        Revision = [int]([regex]::Match($best.Name, '@(\d+)P-').Groups[1].Value)
        Label    = ($best.Name.Split('@')[1] -replace '\.xml$', '')
    }
}

$results = foreach ($item in $wanted) {
    $found = Get-PublishedRevision -Root $CachePath -Key $item.Key
    $bundledRev = [int]([regex]::Match($item.Revision, '^(\d+)P').Groups[1].Value)

    $status =
        if (-not $found)                         { 'MISSING' }
        elseif ($found.Revision -lt $bundledRev) { 'OLDER' }
        else                                     { 'OK' }

    [pscustomobject]@{
        Library  = $item.Key -replace '^VOScript\.Starter\.', ''
        Tier     = $item.Tier
        Type     = $item.Type
        InTenant = if ($found) { $found.Label } else { '-' }
        Bundled  = $item.Revision
        Status   = $status
        Key      = $item.Key
    }
}

$results | Format-Table Library, Tier, Type, InTenant, Bundled, Status -AutoSize

$missing = $results | Where-Object { $_.Status -eq 'MISSING' }
$stale   = $results | Where-Object { $_.Status -eq 'OLDER' }

if (-not $missing -and -not $stale) {
    Write-Host "All prerequisites present and at or above the bundled revision." -ForegroundColor Green
    return
}

if ($missing) {
    Write-Host ""
    Write-Host "MISSING - nothing the scaffolder generates will compile until these exist:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $($_.Key)   (declare as: $($_.Type))" }
}

if ($stale) {
    Write-Host ""
    Write-Host "OLDER than the revision the templates were written against. Check that the helpers" -ForegroundColor Yellow
    Write-Host "your scripts call actually exist in the tenant's copy before assuming it is fine:" -ForegroundColor Yellow
    $stale | ForEach-Object { Write-Host "  $($_.Key)  tenant $($_.InTenant)  bundled $($_.Bundled)" }
}

# ---------------------------------------------------------------- stage the source
if ($OutDir) {
    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
    $OutDir = (Resolve-Path $OutDir).Path

    $toStage = @($missing)
    if ($IncludeStale) { $toStage += $stale }

    Write-Host ""
    foreach ($r in $toStage) {
        $src = Join-Path $prereqRoot "$($r.Key).cs"
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $OutDir "$($r.Key).cs") -Force
            Write-Host "  staged  $($r.Key).cs" -ForegroundColor Green
        }
        else {
            Write-Warning "No bundled copy of $($r.Key)"
        }
    }

    Write-Host ""
    Write-Host "Create each of these in PRO before generating scripts. The ExtensionType is in the" -ForegroundColor Cyan
    Write-Host "header comment of each file and in the table above - BrowserManager is IDisposable," -ForegroundColor Cyan
    Write-Host "not ExtensionScript, and pro_script_create requires the exact declaration." -ForegroundColor Cyan
}
else {
    Write-Host ""
    Write-Host "Re-run with -OutDir <path> to stage the source for the ones you need." -ForegroundColor Cyan
}
