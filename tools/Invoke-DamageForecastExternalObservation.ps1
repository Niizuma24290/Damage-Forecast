[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("stable", "beta")]
    [string]$Target,

    [Parameter(Mandatory = $true)]
    [string]$ScenarioPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$ProviderRoot,

    [Parameter(Mandatory = $true)]
    [string]$CheckpointPath,

    [Parameter(Mandatory = $true)]
    [string]$CapabilitiesPath,

    [Parameter(Mandatory = $true)]
    [string]$GameDataDir,

    [Parameter(Mandatory = $true)]
    [string]$DotnetExe,

    [ValidateRange(1, 900)]
    [int]$TimeoutSeconds = 120,

    [ValidateRange(0, 900000)]
    [int]$TimeoutMilliseconds = 0,

    [ValidateSet("ss4-tests", "contract-probe")]
    [string]$ProviderMode = "ss4-tests",

    [string]$CancellationFile
)

$ErrorActionPreference = "Stop"

$adapterVersion = "df-s2b-v1"
$requiredProviderRevision = "42396191e4bd66ca8ab27cd9b9b9f4f537966978"
$supportedSchemaVersion = 1
$requiredCheckpointSha256 =
    "6f5032ac3cf13ac85fc96debb0d616fab018f7bf16ac7361ed7b3d6d688fd2ae"
$requiredCapabilitiesSha256 =
    "58975473b8900da6560d36dbc29b66fa4adbc54c2f3c41ecbb4d37d9bcc31257"
$requiredUnsupportedRegistrySha256 =
    "70ac09aee8d948bf2396b0f0df2b3aa569358e5b105297c6768dfcff5c4c65de"
$requiredTargets = @{
    stable = [ordered]@{
        version = "v0.107.1"
        commit = "59260271"
        gameAssemblySha256 =
            "A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52"
        adapterVersion = "stable-v1"
        artifactSha256 =
            "DB7344AD0E4F66A94EB348CCD9CB76A6723704D382E5DCF96928CBD53FB64E72"
    }
    beta = [ordered]@{
        version = "v0.109.0"
        commit = "c12f634d"
        gameAssemblySha256 =
            "EE45848FF6319DFC7AF2538D3A52D05D82BEF35EE4C5FD0400DC9EFE8F9054AA"
        adapterVersion = "beta-v1"
        artifactSha256 =
            "FA03F05EA583F78DEC328E8CF9659DFDB7A5CCD83D30EC19E11119260E673D29"
    }
}

function Resolve-ExistingFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-ExistingDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Name not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $resolved = Resolve-ExistingFile -Path $Path -Name $Name
    try {
        return Get-Content -LiteralPath $resolved -Encoding utf8 -Raw |
            ConvertFrom-Json
    }
    catch {
        throw "$Name is not valid JSON: $resolved`n$($_.Exception.Message)"
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextSha256 {
    param(
        [AllowEmptyString()]
        [string]$Text
    )

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString(
            $sha.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Scope
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        throw "$Scope.$Name is required."
    }

    return $property.Value
}

function Assert-ExactValue {
    param(
        [AllowNull()]
        [object]$Actual,

        [AllowNull()]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $numericTypes = @(
        "Byte",
        "SByte",
        "Int16",
        "UInt16",
        "Int32",
        "UInt32",
        "Int64",
        "UInt64",
        "Single",
        "Double",
        "Decimal"
    )
    $bothNumeric = (
        $null -ne $Actual -and
        $null -ne $Expected -and
        $numericTypes -contains $Actual.GetType().Name -and
        $numericTypes -contains $Expected.GetType().Name
    )
    $matches = if ($bothNumeric) {
        [decimal]$Actual -eq [decimal]$Expected
    }
    else {
        [object]::Equals($Actual, $Expected)
    }

    if (-not $matches) {
        throw "$Name mismatch. Expected '$Expected', got '$Actual'."
    }
}

function Resolve-Executable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    if (Test-Path -LiteralPath $Command -PathType Leaf) {
        return (Resolve-Path -LiteralPath $Command).Path
    }

    $resolved = Get-Command $Command -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $resolved) {
        throw "dotnet executable not found: $Command"
    }

    return $resolved.Source
}

