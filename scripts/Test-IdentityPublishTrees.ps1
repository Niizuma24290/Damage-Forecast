param(
    [string]$StableTree = "work/publish/stable/damage-forecast",
    [string]$BetaTree = "work/publish/beta/damage-forecast",
    [string]$ContractPath = "tests/DamageForecast.ContractTests/identity-contract.json",
    [string]$PersistenceContractPath = "tests/DamageForecast.ContractTests/post-g6-persistence-contract.json",
    [switch]$ApproveHashDifference
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Assert-ApprovedPublishRoot {
    param(
        [string]$Target,
        [string]$ResolvedTree,
        [string]$ApprovedRelativeRoot
    )

    $approvedRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ApprovedRelativeRoot)).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $approvedPrefix = $approvedRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $ResolvedTree.StartsWith($approvedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Target publish tree must remain under approved publish root $approvedRoot; actual=$ResolvedTree"
    }
}

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $repoRoot $Path)).Path
}

function Assert-Equal {
    param(
        [string]$Name,
        $Expected,
        $Actual
    )

    if ($Actual -ne $Expected) {
        throw "$Name mismatch: expected=$Expected; actual=$Actual"
    }
}

function Test-PublishTree {
    param(
        [string]$Target,
        [string]$TreePath,
        $Contract,
        [string]$ExpectedManifestVersion
    )

    $resolvedTree = Resolve-RepoPath -Path $TreePath
    if (-not (Test-Path -LiteralPath $resolvedTree -PathType Container)) {
        throw "$Target publish tree not found: $resolvedTree"
    }
    Assert-ApprovedPublishRoot `
        -Target $Target `
        -ResolvedTree $resolvedTree `
        -ApprovedRelativeRoot $Contract.publishValidationContract.publishRoot

    $expectedFiles = @($Contract.packaging.publishWhitelist | Sort-Object)
    $files = @(Get-ChildItem -LiteralPath $resolvedTree -File -Recurse)
    $relativeFiles = @($files | ForEach-Object {
        $_.FullName.Substring($resolvedTree.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
    } | Sort-Object)

    if ($files.Count -ne $Contract.publishValidationContract.exactFileCount -or
        (($relativeFiles -join "`n") -ne ($expectedFiles -join "`n"))) {
        throw "$Target publish tree must contain exactly $($expectedFiles -join ', ') at its top level; actual=$($relativeFiles -join ', ')"
    }

    $identity = $Contract.activeIdentity
    $manifestName = "$($identity.manifestStem).json"
    $manifestPath = Join-Path $resolvedTree $manifestName
    $dllPath = Join-Path $resolvedTree $identity.dllName
    try {
        $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    }
    catch {
        throw "$Target manifest is invalid JSON: $manifestPath`n$($_.Exception.Message)"
    }

    Assert-Equal "$Target manifest id" $identity.modId $manifest.id
    Assert-Equal "$Target manifest stem" $identity.manifestStem ([System.IO.Path]::GetFileNameWithoutExtension($manifestPath))
    Assert-Equal "$Target manifest version" $ExpectedManifestVersion $manifest.version
    Assert-Equal "$Target manifest has_dll" $Contract.packaging.hasDll $manifest.has_dll
    Assert-Equal "$Target manifest has_pck" $Contract.packaging.hasPck $manifest.has_pck
    Assert-Equal "$Target manifest min_game_version" $Contract.packaging.minGameVersion $manifest.min_game_version

    $dependencies = @($manifest.dependencies)
    if ($dependencies.Count -ne 1) {
        throw "$Target manifest must contain exactly one dependency; actual=$($dependencies.Count)"
    }
    Assert-Equal "$Target dependency id" $Contract.packaging.dependencyId $dependencies[0].id
    Assert-Equal "$Target dependency min_version" $Contract.packaging.dependencyMinVersion $dependencies[0].min_version

    try {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath).Name
    }
    catch {
        throw "$Target DLL is not a readable managed assembly: $dllPath`n$($_.Exception.Message)"
    }
    Assert-Equal "$Target assembly identity" $identity.assemblyName $assemblyName

    $assemblyBytes = [System.IO.File]::ReadAllBytes($dllPath)
    $assemblyAscii = [System.Text.Encoding]::ASCII.GetString($assemblyBytes)
    $assemblyUtf16 = [System.Text.Encoding]::Unicode.GetString($assemblyBytes)
    $debugTraceMarkerMatches = @($Contract.publishValidationContract.forbiddenAssemblyMarkers | Where-Object {
        $assemblyAscii.IndexOf($_, [System.StringComparison]::Ordinal) -ge 0 -or
        $assemblyUtf16.IndexOf($_, [System.StringComparison]::Ordinal) -ge 0
    })
    if ($debugTraceMarkerMatches.Count -ne 0) {
        throw "$Target publish DLL contains personal Debug Trace markers: $($debugTraceMarkerMatches -join ', ')"
    }

    $hashes = [ordered]@{}
    foreach ($name in $expectedFiles) {
        $hashes[$name] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $resolvedTree $name)).Hash
    }

    return [pscustomobject][ordered]@{
        target = $Target
        tree = $resolvedTree
        fileCount = $files.Count
        files = $relativeFiles
        assemblyName = $assemblyName
        manifestId = $manifest.id
        manifestVersion = $manifest.version
        debugTraceMarkerMatches = $debugTraceMarkerMatches.Count
        sha256 = [pscustomobject]$hashes
    }
}

