using System.Text.Json;

namespace DamageForecast.Compatibility;

internal static class ConfigMigrationTransaction
{
    private static readonly JsonSerializerOptions MarkerJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ConfigMigrationResult Migrate(
        ConfigMigrationOptions options,
        ConfigValidationResult source,
        byte[] targetBytes,
        ConfigValidationResult target,
        bool recovered)
    {
        return ExecuteForward(options, source, targetBytes, target, targetAlreadyExists: false, recovered);
    }

    public static ConfigMigrationResult BindExisting(
        ConfigMigrationOptions options,
        ConfigValidationResult source,
        ConfigValidationResult target,
        bool recovered)
    {
        return ExecuteForward(options, source, null, target, targetAlreadyExists: true, recovered);
    }

    public static ConfigMigrationResult ReverseSync(
        ConfigMigrationOptions options,
        ConfigValidationResult current,
        ConfigValidationResult legacy,
        byte[] legacyBytes,
        string sourceSchema = DamageForecastSchemaV1.SchemaId)
    {
        var reverseTemp = Path.GetFullPath(options.ReverseTempPath);
        try
        {
            ValidatePaths(options);
            Directory.CreateDirectory(options.TransactionRoot);
            ThrowIf(options, ConfigMigrationFailurePoint.SourceRead, "injected source read failure");
            var currentSourceBytes = File.ReadAllBytes(options.CurrentConfigPath);
            EnsureSha(currentSourceBytes, current.Metadata.Sha256, "current source changed before reverse sync");
            var legacySourceBytes = File.ReadAllBytes(options.LegacyConfigPath);
            EnsureSha(legacySourceBytes, legacy.Metadata.Sha256, "legacy source changed before reverse sync");
            var backupRecovered = EnsureBackup(options, legacySourceBytes, legacy.Metadata.Sha256);
            PrepareTemp(options, reverseTemp, legacyBytes, ConfigMigrationFailurePoint.TempWrite);
            ThrowIf(options, ConfigMigrationFailurePoint.ReverseMove, "injected reverse move failure");
            File.Move(reverseTemp, options.LegacyConfigPath, overwrite: true);
            var finalBytes = File.ReadAllBytes(options.LegacyConfigPath);
            var final = ConfigSchemaDetector.Validate(finalBytes, PreDamageForecastSchemaV1.Descriptor);
            if (!final.IsSuccessful || !ConfigMigrationPipeline.AreSemanticallyEqual(current, final))
            {
                throw new InvalidDataException("reverse-sync target verification failed");
            }
            var marker = BuildMarker(
                options,
                current,
                final,
                sourcePath: options.CurrentConfigPath,
                targetPath: options.LegacyConfigPath,
                tempPath: reverseTemp,
                sourceSchema: sourceSchema,
                targetSchema: LegacyIdentityDescriptor.SchemaId,
                strategy: "explicit-new-to-old-reverse-sync",
                status: "rolled-back",
                reverseSyncStatus: "completed");
            WriteMarker(options, marker);
            return new ConfigMigrationResult(
                backupRecovered || current.Grade == ConfigMigrationGrade.RecoveredSuccess
                    ? ConfigMigrationGrade.RecoveredSuccess
                    : ConfigMigrationGrade.ExactSuccess,
                ConfigMigrationStatus.RolledBack,
                MayRegisterCurrentConfig: false,
                "Current configuration was reverse-synced to the legacy schema for rollback.",
                marker);
        }
        catch (Exception exception)
        {
            CleanupTemp(options, reverseTemp);
            return Failed("reverse-sync-failed", exception);
        }
    }

