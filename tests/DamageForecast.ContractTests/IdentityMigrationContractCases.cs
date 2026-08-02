using System.Reflection;
using System.Text.Json;
using Godot;
using DamageForecast.Settings;
using DamageForecast.Identity;
using DamageForecast.UI;

internal static class IdentityMigrationContractCases
{
    private const string DefaultPath = "tests/DamageForecast.ContractTests/fixtures/settings-default.json";
    private const string NonDefaultPath = "tests/DamageForecast.ContractTests/fixtures/settings-nondefault.json";

    public static IEnumerable<ContractCase> Create()
    {
        yield return new("IM-001", "IdentityMigration", "Migration.Schema2_ActiveMigrationIsExplicit", assert =>
        {
            var c = IdentityContractFixture.Contract;
            assert.True(c.SchemaVersion == 2 && c.Status == "active-migrated" && c.Approval.Gate == "G6-3",
                "schema=2; status=active-migrated; gate=G6-3", $"schema={c.SchemaVersion}; status={c.Status}; gate={c.Approval.Gate}");
        });
        yield return new("IM-002", "IdentityMigration", "Migration.ApprovedContract_AllFieldsAndDecisionsValidate", assert =>
        {
            var c = IdentityContractFixture.Contract;
            var errors = IdentityContractFixture.ValidateMigrationStructure();
            var g6Two = CompilerPrepared(c);
            var stagedErrors = IdentityContractFixture.ValidateMigrationStructure(g6Two);
            var migratedErrors = IdentityContractFixture.ValidateMigrationStructure(c with
                { Status = "active-migrated", ActiveIdentity = c.ApprovedTargetIdentity });
            assert.True(errors.Count == 0 && stagedErrors.Count == 0 && migratedErrors.Count == 0,
                "approved, atomic G6-2, and active-migrated lifecycle states validate",
                string.Join(" | ", errors.Concat(stagedErrors).Concat(migratedErrors)));
        });
        yield return new("IM-003", "IdentityMigration", "Migration.Validator_RejectsInvalidStatusAndDispositions", assert =>
        {
            var c = IdentityContractFixture.Contract;
            var status = IdentityContractFixture.ValidateMigrationStructure(c with { Status = "approved-ish" });
            var change = IdentityContractFixture.ValidateMigrationStructure(Replace(c, "ModId", f => f with { TargetValue = f.LegacyValue }));
            var remain = IdentityContractFixture.ValidateMigrationStructure(Replace(c, "PersistenceFileName", f => f with { TargetValue = "DamageForecast.cfg" }));
            var alias = IdentityContractFixture.ValidateMigrationStructure(Replace(c, "ConfigCompatibilityType", f => f with
                { CompatibilityOwner = "none", CompatibilityLifetime = "not-applicable" }));
            var prepared = CompilerPrepared(c);
            var split = IdentityContractFixture.ValidateMigrationStructure(prepared with
                { ActiveIdentity = prepared.ActiveIdentity with { ModId = c.ApprovedTargetIdentity.ModId } });
            assert.True(Has(status, "unknown contract status") && Has(change, "must-change target")
                && Has(remain, "must-remain target changed") && Has(alias, "compatibility alias")
                && Has(split, "atomic identity group is split"),
                "unknown status, invalid dispositions, and split atomic groups rejected",
                string.Join(" | ", status.Concat(change).Concat(remain).Concat(alias).Concat(split)));
        });
        yield return new("IM-004", "IdentityMigration", "Migration.Validator_RejectsMissingDuplicateOrUnapprovedEntries", assert =>
        {
            var c = IdentityContractFixture.Contract;
            var fields = c.MigrationFields.Where(f => f.Field != "DllName")
                .Append(c.MigrationFields.Single(f => f.Field == "ModId")).ToArray();
            var shape = IdentityContractFixture.ValidateMigrationStructure(c with { MigrationFields = fields });
            var decision = IdentityContractFixture.ValidateMigrationStructure(Replace(c, "ModId", f => f with { DecisionIds = ["G6-D99"] }));
            assert.True(Has(shape, "duplicate migration field") && Has(shape, "missing migration field: DllName")
                && Has(decision, "unknown decision reference G6-D99"),
                "duplicate/missing fields and unapproved decisions rejected", string.Join(" | ", shape.Concat(decision)));
        });
        yield return new("IM-005", "IdentityMigration", "Migration.Repository_ActiveMigratedStateIsInternallyAligned", assert =>
        {
            var snapshot = IdentityContractFixture.CaptureRepositoryIdentity();
            var c2 = PostG6PersistenceContractFixture.Contract;
            var errors = IdentityContractFixture.ValidateRepositoryAlignment()
                .Where(error => !error.StartsWith("manifest version", StringComparison.Ordinal)
                    && !error.StartsWith("persistence owner root namespace", StringComparison.Ordinal)
                    && !error.StartsWith("persistence file", StringComparison.Ordinal)
                    && !error.StartsWith("config compatibility type", StringComparison.Ordinal))
                .ToArray();
            assert.True(errors.Length == 0
                && snapshot.ManifestVersion == c2.Release.Version
                && snapshot.PersistenceFileName == c2.Current.File
                && snapshot.ConfigCompatibilityType == c2.Current.Type,
                "G6 compiler/load identity plus approved C2 persistence overlay are aligned",
                string.Join(" | ", errors));
        });
        yield return new("IM-006", "IdentityMigration", "Migration.Validator_RejectsHalfMigrationAndDuplicateIdentity", assert =>
        {
            var c = IdentityContractFixture.Contract;
            var baseline = IdentityContractFixture.CaptureRepositoryIdentity();
            var manifest = IdentityContractFixture.ValidateRepositoryAlignment(c, baseline with
                { ManifestId = c.LegacyIdentity.ModId, ManifestStem = c.LegacyIdentity.ManifestStem,
                    ManifestVersion = c.LegacyIdentity.ManifestVersion, LegacyManifestCount = 1, TargetManifestCount = 0 });
            var slots = IdentityContractFixture.ValidateRepositoryAlignment(c, baseline with
                { ProjectPath = c.LegacyIdentity.ProjectPath,
                    PublishWhitelist = [c.LegacyIdentity.DllName, c.LegacyIdentity.ManifestStem + ".json"] });
            var duplicate = IdentityContractFixture.ValidateRepositoryAlignment(c, baseline with { LegacyManifestCount = 1, TargetManifestCount = 1 });
            assert.True(Has(manifest, "manifest id") && Has(manifest, "half-migrated")
                && Has(slots, "project path") && Has(slots, "publish whitelist") && Has(duplicate, "duplicate or half-migrated"),
                "manifest-only, project/whitelist divergence, and duplicate identities rejected",
                string.Join(" | ", manifest.Concat(slots).Concat(duplicate)));
        });
        yield return new("IM-007", "IdentityMigration", "Persistence.RegistrationAndConfigStorage_AreDistinctContracts", assert =>
        {
            var p = IdentityContractFixture.Contract.PersistenceContract;
            assert.True(p.BaseLibRegistrationRole == "registry-key-only" && p.LegacyRegistrationKey == "sts2-party-watch-v2"
                && p.ApprovedTargetRegistrationKey == "damage-forecast" && p.ConfigOwnerRootNamespace == "STS2PartyWatch"
                && p.ConfigFileName == "STS2PartyWatch.cfg" && p.CompatibilityLifetime == "permanent"
                && p.LegacyRegistrationKey != p.ConfigFileName && p.ApprovedTargetRegistrationKey != p.ConfigFileName,
                "registration changes independently; STS2PartyWatch.cfg remains permanent", JsonSerializer.Serialize(p));
        });
        yield return new("IS-001", "IdentitySettings", "SettingsFixtures_OwnerFileAndOrdered18KeysAreExact", assert =>
        {
            var c = IdentityContractFixture.Contract;
            var a = Load(DefaultPath);
            var b = Load(NonDefaultPath);
            assert.True(a.Keys.SequenceEqual(c.PersistentSettings) && b.Keys.SequenceEqual(c.PersistentSettings)
                && new[] { a.Owner, b.Owner }.All(value => value == c.PersistenceContract.ConfigOwnerRootNamespace)
                && new[] { a.File, b.File }.All(value => value == c.PersistenceContract.ConfigFileName),
                "both fixtures contain ordered 18 values under STS2PartyWatch.cfg", string.Join(',', a.Keys));
        });
        yield return new("IS-002", "IdentitySettings", "SettingsFixture_HistoricalDefaultsRemainDistinctFromCurrentProductionDefaults", assert =>
        {
            var fixture = Load(DefaultPath).Settings;
            var production = CaptureProduction();
            assert.True(
                fixture == HistoricalDefaults
                && production == CurrentProductionDefaults,
                "saved historical fixture keeps RightOfExpectedHpLoss; fresh production default uses LeftOfExpectedHpLoss",
                $"fixture={Describe(fixture)}; production={Describe(production)}");
        });
        yield return new("IS-003", "IdentitySettings", "SettingsFixture_AllNonDefaultValuesAreExactAndNonDefault", assert =>
        {
            var fixture = Load(NonDefaultPath).Settings;
            var differs = fixture.Values().Zip(HistoricalDefaults.Values()).All(pair => !Equals(pair.First, pair.Second));
            assert.True(fixture == NonDefaults && differs, Describe(NonDefaults) + "; every value differs", Describe(fixture) + $"; differs={differs}");
        });
        yield return new("IS-004", "IdentitySettings", "SettingsFixture_EnumNamesAndNumericValuesRemainStable", assert =>
        {
            var stable = (int)DamageForecastConfigLanguage.English == 0 && (int)DamageForecastConfigLanguage.SimplifiedChinese == 1
                && (int)DamageDisplayMode.ExpectedHpLossOnly == 0 && (int)DamageDisplayMode.IncomingDamageOnly == 1 && (int)DamageDisplayMode.Both == 2
                && (int)IncomingDamagePlacement.LeftOfExpectedHpLoss == 0 && (int)IncomingDamagePlacement.RightOfExpectedHpLoss == 1
                && (int)DamageForecastHudAnchor.HealthBarRight == 0 && (int)DamageForecastHudAnchor.HealthBarLeft == 1
                && (int)DamageForecastHudAnchor.HealthBarAbove == 2 && (int)DamageForecastHudAnchor.HealthBarBelow == 3;
            assert.True(stable, "language=0/1; display=0/1/2; placement=0/1; anchor=0/1/2/3", "serialized enum changed");
        });
        yield return new("IS-005", "IdentitySettings", "SettingsFixture_UpgradeRestartAndRollbackPreserveAll18Values", assert =>
        {
            var raw = IdentityContractFixture.Read(NonDefaultPath);
            var before = Parse(raw).Settings;
            var upgraded = Parse(raw).Settings;
            var restarted = Load(NonDefaultPath).Settings;
            var rollback = Parse(raw).Settings;
            var p = IdentityContractFixture.Contract.PersistenceContract;
            assert.True(before == upgraded && before == restarted && before == rollback
                && p.ContinuityStrategy == "continue-reading-and-writing-same-file-without-copy-or-transform"
                && p.RollbackStrategy.Contains("same STS2PartyWatch.cfg", StringComparison.Ordinal),
                "all 18 typed values survive upgrade, fresh read, and rollback", Describe(before));
        });
        yield return new("IM-008", "IdentityMigration", "Migration.LegacyAssemblyConflictDetector_IsCaseInsensitiveAndDeduplicated", assert =>
        {
            var conflicts = LegacyIdentityConflictDetector.FindConflicts(
                ["damage-forecast", "sts2-party-watch-v2", "STS2-PARTY-WATCH-V2", null],
                "damage-forecast", "sts2-party-watch-v2");
            assert.True(conflicts.Count == 1 && conflicts[0] == "sts2-party-watch-v2",
                "one legacy assembly conflict", string.Join(",", conflicts));
        });
        yield return new("IM-009", "IdentityMigration", "Migration.G6LegacyAllowlist_RemainsHistoricalWhileC1OwnsCurrentLocations", assert =>
        {
            var entries = IdentityContractFixture.Contract.LegacyAllowlist;
            var postG6 = PostG6NamingContractFixture.Contract;
            var retiredGodotTokens = postG6.GodotNodeMappings
                .Select(mapping => mapping.Legacy)
                .ToHashSet(StringComparer.Ordinal);
            var compatibilitySource = string.Join("\n", Directory.EnumerateFiles(
                PostG6NamingContractFixture.Resolve("src/DamageForecast/Compatibility"), "*.cs")
                .Select(File.ReadAllText));
            var currentCompatibilityLocationsValid = compatibilitySource.Contains("STS2PartyWatch.cfg", StringComparison.Ordinal)
                && compatibilitySource.Contains("EnablePartyWatchHud", StringComparison.Ordinal)
                && compatibilitySource.Contains("sts2-party-watch-v2", StringComparison.Ordinal);
            var retiredGodotEntriesValid = entries
                .Where(entry => retiredGodotTokens.Contains(entry.Token))
                .All(entry => postG6.GodotNodeMappings.Any(mapping => mapping.Legacy == entry.Token)
                    && !IdentityContractFixture.Read("src/DamageForecast/Patches/ForecastRefreshPatch.cs")
                        .Contains(entry.Token, StringComparison.Ordinal));
            assert.True(entries.Count == 6
                && currentCompatibilityLocationsValid
                && retiredGodotEntriesValid
                && entries.All(entry => !string.IsNullOrWhiteSpace(entry.Owner)
                    && !string.IsNullOrWhiteSpace(entry.Purpose) && !string.IsNullOrWhiteSpace(entry.Lifetime)),
                "six G6 entries remain historical; C2 legacy literals are isolated in Compatibility; Godot literals remain retired",
                JsonSerializer.Serialize(entries));
        });
        yield return new("IM-010", "IdentityMigration", "Migration.LegacyDiagnosticMarker_IsSingleAndTimeBound", assert =>
        {
            var marker = "[STS2 Party Watch]";
            var sourceRoot = Path.Combine(IdentityContractFixture.RepositoryRoot, "src", "DamageForecast");
            var source = string.Join("\n", Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
            var occurrences = source.Split(marker, StringSplitOptions.None).Length - 1;
            var entry = IdentityContractFixture.Contract.LegacyAllowlist.Single(item => item.Token == marker);
            assert.True(occurrences == 0 && entry.Lifetime == "through-v0.2.x"
                && source.Contains("[Damage Forecast]", StringComparison.Ordinal),
                "legacy diagnostic marker expired at v0.3.0; target primary prefix remains", $"occurrences={occurrences}; lifetime={entry.Lifetime}");
        });
    }

    private static bool Has(IEnumerable<string> errors, string text) => errors.Any(error => error.Contains(text, StringComparison.Ordinal));
    private static TechnicalIdentityContract CompilerPrepared(TechnicalIdentityContract c) => c with
    {
        Status = "migration-in-progress",
        ActiveIdentity = c.LegacyIdentity with
        {
            RootNamespace = c.ApprovedTargetIdentity.RootNamespace,
            ProjectPath = c.ApprovedTargetIdentity.ProjectPath,
            TestProjectPath = c.ApprovedTargetIdentity.TestProjectPath,
            PersistenceOwnerRootNamespace = c.ApprovedTargetIdentity.PersistenceOwnerRootNamespace,
            PersistenceFileName = c.ApprovedTargetIdentity.PersistenceFileName,
            ConfigCompatibilityType = c.ApprovedTargetIdentity.ConfigCompatibilityType
        }
    };
    private static TechnicalIdentityContract Replace(TechnicalIdentityContract c, string name, Func<MigrationIdentityField, MigrationIdentityField> replace) =>
        c with { MigrationFields = c.MigrationFields.Select(field => field.Field == name ? replace(field) : field).ToArray() };
    private static SettingsFixture Load(string path) => Parse(IdentityContractFixture.Read(path));

    private static SettingsFixture Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var s = root.GetProperty("settings");
        return new(root.GetProperty("persistenceOwnerRootNamespace").GetString() ?? "",
            root.GetProperty("persistenceFileName").GetString() ?? "", s.EnumerateObject().Select(p => p.Name).ToArray(),
            new(ReadEnum<DamageForecastConfigLanguage>(s, "ConfigLanguage"), s.GetProperty("EnablePartyWatchHud").GetBoolean(),
                s.GetProperty("ShowAdvancedShieldHeartDetails").GetBoolean(), s.GetProperty("FreezeHudNumbersAfterTurnEnd").GetBoolean(),
                ReadEnum<DamageDisplayMode>(s, "DamageDisplayMode"), ReadEnum<IncomingDamagePlacement>(s, "IncomingDamagePlacement"),
                s.GetProperty("IncludeCurrentBlockInIncomingDamage").GetBoolean(), s.GetProperty("IncludePowerBlockInIncomingDamage").GetBoolean(),
                s.GetProperty("IncludeRelicBlockInIncomingDamage").GetBoolean(), s.GetProperty("IncludePowerHpLossModifiersInIncomingDamage").GetBoolean(),
                s.GetProperty("IncludeRelicHpLossModifiersInIncomingDamage").GetBoolean(), s.GetProperty("ShowLocalPlayerHudInMultiplayer").GetBoolean(),
                ReadEnum<DamageForecastHudAnchor>(s, "HudAnchorPreset"), s.GetProperty("HorizontalOffset").GetSingle(),
                s.GetProperty("VerticalOffset").GetSingle(), ReadColor(s, "TotalExpectedLossColor"),
                ReadColor(s, "ShieldDetailColor"), ReadColor(s, "HeartDetailColor")));
    }

