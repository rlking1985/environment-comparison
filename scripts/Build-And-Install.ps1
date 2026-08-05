[CmdletBinding()]
param(
    [string]$XrmToolBoxPath = 'C:\Users\rlkin\git\XrmToolbox',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedXrmToolBoxPath = [System.IO.Path]::GetFullPath($XrmToolBoxPath)
$xrmToolBoxExecutable = Join-Path $resolvedXrmToolBoxPath 'XrmToolBox.exe'
$xrmToolBoxProfileDirectory = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'MscrmTools\XrmToolBox'
$pluginsDirectory = Join-Path $xrmToolBoxProfileDirectory 'Plugins'
$manifestPath = Join-Path $pluginsDirectory 'manifest.json'
$sourceDll = Join-Path $repositoryRoot "src\EnvironmentComparison\bin\$Configuration\net48\EnvironmentComparison.dll"
$destinationDll = Join-Path $pluginsDirectory 'EnvironmentComparison.dll'

if (-not (Test-Path -LiteralPath $resolvedXrmToolBoxPath -PathType Container)) {
    throw "XrmToolBox directory not found: $resolvedXrmToolBoxPath"
}

if (-not (Test-Path -LiteralPath $xrmToolBoxExecutable -PathType Leaf)) {
    throw "XrmToolBox.exe was not found in: $resolvedXrmToolBoxPath"
}

if (Get-Process -Name 'XrmToolBox' -ErrorAction SilentlyContinue) { 
    throw 'XrmToolBox is running. Close it before installing the rebuilt plugin.'
}

& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration $Configuration -SkipTests:$SkipTests
if ($LASTEXITCODE -ne 0) { throw "Build script failed with exit code $LASTEXITCODE." }

New-Item -ItemType Directory -Path $pluginsDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force

if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    $manifestBackupPath = "$manifestPath.development-backup"
    Copy-Item -LiteralPath $manifestPath -Destination $manifestBackupPath -Force
    Remove-Item -LiteralPath $manifestPath -Force
}

Write-Host ''
Write-Host 'Environment Comparison installed successfully.' -ForegroundColor Green
Write-Host "Source:      $sourceDll"
Write-Host "Destination: $destinationDll"
Write-Host 'The generated plugin manifest was invalidated so XrmToolBox will rescan the development DLL.'
Write-Host 'Start XrmToolBox and search for Environment Comparison.'
