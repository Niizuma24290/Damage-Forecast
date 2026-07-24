internal static class ExternalObservationAdapterContractCases
{
    private const string AdapterRelativePath =
        "tools/Invoke-DamageForecastExternalObservation.ps1";

    private const string ExplicitTestsRelativePath =
        "tests/DamageForecast.ExternalObservationAdapterTests/"
        + "Invoke-ExternalObservationAdapterTests.ps1";

    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            new(
                "EA-001",
                "ExternalObservationAdapter",
                "ExternalAdapter.IsExplicitAndNotRequiredByOrdinaryGuardrail",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    var guardrail = ReadRequired("scripts/Test-ForecastGuardrails.ps1");
                    assert.True(
                        adapter.Contains("[CmdletBinding()]", StringComparison.Ordinal)
                        && !guardrail.Contains(
                            "Invoke-DamageForecastExternalObservation",
                            StringComparison.Ordinal)
                        && !guardrail.Contains("StS2Sim", StringComparison.Ordinal),
                        "explicit adapter exists and ordinary guardrail has no adapter/provider dependency",
                        "adapter or ordinary guardrail wiring mismatch");
                }),
            new(
                "EA-002",
                "ExternalObservationAdapter",
                "ExternalAdapter.RequiresAllAuthorityAndTargetInputs",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    foreach (var parameter in new[]
                    {
                        "$Target",
                        "$ScenarioPath",
                        "$OutputPath",
                        "$ProviderRoot",
                        "$CheckpointPath",
                        "$CapabilitiesPath",
                        "$GameDataDir",
                        "$DotnetExe"
                    })
                    {
                        assert.True(
                            adapter.Contains(parameter, StringComparison.Ordinal),
                            $"adapter declares {parameter}",
                            $"missing {parameter}");
                    }

                    assert.True(
                        adapter.Contains(
                            "[Parameter(Mandatory = $true)]",
                            StringComparison.Ordinal),
                        "adapter inputs are explicit",
                        "mandatory parameter marker missing");
                }),
            new(
                "EA-003",
                "ExternalObservationAdapter",
                "ExternalAdapter.PinsExactCleanProviderCheckpoint",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    foreach (var marker in new[]
                    {
                        "42396191e4bd66ca8ab27cd9b9b9f4f537966978",
                        "checkpoint.sourceRevision",
                        "checkpoint.sourceTree",
                        "capabilities.sourceRevision",
                        "provider.gitHead",
                        "provider.sourceTreeClean",
                        "checkpoint.sha256",
                        "capabilities.sha256",
                        "unsupportedRegistry.sha256",
                        "status --porcelain"
                    })
                    {
                        assert.True(
                            adapter.Contains(marker, StringComparison.Ordinal),
                            $"adapter contains checkpoint guard {marker}",
                            $"missing checkpoint guard {marker}");
                    }
                }),
            new(
                "EA-004",
                "ExternalObservationAdapter",
                "ExternalAdapter.ProcessBoundaryIsHiddenRedirectedAndTimeoutControlled",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    foreach (var marker in new[]
                    {
                        "System.Diagnostics.ProcessStartInfo",
                        "UseShellExecute = $false",
                        "CreateNoWindow = $true",
                        "RedirectStandardOutput = $true",
                        "RedirectStandardError = $true",
                        "WaitForExit([Math]::Min(25, $remaining))",
                        "$process.Kill()",
                        "provider.timeout",
                        "provider.cancelled",
                        "provider.nonzero-exit",
                        "provider.output-incomplete"
                    })
                    {
                        assert.True(
                            adapter.Contains(marker, StringComparison.Ordinal),
                            $"adapter contains process guard {marker}",
                            $"missing process guard {marker}");
                    }
                }),
            new(
                "EA-005",
                "ExternalObservationAdapter",
                "ExternalAdapter.HashAndEvidenceMetadataFailClosed",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    foreach (var marker in new[]
                    {
                        "providerArtifactSha256",
                        "gameAssemblySha256",
                        "capabilityManifestSha256",
                        "unsupportedRegistrySha256",
                        "evidenceLevel = \"L2\"",
                        "runtimeVerified = $false",
                        "sourceDirty = $false"
                    })
                    {
                        assert.True(
                            adapter.Contains(marker, StringComparison.Ordinal),
                            $"adapter contains evidence guard {marker}",
                            $"missing evidence guard {marker}");
                    }
                }),
            new(
                "EA-006",
                "ExternalObservationAdapter",
                "ExternalAdapter.OnlyReviewedBurnAndDoubtMappingsCanComplete",
                assert =>
                {
                    var adapter = ReadRequired(AdapterRelativePath);
                    assert.True(
                        adapter.Contains(
                            "turn-end.burn.blockable.v1",
                            StringComparison.Ordinal)
                        && adapter.Contains(
                            "[PASS] Burn turn-end HP loss",
                            StringComparison.Ordinal)
                        && adapter.Contains(
                            "turn-end.doubt.power.v1",
                            StringComparison.Ordinal)
                        && adapter.Contains(
                            "[PASS] Doubt turn-end power",
                            StringComparison.Ordinal)
                        && adapter.Contains(
                            "adapter.unsupported-scenario",
                            StringComparison.Ordinal),
                        "only reviewed mappings complete; unknown scenarios are unsupported",
                        "reviewed mapping boundary missing");
                }),
            new(
                "EA-007",
                "ExternalObservationAdapter",
                "ExternalAdapter.ExplicitIntegrationHarnessIsSeparate",
                assert =>
                {
                    var tests = ReadRequired(ExplicitTestsRelativePath);
                    var guardrail = ReadRequired("scripts/Test-ForecastGuardrails.ps1");
                    assert.True(
                        tests.Contains(
                            "Invoke-DamageForecastExternalObservation.ps1",
                            StringComparison.Ordinal)
                        && tests.Contains("provider.timeout", StringComparison.Ordinal)
                        && tests.Contains(
                            "provider.nonzero-exit",
                            StringComparison.Ordinal)
                        && !guardrail.Contains(
                            "DamageForecast.ExternalObservationAdapterTests",
                            StringComparison.Ordinal),
                        "explicit integration tests remain outside ordinary guardrail",
                        "integration test boundary mismatch");
                })
        ];
    }

    private static string ReadRequired(string relativePath)
    {
        var path = Path.Combine(
            IdentityContractFixture.RepositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required adapter file not found: {path}");
        return File.ReadAllText(path);
    }
}
