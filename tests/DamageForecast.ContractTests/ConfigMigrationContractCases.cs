using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DamageForecast;
using DamageForecast.Compatibility;
using DamageForecast.Settings;
using DamageForecast.UI;

internal static class ConfigMigrationContractCases
{
    private const string OldFixture = "tests/DamageForecast.ContractTests/fixtures/config-old-official-v0.2.0.cfg";
    private const string CurrentFixture = "tests/DamageForecast.ContractTests/fixtures/config-new-default.cfg";
    private const string CurrentNonDefaultFixture = "tests/DamageForecast.ContractTests/fixtures/config-new-nondefault.cfg";

    public static IEnumerable<ContractCase> Create()
    {
        yield return new("C2-001", "ConfigMigration", "C2.ContractAndCurrentPersistenceIdentity_AreExplicit", assert =>
        {
            var c = PostG6PersistenceContractFixture.Contract;
            var type = typeof(DamageForecastBaseLibConfig);
            assert.True(c.SchemaVersion == 2 && c.Status == "c4-complete" && c.Approval.Gate == "C2"
                && c.Closure.Gate == "C4"
                && c.Release.Version == MainFile.ModVersion && c.Release.WorkshopDisposition == "external-unchanged"
                && type.FullName == c.Current.Type && type.GetProperty(c.Current.HudKey) is not null
                && type.GetProperty(c.Legacy.HudKey) is null && type.Assembly.GetType(c.Legacy.Type) is null
                && c.PropertyCount == DamageForecastConfigSchema.PropertyOrder.Length,
                "approved C2 v0.3.0 current identity with exactly 18 settings and no compiled legacy config type/member",
                JsonSerializer.Serialize(c));
        });

        yield return new("C2-002", "ConfigMigration", "C2.SchemaRegistry_AllSupportedSourcesGoDirectlyToCurrent", assert =>
        {
            var c = PostG6PersistenceContractFixture.Contract;
            var schemas = ConfigSchemaRegistry.SupportedSchemas;
            assert.True(schemas.Count == 2
                && schemas.All(schema => ConfigSchemaRegistry.CanMigrateDirectlyToCurrent(schema.Id))
                && c.SchemaGraph.All(edge => ConfigSchemaRegistry.Get(edge.Key).Id == edge.Key
                    && edge.Value == ConfigSchemaRegistry.Current.Id),
                "legacy-v1 and current-v1 each have a direct path to current",
                string.Join(',', schemas.Select(schema => schema.Id)));
        });

        yield return new("C2-003", "ConfigMigration", "C2.OfficialLegacyFixture_Exact18ValueMigration", assert =>
        {
            var source = Validate(OldFixture, PreDamageForecastSchemaV1.Descriptor);
            var current = ConfigSchemaDetector.Validate(
                ConfigMigrationPipeline.TransformToCurrent(source), DamageForecastSchemaV1.Descriptor);
            assert.True(source.Grade == ConfigMigrationGrade.ExactSuccess
                && source.Metadata.Length == PostG6PersistenceContractFixture.Contract.RealBaseline.Length
                && source.Metadata.Sha256 == PostG6PersistenceContractFixture.Contract.RealBaseline.Sha256
                && current.Grade == ConfigMigrationGrade.ExactSuccess
                && source.Metadata.OrderedKeys.Count == 18
                && current.Metadata.OrderedKeys.Count == 18
                && ConfigMigrationPipeline.AreSemanticallyEqual(source, current)
                && source.Metadata.TypedSemanticDigest == current.Metadata.TypedSemanticDigest,
                "official BaseLib strings migrate old key to new key with all 18 typed values unchanged",
                $"source={source.Grade}; target={current.Grade}; digest={current.Metadata.TypedSemanticDigest}");
        });

        yield return new("C2-004", "ConfigMigration", "C2.CurrentDefaultAndNonDefaultFixtures_AreExact", assert =>
        {
            var a = Validate(CurrentFixture, DamageForecastSchemaV1.Descriptor);
            var b = Validate(CurrentNonDefaultFixture, DamageForecastSchemaV1.Descriptor);
            assert.True(a.Grade == ConfigMigrationGrade.ExactSuccess && b.Grade == ConfigMigrationGrade.ExactSuccess
                && a.Metadata.TypedSemanticDigest != b.Metadata.TypedSemanticDigest,
                "both official current schemas exact and semantically distinct",
                $"default={a.Grade}; nondefault={b.Grade}");
        });

        yield return new("C2-005", "ConfigMigration", "C2.KnownRepresentationDifferences_AreRecoveredNotGuessed", assert =>
        {
            var official = File.ReadAllText(Resolve(OldFixture));
            using var parsed = JsonDocument.Parse(official);
            var typed = parsed.RootElement.EnumerateObject().Reverse().ToDictionary(
                property => property.Name,
                property => ConvertKnownValue(property.Name, property.Value.GetString()!),
                StringComparer.Ordinal);
            var recovered = ConfigSchemaDetector.Validate(JsonSerializer.SerializeToUtf8Bytes(typed), PreDamageForecastSchemaV1.Descriptor);
            var exact = Validate(OldFixture, PreDamageForecastSchemaV1.Descriptor);
            assert.True(recovered.Grade == ConfigMigrationGrade.RecoveredSuccess
                && recovered.Snapshot == exact.Snapshot
                && recovered.Metadata.TypedSemanticDigest == exact.Metadata.TypedSemanticDigest,
                "typed JSON and reversed key order are RecoveredSuccess with identical semantics",
                string.Join('|', recovered.Diagnostics));
        });

        yield return new("C2-006", "ConfigMigration", "C2.InvalidInputs_AreSalvagedOrFailedSafeWithoutTarget", assert =>
        {
            var source = File.ReadAllText(Resolve(OldFixture));
            var invalid = new Dictionary<string, byte[]>
            {
                ["missing"] = Encoding.UTF8.GetBytes(RemoveProperty(source, "VerticalOffset")),
                ["unknown"] = Encoding.UTF8.GetBytes(InsertBeforeFinalBrace(source, "  \"FutureKey\": \"kept\"")),
                ["duplicate"] = Encoding.UTF8.GetBytes(source.Replace("  \"ConfigLanguage\"", "  \"ConfigLanguage\": \"English\",\n  \"ConfigLanguage\"", StringComparison.Ordinal)),
                ["bool"] = Encoding.UTF8.GetBytes(source.Replace("\"EnablePartyWatchHud\": \"True\"", "\"EnablePartyWatchHud\": \"maybe\"", StringComparison.Ordinal)),
                ["enum"] = Encoding.UTF8.GetBytes(source.Replace("\"DamageDisplayMode\": \"Both\"", "\"DamageDisplayMode\": \"Mystery\"", StringComparison.Ordinal)),
                ["nan"] = Encoding.UTF8.GetBytes(source.Replace("\"HorizontalOffset\": \"0\"", "\"HorizontalOffset\": \"NaN\"", StringComparison.Ordinal)),
                ["infinity"] = Encoding.UTF8.GetBytes(source.Replace("\"VerticalOffset\": \"0\"", "\"VerticalOffset\": \"Infinity\"", StringComparison.Ordinal)),
                ["color"] = Encoding.UTF8.GetBytes(source.Replace("\"[1, 1, 1, 1]\"", "\"[1, 1, 1]\"", StringComparison.Ordinal)),
                ["truncated"] = File.ReadAllBytes(Resolve("tests/DamageForecast.ContractTests/fixtures/config-truncated.cfg")),
                ["invalid-json"] = File.ReadAllBytes(Resolve("tests/DamageForecast.ContractTests/fixtures/config-invalid-json.cfg")),
                ["utf8"] = [0xFF, 0xFE, 0xFA]
            };
            var results = invalid.ToDictionary(item => item.Key,
                item => ConfigSchemaDetector.Validate(item.Value, PreDamageForecastSchemaV1.Descriptor));
            assert.True(results.Values.All(result => !result.IsSuccessful)
                && results.Where(item => item.Key != "unknown").All(item => item.Value.Snapshot is null)
                && results["unknown"].Snapshot is not null
                && results["unknown"].ExtensionBag.ContainsKey("FutureKey")
                && results["truncated"].Grade == ConfigMigrationGrade.FailedSafe
                && results["utf8"].Grade == ConfigMigrationGrade.FailedSafe,
                "all malformed/unknown inputs block completion; unknown raw field is retained in extension bag",
                string.Join(',', results.Select(item => $"{item.Key}:{item.Value.Grade}")));
        });

        yield return new("C2-007", "ConfigMigration", "C2.StateMatrix_OldOnlyCompletesTransactionalMigration", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture);
            var result = CompatibilityBootstrap.Run(f.Options);
            var final = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.Options.CurrentConfigPath), DamageForecastSchemaV1.Descriptor);
            assert.True(result.Status == ConfigMigrationStatus.Completed && result.MayRegisterCurrentConfig
                && File.Exists(f.Options.LegacyConfigPath) && File.Exists(f.Options.BackupPath) && File.Exists(f.Options.MarkerPath)
                && ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath)) == result.Marker?.SourceSha256
                && ConfigMigrationPipeline.AreSemanticallyEqual(Validate(OldFixture, PreDamageForecastSchemaV1.Descriptor), final),
                "source preserved; CreateNew backup, verified current target, and completed marker exist",
                result.Message);
        });

        yield return new("C2-019", "ConfigMigration", "C2.CrashRecovery_MissingTargetExistingBackupAndCleanupFailureRecover", assert =>
        {
            using var markerMissingTarget = MigrationFixture.Create(old: OldFixture);
            var first = CompatibilityBootstrap.Run(markerMissingTarget.Options);
            File.Delete(markerMissingTarget.Options.CurrentConfigPath);
            var rebuilt = CompatibilityBootstrap.Run(markerMissingTarget.Options with { TransactionId = "target-rebuilt" });

            using var interrupted = MigrationFixture.Create(old: OldFixture);
            var stopped = CompatibilityBootstrap.Run(interrupted.Options with
                { FailurePoints = ConfigMigrationFailurePoint.TargetMove | ConfigMigrationFailurePoint.CleanupTemp });
            var tempPreserved = File.Exists(interrupted.Options.TargetTempPath);
            var resumed = CompatibilityBootstrap.Run(interrupted.Options);

            assert.True(first.Status == ConfigMigrationStatus.Completed && rebuilt.Status == ConfigMigrationStatus.Recovered
                && File.Exists(markerMissingTarget.Options.CurrentConfigPath)
                && stopped.Status == ConfigMigrationStatus.FailedSafe && tempPreserved
                && resumed.Status == ConfigMigrationStatus.Recovered && resumed.MayRegisterCurrentConfig,
                "marker-without-target rebuilds from preserved old; existing backup/temp resume after interrupted cleanup",
                $"rebuilt={rebuilt.Status}; stopped={stopped.Status}; resumed={resumed.Status}");
        });

        yield return new("C2-020", "ConfigMigration", "C2.IOAndRaceFailureInjection_NeverOverwritesSourceOrCompletes", assert =>
        {
            var points = new[] { ConfigMigrationFailurePoint.DiskFull, ConfigMigrationFailurePoint.AccessDenied,
                ConfigMigrationFailurePoint.SourceRead, ConfigMigrationFailurePoint.TargetRace };
            var outcomes = new List<string>();
            foreach (var point in points)
            {
                using var f = MigrationFixture.Create(old: OldFixture);
                var before = ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
                var result = CompatibilityBootstrap.Run(f.Options with { FailurePoints = point });
                outcomes.Add($"{point}:{result.Status}");
                if (result.Status != ConfigMigrationStatus.FailedSafe
                    || before != ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath))
                    || File.Exists(f.Options.MarkerPath))
                    throw new InvalidOperationException(string.Join(',', outcomes));
            }
            assert.True(true, "disk-full, access-denied, source-read, and target-race injections preserve source and block marker", string.Join(',', outcomes));
        });

        yield return new("C2-008", "ConfigMigration", "C2.StateMatrix_NewOnlyAndNeitherDoNotInventLegacy", assert =>
        {
            using var current = MigrationFixture.Create(current: CurrentFixture);
            using var neither = MigrationFixture.Create();
            var a = CompatibilityBootstrap.Run(current.Options);
            var b = CompatibilityBootstrap.Run(neither.Options);
            assert.True(a.Status == ConfigMigrationStatus.ExistingCurrent && a.Marker is null
                && b.Status == ConfigMigrationStatus.FreshInstall && b.MayRegisterCurrentConfig
                && !File.Exists(current.Options.LegacyConfigPath) && !File.Exists(neither.Options.CurrentConfigPath),
                "new-only uses validated current without legacy marker; neither delegates fresh defaults to BaseLib",
                $"new={a.Status}; neither={b.Status}");
        });

        yield return new("C2-009", "ConfigMigration", "C2.StateMatrix_BothEqualBindAndCompletedRestartIsIdempotent", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture);
            var first = CompatibilityBootstrap.Run(f.Options);
            File.WriteAllBytes(f.Options.LegacyConfigPath, [0xFF, 0xFE]);
            File.Copy(Resolve(CurrentNonDefaultFixture), f.Options.CurrentConfigPath, overwrite: true);
            var second = CompatibilityBootstrap.Run(f.Options with { TransactionId = "restart" });
            var edited = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.Options.CurrentConfigPath), DamageForecastSchemaV1.Descriptor);
            assert.True(first.Status == ConfigMigrationStatus.Completed
                && second.Status == ConfigMigrationStatus.AlreadyCompleted && second.MayRegisterCurrentConfig
                && edited.Metadata.TypedSemanticDigest != first.Marker?.TypedSemanticDigest,
                "completed lineage + valid user-edited current config skips later legacy parsing",
                $"first={first.Status}; second={second.Status}");
        });

        yield return new("C2-010", "ConfigMigration", "C2.StateMatrix_BothDivergentFailsClosed", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture, current: CurrentNonDefaultFixture);
            var oldHash = ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
            var newHash = ConfigDigest.Sha256(File.ReadAllBytes(f.Options.CurrentConfigPath));
            var result = CompatibilityBootstrap.Run(f.Options);
            assert.True(result.Status == ConfigMigrationStatus.Conflict && !result.MayRegisterCurrentConfig
                && oldHash == ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath))
                && newHash == ConfigDigest.Sha256(File.ReadAllBytes(f.Options.CurrentConfigPath))
                && !File.Exists(f.Options.MarkerPath),
                "valid divergent files are both preserved and no side is guessed",
                result.Message);
        });

        yield return new("C2-011", "ConfigMigration", "C2.SourceHashChangeBetweenPlanAndExecute_FailsClosed", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture);
            var original = File.ReadAllBytes(f.Options.LegacyConfigPath);
            var options = f.Options with { BeforeExecute = () => File.AppendAllText(f.Options.LegacyConfigPath, " ") };
            var result = CompatibilityBootstrap.Run(options);
            assert.True(result.Status == ConfigMigrationStatus.FailedSafe && !result.MayRegisterCurrentConfig
                && !File.Exists(f.Options.CurrentConfigPath) && !File.Exists(f.Options.MarkerPath)
                && File.ReadAllBytes(f.Options.LegacyConfigPath).Length == original.Length + 1,
                "changed source blocks backup/target/marker and is never overwritten",
                result.Message);
        });

        yield return new("C2-012", "ConfigMigration", "C2.FailureInjection_BackupTempMoveAndMarkerRemainRecoverable", assert =>
        {
            var points = new[] { ConfigMigrationFailurePoint.BackupCreate, ConfigMigrationFailurePoint.BackupFlush,
                ConfigMigrationFailurePoint.TempWrite, ConfigMigrationFailurePoint.TargetMove, ConfigMigrationFailurePoint.MarkerWrite };
            var outcomes = new List<string>();
            foreach (var point in points)
            {
                using var f = MigrationFixture.Create(old: OldFixture);
                var sourceHash = ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
                var failed = CompatibilityBootstrap.Run(f.Options with { FailurePoints = point });
                var sourceSafe = sourceHash == ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
                var noFalseMarker = !File.Exists(f.Options.MarkerPath);
                outcomes.Add($"{point}:{failed.Status}:{sourceSafe}:{noFalseMarker}");
                if (failed.Status != ConfigMigrationStatus.FailedSafe || !sourceSafe || !noFalseMarker)
                    throw new InvalidOperationException(string.Join(',', outcomes));
                if (point == ConfigMigrationFailurePoint.MarkerWrite)
                {
                    var recovered = CompatibilityBootstrap.Run(f.Options with { TransactionId = "recovered" });
                    if (recovered.Status != ConfigMigrationStatus.Recovered || !recovered.MayRegisterCurrentConfig)
                        throw new InvalidOperationException($"marker recovery failed: {recovered.Message}");
                }
            }
            assert.True(true, "all injected transaction failures preserve source and never create a false completed marker", string.Join(',', outcomes));
        });

        yield return new("C2-013", "ConfigMigration", "C2.RollbackReverseSync_IsVerifiedAtomicAndFailureSafe", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture, current: CurrentNonDefaultFixture);
            var oldBefore = ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
            var injected = CompatibilityBootstrap.ReverseSyncForRollback(f.Options with
                { TransactionId = "reverse-fail", FailurePoints = ConfigMigrationFailurePoint.ReverseMove });
            var unchanged = oldBefore == ConfigDigest.Sha256(File.ReadAllBytes(f.Options.LegacyConfigPath));
            var completed = CompatibilityBootstrap.ReverseSyncForRollback(f.Options with { TransactionId = "reverse-ok" });
            var legacy = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.Options.LegacyConfigPath), PreDamageForecastSchemaV1.Descriptor);
            var current = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.Options.CurrentConfigPath), DamageForecastSchemaV1.Descriptor);
            assert.True(injected.Status == ConfigMigrationStatus.FailedSafe && unchanged
                && completed.Status == ConfigMigrationStatus.RolledBack
                && completed.Marker?.ReverseSyncStatus == "completed"
                && ConfigMigrationPipeline.AreSemanticallyEqual(current, legacy),
                "injected reverse move preserves old; successful retry backs up and atomically syncs new to old",
                $"injected={injected.Status}; completed={completed.Status}");
        });

        yield return new("C2-014", "ConfigMigration", "C2.FutureOptionalSettings_UseExplicitVersionDefaults", assert =>
        {
            var source = Validate(OldFixture, PreDamageForecastSchemaV1.Descriptor).Snapshot!;
            var future = new ConfigSchemaDescriptor("damage-forecast-v2", "DamageForecast.cfg",
                DamageForecastSchemaV1.Descriptor.OrderedKeys.Concat(["FutureOptionalSetting"]).ToArray(),
                DamageForecastSchemaV1.HudEnabledKey, true);
            using var json = JsonDocument.Parse(ConfigMigrationPipeline.Serialize(source, future,
                new Dictionary<string, string> { ["FutureOptionalSetting"] = "ApprovedDefault" }));
            assert.True(json.RootElement.EnumerateObject().Count() == 19
                && json.RootElement.GetProperty("FutureOptionalSetting").GetString() == "ApprovedDefault",
                "future v2 adds setting 19 only through an explicit schema default",
                json.RootElement.GetRawText());
        });

        yield return new("C2-015", "ConfigMigration", "C2.RandomLegalConfigurations_10000RoundTripsPreserveSemantics", assert =>
        {
            var random = new Random(0xC20018);
            var languages = new HashSet<DamageForecastConfigLanguage>();
            var displays = new HashSet<DamageDisplayMode>();
            var placements = new HashSet<IncomingDamagePlacement>();
            var anchors = new HashSet<DamageForecastHudAnchor>();
            var bools = new HashSet<bool>();
            for (var index = 0; index < 10_000; index++)
            {
                var snapshot = RandomSnapshot(random);
                languages.Add(snapshot.ConfigLanguage);
                displays.Add(snapshot.DamageDisplayMode);
                placements.Add(snapshot.IncomingDamagePlacement);
                anchors.Add(snapshot.HudAnchorPreset);
                bools.Add(snapshot.EnableDamageForecastHud);
                var old = ConfigSchemaDetector.Validate(
                    ConfigMigrationPipeline.Serialize(snapshot, PreDamageForecastSchemaV1.Descriptor),
                    PreDamageForecastSchemaV1.Descriptor);
                var current = ConfigSchemaDetector.Validate(ConfigMigrationPipeline.TransformToCurrent(old), DamageForecastSchemaV1.Descriptor);
                var rollback = ConfigSchemaDetector.Validate(ConfigMigrationPipeline.TransformToLegacy(current), PreDamageForecastSchemaV1.Descriptor);
                if (!ConfigMigrationPipeline.AreSemanticallyEqual(old, current)
                    || !ConfigMigrationPipeline.AreSemanticallyEqual(current, rollback))
                    throw new InvalidOperationException($"random round trip failed at {index}");
            }
            assert.True(languages.Count == 2 && displays.Count == 3 && placements.Count == 2
                && anchors.Count == 4 && bools.SetEquals([false, true]),
                "10,000 fixed-seed legal old-current-old round trips preserve semantics and cover every enum/bool boundary",
                $"language={languages.Count}; display={displays.Count}; placement={placements.Count}; anchor={anchors.Count}; bool={bools.Count}");
        });

        yield return new("C2-016", "ConfigMigration", "C2.BootstrapOrderingAndLegacyLiteralIsolation_AreFrozen", assert =>
        {
            var main = IdentityContractFixture.Read("src/DamageForecast/MainFile.cs");
            var bootstrap = main.IndexOf("CompatibilityBootstrap.Run", StringComparison.Ordinal);
            var construct = main.IndexOf("new DamageForecastBaseLibConfig", StringComparison.Ordinal);
            var register = main.IndexOf("ModConfigRegistry.Register", StringComparison.Ordinal);
            var registrationCount = main.Split("ModConfigRegistry.Register", StringSplitOptions.None).Length - 1;
            var ordinaryRoots = new[] { "Combat", "Diagnostics", "Forecast", "Patches", "Settings", "UI" };
            var hits = ordinaryRoots.SelectMany(root => Directory.EnumerateFiles(
                    Path.Combine(IdentityContractFixture.RepositoryRoot, "src", "DamageForecast", root), "*.cs", SearchOption.AllDirectories))
                .Where(path => File.ReadAllText(path).Contains("STS2PartyWatch.cfg", StringComparison.Ordinal)
                    || File.ReadAllText(path).Contains("EnablePartyWatchHud", StringComparison.Ordinal))
                .ToArray();
            assert.True(bootstrap >= 0 && bootstrap < construct && construct < register
                && registrationCount == 1 && hits.Length == 0,
                "migration runs before the single config construction/registration; old filename/key are compatibility-only",
                $"bootstrap={bootstrap}; construct={construct}; register={register}; registrationCount={registrationCount}; hits={string.Join(',', hits)}");
        });

        yield return new("C2-017", "ConfigMigration", "C2.MarkerContainsRequiredTransactionEvidence", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture);
            var result = CompatibilityBootstrap.Run(f.Options);
            var marker = result.Marker!;
            assert.True(marker.SchemaVersion == 1 && marker.TransactionId == f.Options.TransactionId
                && marker.SourceLength > 0 && marker.TargetLength > 0 && marker.BackupLength == marker.SourceLength
                && marker.SourceSha256 == marker.BackupSha256 && marker.SourceSha256 != marker.TargetSha256
                && marker.OrderedKeyDigest.Length == 64 && marker.TypedSemanticDigest.Length == 64
                && marker.Status == "completed" && marker.ReverseSyncStatus == "not-requested" && !marker.CleanupEligible,
                "marker binds paths, lengths, SHA256, ordered keys, typed digest, versions, strategy, status, reverse status, cleanup",
                JsonSerializer.Serialize(marker));
        });

        yield return new("C2-018", "ConfigMigration", "C2.InstallerRollback_PerformsReviewedReverseSyncInTemporaryFixture", assert =>
        {
            using var f = InstallerRollbackFixture.Create();
            var result = f.Run();
            var old = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.LegacyConfigPath), PreDamageForecastSchemaV1.Descriptor);
            var current = ConfigSchemaDetector.Validate(File.ReadAllBytes(f.CurrentConfigPath), DamageForecastSchemaV1.Descriptor);
            var backups = Directory.Exists(f.MigrationRoot)
                ? Directory.EnumerateFiles(f.MigrationRoot, "STS2PartyWatch.cfg.backup", SearchOption.AllDirectories).Count()
                : 0;
            assert.True(result.ExitCode == 0 && ConfigMigrationPipeline.AreSemanticallyEqual(old, current)
                && backups == 1
                && Directory.Exists(Path.Combine(f.GameRoot, "mods", "damage-forecast"))
                && InstallerRollbackFixture.ReadActiveVersion(f.GameRoot) == "v0.2.0",
                "reviewed old/new and same-identity Mod hashes gate reverse-sync before temporary v0.3.0 to v0.2.0 rollback",
                $"exit={result.ExitCode}; stdout={result.Output}; stderr={result.Error}; backups={backups}");
        });

        yield return new("C2-021", "ConfigMigration", "C2.TransactionPaths_CannotEscapeApprovedRoots", assert =>
        {
            using var f = MigrationFixture.Create(old: OldFixture);
            var insideConfig = CompatibilityBootstrap.Run(f.Options with
                { MigrationRoot = Path.Combine(f.Options.ConfigRoot, "forbidden-migration") });
            var traversal = CompatibilityBootstrap.Run(f.Options with
                { TransactionId = Path.Combine("..", "..", "..", "escape") });
            assert.True(insideConfig.Status == ConfigMigrationStatus.FailedSafe
                && traversal.Status == ConfigMigrationStatus.FailedSafe
                && !File.Exists(f.Options.CurrentConfigPath),
                "migration root inside mod_configs and transaction traversal both fail before target creation",
                $"inside={insideConfig.Message}; traversal={traversal.Message}");
        });

        yield return new("DFCM-001", "ConfigMigration", "DFCM.HistoricalPartialLegacyConfig_DefaultsOnlyKnownAddedSettings", assert =>
        {
            var sourceText = File.ReadAllText(Resolve(OldFixture));
            var partialText = DamageForecastSchemaV1.HistoricalOptionalDefaults.Keys
                .Aggregate(sourceText, RemoveProperty);
            var partialBytes = new UTF8Encoding(false).GetBytes(partialText);
            var expected = Validate(OldFixture, PreDamageForecastSchemaV1.Descriptor).Snapshot! with
            {
                DamageDisplayMode = DamageDisplayMode.ExpectedHpLossOnly,
                IncomingDamagePlacement = IncomingDamagePlacement.LeftOfExpectedHpLoss,
                IncludeCurrentBlockInIncomingDamage = false,
                IncludePowerBlockInIncomingDamage = false,
                IncludeRelicBlockInIncomingDamage = false,
                IncludePowerHpLossModifiersInIncomingDamage = false,
                IncludeRelicHpLossModifiersInIncomingDamage = false
            };

            using var f = MigrationFixture.Create(old: OldFixture);
            File.WriteAllBytes(f.Options.LegacyConfigPath, partialBytes);
            var result = CompatibilityBootstrap.Run(f.Options);
            var current = ConfigSchemaDetector.Validate(
                File.ReadAllBytes(f.Options.CurrentConfigPath),
                DamageForecastSchemaV1.Descriptor);
            var placement = HudPlacementConfigFileMigration.Run(
                f.Options with { TransactionId = "historical-placement" });

            assert.True(result.Status == ConfigMigrationStatus.Recovered
                && result.MayRegisterCurrentConfig
                && result.Marker?.Status == "recovered"
                && File.ReadAllBytes(f.Options.BackupPath).AsSpan().SequenceEqual(partialBytes)
                && current.IsSuccessful
                && current.Snapshot == expected
                && placement.MayContinue
                && HudPlacementConfigFileMigration.IsStrictV2(File.ReadAllBytes(f.Options.CurrentConfigPath)),
                "the player-reported seven missing settings receive versioned defaults; existing values and the exact original backup are preserved; startup can continue through HUD placement V2",
                $"migration={result.Status}; placement={placement.Status}; diagnostics={result.Message}");
        });

        yield return new("DFCM-002", "ConfigMigration", "DFCM.PartialRecovery_StillRejectsUnknownMissingAndInvalidInputs", assert =>
        {
            var sourceText = File.ReadAllText(Resolve(OldFixture));
            var knownPartial = DamageForecastSchemaV1.HistoricalOptionalDefaults.Keys
                .Aggregate(sourceText, RemoveProperty);

            using var unrelatedMissing = MigrationFixture.Create(old: OldFixture);
            File.WriteAllText(unrelatedMissing.Options.LegacyConfigPath, RemoveProperty(sourceText, "VerticalOffset"));
            var unrelatedResult = CompatibilityBootstrap.Run(unrelatedMissing.Options);

            using var invalidPresent = MigrationFixture.Create(old: OldFixture);
            File.WriteAllText(invalidPresent.Options.LegacyConfigPath,
                knownPartial.Replace("\"EnablePartyWatchHud\": \"True\"", "\"EnablePartyWatchHud\": \"maybe\"", StringComparison.Ordinal));
            var invalidResult = CompatibilityBootstrap.Run(invalidPresent.Options);

            using var unknownPresent = MigrationFixture.Create(old: OldFixture);
            File.WriteAllText(unknownPresent.Options.LegacyConfigPath,
                InsertBeforeFinalBrace(knownPartial, "  \"FutureKey\": \"kept\""));
            var unknownResult = CompatibilityBootstrap.Run(unknownPresent.Options);

            assert.True(new[] { unrelatedResult, invalidResult, unknownResult }
                    .All(result => result.Status == ConfigMigrationStatus.Salvaged
                        && !result.MayRegisterCurrentConfig)
                && !File.Exists(unrelatedMissing.Options.CurrentConfigPath)
                && !File.Exists(invalidPresent.Options.CurrentConfigPath)
                && !File.Exists(unknownPresent.Options.CurrentConfigPath),
                "only the known historical additions are defaulted; unrelated missing fields, invalid retained values, and unknown fields still block before target creation",
                $"unrelated={unrelatedResult.Message}; invalid={invalidResult.Message}; unknown={unknownResult.Message}");
        });
    }

    private static ConfigValidationResult Validate(string path, ConfigSchemaDescriptor schema) =>
        ConfigSchemaDetector.Validate(File.ReadAllBytes(Resolve(path)), schema);

    private static string Resolve(string path) => Path.Combine(IdentityContractFixture.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));

    private static object ConvertKnownValue(string name, string value)
    {
        if (name.Contains("Color", StringComparison.Ordinal))
            return value.Trim('[', ']').Split(',').Select(piece => float.Parse(piece, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (name is "HorizontalOffset" or "VerticalOffset") return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        if (name.StartsWith("Enable", StringComparison.Ordinal) || name.StartsWith("Show", StringComparison.Ordinal)
            || name.StartsWith("Freeze", StringComparison.Ordinal) || name.StartsWith("Include", StringComparison.Ordinal))
            return bool.Parse(value);
        return value;
    }

    private static string RemoveProperty(string json, string property)
    {
        var lines = json.Split('\n').Where(line => !line.Contains($"\"{property}\"", StringComparison.Ordinal)).ToArray();
        return string.Join('\n', lines).Replace(",\n}", "\n}", StringComparison.Ordinal);
    }

    private static string InsertBeforeFinalBrace(string json, string propertyLine)
    {
        var trimmed = json.TrimEnd();
        return trimmed[..^1].TrimEnd() + ",\n" + propertyLine + "\n}";
    }

    private static DamageForecastConfigSnapshot RandomSnapshot(Random random)
    {
        static float F(Random r) => (float)(r.NextDouble() * 4000.0 - 2000.0);
        static DamageForecastRgba C(Random r) => new((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble());
        return new DamageForecastConfigSnapshot(
            (DamageForecastConfigLanguage)random.Next(2), random.Next(2) == 1, random.Next(2) == 1, random.Next(2) == 1,
            (DamageDisplayMode)random.Next(3), (IncomingDamagePlacement)random.Next(2),
            random.Next(2) == 1, random.Next(2) == 1, random.Next(2) == 1, random.Next(2) == 1,
            random.Next(2) == 1, random.Next(2) == 1, (DamageForecastHudAnchor)random.Next(4),
            F(random), F(random), C(random), C(random), C(random));
    }

    private sealed class MigrationFixture : IDisposable
    {
        private MigrationFixture(string root, ConfigMigrationOptions options) { Root = root; Options = options; }
        public string Root { get; }
        public ConfigMigrationOptions Options { get; }

        public static MigrationFixture Create(string? old = null, string? current = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "damage-forecast-c2-contracts", Guid.NewGuid().ToString("N"));
            var config = Path.Combine(root, "mod_configs");
            var migration = Path.Combine(root, "damage-forecast-migration");
            Directory.CreateDirectory(config);
            var options = new ConfigMigrationOptions(config, migration, "tx", "v0.3.0", "contract-test",
                new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero));
            if (old is not null) File.Copy(Resolve(old), options.LegacyConfigPath);
            if (current is not null) File.Copy(Resolve(current), options.CurrentConfigPath);
            return new MigrationFixture(root, options);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class InstallerRollbackFixture : IDisposable
    {
        private InstallerRollbackFixture(string root)
        {
            Root = root;
            GameRoot = Path.Combine(root, "game");
            BackupRoot = Path.Combine(GameRoot, ".damage-forecast-backups");
            ConfigRoot = Path.Combine(root, "appdata", "mod_configs");
            MigrationRoot = Path.Combine(root, "appdata", "damage-forecast-migration");
            LegacyConfigPath = Path.Combine(ConfigRoot, LegacyIdentityDescriptor.ConfigFileName);
            CurrentConfigPath = Path.Combine(ConfigRoot, DamageForecastSchemaV1.ConfigFileName);
        }

        public string Root { get; }
        public string GameRoot { get; }
        public string BackupRoot { get; }
        public string ConfigRoot { get; }
        public string MigrationRoot { get; }
        public string LegacyConfigPath { get; }
        public string CurrentConfigPath { get; }

        public static InstallerRollbackFixture Create()
        {
            var fixture = new InstallerRollbackFixture(Path.Combine(Path.GetTempPath(), "damage-forecast-c2-installer", Guid.NewGuid().ToString("N")));
            var target = Path.Combine(fixture.GameRoot, "mods", "damage-forecast");
            var legacy = Path.Combine(fixture.BackupRoot, "target-v0.2.0-backup");
            Directory.CreateDirectory(target);
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(fixture.ConfigRoot);
            File.Copy(Resolve("src/DamageForecast/damage-forecast.json"), Path.Combine(target, "damage-forecast.json"));
            File.Copy(typeof(MainFile).Assembly.Location, Path.Combine(target, "damage-forecast.dll"));
            var rollbackManifest = File.ReadAllText(Resolve("src/DamageForecast/damage-forecast.json"))
                .Replace("\"version\": \"v0.3.0\"", "\"version\": \"v0.2.0\"", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(legacy, "damage-forecast.json"), rollbackManifest);
            File.Copy(typeof(MainFile).Assembly.Location, Path.Combine(legacy, "damage-forecast.dll"));
            File.Copy(Resolve(OldFixture), fixture.LegacyConfigPath);
            File.Copy(Resolve(CurrentNonDefaultFixture), fixture.CurrentConfigPath);
            return fixture;
        }

        public (int ExitCode, string Output, string Error) Run()
        {
            static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            var start = new ProcessStartInfo("powershell")
            {
                WorkingDirectory = IdentityContractFixture.RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[]
            {
                "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Resolve("scripts/Install-LocalMod.ps1"),
                "-Mode", "Rollback", "-Execute", "-Sts2GameRoot", GameRoot, "-BackupRoot", BackupRoot,
                "-BackupPath", "target-v0.2.0-backup", "-TransactionId", "c2-installer",
                "-ConfigRoot", ConfigRoot, "-ConfigMigrationRoot", MigrationRoot,
                "-ExpectedActiveManifestSha256", Sha(Path.Combine(GameRoot, "mods", "damage-forecast", "damage-forecast.json")),
                "-ExpectedActiveDllSha256", Sha(Path.Combine(GameRoot, "mods", "damage-forecast", "damage-forecast.dll")),
                "-ExpectedBackupManifestSha256", Sha(Path.Combine(BackupRoot, "target-v0.2.0-backup", "damage-forecast.json")),
                "-ExpectedBackupDllSha256", Sha(Path.Combine(BackupRoot, "target-v0.2.0-backup", "damage-forecast.dll")),
                "-ExpectedLegacyConfigSha256", Sha(LegacyConfigPath),
                "-ExpectedCurrentConfigSha256", Sha(CurrentConfigPath)
            }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start installer rollback fixture.");
            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            return (process.ExitCode, output, error);
        }

        public static string ReadActiveVersion(string gameRoot)
        {
            var path = Path.Combine(gameRoot, "mods", "damage-forecast", "damage-forecast.json");
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            return json.RootElement.GetProperty("version").GetString() ?? "";
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
