using DamageForecast.Settings;
using DamageForecast.UI;
using DamageForecast.Compatibility;
using System.Text;
using System.Text.Json;

internal static class HudPlacementConfigContractCases
{
    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "HPC-001",
            "HudPlacementConfig",
            "HudPlacementConfig.SchemaV1AndV2_HaveExactKeySets",
            assert =>
            {
                var v1 = HudPlacementConfigSchema.V1PropertyOrder;
                var v2 = HudPlacementConfigSchema.V2PropertyOrder;
                var added = v2.Except(v1, StringComparer.Ordinal).ToArray();
                var removed = v1.Except(v2, StringComparer.Ordinal).ToArray();
                assert.True(
                    v1.Length == 18
                    && v2.Length == 20
                    && removed.SequenceEqual(["HudAnchorPreset"])
                    && added.SequenceEqual(
                    [
                        "ExpectedHpLossPlacementPreset",
                        "IncomingDamagePlacementPreset",
                        "DetailsPlacementPreset"
                    ]),
                    "V1=18; V2=20; one anchor replaced by three placement keys",
                    $"v1={v1.Length}; v2={v2.Length}; removed={string.Join(',', removed)}; added={string.Join(',', added)}");
            });

        yield return new(
            "HPC-002",
            "HudPlacementConfig",
            "HudPlacementConfig.V1Upgrade_CopiesAnchorToAllThreePlacements",
            assert =>
            {
                var mappings = Enum.GetValues<DamageForecastHudAnchor>()
                    .Select(anchor => (
                        anchor,
                        upgraded: HudPlacementConfigMigrationPolicy.Upgrade(
                            new HudPlacementConfigV1(
                                anchor,
                                FreezeHudNumbersAfterTurnEnd: true))))
                    .ToArray();
                assert.True(
                    mappings.All(item =>
                        item.upgraded.ExpectedHpLossPlacementPreset
                            == item.upgraded.IncomingDamagePlacementPreset
                        && item.upgraded.ExpectedHpLossPlacementPreset
                            == item.upgraded.DetailsPlacementPreset),
                    "each V1 anchor is copied to all three V2 placements",
                    string.Join(',', mappings.Select(item => $"{item.anchor}:{item.upgraded.ExpectedHpLossPlacementPreset}")));
            });

        yield return new(
            "HPC-003",
            "HudPlacementConfig",
            "HudPlacementConfig.V1Upgrade_NormalizesFreezeToTrue",
            assert =>
            {
                var actual = HudPlacementConfigMigrationPolicy.Upgrade(
                    new HudPlacementConfigV1(
                        DamageForecastHudAnchor.HealthBarLeft,
                        FreezeHudNumbersAfterTurnEnd: false));
                assert.Equal(true, actual.FreezeHudNumbersAfterTurnEnd);
            });

        yield return new(
            "HPC-004",
            "HudPlacementConfig",
            "HudPlacementConfig.Defaults_PreserveCurrentRightAnchorAndFixedFreeze",
            assert =>
            {
                var actual = HudPlacementConfigSchema.Defaults;
                assert.True(
                    actual.ExpectedHpLossPlacementPreset == HudPlacementPreset.HealthBarRight
                    && actual.IncomingDamagePlacementPreset == HudPlacementPreset.HealthBarRight
                    && actual.DetailsPlacementPreset == HudPlacementPreset.HealthBarRight
                    && actual.FreezeHudNumbersAfterTurnEnd,
                    "three HealthBarRight defaults; freeze=true",
                    actual.ToString());
            });

        yield return new(
            "HPC-005",
            "HudPlacementConfig",
            "HudPlacementConfig.EqualSupportedPlacements_DowngradeExactly",
            assert =>
            {
                var source = new HudPlacementConfigV2(
                    HudPlacementPreset.HealthBarAbove,
                    HudPlacementPreset.HealthBarAbove,
                    HudPlacementPreset.HealthBarAbove,
                    FreezeHudNumbersAfterTurnEnd: false);
                var actual = HudPlacementConfigMigrationPolicy.TryDowngrade(source);
                assert.True(
                    actual.Status == HudPlacementConfigDowngradeStatus.Exact
                    && actual.Config?.HudAnchorPreset == DamageForecastHudAnchor.HealthBarAbove
                    && actual.Config?.FreezeHudNumbersAfterTurnEnd == true,
                    "exact HealthBarAbove downgrade with freeze normalized true",
                    actual.ToString());
            });

        yield return new(
            "HPC-006",
            "HudPlacementConfig",
            "HudPlacementConfig.DivergentPlacements_BlockLossyRollback",
            assert =>
            {
                var actual = HudPlacementConfigMigrationPolicy.TryDowngrade(
                    new HudPlacementConfigV2(
                        HudPlacementPreset.HealthBarLeft,
                        HudPlacementPreset.HealthBarRight,
                        HudPlacementPreset.HealthBarLeft,
                        FreezeHudNumbersAfterTurnEnd: true));
                assert.True(
                    actual.Status == HudPlacementConfigDowngradeStatus.DivergentPlacements
                    && actual.Config is null,
                    "divergent placements fail closed without V1 output",
                    actual.ToString());
            });

        yield return new(
            "HPC-007",
            "HudPlacementConfig",
            "HudPlacementConfig.EndTurnPreset_BlocksUnsupportedRollback",
            assert =>
            {
                var actual = HudPlacementConfigMigrationPolicy.TryDowngrade(
                    new HudPlacementConfigV2(
                        HudPlacementPreset.EndTurnButtonAbove,
                        HudPlacementPreset.EndTurnButtonAbove,
                        HudPlacementPreset.EndTurnButtonAbove,
                        FreezeHudNumbersAfterTurnEnd: true));
                assert.True(
                    actual.Status == HudPlacementConfigDowngradeStatus.UnsupportedPlacement
                    && actual.Config is null,
                    "V1 has no end-turn-button anchor and rollback fails closed",
                    actual.ToString());
            });

        yield return new(
            "HPC-008",
            "HudPlacementConfig",
            "HudPlacementConfig.InvalidV1Enum_IsRejected",
            assert =>
            {
                var rejected = false;
                try
                {
                    _ = HudPlacementConfigMigrationPolicy.Upgrade(
                        new HudPlacementConfigV1(
                            (DamageForecastHudAnchor)999,
                            FreezeHudNumbersAfterTurnEnd: true));
                }
                catch (ArgumentOutOfRangeException)
                {
                    rejected = true;
                }

                assert.Equal(true, rejected);
            });

        yield return new(
            "HPC-009",
            "HudPlacementConfig",
            "HudPlacementConfig.FileMigration_UpgradesStrictV1Transactionally",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1();
                var result = HudPlacementConfigFileMigration.Run(fixture.Options);
                var raw = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;
                assert.True(
                    result.MayContinue
                    && result.Status == "MigratedV1ToV2"
                    && HudPlacementConfigFileMigration.IsStrictV2(raw)
                    && root.GetProperty("ExpectedHpLossPlacementPreset").GetString() == "HealthBarRight"
                    && root.GetProperty("IncomingDamagePlacementPreset").GetString() == "HealthBarRight"
                    && root.GetProperty("DetailsPlacementPreset").GetString() == "HealthBarRight"
                    && root.GetProperty("FreezeHudNumbersAfterTurnEnd").GetString() == "True"
                    && Directory.EnumerateFiles(
                            Path.Combine(fixture.Options.MigrationRoot, "hud-placement-v2"),
                            "*.backup",
                            SearchOption.AllDirectories)
                        .Any(),
                    "strict V1 becomes verified V2 with backup and fixed freeze",
                    result.ToString());
            });

        yield return new(
            "HPC-010",
            "HudPlacementConfig",
            "HudPlacementConfig.StrictV2Restart_IsAcceptedAndIdempotent",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1();
                var first = HudPlacementConfigFileMigration.Run(fixture.Options);
                var before = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                var restart = HudPlacementConfigFileMigration.Run(
                    fixture.Options with { TransactionId = "restart" });
                var identityBootstrap = CompatibilityBootstrap.Run(
                    fixture.Options with { TransactionId = "identity-restart" });
                var after = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                assert.True(
                    first.MayContinue
                    && restart.MayContinue
                    && restart.Status == "AlreadyV2"
                    && identityBootstrap.MayRegisterCurrentConfig
                    && before.SequenceEqual(after),
                    "strict V2 is accepted by both placement and identity bootstraps without rewrite",
                    $"restart={restart}; identity={identityBootstrap}");
            });

        yield return new(
            "HPC-011",
            "HudPlacementConfig",
            "HudPlacementConfig.V2DivergentAndEndTurnPlacements_RemainValidRuntimeState",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1();
                _ = HudPlacementConfigFileMigration.Run(fixture.Options);
                var text = File.ReadAllText(fixture.Options.CurrentConfigPath)
                    .Replace(
                        "\"ExpectedHpLossPlacementPreset\": \"HealthBarRight\"",
                        "\"ExpectedHpLossPlacementPreset\": \"EndTurnButtonAbove\"",
                        StringComparison.Ordinal)
                    .Replace(
                        "\"IncomingDamagePlacementPreset\": \"HealthBarRight\"",
                        "\"IncomingDamagePlacementPreset\": \"HealthBarLeft\"",
                        StringComparison.Ordinal);
                File.WriteAllText(
                    fixture.Options.CurrentConfigPath,
                    text,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var raw = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                var rollback = HudPlacementConfigMigrationPolicy.TryDowngrade(
                    new HudPlacementConfigV2(
                        HudPlacementPreset.EndTurnButtonAbove,
                        HudPlacementPreset.HealthBarLeft,
                        HudPlacementPreset.HealthBarRight,
                        true));
                assert.True(
                    HudPlacementConfigFileMigration.IsStrictV2(raw)
                    && rollback.Status == HudPlacementConfigDowngradeStatus.DivergentPlacements,
                    "V2 runtime accepts independent presets while rollback remains fail-closed",
                    rollback.ToString());
            });

        yield return new(
            "HPC-012",
            "HudPlacementConfig",
            "HudPlacementConfig.InvalidV2_FailsClosedWithoutOverwrite",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1();
                _ = HudPlacementConfigFileMigration.Run(fixture.Options);
                File.AppendAllText(
                    fixture.Options.CurrentConfigPath,
                    Environment.NewLine + "{}",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var before = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                var result = HudPlacementConfigFileMigration.Run(
                    fixture.Options with { TransactionId = "invalid" });
                var after = File.ReadAllBytes(fixture.Options.CurrentConfigPath);
                assert.True(
                    !result.MayContinue && before.SequenceEqual(after),
                    "invalid current bytes are preserved and startup is blocked",
                    result.ToString());
            });

        yield return new(
            "HPC-013",
            "HudPlacementConfig",
            "HudPlacementConfig.EqualV2_ReverseSyncsLosslessly",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1(includeLegacy: true);
                _ = HudPlacementConfigFileMigration.Run(fixture.Options);
                var result = CompatibilityBootstrap.ReverseSyncForRollback(
                    fixture.Options with { TransactionId = "rollback" });
                assert.True(
                    result.Status == ConfigMigrationStatus.RolledBack
                    && result.Marker?.SourceSchema == HudPlacementConfigFileMigration.SchemaId,
                    "equal health-bar placements reverse-sync with V2 source evidence",
                    result.ToString());
            });

        yield return new(
            "HPC-014",
            "HudPlacementConfig",
            "HudPlacementConfig.DivergentV2_RollbackFailsClosed",
            assert =>
            {
                using var fixture = HudPlacementMigrationFixture.CreateFromV1(includeLegacy: true);
                _ = HudPlacementConfigFileMigration.Run(fixture.Options);
                var text = File.ReadAllText(fixture.Options.CurrentConfigPath)
                    .Replace(
                        "\"IncomingDamagePlacementPreset\": \"HealthBarRight\"",
                        "\"IncomingDamagePlacementPreset\": \"HealthBarLeft\"",
                        StringComparison.Ordinal);
                File.WriteAllText(
                    fixture.Options.CurrentConfigPath,
                    text,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var legacyBefore = File.ReadAllBytes(fixture.Options.LegacyConfigPath);
                var result = CompatibilityBootstrap.ReverseSyncForRollback(
                    fixture.Options with { TransactionId = "rollback-blocked" });
                var legacyAfter = File.ReadAllBytes(fixture.Options.LegacyConfigPath);
                assert.True(
                    result.Status == ConfigMigrationStatus.FailedSafe
                    && legacyBefore.SequenceEqual(legacyAfter),
                    "divergent placements preserve legacy bytes and block rollback",
                    result.ToString());
            });
    }

    private sealed class HudPlacementMigrationFixture : IDisposable
    {
        private HudPlacementMigrationFixture(string root, ConfigMigrationOptions options)
        {
            Root = root;
            Options = options;
        }

        public string Root { get; }

        public ConfigMigrationOptions Options { get; }

        public static HudPlacementMigrationFixture CreateFromV1(bool includeLegacy = false)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "damage-forecast-hud-placement-contracts",
                Guid.NewGuid().ToString("N"));
            var configRoot = Path.Combine(root, "config");
            Directory.CreateDirectory(configRoot);
            File.Copy(
                Path.Combine(
                    IdentityContractFixture.RepositoryRoot,
                    "tests",
                    "DamageForecast.ContractTests",
                    "fixtures",
                    "config-new-default.cfg"),
                Path.Combine(configRoot, DamageForecastSchemaV1.ConfigFileName));
            if (includeLegacy)
            {
                File.Copy(
                    Path.Combine(
                        IdentityContractFixture.RepositoryRoot,
                        "tests",
                        "DamageForecast.ContractTests",
                        "fixtures",
                        "config-old-official-v0.2.0.cfg"),
                    Path.Combine(configRoot, LegacyIdentityDescriptor.ConfigFileName));
            }
            var options = new ConfigMigrationOptions(
                configRoot,
                Path.Combine(root, "migration"),
                "initial",
                "contract",
                "contract",
                DateTimeOffset.UnixEpoch);
            return new HudPlacementMigrationFixture(root, options);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