    private static T ReadEnum<T>(JsonElement settings, string name) where T : struct, Enum =>
        Enum.Parse<T>(settings.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Null enum: {name}"));
    private static Color ReadColor(JsonElement settings, string name)
    {
        var v = settings.GetProperty(name).EnumerateArray().Select(item => item.GetSingle()).ToArray();
        if (v.Length != 4) throw new InvalidOperationException($"Color must have four components: {name}");
        return new(v[0], v[1], v[2], v[3]);
    }

    private static Settings CaptureProduction()
    {
        var order = (string[]?)typeof(DamageForecastBaseLibConfig).GetField("PropertyOrder", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? [];
        var currentOrder = IdentityContractFixture.Contract.PersistentSettings
            .Select(name => name == "EnablePartyWatchHud" ? "EnableDamageForecastHud" : name);
        if (!order.SequenceEqual(currentOrder)) throw new InvalidOperationException("Config order changed.");
        return new(DamageForecastBaseLibConfig.ConfigLanguage, DamageForecastBaseLibConfig.EnableDamageForecastHud,
            DamageForecastBaseLibConfig.ShowAdvancedShieldHeartDetails, DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd,
            DamageForecastBaseLibConfig.DamageDisplayMode, DamageForecastBaseLibConfig.IncomingDamagePlacement,
            DamageForecastBaseLibConfig.IncludeCurrentBlockInIncomingDamage, DamageForecastBaseLibConfig.IncludePowerBlockInIncomingDamage,
            DamageForecastBaseLibConfig.IncludeRelicBlockInIncomingDamage, DamageForecastBaseLibConfig.IncludePowerHpLossModifiersInIncomingDamage,
            DamageForecastBaseLibConfig.IncludeRelicHpLossModifiersInIncomingDamage, DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer,
            DamageForecastBaseLibConfig.HudAnchorPreset, DamageForecastBaseLibConfig.HorizontalOffset, DamageForecastBaseLibConfig.VerticalOffset,
            DamageForecastBaseLibConfig.TotalExpectedLossColor, DamageForecastBaseLibConfig.ShieldDetailColor, DamageForecastBaseLibConfig.HeartDetailColor);
    }

    private static string Describe(Settings s) => string.Join(";", s.Values().Select(value => value switch
        { float n => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture), Color c => $"({c.R:R},{c.G:R},{c.B:R},{c.A:R})", _ => value }));

    private static readonly Settings HistoricalDefaults = new(DamageForecastConfigLanguage.English, true, false, true,
        DamageDisplayMode.ExpectedHpLossOnly, IncomingDamagePlacement.RightOfExpectedHpLoss,
        false, false, false, false, false, true, DamageForecastHudAnchor.HealthBarRight, 0f, 0f,
        Colors.White, new(0.55f, 0.85f, 1f), new(1f, 0.55f, 0.62f));
    private static readonly Settings CurrentProductionDefaults = HistoricalDefaults with
    {
        IncomingDamagePlacement = IncomingDamagePlacement.LeftOfExpectedHpLoss
    };
    private static readonly Settings NonDefaults = new(DamageForecastConfigLanguage.SimplifiedChinese, false, true, false,
        DamageDisplayMode.Both, IncomingDamagePlacement.LeftOfExpectedHpLoss,
        true, true, true, true, true, false, DamageForecastHudAnchor.HealthBarBelow, 123.5f, -67.25f,
        new(0.125f, 0.25f, 0.375f), new(0.2f, 0.4f, 0.6f), new(0.9f, 0.3f, 0.1f));

    private sealed record SettingsFixture(string Owner, string File, IReadOnlyList<string> Keys, Settings Settings);
    private sealed record Settings(DamageForecastConfigLanguage ConfigLanguage, bool EnablePartyWatchHud,
        bool ShowAdvancedShieldHeartDetails, bool FreezeHudNumbersAfterTurnEnd, DamageDisplayMode DamageDisplayMode,
        IncomingDamagePlacement IncomingDamagePlacement, bool IncludeCurrentBlockInIncomingDamage,
        bool IncludePowerBlockInIncomingDamage, bool IncludeRelicBlockInIncomingDamage,
        bool IncludePowerHpLossModifiersInIncomingDamage, bool IncludeRelicHpLossModifiersInIncomingDamage,
        bool ShowLocalPlayerHudInMultiplayer, DamageForecastHudAnchor HudAnchorPreset, float HorizontalOffset,
        float VerticalOffset, Color TotalExpectedLossColor, Color ShieldDetailColor, Color HeartDetailColor)
    {
        public IReadOnlyList<object> Values() => [ConfigLanguage, EnablePartyWatchHud, ShowAdvancedShieldHeartDetails,
            FreezeHudNumbersAfterTurnEnd, DamageDisplayMode, IncomingDamagePlacement, IncludeCurrentBlockInIncomingDamage,
            IncludePowerBlockInIncomingDamage, IncludeRelicBlockInIncomingDamage, IncludePowerHpLossModifiersInIncomingDamage,
            IncludeRelicHpLossModifiersInIncomingDamage, ShowLocalPlayerHudInMultiplayer, HudAnchorPreset, HorizontalOffset,
            VerticalOffset, TotalExpectedLossColor, ShieldDetailColor, HeartDetailColor];
    }
}
