[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProviderRoot,

    [Parameter(Mandatory = $true)]
    [string]$CheckpointPath,

    [Parameter(Mandatory = $true)]
    [string]$CapabilitiesPath,

    [Parameter(Mandatory = $true)]
    [string]$StableGameDataDir,

    [Parameter(Mandatory = $true)]
    [string]$BetaGameDataDir,

    [Parameter(Mandatory = $true)]
    [string]$DotnetExe,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (
    Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")
).Path
$adapterPath = Join-Path `
    $repositoryRoot `
    "tools\Invoke-DamageForecastExternalObservation.ps1"
$fixtureRoot = Join-Path `
    $repositoryRoot `
    "tests\DamageForecast.ContractTests\fixtures\external-observation"

if (Test-Path -LiteralPath $OutputRoot) {
    throw "OutputRoot already exists: $OutputRoot"
}
[void](New-Item -ItemType Directory -Path $OutputRoot)
$resolvedOutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path

$script:passed = 0
$script:failed = 0

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-Json {
    param([string]$Path)

    return Get-Content -LiteralPath $Path -Encoding utf8 -Raw |
        ConvertFrom-Json
}

function Invoke-Adapter {
    param(
        [string]$Name,
        [string]$Target,
        [string]$ScenarioPath,
        [string]$GameDataDir,
        [string]$OutputPath,
        [string]$ProcessExe = $DotnetExe,
        [int]$TimeoutSeconds = 120,
        [int]$TimeoutMilliseconds = 0,
        [string]$ProviderMode = "ss4-tests",
        [string]$CancellationFile
    )

    $adapterArguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $adapterPath,
        "-Target",
        $Target,
        "-ScenarioPath",
        $ScenarioPath,
        "-OutputPath",
        $OutputPath,
        "-ProviderRoot",
        $ProviderRoot,
        "-CheckpointPath",
        $CheckpointPath,
        "-CapabilitiesPath",
        $CapabilitiesPath,
        "-GameDataDir",
        $GameDataDir,
        "-DotnetExe",
        $ProcessExe,
        "-TimeoutSeconds",
        $TimeoutSeconds,
        "-TimeoutMilliseconds",
        $TimeoutMilliseconds,
        "-ProviderMode",
        $ProviderMode
    )
    if (-not [string]::IsNullOrWhiteSpace($CancellationFile)) {
        $adapterArguments += @("-CancellationFile", $CancellationFile)
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $adapterOutput = & powershell @adapterArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        $adapterOutput | ForEach-Object {
            Write-Verbose "$Name :: $_"
        }
    }
    return $exitCode
}

function Run-Case {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    try {
        & $Action
        $script:passed++
        Write-Output "PASS $Name"
    }
    catch {
        $script:failed++
        Write-Output "FAIL $Name :: $($_.Exception.Message)"
    }
}

$stableScenario = Join-Path $fixtureRoot "stable-burn.scenario.json"
$betaScenario = Join-Path $fixtureRoot "beta-doubt.scenario.json"

Run-Case "stable Burn process observation" {
    $output = Join-Path $resolvedOutputRoot "stable-burn.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "stable-burn" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output
    Assert-True ($exitCode -eq 0) "stable adapter exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "Complete") "stable status was not Complete"
    Assert-True ($observation.blockableTotal -eq 2) "stable blockable total was not 2"
    Assert-True ($observation.metadata.sourceRevision -ceq "42396191e4bd66ca8ab27cd9b9b9f4f537966978") `
        "stable source revision mismatch"
    Assert-True ($observation.metadata.runtimeVerified -eq $false) `
        "stable observation promoted RuntimeVerified"
}

Run-Case "beta Doubt process observation" {
    $output = Join-Path $resolvedOutputRoot "beta-doubt.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "beta-doubt" `
        -Target "beta" `
        -ScenarioPath $betaScenario `
        -GameDataDir $BetaGameDataDir `
        -OutputPath $output
    Assert-True ($exitCode -eq 0) "beta adapter exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "Complete") "beta status was not Complete"
    Assert-True ($observation.directHpLossTotal -eq 0) "beta direct total was not 0"
    Assert-True ($observation.events[0].lane -ceq "NonDamage") "beta lane was not NonDamage"
    Assert-True ($observation.metadata.runtimeVerified -eq $false) `
        "beta observation promoted RuntimeVerified"
}

Run-Case "same target and seed repeat deterministically" {
    $firstPath = Join-Path $resolvedOutputRoot "stable-repeat-1.observation.json"
    $secondPath = Join-Path $resolvedOutputRoot "stable-repeat-2.observation.json"
    $firstExit = Invoke-Adapter `
        -Name "repeat-1" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $firstPath
    $secondExit = Invoke-Adapter `
        -Name "repeat-2" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $secondPath
    Assert-True ($firstExit -eq 0 -and $secondExit -eq 0) `
        "repeat process invocation failed"
    $first = Read-Json $firstPath
    $second = Read-Json $secondPath
    Assert-True ($first.rawProviderOutputHash -ceq $second.rawProviderOutputHash) `
        "raw provider hashes differed"
    Assert-True (
        ($first.events | ConvertTo-Json -Depth 10 -Compress) -ceq
        ($second.events | ConvertTo-Json -Depth 10 -Compress)
    ) "semantic events differed"
}

