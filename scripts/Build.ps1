[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'EnvironmentComparison.sln'
$projectPath = Join-Path $repositoryRoot 'src\EnvironmentComparison\EnvironmentComparison.csproj'
$artifactDirectory = Join-Path $repositoryRoot 'artifacts'

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        Write-Host "Restoring packages..."
        & dotnet restore $solutionPath
        if ($LASTEXITCODE -ne 0) { throw "Restore failed with exit code $LASTEXITCODE." }
    }

    if (-not $SkipTests) {
        Write-Host "Running synthetic tests..."
        & dotnet test $solutionPath -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }
    }

    Write-Host "Building and packaging Environment Comparison..."
    & dotnet build $projectPath -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

$dll = Join-Path $repositoryRoot "src\EnvironmentComparison\bin\$Configuration\net48\EnvironmentComparison.dll"
$package = Get-ChildItem -LiteralPath $artifactDirectory -Filter 'EnvironmentComparison.XrmToolBox.*.nupkg' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) {
    throw "Build completed but the plugin DLL was not found: $dll"
}

if ($null -eq $package) {
    throw "Build completed but no NuGet package was found in: $artifactDirectory"
}

Write-Host ''
Write-Host 'Environment Comparison build completed.' -ForegroundColor Green
Write-Host "DLL:     $dll"
Write-Host "Package: $($package.FullName)"
Write-Host 'No Dataverse environment was contacted.'

