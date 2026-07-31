using DamageForecast.UI;

namespace DamageForecast.Settings;

internal enum DamageForecastConfigLanguage
{
    English,
    SimplifiedChinese
}

internal static class DamageForecastConfigText
{
    public const string EnglishProductName = "Damage Forecast";
    public const string SimplifiedChineseProductName = "伤害预测";

    public static string ProductName(DamageForecastConfigLanguage language)
    {
        return language == DamageForecastConfigLanguage.SimplifiedChinese
            ? SimplifiedChineseProductName
            : EnglishProductName;
    }

    public static string LanguageName(DamageForecastConfigLanguage language)
    {
        return language == DamageForecastConfigLanguage.SimplifiedChinese ? "简体中文" : "English";
    }

    public static string Section(string key, DamageForecastConfigLanguage language)
    {
        var zh = language == DamageForecastConfigLanguage.SimplifiedChinese;
        return key switch
        {
            "Display" => zh ? "显示" : "Display",
            "IncomingDamage" => zh ? "来袭总伤害 N 的计算方式" : "Incoming Damage N Calculation",
            "Multiplayer" => zh ? "多人模式" : "Multiplayer",
            "PositionAndAppearance" => zh ? "位置与外观" : "Position & Appearance",
            _ => key
        };
    }

    public static string Setting(string propertyName, DamageForecastConfigLanguage language)
    {
        var zh = language == DamageForecastConfigLanguage.SimplifiedChinese;
        return propertyName switch
        {
            nameof(DamageForecastBaseLibConfig.ConfigLanguage) => zh ? "语言" : "Language",
            nameof(DamageForecastBaseLibConfig.EnableDamageForecastHud) => zh ? "启用伤害预测 HUD" : "Enable Damage Forecast HUD",
            nameof(DamageForecastBaseLibConfig.ShowAdvancedShieldHeartDetails) => zh ? "显示护盾与生命损失详情" : "Show Block & HP Loss Details",
            nameof(DamageForecastBaseLibConfig.FreezeHudNumbersAfterTurnEnd) => zh ? "回合结束后冻结 HUD 数字" : "Freeze HUD Numbers After Turn End",
            nameof(DamageForecastBaseLibConfig.DamageDisplayMode) => zh ? "伤害显示模式" : "Damage Display Mode",
            nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement) => zh ? "来袭总伤害 N 的位置" : "Incoming Damage N Position",
            nameof(DamageForecastBaseLibConfig.IncludeCurrentBlockInIncomingDamage) => zh ? "计入当前护盾" : "Apply Current Block",
            nameof(DamageForecastBaseLibConfig.IncludePowerBlockInIncomingDamage) => zh ? "计入能力护盾" : "Apply Power Block",
            nameof(DamageForecastBaseLibConfig.IncludeRelicBlockInIncomingDamage) => zh ? "计入遗物护盾" : "Apply Relic Block",
            nameof(DamageForecastBaseLibConfig.IncludePowerHpLossModifiersInIncomingDamage) => zh ? "计入能力减伤" : "Apply Power Damage Reduction",
            nameof(DamageForecastBaseLibConfig.IncludeRelicHpLossModifiersInIncomingDamage) => zh ? "计入遗物减伤" : "Apply Relic Damage Reduction",
            nameof(DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer) => zh ? "在多人模式中显示本机伤害预测 HUD" : "Show local-player Damage Forecast HUD in Multiplayer",
            nameof(DamageForecastBaseLibConfig.HudAnchorPreset) => zh ? "HUD 锚点位置" : "HUD Position",
            nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset) => zh ? "预计掉血 -N 的位置" : "Expected HP Loss -N Position",
            nameof(DamageForecastBaseLibConfig.IncomingDamagePlacementPreset) => zh ? "来袭总伤害 N 的位置" : "Incoming Damage N Position",
            nameof(DamageForecastBaseLibConfig.DetailsPlacementPreset) => zh ? "护盾与生命损失明细的位置" : "Block & HP Loss Details Position",
            nameof(DamageForecastBaseLibConfig.HorizontalOffset) => zh ? "水平偏移" : "Horizontal Offset",
            nameof(DamageForecastBaseLibConfig.VerticalOffset) => zh ? "垂直偏移" : "Vertical Offset",
            nameof(DamageForecastBaseLibConfig.TotalExpectedLossColor) => zh ? "HUD 颜色" : "HUD Color",
            nameof(DamageForecastBaseLibConfig.ShieldDetailColor) => zh ? "护盾详情颜色" : "Block Detail Color",
            nameof(DamageForecastBaseLibConfig.HeartDetailColor) => zh ? "生命损失详情颜色" : "HP Loss Detail Color",
            _ => propertyName
        };
    }

    public static string EnumValue(string propertyName, object? value, DamageForecastConfigLanguage language)
    {
        if (value is DamageForecastConfigLanguage configLanguage)
        {
            return LanguageName(configLanguage);
        }

        var zh = language == DamageForecastConfigLanguage.SimplifiedChinese;
        if (value is DamageDisplayMode damageDisplayMode)
        {
            return damageDisplayMode switch
            {
                DamageDisplayMode.ExpectedHpLossOnly => zh ? "预计掉血（默认）" : "Expected HP Loss (Default)",
                DamageDisplayMode.IncomingDamageOnly => zh ? "来袭总伤害" : "Incoming Damage",
                DamageDisplayMode.Both => zh ? "同时显示" : "Show Both",
                _ => damageDisplayMode.ToString()
            };
        }

        if (value is IncomingDamagePlacement incomingDamagePlacement)
        {
            return incomingDamagePlacement switch
            {
                IncomingDamagePlacement.LeftOfExpectedHpLoss => zh ? "显示在预计掉血左侧" : "Left of Expected Loss",
                IncomingDamagePlacement.RightOfExpectedHpLoss => zh ? "显示在预计掉血右侧" : "Right of Expected Loss",
                _ => incomingDamagePlacement.ToString()
            };
        }

        if (value is DamageForecastHudAnchor anchor)
        {
            return anchor switch
            {
                DamageForecastHudAnchor.HealthBarRight => zh ? "血条右侧" : "Right of Health Bar",
                DamageForecastHudAnchor.HealthBarLeft => zh ? "血条左侧" : "Left of Health Bar",
                DamageForecastHudAnchor.HealthBarAbove => zh ? "血条上方" : "Above Health Bar",
                DamageForecastHudAnchor.HealthBarBelow => zh ? "血条下方" : "Below Health Bar",
                _ => anchor.ToString()
            };
        }

        if (value is HudPlacementPreset preset)
        {
            return preset switch
            {
                HudPlacementPreset.HealthBarRight => zh ? "血条右侧" : "Right of Health Bar",
                HudPlacementPreset.HealthBarLeft => zh ? "血条左侧" : "Left of Health Bar",
                HudPlacementPreset.HealthBarAbove => zh ? "人物上方" : "Above Character",
                HudPlacementPreset.HealthBarBelow => zh ? "血条下方" : "Below Health Bar",
                HudPlacementPreset.EndTurnButtonAbove => zh ? "结束回合按钮上方" : "Above End Turn Button",
                _ => preset.ToString()
            };
        }

        return value?.ToString() ?? propertyName;
    }
}
