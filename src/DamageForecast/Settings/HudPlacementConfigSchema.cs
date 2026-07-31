using DamageForecast.UI;

namespace DamageForecast.Settings;

internal static class HudPlacementConfigSchema
{
    public static readonly string[] V1PropertyOrder =
        DamageForecastConfigSchema.PropertyOrder.ToArray();

    public static readonly string[] V2PropertyOrder =
    [
        nameof(DamageForecastBaseLibConfig.ConfigLanguage),
        nameof(DamageForecastBaseLibConfig.EnableDamageForecastHud),
        nameof(DamageForecastBaseLibConfig.ShowAdvancedShieldHeartDetails),
        nameof(DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd),
        nameof(DamageForecastBaseLibConfig.DamageDisplayMode),
        nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement),
        nameof(DamageForecastBaseLibConfig.IncludeCurrentBlockInIncomingDamage),
        nameof(DamageForecastBaseLibConfig.IncludePowerBlockInIncomingDamage),
        nameof(DamageForecastBaseLibConfig.IncludeRelicBlockInIncomingDamage),
        nameof(DamageForecastBaseLibConfig.IncludePowerHpLossModifiersInIncomingDamage),
        nameof(DamageForecastBaseLibConfig.IncludeRelicHpLossModifiersInIncomingDamage),
        nameof(DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer),
        nameof(HudPlacementConfigV2.ExpectedHpLossPlacementPreset),
        nameof(HudPlacementConfigV2.IncomingDamagePlacementPreset),
        nameof(HudPlacementConfigV2.DetailsPlacementPreset),
        nameof(DamageForecastBaseLibConfig.HorizontalOffset),
        nameof(DamageForecastBaseLibConfig.VerticalOffset),
        nameof(DamageForecastBaseLibConfig.TotalExpectedLossColor),
        nameof(DamageForecastBaseLibConfig.ShieldDetailColor),
        nameof(DamageForecastBaseLibConfig.HeartDetailColor)
    ];

    public static HudPlacementConfigV2 Defaults => new(
        HudPlacementPreset.HealthBarRight,
        HudPlacementPreset.HealthBarRight,
        HudPlacementPreset.HealthBarRight,
        FreezeHudNumbersAfterTurnEnd: true);
}

internal readonly record struct HudPlacementConfigV1(
    DamageForecastHudAnchor HudAnchorPreset,
    bool FreezeHudNumbersAfterTurnEnd);

internal readonly record struct HudPlacementConfigV2(
    HudPlacementPreset ExpectedHpLossPlacementPreset,
    HudPlacementPreset IncomingDamagePlacementPreset,
    HudPlacementPreset DetailsPlacementPreset,
    bool FreezeHudNumbersAfterTurnEnd);

internal static class HudPlacementConfigMigrationPolicy
{
    public static HudPlacementConfigV2 Upgrade(HudPlacementConfigV1 source)
    {
        var preset = source.HudAnchorPreset switch
        {
            DamageForecastHudAnchor.HealthBarRight => HudPlacementPreset.HealthBarRight,
            DamageForecastHudAnchor.HealthBarLeft => HudPlacementPreset.HealthBarLeft,
            DamageForecastHudAnchor.HealthBarAbove => HudPlacementPreset.HealthBarAbove,
            DamageForecastHudAnchor.HealthBarBelow => HudPlacementPreset.HealthBarBelow,
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source.HudAnchorPreset,
                "Unsupported V1 HUD anchor")
        };
        return new HudPlacementConfigV2(
            preset,
            preset,
            preset,
            FreezeHudNumbersAfterTurnEnd: true);
    }

    public static HudPlacementConfigDowngradeResult TryDowngrade(
        HudPlacementConfigV2 source)
    {
        if (source.ExpectedHpLossPlacementPreset != source.IncomingDamagePlacementPreset
            || source.ExpectedHpLossPlacementPreset != source.DetailsPlacementPreset)
        {
            return new(
                HudPlacementConfigDowngradeStatus.DivergentPlacements,
                null);
        }

        var anchor = source.ExpectedHpLossPlacementPreset switch
        {
            HudPlacementPreset.HealthBarRight => DamageForecastHudAnchor.HealthBarRight,
            HudPlacementPreset.HealthBarLeft => DamageForecastHudAnchor.HealthBarLeft,
            HudPlacementPreset.HealthBarAbove => DamageForecastHudAnchor.HealthBarAbove,
            HudPlacementPreset.HealthBarBelow => DamageForecastHudAnchor.HealthBarBelow,
            _ => (DamageForecastHudAnchor?)null
        };
        return anchor is { } supported
            ? new(
                HudPlacementConfigDowngradeStatus.Exact,
                new HudPlacementConfigV1(
                    supported,
                    FreezeHudNumbersAfterTurnEnd: true))
            : new(
                HudPlacementConfigDowngradeStatus.UnsupportedPlacement,
                null);
    }
}

internal enum HudPlacementConfigDowngradeStatus
{
    Exact,
    DivergentPlacements,
    UnsupportedPlacement
}

internal readonly record struct HudPlacementConfigDowngradeResult(
    HudPlacementConfigDowngradeStatus Status,
    HudPlacementConfigV1? Config);