function Get-ProviderGitState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $safeDirectory = "safe.directory=$($Root.Replace('\', '/'))"
    $head = (
        & git -c $safeDirectory -C $Root rev-parse HEAD 2>&1 |
            Out-String
    ).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "ProviderRoot is not a readable Git checkout: $Root`n$head"
    }

    $status = (
        & git -c $safeDirectory -C $Root status --porcelain 2>&1 |
            Out-String
    ).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read provider Git status: $Root`n$status"
    }

    return [pscustomobject]@{
        Head = $head
        IsClean = [string]::IsNullOrWhiteSpace($status)
    }
}

function Get-TargetCapability {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Capabilities,

        [Parameter(Mandatory = $true)]
        [string]$Channel
    )

    $matches = @($Capabilities.targets | Where-Object {
        $_.channel -ceq $Channel
    })
    if ($matches.Count -ne 1) {
        throw "Capabilities must contain exactly one '$Channel' target."
    }

    return $matches[0]
}

function Assert-GameIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DataDir,

        [Parameter(Mandatory = $true)]
        [object]$TargetCapability
    )

    $assemblyPath = Resolve-ExistingFile `
        -Path (Join-Path $DataDir "sts2.dll") `
        -Name "game assembly"
    $actualHash = Get-Sha256 -Path $assemblyPath
    $expectedHash = ([string](Get-RequiredProperty `
        -Object $TargetCapability `
        -Name "gameAssemblySha256" `
        -Scope "capability.target")).ToLowerInvariant()
    Assert-ExactValue `
        -Actual $actualHash `
        -Expected $expectedHash `
        -Name "gameAssemblySha256"

    $releaseInfoCandidates = @(
        (Join-Path $DataDir "release_info.json"),
        (Join-Path (Split-Path -Parent $DataDir) "release_info.json")
    )
    $releaseInfoPath = $releaseInfoCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1

    if ($null -ne $releaseInfoPath) {
        $release = Read-JsonFile -Path $releaseInfoPath -Name "release_info"
        Assert-ExactValue `
            -Actual ([string](Get-RequiredProperty $release "version" "release_info")) `
            -Expected ([string](Get-RequiredProperty $TargetCapability "version" "capability.target")) `
            -Name "gameVersion"
        Assert-ExactValue `
            -Actual ([string](Get-RequiredProperty $release "commit" "release_info")) `
            -Expected ([string](Get-RequiredProperty $TargetCapability "commit" "capability.target")) `
            -Name "gameCommit"
    }
    else {
        $runtimeManifestPath = Join-Path $DataDir "runtime-manifest.json"
        if (-not (Test-Path -LiteralPath $runtimeManifestPath -PathType Leaf)) {
            throw "No release_info.json or runtime-manifest.json found for GameDataDir."
        }

        $runtimeManifest = Read-JsonFile `
            -Path $runtimeManifestPath `
            -Name "runtime manifest"
        $manifestTarget = Get-RequiredProperty `
            -Object $runtimeManifest `
            -Name "target" `
            -Scope "runtimeManifest"
        Assert-ExactValue `
            -Actual ([string](Get-RequiredProperty $manifestTarget "channel" "runtimeManifest.target")) `
            -Expected ([string](Get-RequiredProperty $TargetCapability "channel" "capability.target")) `
            -Name "gameChannel"
        Assert-ExactValue `
            -Actual ([string](Get-RequiredProperty $manifestTarget "version" "runtimeManifest.target")) `
            -Expected ([string](Get-RequiredProperty $TargetCapability "version" "capability.target")) `
            -Name "gameVersion"
        Assert-ExactValue `
            -Actual ([string](Get-RequiredProperty $manifestTarget "commit" "runtimeManifest.target")) `
            -Expected ([string](Get-RequiredProperty $TargetCapability "commit" "capability.target")) `
            -Name "gameCommit"
    }

    return [pscustomobject]@{
        AssemblyPath = $assemblyPath
        AssemblySha256 = $actualHash
    }
}

function Get-SupportedScenario {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Scenario,

        [Parameter(Mandatory = $true)]
        [string]$Channel
    )

    $scenarioId = [string](Get-RequiredProperty $Scenario "scenarioId" "scenario")
    if ($scenarioId -ceq "turn-end.burn.blockable.v1" -and $Channel -ceq "stable") {
        return [pscustomobject]@{
            Name = "Burn"
            PassMarker = "[PASS] Burn turn-end HP loss"
            EventId = "observed.burn"
            SourceId = "card.burn"
            Phase = "TurnEndInHand"
            Order = 0
            Lane = "Blockable"
            Granularity = "SingleEvent"
            Amount = 2L
            BlockableTotal = 2L
            DirectHpLossTotal = 0L
        }
    }

    if ($scenarioId -ceq "turn-end.doubt.power.v1" -and $Channel -ceq "beta") {
        return [pscustomobject]@{
            Name = "Doubt"
            PassMarker = "[PASS] Doubt turn-end power"
            EventId = "observed.doubt"
            SourceId = "card.doubt"
            Phase = "TurnEndInHand"
            Order = 0
            Lane = "NonDamage"
            Granularity = "SingleEvent"
            Amount = 0L
            BlockableTotal = 0L
            DirectHpLossTotal = 0L
        }
    }

    return $null
}

function Assert-SupportedScenarioShape {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Scenario,

        [Parameter(Mandatory = $true)]
        [object]$Supported
    )

    $inputs = @(Get-RequiredProperty $Scenario "orderedInputs" "scenario")
    if ($inputs.Count -ne 1) {
        throw "Supported scenario must contain exactly one ordered input."
    }

    $input = $inputs[0]
    foreach ($field in @(
        @("sourceId", $Supported.SourceId),
        @("phase", $Supported.Phase),
        @("order", $Supported.Order),
        @("lane", $Supported.Lane),
        @("granularity", $Supported.Granularity),
        @("amount", $Supported.Amount)
    )) {
        Assert-ExactValue `
            -Actual (Get-RequiredProperty $input $field[0] "scenario.orderedInput") `
            -Expected $field[1] `
            -Name "scenario.orderedInput.$($field[0])"
    }
}

