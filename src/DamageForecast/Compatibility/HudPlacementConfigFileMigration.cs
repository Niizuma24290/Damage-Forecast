using System.Text;
using System.Text.Json;
using DamageForecast.Settings;
using DamageForecast.UI;

namespace DamageForecast.Compatibility;

internal static class HudPlacementConfigFileMigration
{
    public const string SchemaId = "damage-forecast-hud-placement-v2";

    public static HudPlacementFileMigrationResult Run(ConfigMigrationOptions options)
    {
        if (!File.Exists(options.CurrentConfigPath))
        {
            return new(true, "FreshInstall", "No current config requires HUD placement migration.");
        }

        try
        {
            var source = File.ReadAllBytes(options.CurrentConfigPath);
            var v2 = ValidateV2(source);
            byte[] target;
            string status;
            if (v2.IsValid)
            {
                if (!v2.RequiresNormalization)
                {
                    return new(true, "AlreadyV2", "HUD placement config is already strict V2.");
                }

                target = WriteV2(v2.Values!, v2.Placements);
                status = "NormalizedV2";
            }
            else
            {
                var v1 = ConfigSchemaDetector.Validate(source, DamageForecastSchemaV1.Descriptor);
                if (!v1.IsSuccessful)
                {
                    return new(
                        false,
                        "FailedSafe",
                        $"Current config is neither safe V1 nor strict V2:{string.Join('|', v1.Diagnostics)}");
                }

                using var document = JsonDocument.Parse(source);
                var values = document.RootElement.EnumerateObject()
                    .ToDictionary(
                        property => property.Name,
                        property => property.Value.Clone(),
                        StringComparer.Ordinal);
                if (!TryReadLegacyAnchor(values[nameof(DamageForecastBaseLibConfig.HudAnchorPreset)], out var anchor))
                {
                    return new(false, "FailedSafe", "V1 HUD anchor cannot be migrated safely.");
                }

                var upgraded = HudPlacementConfigMigrationPolicy.Upgrade(
                    new HudPlacementConfigV1(anchor, FreezeHudNumbersAfterTurnEnd: true));
                target = WriteV2(values, upgraded);
                status = "MigratedV1ToV2";
            }

            var verified = ValidateV2(target);
            if (!verified.IsValid || verified.RequiresNormalization)
            {
                return new(false, "FailedSafe", "Generated V2 config failed strict verification.");
            }

            WriteTransaction(options, source, target, status);
            return new(true, status, "HUD placement config migration completed transactionally.");
        }
        catch (Exception exception)
        {
            return new(
                false,
                "FailedSafe",
                $"hud-placement-migration-failed:{exception.GetType().Name}:{exception.Message}");
        }
    }

    public static bool IsStrictV2(byte[] raw) => ValidateV2(raw).IsValid;

    public static bool TryCreateRollbackV1(
        byte[] raw,
        out byte[] v1,
        out string message)
    {
        var validation = ValidateV2(raw);
        if (!validation.IsValid || validation.Values is null)
        {
            v1 = [];
            message = "Current config is not strict HUD placement V2.";
            return false;
        }

        var downgrade = HudPlacementConfigMigrationPolicy.TryDowngrade(
            validation.Placements);
        if (downgrade.Status != HudPlacementConfigDowngradeStatus.Exact
            || downgrade.Config is not { } config)
        {
            v1 = [];
            message = $"HUD placement V2 cannot be represented losslessly in V1:{downgrade.Status}";
            return false;
        }

        v1 = WriteV1(validation.Values, config);
        var verified = ConfigSchemaDetector.Validate(v1, DamageForecastSchemaV1.Descriptor);
        if (!verified.IsSuccessful)
        {
            v1 = [];
            message = "Generated rollback V1 failed strict verification.";
            return false;
        }

        message = "HUD placement V2 was represented exactly as V1.";
        return true;
    }

    private static HudPlacementV2Validation ValidateV2(byte[] raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return HudPlacementV2Validation.Invalid;
            }

