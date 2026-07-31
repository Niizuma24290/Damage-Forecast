using Godot;
using DamageForecast.Combat;
using DamageForecast.Forecast;

namespace DamageForecast.UI;

internal static class DamageForecastHudDisplay
{
    private const int MainFontSize = 24;
    private const int DetailFontSize = 18;
    private const int DetailShieldFontSize = 15;
    private const int DetailHeartFontSize = 22;
    private const float HealthBarRightPadding = 6f;
    private const float SegmentGap = 10f;
    private const float DetailRightGap = 12f;

    public static string BuildMainHudDisplay(ForecastResult result)
    {
        var projection = ForecastHudProjectionPolicy.Project(
            new ForecastHudSnapshot(result, IncomingDamageDisplayRead.Hidden),
            DamageDisplayMode.ExpectedHpLossOnly,
            IncomingDamagePlacement.RightOfExpectedHpLoss);
        return projection.ExpectedTotalHpLoss <= 0
            ? string.Empty
            : $"-{projection.ExpectedTotalHpLoss}";
    }

    public static string BuildHudDetails(ForecastResult result)
    {
        var projection = ForecastHudProjectionPolicy.Project(
            new ForecastHudSnapshot(result, IncomingDamageDisplayRead.Hidden),
            DamageDisplayMode.ExpectedHpLossOnly,
            IncomingDamagePlacement.RightOfExpectedHpLoss);
        return DamageForecastUiSettings.ShowBreakdownDetails && projection.ExpectedTotalHpLoss > 0
            ? BuildForecastDetails(
                projection.ExpectedBlockableHpLoss,
                projection.ExpectedDirectHpLoss)
            : string.Empty;
    }

    public static string BuildIncomingHudDisplay(IncomingDamageDisplayRead read)
    {
        var projection = ForecastHudProjectionPolicy.Project(
            new ForecastHudSnapshot(ForecastResult.Hidden, read),
            DamageDisplayMode.IncomingDamageOnly,
            IncomingDamagePlacement.RightOfExpectedHpLoss);
        return projection.IncomingDamage > 0
            ? projection.IncomingDamage.ToString()
            : string.Empty;
    }

    public static bool ShouldShowExpectedHpLoss(ForecastHudSnapshot snapshot)
    {
        return Project(snapshot).ShowExpectedHpLoss;
    }

    public static bool ShouldShowIncomingDamage(ForecastHudSnapshot snapshot)
    {
        return Project(snapshot).ShowIncomingDamage;
    }

    public static bool HasDisplayableSnapshot(ForecastHudSnapshot snapshot)
    {
        return Project(snapshot).HasDisplayableValue;
    }

    public static void ApplyMainHudStyle(Label label)
    {
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.CustomMinimumSize = new Vector2(GetMainWidth(), GetMainHeight());
        label.Size = label.CustomMinimumSize;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", MainFontSize);
        label.AddThemeColorOverride("font_color", DamageForecastUiSettings.TotalLossColor);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
    }

    public static void ApplyIncomingHudStyle(Label label)
    {
        ApplyMainHudStyle(label);
    }

    public static void ApplyNumericHudAlignment(
        Label label,
        HudPlacementPreset preset)
    {
        label.HorizontalAlignment = HudNumericAlignmentPolicy.Resolve(preset);
    }

    public static void ApplyMainHudTextBounds(Label label)
    {
        ApplyHudTextBounds(label);
    }

    public static void ApplyHudTextBounds(Label label)
    {
        var textSize = GetMainTextSize(label);
        label.CustomMinimumSize = textSize;
        label.Size = textSize;
    }

    public static void ApplyDetailHudStyle(RichTextLabel label)
    {
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.BbcodeEnabled = true;
        label.ScrollActive = false;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.CustomMinimumSize = new Vector2(GetDetailWidth(), GetDetailHeight());
        label.Size = label.CustomMinimumSize;
        label.AddThemeFontSizeOverride("font_size", DetailFontSize);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
    }

    public static void ApplyDetailHudTextBounds(RichTextLabel label)
    {
        var contentWidth = MathF.Max(1f, label.GetContentWidth());
        var contentHeight = MathF.Max(GetDetailHeight(), label.GetContentHeight());
        label.CustomMinimumSize = new Vector2(contentWidth, contentHeight);
        label.Size = label.CustomMinimumSize;
    }

