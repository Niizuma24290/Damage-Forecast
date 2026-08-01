using System.Reflection;
using BaseLib.Config.UI;
using DamageForecast.Settings;
using DamageForecast.UI;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

internal static class BaseLibDropdownLocalizationContractCases
{
    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "BLP3-001",
            "BaseLibDropdownLocalization",
            "BaseLibDropdown.Compatibility_SourceAndLiveItemMembersAvailable",
            assert =>
            {
                var itemsField = typeof(NConfigDropdown).GetField(
                    "_items",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var dataField = typeof(NConfigDropdownItem).GetField(
                    nameof(NConfigDropdownItem.Data),
                    BindingFlags.Instance | BindingFlags.Public);
                var textProperty = typeof(NDropdownItem).GetProperty(
                    nameof(NDropdownItem.Text),
                    BindingFlags.Instance | BindingFlags.Public);
                var itemDataConstructor = typeof(NConfigDropdownItem.ItemData).GetConstructor(
                    [typeof(string), typeof(object), typeof(Action)]);

                assert.True(
                    itemsField is not null
                    && dataField?.FieldType == typeof(NConfigDropdownItem.ItemData)
                    && textProperty?.CanWrite == true
                    && itemDataConstructor is not null,
                    "private source list plus public Data/Text synchronization seam available",
                    $"items={itemsField is not null}; data={dataField?.FieldType}; "
                    + $"textWritable={textProperty?.CanWrite}; ctor={itemDataConstructor is not null}");
            });

        yield return new(
            "BLP3-002",
            "BaseLibDropdownLocalization",
            "BaseLibDropdown.LiveItemMatching_UsesValueNotDisplayIndex",
            assert =>
            {
                Action expectedCallback = () => { };
                var replacements = new[]
                {
                    new NConfigDropdownItem.ItemData(
                        "同时显示",
                        DamageDisplayMode.Both,
                        () => { }),
                    new NConfigDropdownItem.ItemData(
                        "预计掉血（默认）",
                        DamageDisplayMode.ExpectedHpLossOnly,
                        expectedCallback),
                    new NConfigDropdownItem.ItemData(
                        "来袭总伤害",
                        DamageDisplayMode.IncomingDamageOnly,
                        () => { })
                };

                var actual = DamageForecastBaseLibConfig.FindDropdownReplacement(
                    replacements,
                    DamageDisplayMode.ExpectedHpLossOnly);

                assert.True(
                    actual?.Text == "预计掉血（默认）"
                    && Equals(actual.Value, DamageDisplayMode.ExpectedHpLossOnly)
                    && ReferenceEquals(actual.OnSet, expectedCallback),
                    "matching is by enum Value and preserves the selected replacement callback",
                    actual is null
                        ? "no replacement"
                        : $"text={actual.Text}; value={actual.Value}; callbackPreserved={ReferenceEquals(actual.OnSet, expectedCallback)}");
            });

        yield return new(
            "BLP3-003",
            "BaseLibDropdownLocalization",
            "BaseLibDropdown.AllConfiguredEnumOptions_HaveFriendlyBilingualText",
            assert =>
            {
                var options = ConfiguredDropdownOptions().ToArray();
                var failures = options
                    .Where(option =>
                    {
                        var english = DamageForecastConfigText.EnumValue(
                            option.PropertyName,
                            option.Value,
                            DamageForecastConfigLanguage.English);
                        var chinese = DamageForecastConfigText.EnumValue(
                            option.PropertyName,
                            option.Value,
                            DamageForecastConfigLanguage.SimplifiedChinese);
                        return string.IsNullOrWhiteSpace(english)
                            || string.IsNullOrWhiteSpace(chinese)
                            || string.Equals(english, option.Value.ToString(), StringComparison.Ordinal)
                            || string.Equals(chinese, option.Value.ToString(), StringComparison.Ordinal);
                    })
                    .Select(option => $"{option.PropertyName}:{option.Value}")
                    .ToArray();

                var languageEnglish = DamageForecastConfigText.EnumValue(
                    nameof(DamageForecastBaseLibConfig.ConfigLanguage),
                    DamageForecastConfigLanguage.English,
                    DamageForecastConfigLanguage.SimplifiedChinese);
                var languageChinese = DamageForecastConfigText.EnumValue(
                    nameof(DamageForecastBaseLibConfig.ConfigLanguage),
                    DamageForecastConfigLanguage.SimplifiedChinese,
                    DamageForecastConfigLanguage.English);

                assert.True(
                    failures.Length == 0
                    && languageEnglish == "English"
                    && languageChinese == "简体中文",
                    "all configured enum dropdown values have friendly English and Simplified Chinese labels",
                    failures.Length == 0
                        ? $"optionCount={options.Length}; language={languageEnglish}/{languageChinese}"
                        : string.Join(',', failures));
            });