            var properties = document.RootElement.EnumerateObject().ToArray();
            var keys = properties.Select(property => property.Name).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length
                || keys.Length != HudPlacementConfigSchema.V2PropertyOrder.Length
                || keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(HudPlacementConfigSchema.V2PropertyOrder) is false)
            {
                return HudPlacementV2Validation.Invalid;
            }

            var values = properties.ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal);
            if (!TryReadPlacement(values[nameof(HudPlacementConfigV2.ExpectedHpLossPlacementPreset)], out var expected, out var expectedNormalized)
                || !TryReadPlacement(values[nameof(HudPlacementConfigV2.IncomingDamagePlacementPreset)], out var incoming, out var incomingNormalized)
                || !TryReadPlacement(values[nameof(HudPlacementConfigV2.DetailsPlacementPreset)], out var details, out var detailsNormalized)
                || !TryReadBool(values[nameof(DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd)], out var freeze, out var freezeNormalized))
            {
                return HudPlacementV2Validation.Invalid;
            }

            var syntheticV1 = WriteSyntheticV1(values);
            var commonValidation = ConfigSchemaDetector.Validate(
                syntheticV1,
                DamageForecastSchemaV1.Descriptor);
            if (!commonValidation.IsSuccessful)
            {
                return HudPlacementV2Validation.Invalid;
            }

            var exactOrder = keys.SequenceEqual(
                HudPlacementConfigSchema.V2PropertyOrder,
                StringComparer.Ordinal);
            return new HudPlacementV2Validation(
                true,
                !exactOrder
                    || expectedNormalized
                    || incomingNormalized
                    || detailsNormalized
                    || freezeNormalized
                    || !freeze,
                values,
                new HudPlacementConfigV2(expected, incoming, details, true));
        }
        catch
        {
            return HudPlacementV2Validation.Invalid;
        }
    }

    private static byte[] WriteSyntheticV1(IReadOnlyDictionary<string, JsonElement> values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var key in HudPlacementConfigSchema.V1PropertyOrder)
            {
                writer.WritePropertyName(key);
                if (key == nameof(DamageForecastBaseLibConfig.HudAnchorPreset))
                {
                    writer.WriteStringValue(nameof(DamageForecastHudAnchor.HealthBarRight));
                }
                else
                {
                    writer.WriteRawValue(values[key].GetRawText());
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteV2(
        IReadOnlyDictionary<string, JsonElement> values,
        HudPlacementConfigV2 placements)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var key in HudPlacementConfigSchema.V2PropertyOrder)
            {
                writer.WritePropertyName(key);
                switch (key)
                {
                    case nameof(HudPlacementConfigV2.ExpectedHpLossPlacementPreset):
                        writer.WriteStringValue(placements.ExpectedHpLossPlacementPreset.ToString());
                        break;
                    case nameof(HudPlacementConfigV2.IncomingDamagePlacementPreset):
                        writer.WriteStringValue(placements.IncomingDamagePlacementPreset.ToString());
                        break;
                    case nameof(HudPlacementConfigV2.DetailsPlacementPreset):
                        writer.WriteStringValue(placements.DetailsPlacementPreset.ToString());
                        break;
                    case nameof(DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd):
                        writer.WriteStringValue("True");
                        break;
                    default:
                        writer.WriteRawValue(values[key].GetRawText());
                        break;
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static byte[] WriteV1(
        IReadOnlyDictionary<string, JsonElement> values,
        HudPlacementConfigV1 placement)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var key in HudPlacementConfigSchema.V1PropertyOrder)
            {
                writer.WritePropertyName(key);
                if (key == nameof(DamageForecastBaseLibConfig.HudAnchorPreset))
                {
                    writer.WriteStringValue(placement.HudAnchorPreset.ToString());
                }
                else if (key == nameof(DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd))
                {
                    writer.WriteStringValue("True");
                }
                else
                {
                    writer.WriteRawValue(values[key].GetRawText());
                }
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteTransaction(
        ConfigMigrationOptions options,
        byte[] source,
        byte[] target,
        string status)
    {
        var transactionRoot = Path.Combine(
            options.MigrationRoot,
            "hud-placement-v2",
            options.TransactionId);
        Directory.CreateDirectory(transactionRoot);
        Directory.CreateDirectory(options.ConfigRoot);
        var backupPath = Path.Combine(transactionRoot, DamageForecastSchemaV1.ConfigFileName + ".backup");
        var markerPath = Path.Combine(transactionRoot, "marker.json");
        var tempPath = Path.Combine(
            options.ConfigRoot,
            $".{DamageForecastSchemaV1.ConfigFileName}.{options.TransactionId}.hud-v2.tmp");
        WriteDurably(backupPath, source);
        WriteDurably(tempPath, target);
        File.Move(tempPath, options.CurrentConfigPath, overwrite: true);
        var marker = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                SchemaVersion = 2,
                Status = status,
                SourceSha256 = ConfigDigest.Sha256(source),
                TargetSha256 = ConfigDigest.Sha256(target),
                TimestampUtc = options.TimestampUtc
            },
            new JsonSerializerOptions { WriteIndented = true });
        WriteDurably(markerPath, marker);
    }

    private static void WriteDurably(string path, byte[] bytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static bool TryReadLegacyAnchor(JsonElement element, out DamageForecastHudAnchor value)
    {
        if (element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: false, out value)
            && Enum.IsDefined(value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var numeric))
        {
            value = (DamageForecastHudAnchor)numeric;
            return Enum.IsDefined(value);
        }

        value = default;
        return false;
    }

    private static bool TryReadPlacement(
        JsonElement element,
        out HudPlacementPreset value,
        out bool requiresNormalization)
    {
        requiresNormalization = false;
        if (element.ValueKind == JsonValueKind.String
            && Enum.TryParse(element.GetString(), ignoreCase: false, out value)
            && Enum.IsDefined(value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var numeric))
        {
            value = (HudPlacementPreset)numeric;
            requiresNormalization = Enum.IsDefined(value);
            return requiresNormalization;
        }

        value = default;
        return false;
    }

    private static bool TryReadBool(
        JsonElement element,
        out bool value,
        out bool requiresNormalization)
    {
        requiresNormalization = false;
        if (element.ValueKind == JsonValueKind.String
            && bool.TryParse(element.GetString(), out value))
        {
            requiresNormalization = element.GetString() is not ("True" or "False");
            return true;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            requiresNormalization = true;
            return true;
        }

        value = default;
        return false;
    }

    private sealed record HudPlacementV2Validation(
        bool IsValid,
        bool RequiresNormalization,
        IReadOnlyDictionary<string, JsonElement>? Values,
        HudPlacementConfigV2 Placements)
    {
        public static HudPlacementV2Validation Invalid =>
            new(false, false, null, default);
    }
}

internal readonly record struct HudPlacementFileMigrationResult(
    bool MayContinue,
    string Status,
    string Message);
