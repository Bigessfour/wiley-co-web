[CmdletBinding()]
param(
    [string[]]$TestProjectPaths = @(),
    [string]$ResultsDirectory = "tests/TestResults/coverage"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-TestProjectPaths {
    param(
        [string[]]$ExplicitPaths
    )

    if ($ExplicitPaths.Count -gt 0) {
        return $ExplicitPaths
    }

    return Get-ChildItem -Path "tests" -Recurse -Filter "*.csproj" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Sort-Object FullName |
        Select-Object -ExpandProperty FullName
}

function Ensure-ReportGenerator {
    $candidatePaths = @(
        (Join-Path $HOME ".dotnet/tools/reportgenerator.exe"),
        (Join-Path $HOME ".dotnet/tools/reportgenerator")
    )

    $existingTool = Get-Command reportgenerator -ErrorAction SilentlyContinue
    if ($existingTool) {
        return $existingTool.Source
    }

    $toolPath = $candidatePaths |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $toolPath) {
        Write-Host "Installing dotnet-reportgenerator-globaltool..."
        dotnet tool update -g dotnet-reportgenerator-globaltool | Out-Null

        if ($LASTEXITCODE -ne 0) {
            dotnet tool install -g dotnet-reportgenerator-globaltool | Out-Null
        }

        $existingTool = Get-Command reportgenerator -ErrorAction SilentlyContinue
        if ($existingTool) {
            return $existingTool.Source
        }

        $toolPath = $candidatePaths |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
    }

    if (-not $toolPath) {
        throw "reportgenerator was not found after installation attempt"
    }

    return $toolPath
}

function Write-CoverageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$CoverageXml,
        [Parameter(Mandatory = $true)]
        [string]$ReportPath
    )

    $coverage = $CoverageXml.DocumentElement
    $lineRate = [double]$coverage.'line-rate'
    $branchRate = [double]$coverage.'branch-rate'
    $linesCovered = [int]$coverage.'lines-covered'
    $linesValid = [int]$coverage.'lines-valid'
    $branchesCovered = [int]$coverage.'branches-covered'
    $branchesValid = [int]$coverage.'branches-valid'

    Write-Host ""
    Write-Host "Coverage summary"
    Write-Host ("  Line coverage:   {0:N2}% ({1}/{2})" -f ($lineRate * 100), $linesCovered, $linesValid)
    Write-Host ("  Branch coverage: {0:N2}% ({1}/{2})" -f ($branchRate * 100), $branchesCovered, $branchesValid)
    Write-Host ("  Report: {0}" -f $ReportPath)
}

function Get-CoveragePercent {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$CoverageXml
    )

    return [double]$CoverageXml.DocumentElement.'line-rate' * 100
}

function Get-PlatformCoverageThreshold {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectName
    )

    switch ($ProjectName) {
        'WileyCoWeb.ComponentTests' { return 68 }
        'WileyCoWeb.IntegrationTests' { return 11 }
        'WileyWidget.Tests' { return 32 }
        'WileyCoWeb.E2ETests' { return 0 }
        default { return 0 }
    }
}

if (-not (Test-Path "tests")) {
    throw "tests directory not found"
}

New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null

$projectPaths = Get-TestProjectPaths -ExplicitPaths $TestProjectPaths

if ($projectPaths.Count -eq 0) {
    throw "No test project files were found"
}

$coverageResults = @()

foreach ($projectPath in $projectPaths) {
    if (-not (Test-Path $projectPath)) {
        throw "Test project not found: $projectPath"
    }

    $projectName = Split-Path $projectPath -LeafBase
    $projectResultsDirectory = Join-Path $ResultsDirectory $projectName

    if (Test-Path $projectResultsDirectory) {
        Remove-Item -Path $projectResultsDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $projectResultsDirectory | Out-Null

    Write-Host "Running tests with coverage for $projectPath"

    dotnet test $projectPath --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory $projectResultsDirectory
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $projectCoverageReport = Get-ChildItem -Path $projectResultsDirectory -Recurse -Filter coverage.cobertura.xml |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if (-not $projectCoverageReport) {
        throw "Coverage report was not generated for $projectPath"
    }

    [xml]$projectCoverageXml = Get-Content -Path $projectCoverageReport.FullName -Raw
    $coverageResults += [pscustomobject]@{
        ProjectName = $projectName
        CoveragePercent = [math]::Round((Get-CoveragePercent -CoverageXml $projectCoverageXml), 2)
        Threshold = (Get-PlatformCoverageThreshold -ProjectName $projectName)
        ReportPath = $projectCoverageReport.FullName
    }
}

$coverageReports = $coverageResults | Select-Object -ExpandProperty ReportPath

if (-not $coverageReports) {
    throw "Coverage reports were not generated in $ResultsDirectory"
}

$reportGenerator = Ensure-ReportGenerator
$mergedDirectory = Join-Path $ResultsDirectory "merged"

if (Test-Path $mergedDirectory) {
    Remove-Item -Path $mergedDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $mergedDirectory | Out-Null

$reportList = ($coverageReports -join ';')
& $reportGenerator "-reports:$reportList" "-targetdir:$mergedDirectory" "-reporttypes:Cobertura" | Out-Null

$mergedReportCandidates = @(
    (Join-Path $mergedDirectory 'Cobertura.xml'),
    (Join-Path $mergedDirectory 'coverage.cobertura.xml')
)

$mergedReport = $mergedReportCandidates |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if (-not $mergedReport) {
    throw "Merged coverage report was not generated in $mergedDirectory"
}

[xml]$coverageXml = Get-Content -Path $mergedReport -Raw
Write-CoverageSummary -CoverageXml $coverageXml -ReportPath $mergedReport

Write-Host ""
Write-Host "Per-platform coverage"
foreach ($coverageResult in $coverageResults) {
    $gateNote = if ($coverageResult.ProjectName -eq 'WileyCoWeb.E2ETests') { ' (browser-only; reported separately)' } else { '' }
    Write-Host ("  {0}: {1:N2}%{2} [threshold {3:N0}%]" -f $coverageResult.ProjectName, $coverageResult.CoveragePercent, $gateNote, $coverageResult.Threshold)
}

$gateFailures = $coverageResults |
    Where-Object { $_.CoveragePercent -lt $_.Threshold -and $_.Threshold -gt 0 }

if ($gateFailures) {
    $failureLines = $gateFailures |
        ForEach-Object { "{0}: {1:N2}% < {2:N0}%" -f $_.ProjectName, $_.CoveragePercent, $_.Threshold }

    throw "Coverage gate failed:`n$($failureLines -join [Environment]::NewLine)"
}