function New-ObservationMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Scenario,

        [Parameter(Mandatory = $true)]
        [object]$TargetCapability,

        [Parameter(Mandatory = $true)]
        [string]$ProviderArtifactSha256,

        [Parameter(Mandatory = $true)]
        [string]$CapabilityManifestSha256,

        [Parameter(Mandatory = $true)]
        [string]$UnsupportedRegistrySha256
    )

    return [ordered]@{
        schemaVersion = $supportedSchemaVersion
        providerId = "sts2sim-explicit-process"
        providerVersion = $adapterVersion
        sourceRevision = $requiredProviderRevision
        sourceDirty = $false
        gameChannel = [string]$TargetCapability.channel
        gameVersion = [string]$TargetCapability.version
        gameCommit = [string]$TargetCapability.commit
        gameAssemblySha256 = ([string]$TargetCapability.gameAssemblySha256).ToLowerInvariant()
        adapterVersion = [string]$TargetCapability.adapterVersion
        providerArtifactSha256 = $ProviderArtifactSha256
        evidenceLevel = "L2"
        runtimeVerified = $false
        seed = [long]$Scenario.seed
        runId = [guid]::NewGuid().ToString("N")
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        capabilityManifestSha256 = $CapabilityManifestSha256
        unsupportedRegistrySha256 = $UnsupportedRegistrySha256
    }
}

function Write-Observation {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Observation,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $parent = Split-Path -Parent $Path
    if ([string]::IsNullOrWhiteSpace($parent)) {
        $parent = (Get-Location).Path
    }
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw "Output directory not found: $parent"
    }
    if (Test-Path -LiteralPath $Path) {
        throw "OutputPath already exists: $Path"
    }

    $resolvedParent = (Resolve-Path -LiteralPath $parent).Path
    $leaf = Split-Path -Leaf $Path
    $finalPath = Join-Path $resolvedParent $leaf
    $temporaryPath = "$finalPath.tmp.$([guid]::NewGuid().ToString('N'))"
    $json = $Observation | ConvertTo-Json -Depth 20
    $encoding = New-Object System.Text.UTF8Encoding($false)

    try {
        [System.IO.File]::WriteAllText($temporaryPath, $json, $encoding)
        [System.IO.File]::Move($temporaryPath, $finalPath)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }

    Write-Output "DF_EXTERNAL_OBSERVATION=$finalPath"
}