    public static void ApplyHudPosition(
        Control healthBar,
        Control expectedLabel,
        Control incomingLabel,
        Control? detailLabel,
        Vector2? containerSize,
        bool forceBelowHealthBar)
    {
        var size = containerSize ?? healthBar.Size;
        var expectedVisible = !string.IsNullOrEmpty(GetControlText(expectedLabel));
        var incomingVisible = !string.IsNullOrEmpty(GetControlText(incomingLabel));
        var anchorLabel = expectedVisible ? expectedLabel : incomingLabel;
        var labelSize = anchorLabel.Size;
        var center = GetHealthBarCenter(healthBar, size);
        var offset = new Vector2(DamageForecastUiSettings.OffsetX, DamageForecastUiSettings.OffsetY);
        var anchor = forceBelowHealthBar ? DamageForecastHudAnchor.HealthBarBelow : DamageForecastUiSettings.HudAnchor;
        var position = anchor switch
        {
            DamageForecastHudAnchor.HealthBarLeft => new Vector2(
                healthBar.Position.X - labelSize.X - 22f,
                healthBar.Position.Y + MathF.Max(0f, (size.Y - labelSize.Y) * 0.5f)),
            DamageForecastHudAnchor.HealthBarAbove => new Vector2(
                healthBar.Position.X + MathF.Max(0f, (size.X - labelSize.X) * 0.5f),
                healthBar.Position.Y - labelSize.Y - 14f),
            DamageForecastHudAnchor.HealthBarBelow => new Vector2(
                healthBar.Position.X + MathF.Max(0f, (size.X - labelSize.X) * 0.5f),
                healthBar.Position.Y + size.Y + 14f),
            _ => new Vector2(
                healthBar.Position.X + size.X + HealthBarRightPadding,
                center.Y - (labelSize.Y * 0.5f))
        };

        position += offset;
        var anchorPosition = new Vector2(MathF.Max(0f, position.X), position.Y);
        if (expectedVisible)
        {
            expectedLabel.Position = anchorPosition;
            if (incomingVisible)
            {
                if (ForecastHudProjectionPolicy.NormalizePlacement(
                        DamageForecastUiSettings.IncomingDamagePlacement)
                    == IncomingDamagePlacement.LeftOfExpectedHpLoss)
                {
                    incomingLabel.Position = new Vector2(
                        expectedLabel.Position.X - incomingLabel.Size.X - SegmentGap,
                        expectedLabel.Position.Y);
                }
                else
                {
                    incomingLabel.Position = new Vector2(
                        expectedLabel.Position.X + expectedLabel.Size.X + SegmentGap,
                        expectedLabel.Position.Y);
                }
            }
        }
        else if (incomingVisible)
        {
            incomingLabel.Position = anchorPosition;
        }

        if (detailLabel is not null)
        {
            var rightEdge = GetRightEdge(expectedVisible, expectedLabel, incomingVisible, incomingLabel);
            detailLabel.Position = new Vector2(
                rightEdge + DetailRightGap,
                anchorPosition.Y + ((labelSize.Y - detailLabel.Size.Y) * 0.5f));
        }
    }

    private static string BuildForecastDetails(int blockablePrediction, int directHpLossPrediction)
    {
        var details = new List<string>(2);
        if (blockablePrediction > 0)
        {
            details.Add($"[color={ToHtml(DamageForecastUiSettings.BlockableDetailColor)}][font_size={DetailShieldFontSize}]\U0001F6E1[/font_size] [font_size={DetailFontSize}]{blockablePrediction}[/font_size][/color]");
        }

        if (directHpLossPrediction > 0)
        {
            details.Add($"[color={ToHtml(DamageForecastUiSettings.DirectHpLossDetailColor)}][font_size={DetailHeartFontSize}]\u2665[/font_size] [font_size={DetailFontSize}]{directHpLossPrediction}[/font_size][/color]");
        }

        return string.Join("   ", details);
    }

    private static float GetMainWidth() => 72f;

    private static float GetMainHeight() => 34f;

    private static Vector2 GetHealthBarCenter(Control healthBar, Vector2 size)
    {
        return healthBar.Position + (size * 0.5f);
    }

    private static Vector2 GetMainTextSize(Label label)
    {
        if (string.IsNullOrEmpty(label.Text))
        {
            return new Vector2(GetMainWidth(), GetMainHeight());
        }

        var font = label.GetThemeFont("font");
        var fontSize = label.GetThemeFontSize("font_size");
        var textSize = font.GetStringSize(label.Text, fontSize: fontSize);
        return new Vector2(GetMainWidth(), MathF.Max(1f, textSize.Y));
    }

    private static ForecastHudProjection Project(ForecastHudSnapshot snapshot)
    {
        return ForecastHudProjectionPolicy.Project(
            snapshot,
            DamageForecastUiSettings.DamageDisplayMode,
            DamageForecastUiSettings.IncomingDamagePlacement);
    }

    private static string GetControlText(Control control)
    {
        return control switch
        {
            Label label => label.Text,
            RichTextLabel richTextLabel => richTextLabel.Text,
            _ => string.Empty
        };
    }

    private static float GetRightEdge(
        bool expectedVisible,
        Control expectedLabel,
        bool incomingVisible,
        Control incomingLabel)
    {
        var rightEdge = 0f;
        if (expectedVisible)
        {
            rightEdge = MathF.Max(rightEdge, expectedLabel.Position.X + expectedLabel.Size.X);
        }

        if (incomingVisible)
        {
            rightEdge = MathF.Max(rightEdge, incomingLabel.Position.X + incomingLabel.Size.X);
        }

        return rightEdge;
    }

    private static float GetDetailWidth() => 240f;

    private static float GetDetailHeight() => 28f;

    private static string ToHtml(Color color)
    {
        var r = ToByte(color.R);
        var g = ToByte(color.G);
        var b = ToByte(color.B);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static int ToByte(float value)
    {
        return (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
    }
}

internal static class HudNumericAlignmentPolicy
{
    public static HorizontalAlignment Resolve(HudPlacementPreset preset) =>
        preset == HudPlacementPreset.EndTurnButtonAbove
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
}
