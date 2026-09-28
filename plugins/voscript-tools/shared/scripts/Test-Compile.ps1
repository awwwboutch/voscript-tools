<#
.SYNOPSIS
    Compiles generated VOScript files locally, with the compiler and assemblies the PRO client itself uses, so a
    scaffold can be checked without creating anything in a tenant.

.DESCRIPTION
    PRO compiles every custom script in a tenant as ONE assembly. A script that fails to compile blocks the whole
    set, so a broken scaffold created in a shared tenant breaks everyone's scripts until it is fixed or removed -
    and there is no delete from the MCP. Check here first.

    Uses <PRO client>\roslyn\csc.exe and references every managed assembly in the client folder, plus the WPF /
    WinForms framework assemblies scripts commonly use. -IncludePrerequisites adds the bundled shared libraries
    (_Browser, BrowserManager, _Premium, _Common, _Word, CapResultParser) so generated scripts resolve against them,
    exactly as they would in a tenant that has them.

    A clean compile here means the code is type-correct against this client version. It does not mean it works:
    element names, window titles and keystrokes are still unverified until run against the live application.

.EXAMPLE
    Test-Compile.ps1 -Path .\out -IncludePrerequisites
#>
[CmdletBinding()]
param(
    # .cs files, or folders containing them.
    [Parameter(Mandatory = $true)]
    [string[]]$Path,

    # Also compile the bundled prerequisite libraries from shared/prerequisites.
    [switch]$IncludePrerequisites,

    # PRO client folder. Defaults to the standard install location.
    [string]$ClientDir = 'C:\Program Files (x86)\Voicebrook\PRO_Server\Production\Client',

    # Show warnings too, not just errors.
    [switch]$ShowWarnings
)

$ErrorActionPreference = 'Stop'

$csc = Join-Path $ClientDir 'roslyn\csc.exe'
if (-not (Test-Path $csc)) { throw "PRO's compiler was not found at $csc. Pass -ClientDir." }

$sources = @()
foreach ($p in $Path) {
    if (Test-Path $p -PathType Container) { $sources += Get-ChildItem $p -Recurse -Filter *.cs | ForEach-Object FullName }
    elseif (Test-Path $p)                 { $sources += (Resolve-Path $p).Path }
    else                                  { throw "Not found: $p" }
}

if ($IncludePrerequisites) {
    $prereqRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'prerequisites'
    $names = $sources | ForEach-Object { [IO.Path]::GetFileName($_) }
    # a copy passed in -Path (e.g. a newer tenant revision) wins over the bundled one
    $sources += Get-ChildItem $prereqRoot -Filter *.cs | Where-Object { $names -notcontains $_.Name } | ForEach-Object FullName
}
if (-not $sources) { throw "No .cs files found." }

# Managed assemblies only - the client folder also holds native DLLs.
$refs = Get-ChildItem $ClientDir -Filter *.dll | Where-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); $true } catch { $false }
} | ForEach-Object FullName

$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
$refs += 'System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.Data.dll','System.Drawing.dll',
         'System.Windows.Forms.dll','System.Xaml.dll','Microsoft.CSharp.dll','System.Net.Http.dll' | ForEach-Object { Join-Path $fx $_ }
$refs += 'WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll' | ForEach-Object { Join-Path $fx "WPF\$_" }

$out = Join-Path ([IO.Path]::GetTempPath()) ("VOScriptCompileCheck_{0}.dll" -f [guid]::NewGuid().ToString('N'))
$rsp = [IO.Path]::ChangeExtension($out, '.rsp')

$lines = @('/nologo', '/target:library', '/langversion:latest', '/nowarn:1701,1702', "/out:`"$out`"")
$lines += $refs    | Select-Object -Unique | ForEach-Object { "/r:`"$_`"" }
$lines += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

$output = & $csc "@$rsp" 2>&1 | ForEach-Object { "$_" }
$exit = $LASTEXITCODE
Remove-Item $rsp, $out, ([IO.Path]::ChangeExtension($out, '.pdb')) -ErrorAction SilentlyContinue

$errors   = @($output | Where-Object { $_ -match ': error ' })
$warnings = @($output | Where-Object { $_ -match ': warning ' })

Write-Host ("Compiled {0} files: {1} errors, {2} warnings" -f $sources.Count, $errors.Count, $warnings.Count) -ForegroundColor $(if ($errors) { 'Red' } else { 'Green' })
$errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
if ($ShowWarnings) { $warnings | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow } }
if ($exit -ne 0 -and -not $errors) { $output | ForEach-Object { Write-Host "  $_" } }

exit $exit
