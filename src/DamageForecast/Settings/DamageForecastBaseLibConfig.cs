using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using System.Reflection;
using DamageForecast.Patches;
using DamageForecast.UI;

namespace DamageForecast.Settings;

internal sealed class DamageForecastBaseLibConfig : SimpleModConfig
{
    private static readonly string[] PropertyOrder = HudPlacementConfigSchema.V1PropertyOrder;
    private static readonly string[] LocalizationPropertyOrder = HudPlacementConfigSchema.V2PropertyOrder;

    private static DamageForecastBaseLibConfig? _activeConfig;
    private static DamageForecastConfigLanguage _configLanguage;
    private static DamageDisplayMode _damageDisplayMode = DamageDisplayMode.ExpectedHpLossOnly;
    private static IncomingDamagePlacement _incomingDamagePlacement = IncomingDamagePlacement.RightOfExpectedHpLoss;
    private static DamageForecastHudAnchor _hudAnchorPreset = DamageForecastHudAnchor.HealthBarRight;
    private static HudPlacementPreset _expectedHpLossPlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _incomingDamagePlacementPreset = HudPlacementPreset.HealthBarRight;
    private static HudPlacementPreset _detailsPlacementPreset = HudPlacementPreset.HealthBarRight;

    private readonly Dictionary<string, Control> _settingRows = [];
    private readonly Dictionary<string, Control> _settingControls = [];
    private readonly Dictionary<string, Control> _sectionHeaders = [];
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
                SetFirstText(settingControl, DamageForecastConfigText.EnumValue(propertyName, GetPropertyValue(propertyName), ConfigLanguage));
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

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item is null)
            {
                continue;
            }

            var itemType = item.GetType();
            var value = itemType.GetProperty("Value")?.GetValue(item);
            var onSet = itemType.GetProperty("OnSet")?.GetValue(item) as Action;
            var text = DamageForecastConfigText.EnumValue(propertyName, value, ConfigLanguage);
            var replacement = Activator.CreateInstance(itemType, text, value!, onSet!);
            if (replacement is not null)
            {
                items[i] = replacement;
            }
        }
    }

    private static void ApplyDropdownPopupText(Node node)
    {
        if (TryReadText(node, out var text) && TryGetDropdownText(text, out var itemText))
        {
            SetDropdownText(node, itemText);
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                ApplyDropdownPopupText(childNode);
            }
        }
    }

    private static bool TryReadText(Node node, out string text)
    {
        switch (node)
        {
            case Label label:
                text = label.Text;
                return true;
            case RichTextLabel richTextLabel:
                text = richTextLabel.Text;
                return true;
            case NDropdownItem dropdownItem:
                text = dropdownItem.Text;
                return true;
            default:
                var property = node.GetType().GetProperty(
                    "Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.PropertyType == typeof(string)
                    && property.GetValue(node) is string reflectedText)
                {
                    text = reflectedText;
                    return true;
                }

                text = string.Empty;
                return false;
        }
    }

    private static void SetDropdownText(Node node, string text)
    {
        TrySetReflectedText(node, text);
        SetPrivateTextField(node, "_label", text);
        SetPrivateTextField(node, "_richLabel", text);
    }

    private static bool TrySetReflectedText(Node node, string text)
    {
        switch (node)
        {
            case Label label:
                label.Text = text;
                return true;
            case RichTextLabel richTextLabel:
                richTextLabel.Text = text;
                return true;
            case NDropdownItem dropdownItem:
                dropdownItem.Text = text;
                return true;
            default:
                var property = node.GetType().GetProperty(
                    "Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.PropertyType == typeof(string) && property.CanWrite)
                {
                    property.SetValue(node, text);
                    return true;
                }

                return false;
        }
    }

    private static void SetPrivateTextField(Node node, string fieldName, string text)
    {
        var field = node.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(node) is Node textNode)
        {
            TrySetReflectedText(textNode, text);
        }
    }

    private static bool TryGetDropdownText(string text, out string localized)
    {
        localized = text switch
        {
            nameof(DamageForecastConfigLanguage.SimplifiedChinese) => "简体中文",
            nameof(DamageDisplayMode.ExpectedHpLossOnly) => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.ExpectedHpLossOnly, ConfigLanguage),
            nameof(DamageDisplayMode.IncomingDamageOnly) => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.IncomingDamageOnly, ConfigLanguage),
            nameof(DamageDisplayMode.Both) => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.Both, ConfigLanguage),
            nameof(IncomingDamagePlacement.LeftOfExpectedHpLoss) => DamageForecastConfigText.EnumValue(nameof(IncomingDamagePlacement), IncomingDamagePlacement.LeftOfExpectedHpLoss, ConfigLanguage),
            nameof(IncomingDamagePlacement.RightOfExpectedHpLoss) => DamageForecastConfigText.EnumValue(nameof(IncomingDamagePlacement), IncomingDamagePlacement.RightOfExpectedHpLoss, ConfigLanguage),
            nameof(DamageForecastHudAnchor.HealthBarRight) => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarRight, ConfigLanguage),
            nameof(DamageForecastHudAnchor.HealthBarLeft) => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarLeft, ConfigLanguage),
            nameof(DamageForecastHudAnchor.HealthBarAbove) => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarAbove, ConfigLanguage),
            nameof(DamageForecastHudAnchor.HealthBarBelow) => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarBelow, ConfigLanguage),
            nameof(HudPlacementPreset.EndTurnButtonAbove) => DamageForecastConfigText.EnumValue(nameof(ExpectedHpLossPlacementPreset), HudPlacementPreset.EndTurnButtonAbove, ConfigLanguage),
            "Expected HP Loss (Default)" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.ExpectedHpLossOnly, ConfigLanguage),
            "Incoming Damage" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.IncomingDamageOnly, ConfigLanguage),
            "Show Both" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(DamageDisplayMode), DamageDisplayMode.Both, ConfigLanguage),
            "Left of Expected Loss" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(IncomingDamagePlacement), IncomingDamagePlacement.LeftOfExpectedHpLoss, ConfigLanguage),
            "Right of Expected Loss" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(IncomingDamagePlacement), IncomingDamagePlacement.RightOfExpectedHpLoss, ConfigLanguage),
            "Right of Health Bar" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarRight, ConfigLanguage),
            "Left of Health Bar" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarLeft, ConfigLanguage),
            "Above Health Bar" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarAbove, ConfigLanguage),
            "Below Health Bar" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(HudAnchorPreset), DamageForecastHudAnchor.HealthBarBelow, ConfigLanguage),
            "Above End Turn Button" when ConfigLanguage == DamageForecastConfigLanguage.SimplifiedChinese => DamageForecastConfigText.EnumValue(nameof(ExpectedHpLossPlacementPreset), HudPlacementPreset.EndTurnButtonAbove, ConfigLanguage),
            _ => text
        };
        return !string.Equals(localized, text, StringComparison.Ordinal);
    }

    private sealed partial class DropdownTextUpdater : Node
    {
        public override void _Ready()
        {
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            if (GetTree()?.Root is { } root)
            {
                ApplyDropdownPopupText(root);
            }
        }
    }
}