    private static ConfigMigrationResult ExecuteForward(
        ConfigMigrationOptions options,
        ConfigValidationResult source,
        byte[]? targetBytes,
        ConfigValidationResult target,
        bool targetAlreadyExists,
        bool recovered)
    {
        var targetTemp = Path.GetFullPath(options.TargetTempPath);
        try
        {
            ValidatePaths(options);
            Directory.CreateDirectory(options.TransactionRoot);
            ThrowIf(options, ConfigMigrationFailurePoint.SourceRead, "injected source read failure");
            var sourceBytes = File.ReadAllBytes(options.LegacyConfigPath);
            EnsureSha(sourceBytes, source.Metadata.Sha256, "source changed between validation and execute");
            var backupRecovered = EnsureBackup(options, sourceBytes, source.Metadata.Sha256);

            if (!targetAlreadyExists)
            {
                if (targetBytes is null)
                {
                    throw new InvalidOperationException("forward migration target bytes are missing");
                }
                if (File.Exists(options.CurrentConfigPath))
                {
                    throw new IOException("target move collision: current config already exists");
                }
                PrepareTemp(options, targetTemp, targetBytes, ConfigMigrationFailurePoint.TempWrite);
                ThrowIf(options, ConfigMigrationFailurePoint.TargetRace, "injected target race");
                ThrowIf(options, ConfigMigrationFailurePoint.TargetMove, "injected target move failure");
                File.Move(targetTemp, options.CurrentConfigPath, overwrite: false);
            }

            var finalBytes = File.ReadAllBytes(options.CurrentConfigPath);
            var final = ConfigSchemaDetector.Validate(finalBytes, DamageForecastSchemaV1.Descriptor);
            if (!final.IsSuccessful || !ConfigMigrationPipeline.AreSemanticallyEqual(source, final))
            {
                throw new InvalidDataException("current target verification failed");
            }

            var wasRecovered = recovered
                || backupRecovered
                || source.Grade == ConfigMigrationGrade.RecoveredSuccess
                || target.Grade == ConfigMigrationGrade.RecoveredSuccess;
            var marker = BuildMarker(
                options,
                source,
                final,
                sourcePath: options.LegacyConfigPath,
                targetPath: options.CurrentConfigPath,
                tempPath: targetTemp,
                sourceSchema: LegacyIdentityDescriptor.SchemaId,
                targetSchema: DamageForecastSchemaV1.SchemaId,
                strategy: targetAlreadyExists ? "bind-semantically-equal-existing-target" : "old-v1-to-current-v1",
                status: wasRecovered ? "recovered" : "completed",
                reverseSyncStatus: "not-requested");
            WriteMarker(options, marker);
            return new ConfigMigrationResult(
                wasRecovered ? ConfigMigrationGrade.RecoveredSuccess : ConfigMigrationGrade.ExactSuccess,
                wasRecovered ? ConfigMigrationStatus.Recovered : ConfigMigrationStatus.Completed,
                MayRegisterCurrentConfig: true,
                targetAlreadyExists
                    ? "Existing current configuration was verified and bound to the legacy source."
                    : "Legacy configuration was migrated to the current schema.",
                marker);
        }
        catch (Exception exception)
        {
            CleanupTemp(options, targetTemp);
            return Failed("forward-migration-failed", exception);
        }
    }

