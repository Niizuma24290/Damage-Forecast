using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using System.Reflection;
using DamageForecast.Patches;
using DamageForecast.UI;

namespace DamageForecast.Settings;

internal sealed class DamageForecastBaseLibConfig : SimpleModConfig
{
    internal const float ClosedDropdownArrowSafeInset = 42f;
    internal const float ClosedDropdownFallbackWidth = 324f;
    internal const float LongClosedDropdownFontScale = 0.92f;

    private static readonly string[] PropertyOrder = HudPlacementConfigSchema.V1PropertyOrder;
    private static readonly string[] LocalizationPropertyOrder = HudPlacementConfigSchema.V2PropertyOrder;

    private static DamageForecastBaseLibConfig? _activeConfig;
    private static DamageForecastConfigLanguage _configLanguage;
    private static DamageDisplayMode _damageDisplayMode = DamageDisplayMode.ExpectedHpLossOnly;
    private static IncomingDamagePlacement _incomingDamagePlacement = IncomingDamagePlacement.LeftOfExpectedHpLoss;
    private static DamageForecastHudAnchor _hudAnchorPreset = DamageForecastHudAnchor.HealthBarRight;
    private static HudPlacementPreset _expectedHpLossPlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _incomingDamagePlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _detailsPlacementPreset = HudPlacementPreset.HealthBarRight;

    private readonly Dictionary<string, Control> _settingRows = [];
    private readonly Dictionary<string, Control> _settingControls = [];
    private readonly Dictionary<string, Control> _sectionHeaders = [];
    private readonly Dictionary<ulong, DropdownFontBaseline> _dropdownFontBaselines = [];
    private Control? _optionContainer;