function Invoke-ProviderProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string]$ArtifactPath,

        [Parameter(Mandatory = $true)]
        [string]$Channel,

        [Parameter(Mandatory = $true)]
        [string]$DataDir,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [int]$EffectiveTimeoutMilliseconds
        ,

        [Parameter(Mandatory = $true)]
        [string]$Mode,

        [AllowNull()]
        [string]$CancellationSignalPath
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Executable
    $escapedArtifact = $ArtifactPath.Replace('"', '\"')
    $startInfo.Arguments = "`"$escapedArtifact`" --target $Channel $Mode"
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    if ($null -ne $startInfo.Environment) {
        $startInfo.Environment["STS2_GAME_DIR"] = $DataDir
    }
    elseif ($null -ne $startInfo.EnvironmentVariables) {
        $startInfo.EnvironmentVariables["STS2_GAME_DIR"] = $DataDir
    }
    else {
        throw "ProcessStartInfo does not expose a writable environment dictionary."
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    try {
        if (-not $process.Start()) {
            return [pscustomobject]@{
                Started = $false
                TimedOut = $false
                Cancelled = $false
                ExitCode = $null
                Stdout = ""
                Stderr = "Process.Start returned false."
            }
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds(
            $EffectiveTimeoutMilliseconds)
        while (-not $process.HasExited) {
            if (
                -not [string]::IsNullOrWhiteSpace($CancellationSignalPath) -and
                (Test-Path -LiteralPath $CancellationSignalPath -PathType Leaf)
            ) {
                try {
                    $process.Kill()
                }
                catch {
                    # The cancellation disposition remains fail-closed if termination races.
                }
                $process.WaitForExit()
                return [pscustomobject]@{
                    Started = $true
                    TimedOut = $false
                    Cancelled = $true
                    ExitCode = $null
                    Stdout = $stdoutTask.GetAwaiter().GetResult()
                    Stderr = $stderrTask.GetAwaiter().GetResult()
                }
            }

            $remaining = [int][Math]::Ceiling(
                ($deadline - [DateTimeOffset]::UtcNow).TotalMilliseconds)
            if ($remaining -le 0) {
                try {
                    $process.Kill()
                }
                catch {
                    # The timeout disposition remains fail-closed even if termination races.
                }
                $process.WaitForExit()
                return [pscustomobject]@{
                    Started = $true
                    TimedOut = $true
                    Cancelled = $false
                    ExitCode = $null
                    Stdout = $stdoutTask.GetAwaiter().GetResult()
                    Stderr = $stderrTask.GetAwaiter().GetResult()
                }
            }

            [void]$process.WaitForExit([Math]::Min(25, $remaining))
        }

        return [pscustomobject]@{
            Started = $true
            TimedOut = $false
            Cancelled = $false
            ExitCode = $process.ExitCode
            Stdout = $stdoutTask.GetAwaiter().GetResult()
            Stderr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

$resolvedScenarioPath = Resolve-ExistingFile `
    -Path $ScenarioPath `
    -Name "scenario"
$resolvedProviderRoot = Resolve-ExistingDirectory `
    -Path $ProviderRoot `
    -Name "provider root"
$resolvedCheckpointPath = Resolve-ExistingFile `
    -Path $CheckpointPath `
    -Name "checkpoint"
$resolvedCapabilitiesPath = Resolve-ExistingFile `
    -Path $CapabilitiesPath `
    -Name "capabilities"
$resolvedGameDataDir = Resolve-ExistingDirectory `
    -Path $GameDataDir `
    -Name "game data directory"

Assert-ExactValue `
    -Actual (Get-Sha256 -Path $resolvedCheckpointPath) `
    -Expected $requiredCheckpointSha256 `
    -Name "checkpoint.sha256"
Assert-ExactValue `
    -Actual (Get-Sha256 -Path $resolvedCapabilitiesPath) `
    -Expected $requiredCapabilitiesSha256 `
    -Name "capabilities.sha256"

$scenario = Read-JsonFile -Path $resolvedScenarioPath -Name "scenario"
$checkpoint = Read-JsonFile -Path $resolvedCheckpointPath -Name "checkpoint"
$capabilities = Read-JsonFile -Path $resolvedCapabilitiesPath -Name "capabilities"

Assert-ExactValue `
    -Actual ([int](Get-RequiredProperty $scenario "schemaVersion" "scenario")) `
    -Expected $supportedSchemaVersion `
    -Name "scenario.schemaVersion"
Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $scenario "targetChannel" "scenario")) `
    -Expected $Target `
    -Name "scenario.targetChannel"
Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $scenario "expectedEvidenceLevel" "scenario")) `
    -Expected "L2" `
    -Name "scenario.expectedEvidenceLevel"
[void](Get-RequiredProperty $scenario "seed" "scenario")
[void](Get-RequiredProperty $scenario "requestedCapabilities" "scenario")

Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $checkpoint "sourceRevision" "checkpoint")) `
    -Expected $requiredProviderRevision `
    -Name "checkpoint.sourceRevision"
Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $checkpoint "sourceTree" "checkpoint")) `
    -Expected "clean" `
    -Name "checkpoint.sourceTree"
