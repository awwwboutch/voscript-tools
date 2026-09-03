<#
.SYNOPSIS
    Checks whether a tenant's shared libraries actually provide what the generated scripts call,
    and stages the bundled source for anything missing.

.DESCRIPTION
    Nothing the scaffolder produces compiles without _Browser and Browser.Manager.BrowserManager.
    Not every tenant has them, and where a tenant does have them the code can differ.

    REVISION NUMBERS ARE NOT COMPARABLE ACROSS TENANTS. PRO configuration is not centralized: a
    tenant is assembled from a starter export plus deltas taken from whichever Voicebrook
    resource's configuration, so @1P in one tenant is not the same code as @1P in another, and a
    higher number does not mean newer. This script therefore ignores revision numbers and compares
    the code — specifically, whether every member the templates call is declared in the tenant's
    copy.

.EXAMPLE
    Test-Prerequisites.ps1
    Resolves the tenant from the registry and checks the core libraries.

.EXAMPLE
    Test-Prerequisites.ps1 -IncludeReporting -IncludeAi -OutDir .\prereqs
    Checks every tier and stages anything missing or incompatible.
#>
[CmdletBinding()]
param(
    # Path to a tenant's R_ScriptSource. Omit to resolve from the registry.
    [string]$CachePath,

    # Tenant name, when AvailableTenants lists several.
    [string]$Tenant,

    # Stage the bundled source of anything missing or incompatible here.
    [string]$OutDir,

    # Include _Premium, needed by DictateSection and the return command.
    [switch]$IncludeReporting,

    # Include the CapResultParser libraries, needed by PullBiomarkerResults.
    [switch]$IncludeAi,

    # List every member difference, not just the ones the templates call.
    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

$sharedRoot   = Split-Path -Parent $PSScriptRoot
$prereqRoot   = Join-Path $sharedRoot 'prerequisites'
$templateRoot = Join-Path $sharedRoot 'templates'
$manifestPath = Join-Path $prereqRoot 'manifest.json'

if (-not (Test-Path $manifestPath)) { throw "Prerequisite manifest not found at $manifestPath" }
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json

# ------------------------------------------------------------------ resolve the tenant cache
if (-not $CachePath) {
    $key = 'HKLM:\SOFTWARE\Voicebrook\PRO_Client'
    if (-not (Test-Path $key)) { throw "No -CachePath given and $key is missing. Pass -CachePath explicitly." }

    $p = Get-ItemProperty $key
    # @() matters: a single-value split returns a string, and $tenants[0] would then index the
    # first CHARACTER of the tenant name.
    $tenants = @($p.AvailableTenants -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ })

    if     ($Tenant)             { $chosen = $Tenant }
    elseif ($tenants.Count -eq 1) { $chosen = $tenants[0] }
    else   { throw ("AvailableTenants lists several tenants: {0}. Re-run with -Tenant <name>." -f ($tenants -join ', ')) }

    $CachePath = Join-Path $p.ProgramData (Join-Path $p.ClientEnvironment (Join-Path $chosen 'VO_LocalCache\R_ScriptSource'))
    Write-Host "Tenant  : $($p.ClientEnvironment)\$chosen" -ForegroundColor Cyan
}

Write-Host "Cache   : $CachePath" -ForegroundColor Cyan
if (-not (Test-Path $CachePath)) { Write-Warning "That cache path does not exist. Every prerequisite will read as MISSING." }
Write-Host ""

# ------------------------------------------------------------------ what the templates call
# Derived from the templates at run time so it cannot drift as they change.
$calls = @{}
if (Test-Path $templateRoot) {
    $text = (Get-ChildItem $templateRoot -Recurse -Filter *.cs | Get-Content -Raw) -join "`n"

    foreach ($m in [regex]::Matches($text, '(_Browser|_Premium|_CapEngine|_TemplateDetector)\.([A-Za-z_]\w*)\s*\(')) {
        $lib = $m.Groups[1].Value; $mem = $m.Groups[2].Value
        if (-not $calls[$lib]) { $calls[$lib] = New-Object System.Collections.Generic.HashSet[string] }
        [void]$calls[$lib].Add($mem)
    }
    if ($text -match 'GetBrowserController') {
        $calls['BrowserManager'] = New-Object System.Collections.Generic.HashSet[string]
        [void]$calls['BrowserManager'].Add('GetBrowserController')
    }
}

# short name used to look up $calls, e.g. VOScript.Starter.CapResultParser._CapEngine -> _CapEngine
function Get-ShortName { param([string]$Key)
    $leaf = ($Key -split '\.')[-1]
    if ($leaf -eq 'BrowserManager') { 'BrowserManager' } else { $leaf }
}

function Get-ScriptCode { param([string]$Path)
    try { ([xml](Get-Content -Raw $Path)).RC_ScriptSource.ScriptCode } catch { $null }
}

function Get-DeclaredMembers { param([string]$Code)
    $set = New-Object System.Collections.Generic.HashSet[string]
    if (-not $Code) { return $set }
    # public/internal static or instance members, and constants
    foreach ($m in [regex]::Matches($Code, '(?m)^\s*public\s+(?:static\s+)?(?:readonly\s+)?[\w<>,\[\]\?\s\.]+?\s+([A-Za-z_]\w*)\s*[\(\{=;]')) {
        [void]$set.Add($m.Groups[1].Value)
    }
    return $set
}

function Get-Hash { param([string]$Text)
    if (-not $Text) { return $null }
    # normalize line endings and trailing whitespace so formatting noise is not a difference
    $norm = ($Text -replace "`r`n", "`n").TrimEnd()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($norm)) | ForEach-Object { $_.ToString('x2') }) -join '' }
    finally { $sha.Dispose() }
}