    public static DamageForecastConfigLanguage ConfigLanguage
    {
        get => _configLanguage;
        set
        {
            if (_configLanguage == value)
            {
                return;
            }

            _configLanguage = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    [ConfigSection("Display")]
    public static bool EnableDamageForecastHud { get; set; } = true;

    public static bool ShowAdvancedShieldHeartDetails { get; set; } = false;

    public static bool FreezeHudNumbersAfterTurnEnd { get; set; } = true;

    [ConfigSection("IncomingDamage")]
    public static DamageDisplayMode DamageDisplayMode
    {
        get => _damageDisplayMode;
        set
        {
            if (_damageDisplayMode == value)
            {
                return;
            }

            _damageDisplayMode = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    public static IncomingDamagePlacement IncomingDamagePlacement
    {
        get => _incomingDamagePlacement;
        set
        {
            if (_incomingDamagePlacement == value)
            {
                return;
            }

            _incomingDamagePlacement = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    public static bool IncludeCurrentBlockInIncomingDamage { get; set; } = false;

    public static bool IncludePowerBlockInIncomingDamage { get; set; } = false;

    public static bool IncludeRelicBlockInIncomingDamage { get; set; } = false;

    public static bool IncludePowerHpLossModifiersInIncomingDamage { get; set; } = false;

    public static bool IncludeRelicHpLossModifiersInIncomingDamage { get; set; } = false;

    [ConfigSection("Multiplayer")]
    public static bool ShowLocalPlayerHudInMultiplayer { get; set; } = true;

    [ConfigSection("PositionAndAppearance")]
    [ConfigIgnore]
    public static DamageForecastHudAnchor HudAnchorPreset
    {
        get => _hudAnchorPreset;
        set
        {
            if (_hudAnchorPreset == value)
            {
                return;
            }

            _hudAnchorPreset = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    public static HudPlacementPreset ExpectedHpLossPlacementPreset
    {
        get => _expectedHpLossPlacementPreset;
        set
        {
            if (_expectedHpLossPlacementPreset == value)
            {
                return;
            }

            _expectedHpLossPlacementPreset = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    public static HudPlacementPreset IncomingDamagePlacementPreset
    {
        get => _incomingDamagePlacementPreset;
        set
        {
            if (_incomingDamagePlacementPreset == value)
            {
                return;
            }

            _incomingDamagePlacementPreset = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    public static HudPlacementPreset DetailsPlacementPreset
    {
        get => _detailsPlacementPreset;
        set
        {
            if (_detailsPlacementPreset == value)
            {
                return;
            }

            _detailsPlacementPreset = value;
            _activeConfig?.ApplyLocalizedText();
        }
    }

    [ConfigSlider(-320, 320, 1, Format = "{0}px")]
    public static float HorizontalOffset { get; set; } = 0f;

    [ConfigSlider(-240, 240, 1, Format = "{0}px")]
    public static float VerticalOffset { get; set; } = 0f;

    [ConfigColorPicker(EditAlpha = false)]
    public static Color TotalExpectedLossColor { get; set; } = Colors.White;

    [ConfigColorPicker(EditAlpha = false)]
    public static Color ShieldDetailColor { get; set; } = new(0.55f, 0.85f, 1f);

    [ConfigColorPicker(EditAlpha = false)]
    public static Color HeartDetailColor { get; set; } = new(1f, 0.55f, 0.62f);

    public override void SetupConfigUI(Control optionContainer)
    {
        _activeConfig = this;
        _optionContainer = optionContainer;
        _settingRows.Clear();
        _settingControls.Clear();
        _sectionHeaders.Clear();
        _dropdownFontBaselines.Clear();
        ConfigChanged -= OnConfigChanged;
        ConfigChanged += OnConfigChanged;

        AddProperty(optionContainer, nameof(ConfigLanguage));
        AddSection(optionContainer, "Display");
        AddProperty(optionContainer, nameof(EnableDamageForecastHud));
        AddProperty(optionContainer, nameof(ShowAdvancedShieldHeartDetails));
        AddSection(optionContainer, "IncomingDamage");
        AddProperty(optionContainer, nameof(DamageDisplayMode));
        AddProperty(optionContainer, nameof(IncomingDamagePlacement));
        AddProperty(optionContainer, nameof(IncludeCurrentBlockInIncomingDamage));
        AddProperty(optionContainer, nameof(IncludePowerBlockInIncomingDamage));
        AddProperty(optionContainer, nameof(IncludeRelicBlockInIncomingDamage));
        AddProperty(optionContainer, nameof(IncludePowerHpLossModifiersInIncomingDamage));
        AddProperty(optionContainer, nameof(IncludeRelicHpLossModifiersInIncomingDamage));
        AddSection(optionContainer, "Multiplayer");
        AddProperty(optionContainer, nameof(ShowLocalPlayerHudInMultiplayer));
        AddSection(optionContainer, "PositionAndAppearance");
        AddProperty(optionContainer, nameof(ExpectedHpLossPlacementPreset));
        AddProperty(optionContainer, nameof(IncomingDamagePlacementPreset));
        AddProperty(optionContainer, nameof(DetailsPlacementPreset));
        AddProperty(optionContainer, nameof(HorizontalOffset));
        AddProperty(optionContainer, nameof(VerticalOffset));
        optionContainer.AddChild(new DamageForecastHudPreview());
        AddProperty(optionContainer, nameof(TotalExpectedLossColor));
        AddProperty(optionContainer, nameof(ShieldDetailColor));
        AddProperty(optionContainer, nameof(HeartDetailColor));
        AddRestoreDefaultsButton(optionContainer);
        ApplyLocalizedText();
        SetupFocusNeighbors(optionContainer);
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        ApplyLocalizedText();
    }

    private void AddSection(Control optionContainer, string key)
    {
        var header = CreateSectionHeader(key, false);
        optionContainer.AddChild(header);
        _sectionHeaders[key] = header;
    }

    private void AddProperty(Control optionContainer, string propertyName)
    {
        var property = typeof(DamageForecastBaseLibConfig).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing config property: {propertyName}");
        var row = GenerateOptionFromProperty(property);
        optionContainer.AddChild(row);
        _settingRows[propertyName] = row;
        _settingControls[propertyName] = row.SettingControl;
    }

    private void ApplyLocalizedText()
    {
        DamageForecastBaseLibTitlePatch.RefreshPageTitle(_optionContainer, ConfigLanguage);

        foreach (var (key, header) in _sectionHeaders)
        {
            SetFirstText(header, DamageForecastConfigText.Section(key, ConfigLanguage));
        }

        foreach (var propertyName in LocalizationPropertyOrder)
        {
            if (_settingRows.TryGetValue(propertyName, out var row))
            {
                SetFirstTextOutside(row, _settingControls[propertyName], DamageForecastConfigText.Setting(propertyName, ConfigLanguage));
            }

            if (_settingControls.TryGetValue(propertyName, out var settingControl)
                && IsDropdownProperty(propertyName))
            {
                ApplyDropdownItemSourceText(settingControl, propertyName);
                var value = GetPropertyValue(propertyName);
                var currentValueText = SetFirstTextAndGetControl(
                    settingControl,
                    DamageForecastConfigText.EnumValue(propertyName, value, ConfigLanguage));
                if (currentValueText is not null)
                {
                    ApplyClosedDropdownFont(currentValueText, propertyName, value);
                }
            }
        }
    }

    private static object? GetPropertyValue(string propertyName)
    {
        return typeof(DamageForecastBaseLibConfig).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    private static bool IsDropdownProperty(string propertyName)
    {
        var property = typeof(DamageForecastBaseLibConfig).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static);
        return property?.PropertyType.IsEnum == true;
    }

    private static void SetFirstText(Node node, string text)
    {
        if (TrySetText(node, text))
        {
            return;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                SetFirstText(childNode, text);
                if (ContainsTextControl(childNode))
                {
                    return;
                }
            }
        }
    }

    private static Control? SetFirstTextAndGetControl(Node node, string text)
    {
        switch (node)
        {
            case Label label:
                label.Text = text;
                return label;
            case RichTextLabel richTextLabel:
                richTextLabel.Text = text;
                return richTextLabel;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode
                && SetFirstTextAndGetControl(childNode, text) is { } textControl)
            {
                return textControl;
            }
        }

        return null;
    }

    private static void SetFirstTextOutside(Node node, Node excludedSubtree, string text)
    {
        if (ReferenceEquals(node, excludedSubtree))
        {
            return;
        }

        if (TrySetText(node, text))
        {
            return;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode && !ReferenceEquals(childNode, excludedSubtree))
            {
                SetFirstTextOutside(childNode, excludedSubtree, text);
                if (ContainsTextControlOutside(childNode, excludedSubtree))
                {
                    return;
                }
            }
        }
    }

    private static bool ContainsTextControl(Node node)
    {
        if (node is Label or RichTextLabel)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode && ContainsTextControl(childNode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTextControlOutside(Node node, Node excludedSubtree)
    {
        if (ReferenceEquals(node, excludedSubtree))
        {
            return false;
        }

        if (node is Label or RichTextLabel)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode && ContainsTextControlOutside(childNode, excludedSubtree))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySetText(Node node, string text)
    {
        switch (node)
        {
            case Label label:
                label.Text = text;
                return true;
            case RichTextLabel richTextLabel:
                richTextLabel.Text = text;
                return true;
            default:
                return false;
        }
    }

    private void ApplyClosedDropdownFont(Control textControl, string propertyName, object? value)
    {
        if (!IsClosedDropdownFontManagedProperty(propertyName))
        {
            return;
        }

        var fontSizeName = textControl is RichTextLabel ? "normal_font_size" : "font_size";
        var instanceId = textControl.GetInstanceId();
        var hasLocalOverride = textControl.HasThemeFontSizeOverride(fontSizeName);
        var currentFontSize = textControl.GetThemeFontSize(fontSizeName);
        if (!_dropdownFontBaselines.TryGetValue(instanceId, out var baseline))
        {
            baseline = new DropdownFontBaseline(
                fontSizeName,
                hasLocalOverride,
                currentFontSize);
            _dropdownFontBaselines[instanceId] = baseline;
        }
        else if (baseline.ExpectedHadLocalOverride != hasLocalOverride
            || baseline.ExpectedFontSize != currentFontSize)
        {
            // BaseLib may re-run its own auto-size after the selected value changes.
            // Treat that externally supplied state as the new baseline before fitting.
            baseline.Refresh(hasLocalOverride, currentFontSize);
        }

        if (ShouldFitEnglishClosedDropdownFont(
            propertyName,
            value,
            ConfigLanguage))
        {
            var fontName = textControl is RichTextLabel ? "normal_font" : "font";
            var font = textControl.GetThemeFont(fontName);
            var text = textControl switch
            {
                Label label => label.Text,
                RichTextLabel richTextLabel => richTextLabel.Text,
                _ => string.Empty
            };
            var safeTextWidth = ResolveClosedDropdownSafeTextWidth(
                ResolveClosedDropdownWidth(textControl));
            var maximumFontSize = ResolveClosedDropdownFontSizeLimit(
                baseline.FontSize,
                propertyName,
                value,
                ConfigLanguage);
            var fittedFontSize = ResolveSafeClosedDropdownFontSize(
                maximumFontSize,
                safeTextWidth,
                candidateFontSize => font.GetStringSize(
                    text,
                    HorizontalAlignment.Left,
                    -1,
                    candidateFontSize).X);
            if (fittedFontSize > 0)
            {
                textControl.AddThemeFontSizeOverride(baseline.FontSizeName, fittedFontSize);
                baseline.MarkExpected(true, fittedFontSize);
            }

            return;
        }

        if (baseline.HadLocalOverride)
        {
            textControl.AddThemeFontSizeOverride(baseline.FontSizeName, baseline.FontSize);
            baseline.MarkExpected(true, baseline.FontSize);
        }
        else
        {
            textControl.RemoveThemeFontSizeOverride(baseline.FontSizeName);
            baseline.MarkExpected(false, textControl.GetThemeFontSize(baseline.FontSizeName));
        }
    }

    private static float ResolveClosedDropdownWidth(Control textControl)
    {
        for (Node? current = textControl; current is not null; current = current.GetParent())
        {
            if (current is not NConfigDropdown dropdown)
            {
                continue;
            }

            if (dropdown.Size.X > 0)
            {
                return dropdown.Size.X;
            }

            if (dropdown.CustomMinimumSize.X > 0)
            {
                return dropdown.CustomMinimumSize.X;
            }

            break;
        }

        return ClosedDropdownFallbackWidth;
    }

    private static bool IsPlacementPresetProperty(string propertyName) =>
        propertyName is nameof(ExpectedHpLossPlacementPreset)
            or nameof(IncomingDamagePlacementPreset)
            or nameof(DetailsPlacementPreset);

    private static bool IsClosedDropdownFontManagedProperty(string propertyName) =>
        IsPlacementPresetProperty(propertyName)
        || propertyName is nameof(IncomingDamagePlacement)
            or nameof(DamageDisplayMode);

    private static void ApplyDropdownItemSourceText(Node node, string propertyName)
    {
        if (node is NConfigDropdown dropdown)
        {
            RewriteDropdownItems(dropdown, propertyName);
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                ApplyDropdownItemSourceText(childNode, propertyName);
            }
        }
    }

    private static void RewriteDropdownItems(NConfigDropdown dropdown, string propertyName)
    {
        var itemsField = typeof(NConfigDropdown).GetField(
            "_items",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (itemsField?.GetValue(dropdown) is not System.Collections.IList items)
        {
            return;
        }

        var replacements = new List<NConfigDropdownItem.ItemData>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not NConfigDropdownItem.ItemData item)
            {
                return;
            }

            var text = DamageForecastConfigText.EnumValue(propertyName, item.Value, ConfigLanguage);
            replacements.Add(new NConfigDropdownItem.ItemData(text, item.Value, item.OnSet));
        }

        for (var i = 0; i < replacements.Count; i++)
        {
            items[i] = replacements[i];
        }

        RewriteCreatedDropdownItems(dropdown, replacements);
    }

    private static void RewriteCreatedDropdownItems(
        Node node,
        IReadOnlyList<NConfigDropdownItem.ItemData> replacements)
    {
        if (node is NConfigDropdownItem dropdownItem)
        {
            var replacement = FindDropdownReplacement(replacements, dropdownItem.Data.Value);
            if (replacement is not null)
            {
                dropdownItem.Data = replacement;
                dropdownItem.Text = replacement.Text;
            }
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                RewriteCreatedDropdownItems(childNode, replacements);
            }
        }
    }

    internal static NConfigDropdownItem.ItemData? FindDropdownReplacement(
        IReadOnlyList<NConfigDropdownItem.ItemData> replacements,
        object? value) =>
        replacements.FirstOrDefault(item => Equals(item.Value, value));

    internal static bool ShouldFitEnglishClosedDropdownFont(
        string propertyName,
        object? value,
        DamageForecastConfigLanguage language)
    {
        if (language != DamageForecastConfigLanguage.English)
        {
            return false;
        }

        if (ShouldScaleEnglishClosedDropdownFont(
            propertyName,
            value,
            language))
        {
            return true;
        }

        if (value is HudPlacementPreset.EndTurnButtonAbove
            && IsPlacementPresetProperty(propertyName))
        {
            return true;
        }

        return propertyName == nameof(IncomingDamagePlacement)
            && value is IncomingDamagePlacement.RightOfExpectedHpLoss;
    }

    internal static bool ShouldScaleEnglishClosedDropdownFont(
        string propertyName,
        object? value,
        DamageForecastConfigLanguage language)
    {
        if (language != DamageForecastConfigLanguage.English)
        {
            return false;
        }

        if (propertyName == nameof(DamageDisplayMode)
            && value is DamageDisplayMode.ExpectedHpLossOnly)
        {
            return true;
        }

        if (propertyName == nameof(IncomingDamagePlacement)
            && value is IncomingDamagePlacement.LeftOfExpectedHpLoss)
        {
            return true;
        }

        return IsPlacementPresetProperty(propertyName)
            && value is HudPlacementPreset.HealthBarRight;
    }

    internal static int ResolveClosedDropdownFontSizeLimit(
        int baselineFontSize,
        string propertyName,
        object? value,
        DamageForecastConfigLanguage language)
    {
        if (baselineFontSize <= 0
            || !ShouldScaleEnglishClosedDropdownFont(propertyName, value, language))
        {
            return baselineFontSize;
        }

        return Math.Max(
            1,
            (int)Math.Round(
                baselineFontSize * LongClosedDropdownFontScale,
                MidpointRounding.AwayFromZero));
    }

    internal static float ResolveClosedDropdownSafeTextWidth(float dropdownWidth) =>
        Math.Max(0, dropdownWidth - (2 * ClosedDropdownArrowSafeInset));

    internal static int ResolveSafeClosedDropdownFontSize(
        int baselineFontSize,
        float safeTextWidth,
        Func<int, float> measureTextWidth)
    {
        if (baselineFontSize <= 0)
        {
            return baselineFontSize;
        }

        if (safeTextWidth <= 0)
        {
            return 1;
        }

        for (var candidateFontSize = baselineFontSize; candidateFontSize >= 1; candidateFontSize--)
        {
            var measuredWidth = measureTextWidth(candidateFontSize);
            if (float.IsFinite(measuredWidth) && measuredWidth <= safeTextWidth)
            {
                return candidateFontSize;
            }
        }

        return 1;
    }

    private sealed class DropdownFontBaseline(
        string fontSizeName,
        bool hadLocalOverride,
        int fontSize)
    {
        public string FontSizeName { get; } = fontSizeName;
        public bool HadLocalOverride { get; private set; } = hadLocalOverride;
        public int FontSize { get; private set; } = fontSize;
        public bool ExpectedHadLocalOverride { get; private set; } = hadLocalOverride;
        public int ExpectedFontSize { get; private set; } = fontSize;

        public void Refresh(bool hasLocalOverride, int currentFontSize)
        {
            HadLocalOverride = hasLocalOverride;
            FontSize = currentFontSize;
            MarkExpected(hasLocalOverride, currentFontSize);
        }

        public void MarkExpected(bool hasLocalOverride, int currentFontSize)
        {
            ExpectedHadLocalOverride = hasLocalOverride;
            ExpectedFontSize = currentFontSize;
        }
    }
}
