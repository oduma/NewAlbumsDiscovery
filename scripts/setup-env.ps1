#Requires -Version 5.1
<#
Scans z-com-ai/*.txt and persists each as a Machine-level NewAlbumsDiscovery__* environment
variable. See docs/requirements/FUNCTIONAL_REQUIREMENTS.md -> Phase 2 for the full spec.

Filename -> env var name (mechanical transform):
  1. Strip the .txt extension.
  2. Split the remaining filename on '-'.
  3. For each segment: uppercase the first character, lowercase the rest.
  4. Join the segments with '__'.
  5. Prefix with 'NewAlbumsDiscovery__'.
  Example: sqlite-db-path.txt -> NewAlbumsDiscovery__Sqlite__Db__Path

File content is treated as a path relative to $env:USERPROFILE.

Must be run from an elevated (Administrator) PowerShell window - Machine-scope environment
variables cannot be set otherwise. Run normally (not dot-sourced); $env: writes are
process-scoped, so the invoking terminal sees the new variables immediately either way.
#>

function ConvertTo-EnvVarName {
    param([Parameter(Mandatory)][string]$FileBaseName)

    $segments = $FileBaseName -split '-' | Where-Object { $_ -ne '' }
    $pascalSegments = $segments | ForEach-Object {
        if ($_.Length -le 1) {
            $_.ToUpper()
        } else {
            $_.Substring(0, 1).ToUpper() + $_.Substring(1).ToLower()
        }
    }
    return 'NewAlbumsDiscovery__' + ($pascalSegments -join '__')
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host 'ERROR: this script must be run from an elevated PowerShell window.' -ForegroundColor Red
    Write-Host '  Right-click PowerShell -> Run as Administrator, then re-run: .\scripts\setup-env.ps1' -ForegroundColor Red
    exit 1
}

$zComAiDir = Join-Path $PSScriptRoot '..\z-com-ai'
if (-not (Test-Path $zComAiDir)) {
    Write-Host "No z-com-ai directory found at $zComAiDir. Nothing to do."
    exit 0
}

$files = Get-ChildItem -Path $zComAiDir -Filter '*.txt' -File
if (-not $files -or $files.Count -eq 0) {
    Write-Host "No .txt files found in $zComAiDir. Nothing to do."
    exit 0
}

$processedCount = 0
foreach ($file in $files) {
    $varName = ConvertTo-EnvVarName -FileBaseName $file.BaseName
    $relativePath = (Get-Content -Path $file.FullName -Raw -ErrorAction Stop)
    if ($null -ne $relativePath) { $relativePath = $relativePath.Trim() }

    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        Write-Warning "Skipping $($file.Name): file is empty."
        continue
    }

    $absolutePath = Join-Path $env:USERPROFILE $relativePath

    [Environment]::SetEnvironmentVariable($varName, $absolutePath, 'Machine')
    Set-Item -Path "Env:$varName" -Value $absolutePath

    Write-Host "Set $varName = $absolutePath (Machine)" -ForegroundColor Green
    $processedCount++
}

Write-Host ''
Write-Host "Done. $processedCount variable(s) processed."
Write-Host 'This terminal already has them available. Other already-open terminals, IDEs, or services must be restarted to pick up the change.'