$resolvedContractPath = Resolve-RepoPath -Path $ContractPath
$resolvedPersistenceContractPath = Resolve-RepoPath -Path $PersistenceContractPath
try {
    $contract = Get-Content -Raw -Encoding UTF8 -LiteralPath $resolvedContractPath | ConvertFrom-Json
}
catch {
    throw "Identity contract is invalid JSON: $resolvedContractPath`n$($_.Exception.Message)"
}

try {
    $persistenceContract = Get-Content -Raw -Encoding UTF8 -LiteralPath $resolvedPersistenceContractPath | ConvertFrom-Json
}
catch {
    throw "Post-G6 persistence contract is invalid JSON: $resolvedPersistenceContractPath`n$($_.Exception.Message)"
}
if ($persistenceContract.status -ne "c4-complete" -or
    $persistenceContract.approval.gate -ne "C2" -or
    $persistenceContract.closure.gate -ne "C4") {
    throw "Publish validation requires the approved C2 persistence overlay and C4 closure contract."
}

if ($contract.status -ne "active-migrated") {
    throw "Publish validation requires active-migrated contract; actual=$($contract.status)"
}
if ($contract.publishValidationContract.validatorScriptPath -ne "scripts/Test-IdentityPublishTrees.ps1" -or
    $contract.publishValidationContract.publishRoot -ne "work/publish" -or
    @($contract.publishValidationContract.forbiddenAssemblyMarkers).Count -eq 0 -or
    $contract.publishValidationContract.hashAlgorithm -ne "SHA256" -or
    $contract.publishValidationContract.hashDifferencePolicy -ne "reject-unless-explicitly-approved") {
    throw "Identity contract publish validation policy is incomplete or unsupported."
}

$stable = Test-PublishTree -Target "stable" -TreePath $StableTree -Contract $contract -ExpectedManifestVersion $persistenceContract.release.version
$beta = Test-PublishTree -Target "beta" -TreePath $BetaTree -Contract $contract -ExpectedManifestVersion $persistenceContract.release.version
$expectedFiles = @($contract.packaging.publishWhitelist | Sort-Object)
$differentFiles = @($expectedFiles | Where-Object { $stable.sha256.$_ -ne $beta.sha256.$_ })
$identical = $differentFiles.Count -eq 0

$result = [pscustomobject][ordered]@{
    contractStatus = $contract.status
    algorithm = "SHA256"
    artifactsIdentical = $identical
    differentFiles = $differentFiles
    hashDifferenceApproved = [bool]$ApproveHashDifference
    stable = $stable
    beta = $beta
}

if (-not $identical -and -not $ApproveHashDifference) {
    $result | ConvertTo-Json -Depth 8 -Compress | Write-Output
    throw "Stable/beta publish hashes differ for $($differentFiles -join ', '); explicit -ApproveHashDifference is required after the difference is explained and approved."
}

$result | ConvertTo-Json -Depth 8 -Compress
