using System.Text.Json;

namespace DamageForecast.Compatibility;

internal static class CompatibilityBootstrap
{
    public static ConfigMigrationResult Run(ConfigMigrationOptions options)
    {
        try
        {
            var legacyExists = File.Exists(options.LegacyConfigPath);
            var currentExists = File.Exists(options.CurrentConfigPath);

            if (currentExists)
            {
                var currentBytes = File.ReadAllBytes(options.CurrentConfigPath);
                var completed = FindCompletedMarker(options);
                if (completed is not null)
                {
                    if (HudPlacementConfigFileMigration.IsStrictV2(currentBytes))
                    {
                        return new ConfigMigrationResult(
                            ConfigMigrationGrade.ExactSuccess,
                            ConfigMigrationStatus.AlreadyCompleted,
                            MayRegisterCurrentConfig: true,
                            "Completed identity migration lineage and strict HUD placement V2 config are valid.",
                            completed);
                    }

                    var currentValidation = ConfigSchemaDetector.Validate(
                        currentBytes,
                        DamageForecastSchemaV1.Descriptor);
                    if (!currentValidation.IsSuccessful)
                    {
                        return Failed("completed migration lineage exists but the current config is invalid");
                    }
                    return new ConfigMigrationResult(
                        currentValidation.Grade,
                        ConfigMigrationStatus.AlreadyCompleted,
                        MayRegisterCurrentConfig: true,
                        "Completed migration lineage and current config are valid; post-migration edits are accepted and legacy content was not parsed.",
                        completed);
                }
            }

            if (!legacyExists && !currentExists)
            {
                return new ConfigMigrationResult(
                    ConfigMigrationGrade.ExactSuccess,
                    ConfigMigrationStatus.FreshInstall,
                    MayRegisterCurrentConfig: true,
                    "No legacy or current config exists; BaseLib may create the current defaults.");
            }

            if (currentExists
                && HudPlacementConfigFileMigration.IsStrictV2(
                    File.ReadAllBytes(options.CurrentConfigPath)))
            {
                return legacyExists
                    ? new ConfigMigrationResult(
                        ConfigMigrationGrade.FailedSafe,
                        ConfigMigrationStatus.Conflict,
                        MayRegisterCurrentConfig: false,
                        "Legacy identity config and HUD placement V2 current config coexist without completed lineage.")
                    : new ConfigMigrationResult(
                        ConfigMigrationGrade.ExactSuccess,
                        ConfigMigrationStatus.ExistingCurrent,
                        MayRegisterCurrentConfig: true,
                        "A strict HUD placement V2 current config exists without a legacy source.");
            }

            ConfigValidationResult? legacy = null;
            ConfigValidationResult? current = null;
            if (legacyExists)
            {
                legacy = ConfigSchemaDetector.Validate(
                    File.ReadAllBytes(options.LegacyConfigPath),
                    PreDamageForecastSchemaV1.Descriptor);
                if (!legacy.IsSuccessful)
                {
                    return ValidationFailure("legacy config is not safe to migrate", legacy);
                }
            }
            if (currentExists)
            {
                current = ConfigSchemaDetector.Validate(
                    File.ReadAllBytes(options.CurrentConfigPath),
                    DamageForecastSchemaV1.Descriptor);
                if (!current.IsSuccessful)
                {
                    return ValidationFailure("current config is invalid", current);
                }
            }

            if (legacy is null && current is not null)
            {
                return new ConfigMigrationResult(
                    current.Grade,
                    ConfigMigrationStatus.ExistingCurrent,
                    MayRegisterCurrentConfig: true,
                    "A valid current config exists without a legacy source; no legacy marker was created.");
            }

            if (legacy is not null && current is not null)
            {
                if (!ConfigMigrationPipeline.AreSemanticallyEqual(legacy, current))
                {
                    return new ConfigMigrationResult(
                        ConfigMigrationGrade.FailedSafe,
                        ConfigMigrationStatus.Conflict,
                        MayRegisterCurrentConfig: false,
                        "Legacy and current configs are both valid but semantically divergent; neither was overwritten.");
                }
                options.BeforeExecute?.Invoke();
                return ConfigMigrationTransaction.BindExisting(
                    options,
                    legacy,
                    current,
                    recovered: true);
            }

            if (legacy is not null)
            {
                var targetBytes = ConfigMigrationPipeline.TransformToCurrent(legacy);
                var target = ConfigSchemaDetector.Validate(targetBytes, DamageForecastSchemaV1.Descriptor);
                if (!target.IsSuccessful || !ConfigMigrationPipeline.AreSemanticallyEqual(legacy, target))
                {
                    return Failed("generated current config failed semantic verification");
                }
                var recovered = Directory.Exists(options.TransactionsRoot)
                    && Directory.EnumerateFiles(options.TransactionsRoot, "marker.json", SearchOption.AllDirectories).Any();
                options.BeforeExecute?.Invoke();
                return ConfigMigrationTransaction.Migrate(options, legacy, targetBytes, target, recovered);
            }

            return Failed("unhandled config migration state");
        }
        catch (Exception exception)
        {
            return new ConfigMigrationResult(
                ConfigMigrationGrade.FailedSafe,
                ConfigMigrationStatus.FailedSafe,
                MayRegisterCurrentConfig: false,
                $"bootstrap-failed:{exception.GetType().Name}:{exception.Message}");
        }
    }