    private static bool EnsureBackup(
        ConfigMigrationOptions options,
        byte[] sourceBytes,
        string expectedSha)
    {
        if (File.Exists(options.BackupPath))
        {
            EnsureSha(File.ReadAllBytes(options.BackupPath), expectedSha, "existing backup does not match source");
            return true;
        }
        ThrowIf(options, ConfigMigrationFailurePoint.DiskFull, "injected disk full failure");
        ThrowIf(options, ConfigMigrationFailurePoint.AccessDenied, "injected access denied failure");
        ThrowIf(options, ConfigMigrationFailurePoint.BackupCreate, "injected backup create failure");
        using (var stream = new FileStream(
                   options.BackupPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
        {
            stream.Write(sourceBytes);
            ThrowIf(options, ConfigMigrationFailurePoint.BackupFlush, "injected backup flush failure");
            stream.Flush(flushToDisk: true);
        }
        EnsureSha(File.ReadAllBytes(options.BackupPath), expectedSha, "backup verification failed");
        return false;
    }

    private static void PrepareTemp(
        ConfigMigrationOptions options,
        string tempPath,
        byte[] bytes,
        ConfigMigrationFailurePoint failurePoint)
    {
        if (File.Exists(tempPath))
        {
            if (!File.ReadAllBytes(tempPath).AsSpan().SequenceEqual(bytes))
            {
                throw new IOException($"interrupted temp differs from planned content: {tempPath}");
            }
            return;
        }
        ThrowIf(options, failurePoint, $"injected {failurePoint} failure");
        using (var stream = new FileStream(
                   tempPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        if (!File.ReadAllBytes(tempPath).AsSpan().SequenceEqual(bytes))
        {
            throw new IOException($"temp verification failed: {tempPath}");
        }
    }

    private static ConfigMigrationMarker BuildMarker(
        ConfigMigrationOptions options,
        ConfigValidationResult source,
        ConfigValidationResult target,
        string sourcePath,
        string targetPath,
        string tempPath,
        string sourceSchema,
        string targetSchema,
        string strategy,
        string status,
        string reverseSyncStatus)
    {
        var backupInfo = new FileInfo(options.BackupPath);
        return new ConfigMigrationMarker(
            SchemaVersion: 1,
            TransactionId: options.TransactionId,
            SourceSchema: sourceSchema,
            TargetSchema: targetSchema,
            SourcePath: Path.GetFullPath(sourcePath),
            TargetPath: Path.GetFullPath(targetPath),
            BackupPath: Path.GetFullPath(options.BackupPath),
            TempPath: Path.GetFullPath(tempPath),
            SourceLength: source.Metadata.Length,
            TargetLength: target.Metadata.Length,
            BackupLength: backupInfo.Length,
            SourceSha256: source.Metadata.Sha256,
            TargetSha256: target.Metadata.Sha256,
            BackupSha256: ConfigDigest.Sha256(File.ReadAllBytes(options.BackupPath)),
            OrderedKeyDigest: target.Metadata.OrderedKeyDigest,
            TypedSemanticDigest: target.Metadata.TypedSemanticDigest,
            MigrationStrategy: strategy,
            ModVersion: options.ModVersion,
            GameVersion: options.GameVersion,
            TimestampUtc: options.TimestampUtc,
            Status: status,
            ReverseSyncStatus: reverseSyncStatus,
            CleanupEligible: false);
    }

    private static void WriteMarker(ConfigMigrationOptions options, ConfigMigrationMarker marker)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(marker, MarkerJsonOptions);
        if (File.Exists(options.MarkerPath))
        {
            var existing = JsonSerializer.Deserialize<ConfigMigrationMarker>(
                File.ReadAllBytes(options.MarkerPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (existing?.SourceSha256 == marker.SourceSha256
                && existing.TargetSha256 == marker.TargetSha256
                && existing.TypedSemanticDigest == marker.TypedSemanticDigest)
            {
                return;
            }
            throw new IOException("existing marker conflicts with the current transaction");
        }
        ThrowIf(options, ConfigMigrationFailurePoint.MarkerWrite, "injected marker write failure");
        PrepareTemp(options, options.MarkerTempPath, bytes, ConfigMigrationFailurePoint.MarkerWrite);
        File.Move(options.MarkerTempPath, options.MarkerPath, overwrite: false);
    }

    private static void ValidatePaths(ConfigMigrationOptions options)
    {
        var configRoot = NormalizeRoot(options.ConfigRoot);
        var migrationRoot = NormalizeRoot(options.MigrationRoot);
        if (migrationRoot.Equals(configRoot, StringComparison.OrdinalIgnoreCase)
            || migrationRoot.StartsWith(configRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("migration root must remain outside mod_configs");
        }
        foreach (var path in new[]
                 {
                     options.LegacyConfigPath,
                     options.CurrentConfigPath,
                     options.TargetTempPath,
                     options.ReverseTempPath
                 })
        {
            AssertWithin(configRoot, path, "config transaction path");
        }
        foreach (var path in new[]
                 {
                     options.TransactionRoot,
                     options.BackupPath,
                     options.MarkerPath,
                     options.MarkerTempPath
                 })
        {
            AssertWithin(migrationRoot, path, "migration transaction path");
        }
    }

    private static string NormalizeRoot(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void AssertWithin(string root, string candidate, string label)
    {
        var path = Path.GetFullPath(candidate);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label} escaped approved root: {path}");
        }
    }

    private static void EnsureSha(byte[] bytes, string expected, string message)
    {
        var actual = ConfigDigest.Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new IOException($"{message}: expected={expected}; actual={actual}");
        }
    }

    private static void ThrowIf(
        ConfigMigrationOptions options,
        ConfigMigrationFailurePoint point,
        string message)
    {
        if ((options.FailurePoints & point) != 0)
        {
            throw new IOException(message);
        }
    }

    private static void CleanupTemp(ConfigMigrationOptions options, string tempPath)
    {
        try
        {
            if ((options.FailurePoints & ConfigMigrationFailurePoint.CleanupTemp) != 0)
            {
                throw new IOException("injected cleanup temp failure");
            }
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // A failed cleanup is recorded by the absence of a completed marker; source and backup are preserved.
        }
    }

    private static ConfigMigrationResult Failed(string code, Exception exception) =>
        new(
            ConfigMigrationGrade.FailedSafe,
            ConfigMigrationStatus.FailedSafe,
            MayRegisterCurrentConfig: false,
            $"{code}:{exception.GetType().Name}:{exception.Message}");
}
