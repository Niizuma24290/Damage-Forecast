using Godot;

namespace DamageForecast.UI;

internal static class DamageForecastUiSettings
{
    public static event Action? Changed;

    private static bool _hudEnabled = true;
    private static bool _showLocalHudInMultiplayer = true;
    private static bool _showBreakdownDetails;
    private static DamageDisplayMode _damageDisplayMode = DamageDisplayMode.ExpectedHpLossOnly;
    private static IncomingDamagePlacement _incomingDamagePlacement = IncomingDamagePlacement.LeftOfExpectedHpLoss;
    private static bool _includeCurrentBlockInIncomingDamage;
    private static bool _includePowerBlockInIncomingDamage;
    private static bool _includeRelicBlockInIncomingDamage;
    private static bool _includePowerHpLossModifiersInIncomingDamage;
    private static bool _includeRelicHpLossModifiersInIncomingDamage;
    private static HudPlacementPreset _expectedHpLossPlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _incomingDamagePlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _detailsPlacementPreset = HudPlacementPreset.HealthBarRight;
    private static float _offsetX;
    private static float _offsetY;
    private static Color _totalLossColor = Colors.White;
    private static Color _blockableDetailColor = new(0.55f, 0.85f, 1f);
    private static Color _directHpLossDetailColor = new(1f, 0.55f, 0.62f);

    public static bool HudEnabled => _hudEnabled;

    public static bool ShowLocalHudInMultiplayer => _showLocalHudInMultiplayer;

    public static bool ShowBreakdownDetails => _showBreakdownDetails;

    public static bool FreezeHudWithinPlayerTurn => true;

    public static DamageDisplayMode DamageDisplayMode => _damageDisplayMode;

    public static IncomingDamagePlacement IncomingDamagePlacement => _incomingDamagePlacement;

    public static bool IncludeCurrentBlockInIncomingDamage => _includeCurrentBlockInIncomingDamage;

    public static bool IncludePowerBlockInIncomingDamage => _includePowerBlockInIncomingDamage;

    public static bool IncludeRelicBlockInIncomingDamage => _includeRelicBlockInIncomingDamage;

    public static bool IncludePowerHpLossModifiersInIncomingDamage => _includePowerHpLossModifiersInIncomingDamage;

    public static bool IncludeRelicHpLossModifiersInIncomingDamage => _includeRelicHpLossModifiersInIncomingDamage;

    public static HudPlacementPreset ExpectedHpLossPlacementPreset => _expectedHpLossPlacementPreset;

    public static HudPlacementPreset IncomingDamagePlacementPreset => _incomingDamagePlacementPreset;

    public static HudPlacementPreset DetailsPlacementPreset => _detailsPlacementPreset;

    public static DamageForecastHudAnchor HudAnchor =>
        ExpectedHpLossPlacementPreset switch
        {
            HudPlacementPreset.HealthBarLeft => DamageForecastHudAnchor.HealthBarLeft,
            HudPlacementPreset.HealthBarAbove => DamageForecastHudAnchor.HealthBarAbove,
            HudPlacementPreset.HealthBarBelow => DamageForecastHudAnchor.HealthBarBelow,
            _ => DamageForecastHudAnchor.HealthBarRight
        };

    public static float OffsetX => _offsetX;

    public static float OffsetY => _offsetY;

    public static Color TotalLossColor => _totalLossColor;

    public static Color BlockableDetailColor => _blockableDetailColor;

    public static Color DirectHpLossDetailColor => _directHpLossDetailColor;

    public static void SetHudEnabled(bool value) => Set(ref _hudEnabled, value);

    public static void SetShowLocalHudInMultiplayer(bool value) => Set(ref _showLocalHudInMultiplayer, value);

    public static void SetShowBreakdownDetails(bool value) => Set(ref _showBreakdownDetails, value);

    public static void SetFreezeHudWithinPlayerTurn(bool value)
    {
    }

    public static void SetDamageDisplayMode(DamageDisplayMode value) => Set(ref _damageDisplayMode, value);

    public static void SetIncomingDamagePlacement(IncomingDamagePlacement value) => Set(ref _incomingDamagePlacement, value);

    public static void SetIncludeCurrentBlockInIncomingDamage(bool value) => Set(ref _includeCurrentBlockInIncomingDamage, value);

    public static void SetIncludePowerBlockInIncomingDamage(bool value) => Set(ref _includePowerBlockInIncomingDamage, value);

    public static void SetIncludeRelicBlockInIncomingDamage(bool value) => Set(ref _includeRelicBlockInIncomingDamage, value);

    public static void SetIncludePowerHpLossModifiersInIncomingDamage(bool value) => Set(ref _includePowerHpLossModifiersInIncomingDamage, value);

    public static void SetIncludeRelicHpLossModifiersInIncomingDamage(bool value) => Set(ref _includeRelicHpLossModifiersInIncomingDamage, value);

    public static void SetExpectedHpLossPlacementPreset(HudPlacementPreset value) =>
        Set(ref _expectedHpLossPlacementPreset, value);

    public static void SetIncomingDamagePlacementPreset(HudPlacementPreset value) =>
        Set(ref _incomingDamagePlacementPreset, value);

