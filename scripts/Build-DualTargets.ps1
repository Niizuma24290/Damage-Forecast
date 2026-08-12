param(
    [string]$Configuration = "Release",
    [string]$Project = "src/DamageForecast/DamageForecast.csproj",
    [string]$SnapshotRoot = "C:\Users\ROG\Documents\Codex\STS2-reference-snapshots",
    [string]$StableVersion = "v0.107.1",
    [string]$BetaVersion = "v0.109.0",
    [string]$StableReferenceRoot = "",
    [string]$BetaReferenceRoot = "",
    [string]$BaseLibReferencePath = "",
    [string]$StableOutputDir = "work/publish/stable/damage-forecast",
    [string]$BetaOutputDir = "work/publish/beta/damage-forecast",
    [switch]$SkipRestore,
    [switch]$ApproveHashDifference
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot $Project
$dotnet = "C:\sts2\dotnet\dotnet.exe"
$targets = @()
$scratchRoot = Join-Path $repoRoot "work"
$nugetScratch = Join-Path $scratchRoot "nuget-scratch"
$msbuildTemp = Join-Path $scratchRoot "msbuild-temp"
$baseLibBootstrap = Join-Path $PSScriptRoot "Restore-BaseLibDependency.ps1"
$publishValidator = Join-Path $PSScriptRoot "Test-IdentityPublishTrees.ps1"

function Resolve-SnapshotReferenceRoot {
    param(
        [string]$TargetName,
        [string]$ExpectedVersion
    )

    if (-not (Test-Path -LiteralPath $SnapshotRoot)) {
        throw "Snapshot root not found: $SnapshotRoot"
    }

    $snapshotMatches = foreach ($snapshot in Get-ChildItem -LiteralPath $SnapshotRoot -Directory) {
        $releaseInfoPath = Join-Path $snapshot.FullName "release_info.json"
        if (-not (Test-Path -LiteralPath $releaseInfoPath)) {
            continue
        }

        try {
            $releaseInfo = Get-Content -Raw -Encoding UTF8 -LiteralPath $releaseInfoPath | ConvertFrom-Json
        }
        catch {
            throw "Invalid release info: $releaseInfoPath`n$($_.Exception.Message)"
        }

        if ($releaseInfo.version -eq $ExpectedVersion) {
            [pscustomobject]@{
                Snapshot = $snapshot.FullName
                ReferenceRoot = Join-Path $snapshot.FullName "data_sts2_windows_x86_64"
            }
        }
    }

    if ($snapshotMatches.Count -eq 0) {
        throw "No $TargetName snapshot with version $ExpectedVersion found under $SnapshotRoot"
    }

    if ($snapshotMatches.Count -gt 1) {
        $paths = ($snapshotMatches | ForEach-Object Snapshot) -join [Environment]::NewLine
        throw "Multiple $TargetName snapshots with version $ExpectedVersion found. Pass -$($TargetName.Substring(0, 1).ToUpper())$($TargetName.Substring(1))ReferenceRoot explicitly:$([Environment]::NewLine)$paths"
    }

    Write-Host "Selected $TargetName $ExpectedVersion snapshot: $($snapshotMatches[0].Snapshot)"
    return $snapshotMatches[0].ReferenceRoot
}

function Invoke-Dotnet {
    param([string[]]$Arguments)

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE`: $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Expected local dotnet at $dotnet"
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project not found: $projectPath"
}

if ([string]::IsNullOrWhiteSpace($BaseLibReferencePath)) {
    $BaseLibReferencePath = Join-Path $repoRoot "work/dependencies/BaseLib/3.3.4/BaseLib.dll"
    if (-not (Test-Path -LiteralPath $BaseLibReferencePath)) {
        if (-not (Test-Path -LiteralPath $baseLibBootstrap)) {
            throw "BaseLib bootstrap script not found: $baseLibBootstrap"
        }

        & $baseLibBootstrap -Destination $BaseLibReferencePath
    }
}

if (-not (Test-Path -LiteralPath $BaseLibReferencePath)) {
    throw "BaseLib reference not found: $BaseLibReferencePath"
}

if ([string]::IsNullOrWhiteSpace($StableReferenceRoot) -and [string]::IsNullOrWhiteSpace($BetaReferenceRoot)) {
    $StableReferenceRoot = Resolve-SnapshotReferenceRoot -TargetName "stable" -ExpectedVersion $StableVersion
    $BetaReferenceRoot = Resolve-SnapshotReferenceRoot -TargetName "beta" -ExpectedVersion $BetaVersion
}

if (-not [string]::IsNullOrWhiteSpace($StableReferenceRoot)) {
    $targets += [pscustomobject]@{
        Name = "stable"
        ReferenceRoot = $StableReferenceRoot
        OutputDir = $StableOutputDir
    }
}

if (-not [string]::IsNullOrWhiteSpace($BetaReferenceRoot)) {
    $targets += [pscustomobject]@{
        Name = "beta"
        ReferenceRoot = $BetaReferenceRoot
        OutputDir = $BetaOutputDir
    }
}

if ($targets.Count -eq 0) {
    throw "No build targets selected."
}

New-Item -ItemType Directory -Force -Path $nugetScratch | Out-Null
New-Item -ItemType Directory -Force -Path $msbuildTemp | Out-Null
$env:NUGET_SCRATCH = $nugetScratch
$env:TEMP = $msbuildTemp
$env:TMP = $msbuildTemp

foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target.ReferenceRoot)) {
        throw "$($target.Name) reference root not found: $($target.ReferenceRoot)"
    }

    foreach ($file in @("sts2.dll", "GodotSharp.dll", "0Harmony.dll")) {
        $path = Join-Path $target.ReferenceRoot $file
        if (-not (Test-Path -LiteralPath $path)) {
            throw "$($target.Name) reference root missing $file`: $path"
        }
    }
}

