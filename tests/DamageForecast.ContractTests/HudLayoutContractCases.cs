using System.Reflection;
using DamageForecast.Settings;
using DamageForecast.UI;

internal static class HudLayoutContractCases
{
    private static readonly HudLayoutRect Bounds = new(0f, 0f, 200f, 120f);
    private static readonly HudLayoutRect HealthBar = new(40f, 40f, 20f, 20f);

    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "HL-001",
            "HudLayout",
            "HudLayout.V1PresetSet_IsExactAndFinite",
            assert =>
            {
                var names = Enum.GetNames<HudPlacementPreset>();
                assert.True(
                    names.SequenceEqual(
                    [
                        "HealthBarRight",
                        "HealthBarLeft",
                        "HealthBarAbove",
                        "HealthBarBelow",
                        "EndTurnButtonAbove"
                    ]),
                    "exact five-value V1 preset set",
                    string.Join(',', names));
            });

        yield return new(
            "HL-002",
            "HudLayout",
            "HudLayout.HealthBarRight_UsesAnchorLocalRect",
            assert =>
            {
                var actual = Layout(
                    HudPlacementPreset.HealthBarRight,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)]);
                assert.Equal(
                    new HudLayoutRect(82f, 40f, 30f, 20f),
                    actual.RectFor(HudLayoutContent.ExpectedHpLoss));
            });

        yield return new(
            "HL-003",
            "HudLayout",
            "HudLayout.SamePreset_IncomingLeftOrderIsDeterministic",
            assert =>
            {
                var actual = Layout(
                    HudPlacementPreset.HealthBarRight,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 20f, 20f)
                    ],
                    IncomingDamagePlacement.LeftOfExpectedHpLoss);
                var incoming = actual.RectFor(HudLayoutContent.IncomingDamage);
                var expected = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                assert.True(
                    incoming.Right + 8f == expected.Left,
                    "incoming, 8-unit gap, expected",
                    $"incoming={incoming}; expected={expected}");
            });

        yield return new(
            "HL-004",
            "HudLayout",
            "HudLayout.SamePreset_IncomingRightOrderIsDeterministic",
            assert =>
            {
                var actual = Layout(
                    HudPlacementPreset.HealthBarRight,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 20f, 20f)
                    ],
                    IncomingDamagePlacement.RightOfExpectedHpLoss);
                var incoming = actual.RectFor(HudLayoutContent.IncomingDamage);
                var expected = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                assert.True(
                    expected.Right + 8f == incoming.Left,
                    "expected, 8-unit gap, incoming",
                    $"expected={expected}; incoming={incoming}");
            });

        yield return new(
            "HL-005",
            "HudLayout",
            "HudLayout.DetailsWrapToSecondRowBeforeBeingHidden",
            assert =>
            {
                var actual = Layout(
                    HudPlacementPreset.HealthBarRight,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 30f, 20f),
                        Item(HudLayoutContent.Details, 50f, 10f)
                    ]);
                var expected = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                var details = actual.RectFor(HudLayoutContent.Details);
                assert.True(
                    !actual.DetailsHidden && details.Top > expected.Bottom,
                    "details visible on second row",
                    $"detailsHidden={actual.DetailsHidden}; expected={expected}; details={details}");
            });

        yield return new(
            "HL-006",
            "HudLayout",
            "HudLayout.ExtremeSpace_PreservesNumbersAndHidesDetails",
            assert =>
            {
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    HealthBar,
                    new HudLayoutRect(0f, 0f, 150f, 120f),
                    HudPlacementPreset.HealthBarRight,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 30f, 20f),
                        Item(HudLayoutContent.Details, 100f, 10f)
                    ],
                    IncomingDamagePlacement.RightOfExpectedHpLoss));
                assert.True(
                    actual.DetailsHidden
                    && actual.Contains(HudLayoutContent.ExpectedHpLoss)
                    && actual.Contains(HudLayoutContent.IncomingDamage)
                    && !actual.Contains(HudLayoutContent.Details),
                    "both numeric values retained and details hidden",
                    string.Join(',', actual.Placements.Select(item => item.Content)));
            });

        yield return new(
            "HL-007",
            "HudLayout",
            "HudLayout.HorizontalAnchor_ClampsOnlyTangentAxis",
            assert =>
            {
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    new HudLayoutRect(40f, 110f, 20f, 20f),
                    Bounds,
                    HudPlacementPreset.HealthBarRight,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)],
                    IncomingDamagePlacement.RightOfExpectedHpLoss));
                var rect = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                assert.True(
                    rect.X == 82f && rect.Bottom == Bounds.Bottom,
                    "normal X preserved; tangent Y clamped",
                    rect.ToString());
            });

        yield return new(
            "HL-008",
            "HudLayout",
            "HudLayout.VerticalAnchor_ClampsOnlyTangentAxis",
            assert =>
            {
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    new HudLayoutRect(190f, 50f, 20f, 20f),
                    Bounds,
                    HudPlacementPreset.HealthBarAbove,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)],
                    IncomingDamagePlacement.RightOfExpectedHpLoss));
                var rect = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                assert.True(
                    rect.Right == Bounds.Right && rect.Y == 16f,
                    "tangent X clamped; normal Y preserved",
                    rect.ToString());
            });

        yield return new(
            "HL-009",
            "HudLayout",
            "HudLayout.Offset_AppliesInAnchorLocalLogicalUnits",
            assert =>
            {
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    HealthBar,
                    Bounds,
                    HudPlacementPreset.HealthBarBelow,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)],
                    IncomingDamagePlacement.RightOfExpectedHpLoss,
                    OffsetX: 5f,
                    OffsetY: -3f));
                assert.Equal(
                    new HudLayoutRect(40f, 71f, 30f, 20f),
                    actual.RectFor(HudLayoutContent.ExpectedHpLoss));
            });

        yield return new(
            "HL-010",
            "HudLayout",
            "HudLayout.IndependentPresets_DoNotAutoSwap",
            assert =>
            {
                var left = Layout(
                    HudPlacementPreset.HealthBarLeft,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)]);
                var right = Layout(
                    HudPlacementPreset.HealthBarRight,
                    [Item(HudLayoutContent.IncomingDamage, 30f, 20f)]);
                assert.True(
                    left.RectFor(HudLayoutContent.ExpectedHpLoss).Right < HealthBar.Left
                    && right.RectFor(HudLayoutContent.IncomingDamage).Left > HealthBar.Right,
                    "independent left and right presets remain on their selected sides",
                    $"left={left.Placements[0].Rect}; right={right.Placements[0].Rect}");
            });

        yield return new(
            "HL-011",
            "HudLayout",
            "HudLayout.HealthBarLeft_FirstGroupStaysNearestAndGrowthMovesOutward",
            assert =>
            {
                var anchor = new HudLayoutRect(140f, 40f, 20f, 20f);
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    anchor,
                    Bounds,
                    HudPlacementPreset.HealthBarLeft,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 10f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 10f, 20f),
                        Item(HudLayoutContent.Details, 10f, 20f)
                    ],
                    IncomingDamagePlacement.RightOfExpectedHpLoss));
                var expected = actual.RectFor(HudLayoutContent.ExpectedHpLoss);
                var incoming = actual.RectFor(HudLayoutContent.IncomingDamage);
                var details = actual.RectFor(HudLayoutContent.Details);
                assert.True(
                    expected.Right + 22f == anchor.Left
                    && incoming.Right + 8f == expected.Left
                    && details.Right + 8f == incoming.Left,
                    "expected nearest; incoming then details grow outward",
                    $"expected={expected}; incoming={incoming}; details={details}");
            });

        yield return new(
            "HL-012",
            "HudLayout",
            "HudLayout.EndTurnCluster_IsCenteredOnButtonCenterline",
            assert =>
            {
                var one = Layout(
                    HudPlacementPreset.EndTurnButtonAbove,
                    [Item(HudLayoutContent.ExpectedHpLoss, 20f, 20f)]);
                var two = Layout(
                    HudPlacementPreset.EndTurnButtonAbove,
                    [
                        Item(HudLayoutContent.ExpectedHpLoss, 20f, 20f),
                        Item(HudLayoutContent.IncomingDamage, 20f, 20f)
                    ]);
                var left = two.RectFor(HudLayoutContent.ExpectedHpLoss);
                var right = two.RectFor(HudLayoutContent.IncomingDamage);
                var single = one.RectFor(HudLayoutContent.ExpectedHpLoss);
                assert.True(
                    single.CenterX == HealthBar.CenterX
                    && ((left.Left + right.Right) * 0.5f) == HealthBar.CenterX
                    && single.Bottom == HealthBar.Top
                    && left.Bottom == HealthBar.Top,
                    "one centered; two-item cluster symmetric and shifted up 6 UI units from I2-R10",
                    $"one={single}; left={left}; right={right}");
            });

        yield return new(
            "HL-013",
            "HudLayout",
            "HudLayout.HealthBarBelow_OverlappingBuffsShiftByBuffRowHeight",
            assert =>
            {
                var actual = HudLayoutEngine.Layout(new HudLayoutRequest(
                    HealthBar,
                    Bounds,
                    HudPlacementPreset.HealthBarBelow,
                    [Item(HudLayoutContent.ExpectedHpLoss, 30f, 20f)],
                    IncomingDamagePlacement.RightOfExpectedHpLoss,
                    Avoidance: new HudLayoutAvoidance(
                        new HudLayoutRect(35f, 70f, 40f, 20f),
                        RowHeight: 20f)));
                assert.Equal(
                    new HudLayoutRect(35f, 94f, 30f, 20f),
                    actual.RectFor(HudLayoutContent.ExpectedHpLoss));
            });

        yield return new(
            "HL-014",
            "HudLayout",
            "HudLayout.ContentGroups_RemainExactlyExpectedIncomingAndCompositeDetails",
            assert =>
            {
                var names = Enum.GetNames<HudLayoutContent>();
                assert.True(
                    names.SequenceEqual(["ExpectedHpLoss", "IncomingDamage", "Details"]),
                    "three independently placeable groups with one composite details group",
                    string.Join(',', names));
            });

        yield return new(
            "HL-015",
            "HudLayout",
            "HudCharacterAbove.ShrinkFollowsScaledSemanticHeadPoint",
            assert =>
            {
                var normal = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    new HudAnchorPoint(48f, 8f),
                    new HudLayoutRect(30f, 8f, 40f, 55f));
                var shrunk = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    new HudAnchorPoint(52f, 24f),
                    new HudLayoutRect(44f, 24f, 12f, 28f));
                assert.True(
                    normal.CenterX == 48f
                    && shrunk.CenterX == 52f
                    && shrunk.Top > normal.Top
                    && shrunk.Top == 24f,
                    "semantic head point follows the scaled Visuals node without an unscaled Hitbox",
                    $"normal={normal}; shrunk={shrunk}");
            });

        yield return new(
            "HL-016",
            "HudLayout",
            "HudCharacterAbove.GrowthFollowsScaledSemanticHeadPoint",
            assert =>
            {
                var normal = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    new HudAnchorPoint(50f, 8f),
                    new HudLayoutRect(30f, 8f, 40f, 55f));
                var grown = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    new HudAnchorPoint(50f, -5f),
                    new HudLayoutRect(20f, -5f, 60f, 75f));
                assert.True(
                    grown.CenterX == HealthBar.CenterX
                    && grown.Top < normal.Top
                    && grown.Top == -5f,
                    "growth preserves centerline and increases upward clearance",
                    $"normal={normal}; grown={grown}");
            });

        yield return new(
            "HL-017",
            "HudLayout",
            "HudCharacterAbove.RegentUsesScaledChairBoundsTop",
            assert =>
            {
                var normal = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    semanticPoint: null,
                    new HudLayoutRect(28f, 6f, 44f, 58f));
                var shrunk = HudCharacterAboveAnchorPolicy.Resolve(
                    HealthBar,
                    semanticPoint: null,
                    new HudLayoutRect(42f, 23f, 18f, 30f));
                assert.True(
                    normal.CenterX == 50f
                    && shrunk.CenterX == 51f
                    && shrunk.Top == 23f
                    && shrunk.Top > normal.Top,
                    "Regent chair-top fallback follows current scaled Visuals bounds",
                    $"normal={normal}; shrunk={shrunk}");
            });

        yield return new(
            "HPT-001",
            "ConfigText",
            "HudPlacement.LocalizationLoopUsesV2PropertyOrder",
            assert =>
            {
                var field = typeof(DamageForecastBaseLibConfig).GetField(
                    "LocalizationPropertyOrder",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var actual = field?.GetValue(null) as string[];
                assert.True(
                    actual is not null
                    && actual.SequenceEqual(HudPlacementConfigSchema.V2PropertyOrder),
                    "settings localization loop covers all V2 placement fields",
                    actual is null ? "missing LocalizationPropertyOrder" : string.Join(',', actual));
            });

        yield return new(
            "HL-018",
            "HudLayout",
            "HudLayout.EndTurnNumericSlot_CentersTextWithoutChangingHealthBarSlots",
            assert =>
            {
                var endTurn = HudNumericAlignmentPolicy.Resolve(
                    HudPlacementPreset.EndTurnButtonAbove);
                var healthBar = new[]
                {
                    HudPlacementPreset.HealthBarLeft,
                    HudPlacementPreset.HealthBarRight,
                    HudPlacementPreset.HealthBarAbove,
                    HudPlacementPreset.HealthBarBelow
                }.Select(HudNumericAlignmentPolicy.Resolve).ToArray();
                assert.True(
                    endTurn == Godot.HorizontalAlignment.Center
                    && healthBar.All(value => value == Godot.HorizontalAlignment.Left),
                    "end-turn=Center; all health-bar presets=Left",
                    $"end-turn={endTurn}; health-bar={string.Join(',', healthBar)}");
            });

        yield return new(
            "HPT-002",
            "ConfigText",
            "HudPlacement.CharacterAboveAndPlacementLabelsAreLocalized",
            assert =>
            {
                var above = DamageForecastConfigText.EnumValue(
                    nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset),
                    HudPlacementPreset.HealthBarAbove,
                    DamageForecastConfigLanguage.SimplifiedChinese);
                var expected = DamageForecastConfigText.Setting(
                    nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset),
                    DamageForecastConfigLanguage.SimplifiedChinese);
                var incoming = DamageForecastConfigText.Setting(
                    nameof(DamageForecastBaseLibConfig.IncomingDamagePlacementPreset),
                    DamageForecastConfigLanguage.SimplifiedChinese);
                var details = DamageForecastConfigText.Setting(
                    nameof(DamageForecastBaseLibConfig.DetailsPlacementPreset),
                    DamageForecastConfigLanguage.SimplifiedChinese);
                assert.True(
                    above == "人物上方"
                    && expected != nameof(DamageForecastBaseLibConfig.ExpectedHpLossPlacementPreset)
                    && incoming != nameof(DamageForecastBaseLibConfig.IncomingDamagePlacementPreset)
                    && details != nameof(DamageForecastBaseLibConfig.DetailsPlacementPreset),
                    "character-above wording plus three localized placement labels",
                    $"above={above}; expected={expected}; incoming={incoming}; details={details}");
            });
    }

    private static HudLayoutResult Layout(
        HudPlacementPreset preset,
        IReadOnlyList<HudLayoutItem> items,
        IncomingDamagePlacement placement = IncomingDamagePlacement.RightOfExpectedHpLoss) =>
        HudLayoutEngine.Layout(new HudLayoutRequest(
            HealthBar,
            Bounds,
            preset,
            items,
            placement));

    private static HudLayoutItem Item(
        HudLayoutContent content,
        float width,
        float height) =>
        new(content, new HudLayoutSize(width, height));
}