    public static void SetDetailsPlacementPreset(HudPlacementPreset value) =>
        Set(ref _detailsPlacementPreset, value);

    public static void SetOffsetX(float value) => Set(ref _offsetX, Math.Clamp(value, -320f, 320f));

    public static void SetOffsetY(float value) => Set(ref _offsetY, Math.Clamp(value, -240f, 240f));

    public static void SetTotalLossColor(Color value) => Set(ref _totalLossColor, value);

    public static void SetBlockableDetailColor(Color value) => Set(ref _blockableDetailColor, value);

    public static void SetDirectHpLossDetailColor(Color value) => Set(ref _directHpLossDetailColor, value);

    public static void ResetDefaults()
    {
        Apply(
            hudEnabled: true,
            showLocalHudInMultiplayer: true,
            showBreakdownDetails: false,
            freezeHudWithinPlayerTurn: true,
            damageDisplayMode: DamageDisplayMode.ExpectedHpLossOnly,
            incomingDamagePlacement: IncomingDamagePlacement.LeftOfExpectedHpLoss,
            includeCurrentBlockInIncomingDamage: false,
            includePowerBlockInIncomingDamage: false,
            includeRelicBlockInIncomingDamage: false,
            includePowerHpLossModifiersInIncomingDamage: false,
            includeRelicHpLossModifiersInIncomingDamage: false,
            expectedHpLossPlacementPreset: HudPlacementPreset.HealthBarRight,
            incomingDamagePlacementPreset: HudPlacementPreset.HealthBarRight,
            detailsPlacementPreset: HudPlacementPreset.HealthBarRight,
            offsetX: 0f,
            offsetY: 0f,
            totalLossColor: Colors.White,
            blockableDetailColor: new Color(0.55f, 0.85f, 1f),
            directHpLossDetailColor: new Color(1f, 0.55f, 0.62f));
    }

    internal static void Apply(
        bool hudEnabled,
        bool showLocalHudInMultiplayer,
        bool showBreakdownDetails,
        bool freezeHudWithinPlayerTurn,
        DamageDisplayMode damageDisplayMode,
        IncomingDamagePlacement incomingDamagePlacement,
        bool includeCurrentBlockInIncomingDamage,
        bool includePowerBlockInIncomingDamage,
        bool includeRelicBlockInIncomingDamage,
        bool includePowerHpLossModifiersInIncomingDamage,
        bool includeRelicHpLossModifiersInIncomingDamage,
        HudPlacementPreset expectedHpLossPlacementPreset,
        HudPlacementPreset incomingDamagePlacementPreset,
        HudPlacementPreset detailsPlacementPreset,
        float offsetX,
        float offsetY,
        Color totalLossColor,
        Color blockableDetailColor,
        Color directHpLossDetailColor)
    {
        var changed = false;
        changed |= Apply(ref _hudEnabled, hudEnabled);
        changed |= Apply(ref _showLocalHudInMultiplayer, showLocalHudInMultiplayer);
        changed |= Apply(ref _showBreakdownDetails, showBreakdownDetails);
        changed |= Apply(ref _damageDisplayMode, damageDisplayMode);
        changed |= Apply(ref _incomingDamagePlacement, incomingDamagePlacement);
        changed |= Apply(ref _includeCurrentBlockInIncomingDamage, includeCurrentBlockInIncomingDamage);
        changed |= Apply(ref _includePowerBlockInIncomingDamage, includePowerBlockInIncomingDamage);
        changed |= Apply(ref _includeRelicBlockInIncomingDamage, includeRelicBlockInIncomingDamage);
        changed |= Apply(ref _includePowerHpLossModifiersInIncomingDamage, includePowerHpLossModifiersInIncomingDamage);
        changed |= Apply(ref _includeRelicHpLossModifiersInIncomingDamage, includeRelicHpLossModifiersInIncomingDamage);
        changed |= Apply(ref _expectedHpLossPlacementPreset, expectedHpLossPlacementPreset);
        changed |= Apply(ref _incomingDamagePlacementPreset, incomingDamagePlacementPreset);
        changed |= Apply(ref _detailsPlacementPreset, detailsPlacementPreset);
        changed |= Apply(ref _offsetX, Math.Clamp(offsetX, -320f, 320f));
        changed |= Apply(ref _offsetY, Math.Clamp(offsetY, -240f, 240f));
        changed |= Apply(ref _totalLossColor, totalLossColor);
        changed |= Apply(ref _blockableDetailColor, blockableDetailColor);
        changed |= Apply(ref _directHpLossDetailColor, directHpLossDetailColor);
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private static void Set<T>(ref T field, T value)
    {
        if (Apply(ref field, value))
        {
            Changed?.Invoke();
        }
    }

    private static bool Apply<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}

internal enum DamageForecastHudAnchor
{
    HealthBarRight,
    HealthBarLeft,
    HealthBarAbove,
    HealthBarBelow
}

internal enum DamageDisplayMode
{
    ExpectedHpLossOnly,
    IncomingDamageOnly,
    Both
}

internal enum IncomingDamagePlacement
{
    LeftOfExpectedHpLoss,
    RightOfExpectedHpLoss
}