foreach ($target in $targets) {
    $outputPath = Join-Path $repoRoot $target.OutputDir
    $buildOutputPath = Join-Path $repoRoot "work/bin/$($target.Name)/"
    $intermediateOutputPath = Join-Path $repoRoot "work/obj/$($target.Name)/"
    if (-not $SkipRestore) {
        Invoke-Dotnet @(
            "restore",
            $projectPath,
            "-p:BaseLibReferencePath=$BaseLibReferencePath",
            "-p:Sts2ReferenceRoot=$($target.ReferenceRoot)",
            "-p:DamageForecastDebugTrace=false",
            "-p:OutputPath=$buildOutputPath",
            "-p:BaseIntermediateOutputPath=$intermediateOutputPath"
        )
    }

    Invoke-Dotnet @(
        "build",
        $projectPath,
        "-c",
        $Configuration,
        "--no-restore",
        "-p:BaseLibReferencePath=$BaseLibReferencePath",
        "-p:Sts2ReferenceRoot=$($target.ReferenceRoot)",
        "-p:DamageForecastDebugTrace=false",
        "-p:OutputPath=$buildOutputPath",
        "-p:BaseIntermediateOutputPath=$intermediateOutputPath"
    )
    Invoke-Dotnet @(
        "publish",
        $projectPath,
        "-c",
        $Configuration,
        "--no-build",
        "-p:BaseLibReferencePath=$BaseLibReferencePath",
        "-p:Sts2ReferenceRoot=$($target.ReferenceRoot)",
        "-p:DamageForecastDebugTrace=false",
        "-p:OutputPath=$buildOutputPath",
        "-p:BaseIntermediateOutputPath=$intermediateOutputPath",
        "-o",
        $outputPath
    )

    $requiredFiles = @("damage-forecast.dll", "damage-forecast.json")
    foreach ($file in $requiredFiles) {
        $path = Join-Path $outputPath $file
        if (-not (Test-Path -LiteralPath $path)) {
            throw "$($target.Name) publish output missing required file: $path"
        }
    }

    $forbidden = Get-ChildItem -LiteralPath $outputPath -File -Recurse |
        Where-Object { $_.Name.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase) -or $_.Extension -in @(".pdb", ".pck", ".log") }

    if ($forbidden) {
        $names = ($forbidden | ForEach-Object FullName) -join [Environment]::NewLine
        throw "$($target.Name) publish output contains forbidden files:$([Environment]::NewLine)$names"
    }

    $publishedFiles = @(Get-ChildItem -LiteralPath $outputPath -File -Recurse | ForEach-Object {
        $_.FullName.Substring($outputPath.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
    } | Sort-Object)
    if (($publishedFiles -join "`n") -ne (($requiredFiles | Sort-Object) -join "`n")) {
        throw "$($target.Name) publish output must contain exactly $($requiredFiles -join ', '); actual=$($publishedFiles -join ', ')"
    }

    Write-Host "Built $($target.Name) target to $outputPath"
}

if ($targets.Count -eq 2) {
    if (-not (Test-Path -LiteralPath $publishValidator)) {
        throw "Publish-tree validator not found: $publishValidator"
    }

    $stableTree = Join-Path $repoRoot (($targets | Where-Object Name -eq "stable").OutputDir)
    $betaTree = Join-Path $repoRoot (($targets | Where-Object Name -eq "beta").OutputDir)
    $validatorArguments = @{
        StableTree = $stableTree
        BetaTree = $betaTree
    }
    if ($ApproveHashDifference) {
        $validatorArguments.ApproveHashDifference = $true
    }
    & $publishValidator @validatorArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publish-tree identity/hash validation failed with exit code $LASTEXITCODE."
    }
}