    public static ConfigMigrationResult ReverseSyncForRollback(ConfigMigrationOptions options)
    {
        try
        {
            if (!File.Exists(options.CurrentConfigPath) || !File.Exists(options.LegacyConfigPath))
            {
                return Failed("reverse sync requires both current and legacy configs");
            }
            var currentSourceBytes = File.ReadAllBytes(options.CurrentConfigPath);
            var currentBytes = currentSourceBytes;
            var currentWasV2 = HudPlacementConfigFileMigration.IsStrictV2(currentBytes);
            if (currentWasV2
                && !HudPlacementConfigFileMigration.TryCreateRollbackV1(
                    currentBytes,
                    out currentBytes,
                    out var rollbackMessage))
            {
                return Failed(rollbackMessage);
            }

            var current = ConfigSchemaDetector.Validate(
                currentBytes,
                DamageForecastSchemaV1.Descriptor);
            var legacy = ConfigSchemaDetector.Validate(
                File.ReadAllBytes(options.LegacyConfigPath),
                PreDamageForecastSchemaV1.Descriptor);
            if (!current.IsSuccessful)
            {
                return ValidationFailure("current config is not safe to reverse-sync", current);
            }
            if (!legacy.IsSuccessful)
            {
                return ValidationFailure("legacy config is not safe to replace", legacy);
            }
            if (currentWasV2)
            {
                using var document = JsonDocument.Parse(currentSourceBytes);
                var orderedKeys = document.RootElement.EnumerateObject()
                    .Select(property => property.Name)
                    .ToArray();
                current = current with
                {
                    Metadata = current.Metadata with
                    {
                        Length = currentSourceBytes.LongLength,
                        Sha256 = ConfigDigest.Sha256(currentSourceBytes),
                        OrderedKeyDigest = ConfigDigest.Sha256(string.Join("\n", orderedKeys)),
                        OrderedKeys = orderedKeys
                    }
                };
            }
            var legacyBytes = ConfigMigrationPipeline.TransformToLegacy(current);
            options.BeforeExecute?.Invoke();
            return ConfigMigrationTransaction.ReverseSync(
                options,
                current,
                legacy,
                legacyBytes,
                currentWasV2
                    ? HudPlacementConfigFileMigration.SchemaId
                    : DamageForecastSchemaV1.SchemaId);
        }
        catch (Exception exception)
        {
            return new ConfigMigrationResult(
                ConfigMigrationGrade.FailedSafe,
                ConfigMigrationStatus.FailedSafe,
                MayRegisterCurrentConfig: false,
                $"reverse-bootstrap-failed:{exception.GetType().Name}:{exception.Message}");
        }
    }

    private static ConfigMigrationMarker? FindCompletedMarker(ConfigMigrationOptions options)
    {
        if (!Directory.Exists(options.TransactionsRoot))
        {
            return null;
        }
        var currentPath = Path.GetFullPath(options.CurrentConfigPath);
        foreach (var markerPath in Directory.EnumerateFiles(
                     options.TransactionsRoot,
                     "marker.json",
                     SearchOption.AllDirectories))
        {
            try
            {
                var marker = JsonSerializer.Deserialize<ConfigMigrationMarker>(
                    File.ReadAllBytes(markerPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (marker is not null
                    && marker.SchemaVersion == 1
                    && marker.TargetPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase)
                    && marker.Status is "completed" or "recovered")
                {
                    return marker;
                }
            }
            catch
            {
                // An unreadable marker is not accepted as completion evidence.
            }
        }
        return null;
    }

    private static ConfigMigrationResult ValidationFailure(
        string message,
        ConfigValidationResult validation)
    {
        return new ConfigMigrationResult(
            validation.Grade,
            validation.Grade == ConfigMigrationGrade.Salvaged
                ? ConfigMigrationStatus.Salvaged
                : ConfigMigrationStatus.FailedSafe,
            MayRegisterCurrentConfig: false,
            $"{message}:{string.Join('|', validation.Diagnostics)}");
    }

    private static ConfigMigrationResult Failed(string message) =>
        new(
            ConfigMigrationGrade.FailedSafe,
            ConfigMigrationStatus.FailedSafe,
            MayRegisterCurrentConfig: false,
            message);
}