$tiers = @('core')
if ($IncludeReporting) { $tiers += 'reporting' }
if ($IncludeAi)        { $tiers += 'ai' }
$wanted = $manifest | Where-Object { $tiers -contains $_.Tier }

$results = foreach ($item in $wanted) {
    $short    = Get-ShortName $item.Key
    $required = if ($calls[$short]) { @($calls[$short]) } else { @() }

    $bundledCode    = if (Test-Path (Join-Path $prereqRoot "$($item.Key).cs")) { Get-Content -Raw (Join-Path $prereqRoot "$($item.Key).cs") } else { $null }
    $bundledMembers = Get-DeclaredMembers $bundledCode

    $files = @()
    if (Test-Path $CachePath) {
        $files = @(Get-ChildItem $CachePath -Filter "$($item.Key)@*.xml" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '@\d+P-' })
    }

    if (-not $files) {
        [pscustomobject]@{
            Library = $short; Tier = $item.Tier; Type = $item.Type; Found = '-'
            Status = 'MISSING'; Absent = $required; Key = $item.Key; SameCode = $false
        }
        continue
    }

    # No reliable ordering across tenants, so consider every published revision and keep the one
    # that satisfies the most of what the templates call.
    $best = $null
    foreach ($f in $files) {
        $code    = Get-ScriptCode $f.FullName
        $members = Get-DeclaredMembers $code
        $absent  = @($required | Where-Object { -not $members.Contains($_) })
        $cand = [pscustomobject]@{
            Label = ($f.Name.Split('@')[1] -replace '\.xml$',''); Absent = $absent
            Members = $members; Code = $code
        }
        if (-not $best -or $cand.Absent.Count -lt $best.Absent.Count) { $best = $cand }
    }

    $sameCode = ($null -ne $bundledCode) -and ((Get-Hash $best.Code) -eq (Get-Hash ($bundledCode -replace '(?s)^//[^\n]*\n(//[^\n]*\n)*\r?\n','')))

    $status =
        if     ($best.Absent.Count -gt 0) { 'INCOMPATIBLE' }
        elseif ($sameCode)                { 'IDENTICAL' }
        else                              { 'DIFFERS-OK' }

    [pscustomobject]@{
        Library = $short; Tier = $item.Tier; Type = $item.Type; Found = $best.Label
        Status = $status; Absent = $best.Absent; Key = $item.Key; SameCode = $sameCode
        Extra = @($bundledMembers | Where-Object { -not $best.Members.Contains($_) })
    }
}

$results | Format-Table Library, Tier, Type, Found, Status -AutoSize

Write-Host "Revision numbers are NOT compared - PRO configuration is not centralized, so @1P in" -ForegroundColor DarkGray
Write-Host "one tenant is not the same code as @1P in another. This compares declared members" -ForegroundColor DarkGray
Write-Host "against what the templates actually call." -ForegroundColor DarkGray

$missing      = @($results | Where-Object { $_.Status -eq 'MISSING' })
$incompatible = @($results | Where-Object { $_.Status -eq 'INCOMPATIBLE' })
$differs      = @($results | Where-Object { $_.Status -eq 'DIFFERS-OK' })

if ($missing) {
    Write-Host ""
    Write-Host "MISSING - nothing generated will compile until these exist:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $($_.Key)   (declare as: $($_.Type))" }
}

if ($incompatible) {
    Write-Host ""
    Write-Host "INCOMPATIBLE - present, but missing members the templates call:" -ForegroundColor Red
    $incompatible | ForEach-Object {
        Write-Host "  $($_.Key)  [$($_.Found)]"
        Write-Host "     absent: $($_.Absent -join ', ')" -ForegroundColor Yellow
    }
    Write-Host "  Either create the bundled copy, or rewrite the generated scripts to avoid these." -ForegroundColor Yellow
}

if ($differs) {
    Write-Host ""
    Write-Host "DIFFERS-OK - the tenant's code is not identical to the bundled copy, but every" -ForegroundColor Yellow
    Write-Host "member the templates call is present. Leave the tenant's copy alone; overwriting it" -ForegroundColor Yellow
    Write-Host "could break scripts already in that tenant." -ForegroundColor Yellow
    $differs | ForEach-Object {
        Write-Host "  $($_.Key)  [$($_.Found)]"
        if ($Detailed -and $_.Extra.Count) { Write-Host "     bundled copy also has: $($_.Extra -join ', ')" -ForegroundColor DarkGray }
    }
}

if (-not $missing -and -not $incompatible) {
    Write-Host ""
    Write-Host "Every member the templates call is available in this tenant." -ForegroundColor Green
}

# ------------------------------------------------------------------ stage what is needed
if ($OutDir) {
    $toStage = @($missing) + @($incompatible)
    if (-not $toStage) { Write-Host ""; Write-Host "Nothing to stage." -ForegroundColor Green; return }

    if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }
    $OutDir = (Resolve-Path $OutDir).Path

    Write-Host ""
    foreach ($r in $toStage) {
        $src = Join-Path $prereqRoot "$($r.Key).cs"
        if (Test-Path $src) { Copy-Item $src (Join-Path $OutDir "$($r.Key).cs") -Force; Write-Host "  staged  $($r.Key).cs" -ForegroundColor Green }
        else { Write-Warning "No bundled copy of $($r.Key)" }
    }

    Write-Host ""
    Write-Host "Create these in PRO before generating. The ExtensionType is in each file's header" -ForegroundColor Cyan
    Write-Host "and in the table above - BrowserManager is IDisposable, not ExtensionScript." -ForegroundColor Cyan
}
elseif ($missing -or $incompatible) {
    Write-Host ""
    Write-Host "Re-run with -OutDir <path> to stage the source you need." -ForegroundColor Cyan
}