Assert-ExactValue `
    -Actual ([bool](Get-RequiredProperty $checkpoint "runtimeVerified" "checkpoint")) `
    -Expected $false `
    -Name "checkpoint.runtimeVerified"

Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $capabilities "sourceRevision" "capabilities")) `
    -Expected $requiredProviderRevision `
    -Name "capabilities.sourceRevision"
Assert-ExactValue `
    -Actual ([string](Get-RequiredProperty $capabilities "sourceTree" "capabilities")) `
    -Expected "clean" `
    -Name "capabilities.sourceTree"
Assert-ExactValue `
    -Actual ([bool](Get-RequiredProperty $capabilities "runtimeVerified" "capabilities")) `
    -Expected $false `
    -Name "capabilities.runtimeVerified"

$gitState = Get-ProviderGitState -Root $resolvedProviderRoot
Assert-ExactValue `
    -Actual $gitState.Head `
    -Expected $requiredProviderRevision `
    -Name "provider.gitHead"
Assert-ExactValue `
    -Actual $gitState.IsClean `
    -Expected $true `
    -Name "provider.sourceTreeClean"

$targetCapability = Get-TargetCapability `
    -Capabilities $capabilities `
    -Channel $Target
$checkpointTarget = Get-TargetCapability `
    -Capabilities $checkpoint `
    -Channel $Target
$requiredTarget = $requiredTargets[$Target]
foreach ($propertyName in @(
    "version",
    "commit",
    "gameAssemblySha256",
    "adapterVersion",
    "artifactSha256"
)) {
    Assert-ExactValue `
        -Actual ([string](Get-RequiredProperty $targetCapability $propertyName "capabilities.target")) `
        -Expected ([string]$requiredTarget[$propertyName]) `
        -Name "fixedTarget.$propertyName"
}
foreach ($propertyName in @(
    "adapterVersion",
    "artifactSha256"
)) {
    Assert-ExactValue `
        -Actual ([string](Get-RequiredProperty $targetCapability $propertyName "capabilities.target")) `
        -Expected ([string](Get-RequiredProperty $checkpointTarget $propertyName "checkpoint.target")) `
        -Name "target.$propertyName"
}

$providerArtifactPath = Resolve-ExistingFile `
    -Path (Join-Path $resolvedProviderRoot "artifacts\$Target\StS2Sim.dll") `
    -Name "provider artifact"
$providerArtifactSha256 = Get-Sha256 -Path $providerArtifactPath
Assert-ExactValue `
    -Actual $providerArtifactSha256 `
    -Expected (([string]$targetCapability.artifactSha256).ToLowerInvariant()) `
    -Name "providerArtifactSha256"

$gameIdentity = Assert-GameIdentity `
    -DataDir $resolvedGameDataDir `
    -TargetCapability $targetCapability
$unsupportedRegistryPath = Resolve-ExistingFile `
    -Path (Join-Path $resolvedProviderRoot "maintenance\unsupported-mechanisms.json") `
    -Name "unsupported registry"
$capabilityManifestSha256 = Get-Sha256 -Path $resolvedCapabilitiesPath
$unsupportedRegistrySha256 = Get-Sha256 -Path $unsupportedRegistryPath
Assert-ExactValue `
    -Actual $unsupportedRegistrySha256 `
    -Expected $requiredUnsupportedRegistrySha256 `
    -Name "unsupportedRegistry.sha256"
$metadata = New-ObservationMetadata `
    -Scenario $scenario `
    -TargetCapability $targetCapability `
    -ProviderArtifactSha256 $providerArtifactSha256 `
    -CapabilityManifestSha256 $capabilityManifestSha256 `
    -UnsupportedRegistrySha256 $unsupportedRegistrySha256

$supportedScenario = Get-SupportedScenario `
    -Scenario $scenario `
    -Channel $Target
if ($null -eq $supportedScenario) {
    $unsupportedRaw = "adapter.unsupported-scenario:$($scenario.scenarioId)"
    $unsupportedObservation = [ordered]@{
        scenarioId = [string]$scenario.scenarioId
        status = "Unsupported"
        events = @()
        unsupported = @(
            [ordered]@{
                scope = "scenario"
                reasonCode = "adapter.unsupported-scenario"
                providerMechanismId = $null
                detail = "No reviewed DF-S2B process mapping exists for this scenario."
                failClosed = $true
            }
        )
        metadata = $metadata
        rawProviderOutputHash = Get-TextSha256 -Text $unsupportedRaw
    }
    Write-Observation -Observation $unsupportedObservation -Path $OutputPath
    return
}

Assert-SupportedScenarioShape `
    -Scenario $scenario `
    -Supported $supportedScenario
$resolvedDotnetExe = Resolve-Executable -Command $DotnetExe
$resolvedCancellationFile = $null
if (-not [string]::IsNullOrWhiteSpace($CancellationFile)) {
    $cancellationParent = Split-Path -Parent $CancellationFile
    if ([string]::IsNullOrWhiteSpace($cancellationParent)) {
        $cancellationParent = (Get-Location).Path
    }
    if (-not (Test-Path -LiteralPath $cancellationParent -PathType Container)) {
        throw "CancellationFile directory not found: $cancellationParent"
    }
    $resolvedCancellationFile = Join-Path `
        (Resolve-Path -LiteralPath $cancellationParent).Path `
        (Split-Path -Leaf $CancellationFile)
}
$effectiveTimeoutMilliseconds = if ($TimeoutMilliseconds -gt 0) {
    $TimeoutMilliseconds
}
else {
    $TimeoutSeconds * 1000
}
$processResult = Invoke-ProviderProcess `
    -Executable $resolvedDotnetExe `
    -ArtifactPath $providerArtifactPath `
    -Channel $Target `
    -DataDir $resolvedGameDataDir `
    -WorkingDirectory $resolvedProviderRoot `
    -EffectiveTimeoutMilliseconds $effectiveTimeoutMilliseconds `
    -Mode $ProviderMode `
    -CancellationSignalPath $resolvedCancellationFile
$rawProviderOutput = "stdout:`n$($processResult.Stdout)`nstderr:`n$($processResult.Stderr)"
$rawProviderOutputHash = Get-TextSha256 -Text $rawProviderOutput

$failureReason = $null
$failureDetail = $null
$failureExitCode = 20
if (-not $processResult.Started) {
    $failureReason = "provider.start-failed"
    $failureDetail = $processResult.Stderr
}
elseif ($processResult.TimedOut) {
    $failureReason = "provider.timeout"
    $failureDetail = "StS2Sim exceeded the explicit $effectiveTimeoutMilliseconds ms timeout."
    $failureExitCode = 21
}
elseif ($processResult.Cancelled) {
    $failureReason = "provider.cancelled"
    $failureDetail = "StS2Sim was stopped by the explicit cancellation signal."
    $failureExitCode = 24
}
elseif ($processResult.ExitCode -ne 0) {
    $failureReason = "provider.nonzero-exit"
    $failureDetail = "StS2Sim exited with code $($processResult.ExitCode)."
    $failureExitCode = 22
}
elseif (
    $processResult.Stdout.IndexOf(
        $supportedScenario.PassMarker,
        [System.StringComparison]::Ordinal) -lt 0 -or
    $processResult.Stdout.IndexOf(
        "16/16 passed.",
        [System.StringComparison]::Ordinal) -lt 0
) {
    $failureReason = "provider.output-incomplete"
    $failureDetail = "Expected SS4 markers were not present in provider stdout."
    $failureExitCode = 23
}

if ($null -ne $failureReason) {
    $failureObservation = [ordered]@{
        scenarioId = [string]$scenario.scenarioId
        status = "ProviderFailure"
        events = @()
        unsupported = @(
            [ordered]@{
                scope = "provider"
                reasonCode = $failureReason
                providerMechanismId = "sts2sim.ss4-tests"
                detail = $failureDetail
                failClosed = $true
            }
        )
        metadata = $metadata
        rawProviderOutputHash = $rawProviderOutputHash
    }
    Write-Observation -Observation $failureObservation -Path $OutputPath
    exit $failureExitCode
}

$completeObservation = [ordered]@{
    scenarioId = [string]$scenario.scenarioId
    status = "Complete"
    events = @(
        [ordered]@{
            eventId = $supportedScenario.EventId
            sourceId = $supportedScenario.SourceId
            phase = $supportedScenario.Phase
            order = $supportedScenario.Order
            lane = $supportedScenario.Lane
            granularity = $supportedScenario.Granularity
            amount = $supportedScenario.Amount
            status = "Observed"
            reasonCode = "sts2sim.ss4-named-case-passed"
            providerDetail = "$($supportedScenario.Name) was observed through the fixed SS4 named case."
        }
    )
    blockableTotal = $supportedScenario.BlockableTotal
    directHpLossTotal = $supportedScenario.DirectHpLossTotal
    unsupported = @()
    metadata = $metadata
    rawProviderOutputHash = $rawProviderOutputHash
}
Write-Observation -Observation $completeObservation -Path $OutputPath
