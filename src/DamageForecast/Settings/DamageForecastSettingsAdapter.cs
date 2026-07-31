using DamageForecast.UI;

namespace DamageForecast.Settings;

internal static class DamageForecastSettingsAdapter
{
    private static DamageForecastBaseLibConfig? _config;

    public static void Bind(DamageForecastBaseLibConfig config)
    {
        if (_config is not null)
        {
            _config.ConfigChanged -= ConfigChanged;
        }

        _config = config;
        _config.ConfigChanged += ConfigChanged;
    }

    public static void ApplyCurrent()
    {
        DamageForecastUiSettings.Apply(
            hudEnabled: DamageForecastBaseLibConfig.EnableDamageForecastHud,
            showLocalHudInMultiplayer: DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer,
            showBreakdownDetails: DamageForecastBaseLibConfig.ShowAdvancedShieldHeartDetails,
            freezeHudWithinPlayerTurn: true,
            damageDisplayMode: DamageForecastBaseLibConfig.DamageDisplayMode,
            incomingDamagePlacement: DamageForecastBaseLibConfig.IncomingDamagePlacement,
            includeCurrentBlockInIncomingDamage: DamageForecastBaseLibConfig.IncludeCurrentBlockInIncomingDamage,
            includePowerBlockInIncomingDamage: DamageForecastBaseLibConfig.IncludePowerBlockInIncomingDamage,
            includeRelicBlockInIncomingDamage: DamageForecastBaseLibConfig.IncludeRelicBlockInIncomingDamage,
            includePowerHpLossModifiersInIncomingDamage: DamageForecastBaseLibConfig.IncludePowerHpLossModifiersInIncomingDamage,
            includeRelicHpLossModifiersInIncomingDamage: DamageForecastBaseLibConfig.IncludeRelicHpLossModifiersInIncomingDamage,
            expectedHpLossPlacementPreset: DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset,
            incomingDamagePlacementPreset: DamageForecastBaseLibConfig.IncomingDamagePlacementPreset,
            detailsPlacementPreset: DamageForecastBaseLibConfig.DetailsPlacementPreset,
            offsetX: DamageForecastBaseLibConfig.HorizontalOffset,
            offsetY: DamageForecastBaseLibConfig.VerticalOffset,
            totalLossColor: DamageForecastBaseLibConfig.TotalExpectedLossColor,
            blockableDetailColor: DamageForecastBaseLibConfig.ShieldDetailColor,
            directHpLossDetailColor: DamageForecastBaseLibConfig.HeartDetailColor);
    }

    private static void ConfigChanged(object? sender, EventArgs e)
    {
        ApplyCurrent();
    }
}