        yield return new(
            "BLP3-004",
            "BaseLibDropdownLocalization",
            "BaseLibDropdown.DeadGlobalPopupScanner_IsAbsent",
            assert =>
            {
                var owner = typeof(DamageForecastBaseLibConfig);
                var updater = owner.GetNestedType("DropdownTextUpdater", BindingFlags.NonPublic);
                var scanner = owner.GetMethod(
                    "ApplyDropdownPopupText",
                    BindingFlags.Static | BindingFlags.NonPublic);

                assert.True(
                    updater is null && scanner is null,
                    "no per-frame root-tree popup scanner remains",
                    $"updater={updater?.FullName ?? "absent"}; scanner={scanner?.Name ?? "absent"}");
            });

        yield return new(
            "BLP4I-001",
            "BaseLibDropdownTypography",
            "BaseLibDropdown.SafeClosedFont_TargetsOnlyTwoKnownLongEnglishValues",
            assert =>
            {
                var targetProperties = new[]
                {
                    nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset),
                    nameof(DamageForecastBaseLibConfig.IncomingDamagePlacementPreset),
                    nameof(DamageForecastBaseLibConfig.DetailsPlacementPreset)
                };
                var allEndTurnTargetsFit = targetProperties.All(propertyName =>
                    DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                        propertyName,
                        HudPlacementPreset.EndTurnButtonAbove,
                        DamageForecastConfigLanguage.English));
                var rightExpectedFits =
                    DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                        nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement),
                        IncomingDamagePlacement.RightOfExpectedHpLoss,
                        DamageForecastConfigLanguage.English);
                var chineseDefault = !DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                    targetProperties[0],
                    HudPlacementPreset.EndTurnButtonAbove,
                    DamageForecastConfigLanguage.SimplifiedChinese);
                var shorterDefault = !DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                    targetProperties[0],
                    HudPlacementPreset.HealthBarRight,
                    DamageForecastConfigLanguage.English);
                var leftExpectedDefault = !DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                    nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement),
                    IncomingDamagePlacement.LeftOfExpectedHpLoss,
                    DamageForecastConfigLanguage.English);
                var unrelatedDefault = !DamageForecastBaseLibConfig.ShouldFitEnglishClosedDropdownFont(
                    nameof(DamageForecastBaseLibConfig.DamageDisplayMode),
                    HudPlacementPreset.EndTurnButtonAbove,
                    DamageForecastConfigLanguage.English);

                assert.True(
                    allEndTurnTargetsFit
                    && rightExpectedFits
                    && chineseDefault
                    && shorterDefault
                    && leftExpectedDefault
                    && unrelatedDefault,
                    "only the two known long closed English values use arrow-safe fitting",
                    $"endTurn={allEndTurnTargetsFit}; rightExpected={rightExpectedFits}; "
                    + $"chinese={chineseDefault}; short={shorterDefault}; "
                    + $"leftExpected={leftExpectedDefault}; unrelated={unrelatedDefault}");
            });

        yield return new(
            "BLP4I-002",
            "BaseLibDropdownTypography",
            "BaseLibDropdown.SafeClosedFont_ReservesSymmetricArrowClearance",
            assert =>
            {
                var nativeWidth = DamageForecastBaseLibConfig.ResolveClosedDropdownSafeTextWidth(324);
                var scaledWidth = DamageForecastBaseLibConfig.ResolveClosedDropdownSafeTextWidth(432);
                var tooNarrow = DamageForecastBaseLibConfig.ResolveClosedDropdownSafeTextWidth(60);
                assert.True(
                    DamageForecastBaseLibConfig.ClosedDropdownArrowSafeInset == 42
                    && DamageForecastBaseLibConfig.ClosedDropdownFallbackWidth == 324
                    && nativeWidth == 240
                    && scaledWidth == 348
                    && tooNarrow == 0,
                    "centered text reserves the same arrow-safe inset on both sides",
                    $"inset={DamageForecastBaseLibConfig.ClosedDropdownArrowSafeInset}; "
                    + $"safe={nativeWidth}/{scaledWidth}/{tooNarrow}");
            });

        yield return new(
            "BLP4I-003",
            "BaseLibDropdownTypography",
            "BaseLibDropdown.SafeClosedFont_MeasuresFromBaselineWithoutEnlarging",
            assert =>
            {
                var fitted = DamageForecastBaseLibConfig.ResolveSafeClosedDropdownFontSize(
                    32,
                    240,
                    fontSize => fontSize * 9);
                var alreadyFits = DamageForecastBaseLibConfig.ResolveSafeClosedDropdownFontSize(
                    32,
                    300,
                    fontSize => fontSize * 9);
                var zeroBaseline = DamageForecastBaseLibConfig.ResolveSafeClosedDropdownFontSize(
                    0,
                    240,
                    fontSize => fontSize * 9);
                var noSpace = DamageForecastBaseLibConfig.ResolveSafeClosedDropdownFontSize(
                    32,
                    0,
                    fontSize => fontSize * 9);
                assert.True(
                    fitted == 26
                    && alreadyFits == 32
                    && zeroBaseline == 0
                    && noSpace == 1,
                    "actual measured width selects the largest fitting size at or below BaseLib's baseline",
                    $"fitted={fitted}; already={alreadyFits}; zero={zeroBaseline}; noSpace={noSpace}");
            });

        yield return new(
            "BLP4I-004",
            "BaseLibDropdownTypography",
            "BaseLibDropdown.SafeClosedFont_PreservesRefreshableBaselineMetadata",
            assert =>
            {
                var baselineType = typeof(DamageForecastBaseLibConfig).GetNestedType(
                    "DropdownFontBaseline",
                    BindingFlags.NonPublic);
                var hadOverride = baselineType?.GetProperty("HadLocalOverride");
                var fontSize = baselineType?.GetProperty("FontSize");
                var fontSizeName = baselineType?.GetProperty("FontSizeName");
                var expectedOverride = baselineType?.GetProperty("ExpectedHadLocalOverride");
                var expectedFontSize = baselineType?.GetProperty("ExpectedFontSize");
                var refresh = baselineType?.GetMethod("Refresh");
                assert.True(
                    baselineType is not null
                    && hadOverride?.PropertyType == typeof(bool)
                    && fontSize?.PropertyType == typeof(int)
                    && fontSizeName?.PropertyType == typeof(string)
                    && expectedOverride?.PropertyType == typeof(bool)
                    && expectedFontSize?.PropertyType == typeof(int)
                    && refresh is not null,
                    "baseline distinguishes our last applied state from a later BaseLib auto-size refresh",
                    $"type={baselineType?.Name ?? "missing"}; had={hadOverride?.PropertyType}; "
                    + $"size={fontSize?.PropertyType}; expected={expectedOverride?.PropertyType}/"
                    + $"{expectedFontSize?.PropertyType}; refresh={refresh?.Name ?? "missing"}");
            });

    }

    private static IEnumerable<(string PropertyName, object Value)> ConfiguredDropdownOptions()
    {
        foreach (var value in Enum.GetValues<DamageDisplayMode>())
        {
            yield return (nameof(DamageForecastBaseLibConfig.DamageDisplayMode), value);
        }

        foreach (var value in Enum.GetValues<IncomingDamagePlacement>())
        {
            yield return (nameof(DamageForecastBaseLibConfig.IncomingDamagePlacement), value);
        }

        var placementProperties = new[]
        {
            nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset),
            nameof(DamageForecastBaseLibConfig.IncomingDamagePlacementPreset),
            nameof(DamageForecastBaseLibConfig.DetailsPlacementPreset)
        };
        foreach (var propertyName in placementProperties)
        {
            foreach (var value in Enum.GetValues<HudPlacementPreset>())
            {
                yield return (propertyName, value);
            }
        }
    }
}
