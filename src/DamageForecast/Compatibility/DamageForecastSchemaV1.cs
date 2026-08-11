using System.Text.Json;
using DamageForecast.Settings;
using DamageForecast.UI;

namespace DamageForecast.Compatibility;

internal static class DamageForecastSchemaV1
{
    public const string SchemaId = "damage-forecast-v1";
    public const string ConfigFileName = "DamageForecast.cfg";
    public const string HudEnabledKey = "EnableDamageForecastHud";

    public static readonly IReadOnlyDictionary<string, string> HistoricalOptionalDefaults =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(DamageForecastBaseLibConfig.DamageDisplayMode)] = nameof(DamageDisplayMode.ExpectedHpLossOnly),
            [nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement)] = nameof(IncomingDamagePlacement.LeftOfExpectedHpLoss),
            [nameof(DamageForecastBaseLibConfig.IncludeCurrentBlockInIncomingDamage)] = "False",
            [nameof(DamageForecastBaseLibConfig.IncludePowerBlockInIncomingDamage)] = "False",
            [nameof(DamageForecastBaseLibConfig.IncludeRelicBlockInIncomingDamage)] = "False",
            [nameof(DamageForecastBaseLibConfig.IncludePowerHpLossModifiersInIncomingDamage)] = "False",
            [nameof(DamageForecastBaseLibConfig.IncludeRelicHpLossModifiersInIncomingDamage)] = "False"
        };

    public static readonly ConfigSchemaDescriptor Descriptor = new(
        Id: SchemaId,
        ConfigFileName: ConfigFileName,
        OrderedKeys: DamageForecastConfigSchema.PropertyOrder,
        HudEnabledKey: HudEnabledKey,
        IsCurrent: true);
}

internal static class HistoricalLegacyConfigRecovery
{
    public static bool TryRecover(byte[] raw, out ConfigValidationResult recovered)
    {
        recovered = ConfigSchemaDetector.Validate(raw, PreDamageForecastSchemaV1.Descriptor);
        if (recovered.IsSuccessful)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var properties = document.RootElement.EnumerateObject().ToArray();
            var orderedKeys = properties.Select(property => property.Name).ToArray();
            if (orderedKeys.Distinct(StringComparer.Ordinal).Count() != orderedKeys.Length)
            {
                return false;
            }

            var schema = PreDamageForecastSchemaV1.Descriptor;
            var expected = schema.OrderedKeys.ToHashSet(StringComparer.Ordinal);
            var unknown = orderedKeys.Where(key => !expected.Contains(key)).ToArray();
            var missing = schema.OrderedKeys.Where(key => !orderedKeys.Contains(key, StringComparer.Ordinal)).ToArray();
            if (unknown.Length > 0
                || missing.Length == 0
                || missing.Any(key => !DamageForecastSchemaV1.HistoricalOptionalDefaults.ContainsKey(key)))
            {
                return false;
            }

            var values = properties.ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var key in schema.OrderedKeys)
                {
                    writer.WritePropertyName(key);
                    if (values.TryGetValue(key, out var value))
                    {
                        writer.WriteRawValue(value.GetRawText());
                    }
                    else
                    {
                        writer.WriteStringValue(DamageForecastSchemaV1.HistoricalOptionalDefaults[key]);
                    }
                }
                writer.WriteEndObject();
            }

            var completed = ConfigSchemaDetector.Validate(stream.ToArray(), schema);
            if (!completed.IsSuccessful || completed.Snapshot is null)
            {
                return false;
            }

            recovered = completed with
            {
                Grade = ConfigMigrationGrade.RecoveredSuccess,
                Metadata = completed.Metadata with
                {
                    Length = raw.LongLength,
                    Sha256 = ConfigDigest.Sha256(raw),
                    OrderedKeyDigest = ConfigDigest.Sha256(string.Join("\n", orderedKeys)),
                    OrderedKeys = orderedKeys
                },
                Diagnostics = completed.Diagnostics
                    .Append($"historical-defaults-applied:{string.Join(',', missing)}")
                    .ToArray()
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