Run-Case "unknown scenario fails closed without provider result" {
    $unknownScenarioPath = Join-Path $resolvedOutputRoot "unknown.scenario.json"
    $unknownScenario = Read-Json $stableScenario
    $unknownScenario.scenarioId = "enemy-turn.attack-intent.v1"
    $unknownScenario.requestedCapabilities = @("real-enemy-turn")
    $unknownScenario |
        ConvertTo-Json -Depth 20 |
        Set-Content -LiteralPath $unknownScenarioPath -Encoding utf8
    $output = Join-Path $resolvedOutputRoot "unknown.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "unknown" `
        -Target "stable" `
        -ScenarioPath $unknownScenarioPath `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -ProcessExe "definitely-missing-dotnet.exe"
    Assert-True ($exitCode -eq 0) "unsupported scenario exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "Unsupported") "unknown status was not Unsupported"
    Assert-True ($observation.unsupported[0].failClosed -eq $true) `
        "unknown scenario was not fail-closed"
}

Run-Case "cross-target request fails before output" {
    $output = Join-Path $resolvedOutputRoot "cross-target.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "cross-target" `
        -Target "beta" `
        -ScenarioPath $stableScenario `
        -GameDataDir $BetaGameDataDir `
        -OutputPath $output
    Assert-True ($exitCode -ne 0) "cross-target request unexpectedly succeeded"
    Assert-True (-not (Test-Path -LiteralPath $output)) `
        "cross-target request wrote an observation"
}

Run-Case "provider timeout writes fail-closed observation" {
    $output = Join-Path $resolvedOutputRoot "timeout.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "timeout" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -TimeoutMilliseconds 1
    Assert-True ($exitCode -eq 21) "timeout exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "ProviderFailure") `
        "timeout status was not ProviderFailure"
    Assert-True ($observation.unsupported[0].reasonCode -ceq "provider.timeout") `
        "timeout reason code mismatch"
    Assert-True ($observation.unsupported[0].failClosed -eq $true) `
        "timeout was not fail-closed"
}

Run-Case "provider nonzero exit writes fail-closed observation" {
    $output = Join-Path $resolvedOutputRoot "nonzero.observation.json"
    $powerShellExe = (Get-Command powershell -CommandType Application).Source
    $exitCode = Invoke-Adapter `
        -Name "nonzero" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -ProcessExe $powerShellExe
    Assert-True ($exitCode -eq 22) "nonzero exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "ProviderFailure") `
        "nonzero status was not ProviderFailure"
    Assert-True ($observation.unsupported[0].reasonCode -ceq "provider.nonzero-exit") `
        "nonzero reason code mismatch"
}

Run-Case "supported scenario with missing tool fails before output" {
    $output = Join-Path $resolvedOutputRoot "missing-tool.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "missing-tool" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -ProcessExe "definitely-missing-dotnet.exe"
    Assert-True ($exitCode -ne 0) "missing tool unexpectedly succeeded"
    Assert-True (-not (Test-Path -LiteralPath $output)) `
        "missing tool wrote an observation"
}

Run-Case "zero exit with incomplete output fails closed" {
    $output = Join-Path $resolvedOutputRoot "incomplete.observation.json"
    $exitCode = Invoke-Adapter `
        -Name "incomplete" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -ProviderMode "contract-probe"
    Assert-True ($exitCode -eq 23) "incomplete output exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "ProviderFailure") `
        "incomplete output status was not ProviderFailure"
    Assert-True ($observation.unsupported[0].reasonCode -ceq "provider.output-incomplete") `
        "incomplete output reason code mismatch"
}

Run-Case "explicit cancellation writes fail-closed observation" {
    $output = Join-Path $resolvedOutputRoot "cancelled.observation.json"
    $cancellationFile = Join-Path $resolvedOutputRoot "cancel.signal"
    Set-Content -LiteralPath $cancellationFile -Value "cancel" -Encoding ascii
    $exitCode = Invoke-Adapter `
        -Name "cancelled" `
        -Target "stable" `
        -ScenarioPath $stableScenario `
        -GameDataDir $StableGameDataDir `
        -OutputPath $output `
        -CancellationFile $cancellationFile
    Assert-True ($exitCode -eq 24) "cancellation exit code was $exitCode"
    $observation = Read-Json $output
    Assert-True ($observation.status -ceq "ProviderFailure") `
        "cancellation status was not ProviderFailure"
    Assert-True ($observation.unsupported[0].reasonCode -ceq "provider.cancelled") `
        "cancellation reason code mismatch"
}

Write-Output "SUMMARY discovered=$($script:passed + $script:failed) passed=$script:passed failed=$script:failed"
if ($script:failed -ne 0) {
    exit 1
}
