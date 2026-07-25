using DamageForecast;
using DamageForecast.Combat;
using DamageForecast.Diagnostics;
using DamageForecast.Forecast;
using DamageForecast.Patches;
using DamageForecast.Settings;

var cases = new List<ContractCase>
{
    new(
        "BC-001",
        "BurnShape",
        "TurnEndCard.ExactVerifiedSingleBlockable_Accepted",
        assert =>
        {
            var actual = VerifiedTurnEndDamagePolicy.IsVerifiedSingleBlockableDamageShape(
                typeof(VerifiedCard),
                typeof(VerifiedCard),
                1,
                false);
            assert.Equal(true, actual, "exact verified card with one blockable DamageVar is accepted");
        }),
    new(
        "BC-002",
        "BurnShape",
        "TurnEndCard.DerivedVerified_Rejected",
        assert =>
        {
            var actual = VerifiedTurnEndDamagePolicy.IsVerifiedSingleBlockableDamageShape(
                typeof(DerivedVerifiedCard),
                typeof(VerifiedCard),
                1,
                false);
            assert.Equal(false, actual, "derived card cannot inherit built-in verification");
        }),
    new(
        "BC-003",
        "BurnShape",
        "TurnEndCard.UnverifiedType_Rejected",
        assert =>
        {
            var actual = VerifiedTurnEndDamagePolicy.IsVerifiedSingleBlockableDamageShape(
                typeof(UnverifiedCard),
                typeof(VerifiedCard),
                1,
                false);
            assert.Equal(false, actual, "unverified card type is rejected");
        }),
    new(
        "BC-004",
        "BurnShape",
        "TurnEndCard.MultipleDamageVars_Rejected",
        assert =>
        {
            var actual = VerifiedTurnEndDamagePolicy.IsVerifiedSingleBlockableDamageShape(
                typeof(VerifiedCard),
                typeof(VerifiedCard),
                2,
                false);
            assert.Equal(false, actual, "multiple DamageVars are rejected");
        }),
    new(
        "BC-005",
        "BurnShape",
        "TurnEndCard.UnblockableDamageVar_Rejected",
        assert =>
        {
            var actual = VerifiedTurnEndDamagePolicy.IsVerifiedSingleBlockableDamageShape(
                typeof(VerifiedCard),
                typeof(VerifiedCard),
                1,
                true);
            assert.Equal(false, actual, "unblockable DamageVar is rejected");
        }),
    new(
        "FR-001",
        "ForecastResult",
        "ForecastResult.NegativeLanes_ClampedToZero",
        assert =>
        {
            var actual = ForecastResult.KnownDamage(-5, 7);
            assert.True(
                actual.OutDamage == 0 && actual.DirectHpLoss == 7,
                "OutDamage=0; DirectHpLoss=7",
                $"OutDamage={actual.OutDamage}; DirectHpLoss={actual.DirectHpLoss}",
                "forecast result clamps negative lanes");
        }),
    new(
        "DI-001",
        "IncomingDisplay",
        "IncomingDisplay.NonPositive_Hidden",
        assert =>
        {
            var actual = IncomingDamageDisplayRead.Known(-4);
            assert.True(
                actual.State == IncomingDamageDisplayReadState.Hidden && actual.Damage == 0,
                "State=Hidden; Damage=0",
                $"State={actual.State}; Damage={actual.Damage}",
                "incoming zero or negative result is hidden");
        }),
    new(
        "DI-002",
        "IncomingDisplay",
        "IncomingDisplay.Positive_Known",
        assert =>
        {
            var actual = IncomingDamageDisplayRead.Known(4);
            assert.True(
                actual.State == IncomingDamageDisplayReadState.Known && actual.Damage == 4,
                "State=Known; Damage=4",
                $"State={actual.State}; Damage={actual.Damage}",
                "positive incoming result remains known");
        }),
    new(
        "DG-001",
        "Diagnostics",
        "Diagnostics.FirstReport_Accepted",
        assert =>
        {
            var actual = DamageForecastDiagnostics.TryMarkReported("contract-test.dg-001");
            assert.Equal(true, actual, "diagnostic code reports the first time");
        }),
    new(
        "DG-002",
        "Diagnostics",
        "Diagnostics.DuplicateReport_RateLimited",
        assert =>
        {
            const string code = "contract-test.dg-002";
            var first = DamageForecastDiagnostics.TryMarkReported(code);
            var second = DamageForecastDiagnostics.TryMarkReported(code);
            assert.True(
                first && !second,
                "first=true; second=false",
                $"first={first}; second={second}",
                "diagnostic code is rate-limited after the first report");
        }),
    new(
        "CT-001",
        "ConfigText",
        "ConfigText.EnglishHudLabel_UsesCurrentProductName",
        assert =>
        {
            var actual = DamageForecastConfigText.Setting(
                nameof(DamageForecastBaseLibConfig.EnableDamageForecastHud),
                DamageForecastConfigLanguage.English);
            assert.Equal("Enable Damage Forecast HUD", actual);
        }),
    new(
        "CT-002",
        "ConfigText",
        "ConfigText.SimplifiedChineseHudLabel_UsesCurrentProductName",
        assert =>
        {
            var actual = DamageForecastConfigText.Setting(
                nameof(DamageForecastBaseLibConfig.EnableDamageForecastHud),
                DamageForecastConfigLanguage.SimplifiedChinese);
            assert.Equal("启用伤害预测 HUD", actual);
        }),
    new(
        "CT-003",
        "ConfigText",
        "ConfigText.EnglishMultiplayerLabel_StatesLocalBoundary",
        assert =>
        {
            var actual = DamageForecastConfigText.Setting(
                nameof(DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer),
                DamageForecastConfigLanguage.English);
            assert.Equal("Show local-player Damage Forecast HUD in Multiplayer", actual);
        }),
    new(
        "CT-004",
        "ConfigText",
        "ConfigText.SimplifiedChineseMultiplayerLabel_StatesLocalBoundary",
        assert =>
        {
            var actual = DamageForecastConfigText.Setting(
                nameof(DamageForecastBaseLibConfig.ShowLocalPlayerHudInMultiplayer),
                DamageForecastConfigLanguage.SimplifiedChinese);
            assert.Equal("在多人模式中显示本机伤害预测 HUD", actual);
        }),
    new(
        "BL-001",
        "BaseLibTitle",
        "BaseLib.ListTitle.CurrentMod_UsesStableEnglishProductName",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolveListTitle(
                MainFile.ModId,
                "STS2PartyWatch");
            assert.Equal("Damage Forecast", actual);
        }),
    new(
        "BL-002",
        "BaseLibTitle",
        "BaseLib.ListTitle.CurrentMod_RepeatedResolutionStaysEnglish",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolveListTitle(
                MainFile.ModId,
                "STS2PartyWatch");
            assert.Equal(
                "Damage Forecast",
                actual,
                "preserves the original duplicate assertion that represented a config-language change");
        }),
    new(
        "BL-003",
        "BaseLibTitle",
        "BaseLib.ListTitle.OtherMod_PreservesFallback",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolveListTitle(
                "another-mod",
                "AnotherMod");
            assert.Equal("AnotherMod", actual);
        }),
    new(
        "BL-004",
        "BaseLibTitle",
        "BaseLib.PageTitle.CurrentModEnglish_UsesEnglishProductName",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolvePageTitle(
                MainFile.ModId,
                DamageForecastConfigLanguage.English,
                "STS2PartyWatch");
            assert.Equal("Damage Forecast", actual);
        }),
    new(
        "BL-005",
        "BaseLibTitle",
        "BaseLib.PageTitle.CurrentModChinese_UsesChineseProductName",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolvePageTitle(
                MainFile.ModId,
                DamageForecastConfigLanguage.SimplifiedChinese,
                "STS2PartyWatch");
            assert.Equal("伤害预测", actual);
        }),
    new(
        "BL-006",
        "BaseLibTitle",
        "BaseLib.PageTitle.OtherMod_PreservesFallback",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.ResolvePageTitle(
                "another-mod",
                DamageForecastConfigLanguage.SimplifiedChinese,
                "AnotherMod");
            assert.Equal("AnotherMod", actual);
        }),
    new(
        "BL-007",
        "BaseLibTitle",
        "BaseLib.Compatibility.RequiredTitleTargets_Available",
        assert =>
        {
            var actual = DamageForecastBaseLibTitlePatch.HasCompatibleTarget();
            assert.Equal(
                true,
                actual,
                "BaseLib 3.3.4 exposes the title resolver, page-load hook, and title field");
        }),
    new(
        "VP-001",
        "VisibilityProfiler",
        "VisibilityProfiler.EntryPoints_Aggregated",
        assert =>
        {
            var actual = CreateVisibilitySnapshot();
            assert.True(
                actual.ShowCalls == 1
                && actual.HideCalls == 1
                && actual.SetVisibleCalls == 1
                && actual.TotalCalls == 3,
                "show=1; hide=1; setVisible=1; total=3",
                $"show={actual.ShowCalls}; hide={actual.HideCalls}; setVisible={actual.SetVisibleCalls}; total={actual.TotalCalls}");
        }),
    new(
        "VP-002",
        "VisibilityProfiler",
        "VisibilityProfiler.MatchOutcomes_Aggregated",
        assert =>
        {
            var actual = CreateVisibilitySnapshot();
            assert.True(
                actual.UnmatchedCalls == 1 && actual.MatchedCalls == 2,
                "unmatched=1; matched=2",
                $"unmatched={actual.UnmatchedCalls}; matched={actual.MatchedCalls}");
        }),
    new(
        "VP-003",
        "VisibilityProfiler",
        "VisibilityProfiler.TransitionsAndDuplicates_Separated",
        assert =>
        {
            var actual = CreateVisibilitySnapshot();
            assert.True(
                actual.Transitions == 1 && actual.Duplicates == 1,
                "transitions=1; duplicates=1",
                $"transitions={actual.Transitions}; duplicates={actual.Duplicates}");
        }),
    new(
        "VP-004",
        "VisibilityProfiler",
        "VisibilityProfiler.RefreshWork_Aggregated",
        assert =>
        {
            var actual = CreateVisibilitySnapshot();
            assert.True(
                actual.Refreshes == 2 && actual.BarsVisited == 5,
                "refreshes=2; barsVisited=5",
                $"refreshes={actual.Refreshes}; barsVisited={actual.BarsVisited}");
        }),
    new(
        "VP-005",
        "VisibilityProfiler",
        "VisibilityProfiler.TimingTotalsAndMaximums_Aggregated",
        assert =>
        {
            var actual = CreateVisibilitySnapshot();
            assert.True(
                actual.TypeCheckTotalTicks == 60
                && actual.TypeCheckMaxTicks == 30
                && actual.CallbackTotalTicks == 600
                && actual.CallbackMaxTicks == 300
                && actual.RefreshTotalTicks == 70
                && actual.RefreshMaxTicks == 40,
                "typeCheck=60/30; callback=600/300; refresh=70/40",
                $"typeCheck={actual.TypeCheckTotalTicks}/{actual.TypeCheckMaxTicks}; "
                + $"callback={actual.CallbackTotalTicks}/{actual.CallbackMaxTicks}; "
                + $"refresh={actual.RefreshTotalTicks}/{actual.RefreshMaxTicks}");
        }),
    new(
        "VP-006",
        "VisibilityProfiler",
        "VisibilityProfiler.SummaryFormatting_StableAndInvariant",
        assert =>
        {
            var snapshot = CreateVisibilitySnapshot();
            var actual = Aud0007VisibilitySummaryFormatter.Format(
                "contract-test",
                snapshot,
                1_000_000);
            assert.True(
                actual.StartsWith(
                    "[Damage Forecast][AUD-0007] reason=contract-test window_s=2.000",
                    StringComparison.Ordinal)
                && actual.Contains("calls_per_s=1.500", StringComparison.Ordinal)
                && actual.Contains("match_pct=66.667", StringComparison.Ordinal)
                && actual.Contains("duplicate_pct=50.000", StringComparison.Ordinal)
                && actual.Contains("refreshes_per_transition=2.000", StringComparison.Ordinal),
                "stable invariant AUD-0007 summary fields",
                actual);
        }),
    new(
        "VP-007",
        "VisibilityProfiler",
        "VisibilityProfiler.Reset_ClearsCounters",
        assert =>
        {
            var accumulator = CreateVisibilityAccumulator();
            accumulator.Reset();
            var actual = accumulator.Snapshot(0);
            assert.Equal(0L, actual.TotalCalls);
        })
};

cases.AddRange(ForecastResultContractCases.Create());
cases.AddRange(HandCardDamageContractCases.Create());
cases.AddRange(ProjectionContractCases.Create());
cases.AddRange(BlockPolicyContractCases.Create());
cases.AddRange(FeelNoPainContractCases.Create());
cases.AddRange(HpLossModifierContractCases.Create());
cases.AddRange(PoisonPolicyContractCases.Create());
cases.AddRange(LifecycleContractCases.Create());
cases.AddRange(NativeCoveringScreenContractCases.Create());
cases.AddRange(ExternalObservationFixtureCases.Create());
cases.AddRange(ExternalObservationValidationCases.Create());
cases.AddRange(ExternalObservationAdapterContractCases.Create());
cases.AddRange(DifferentialScenarioMatrixContractCases.Create(
    cases.Select(testCase => testCase.Id).ToHashSet(StringComparer.Ordinal)));
cases.AddRange(IdentityPackagingContractCases.Create());
cases.AddRange(IdentityMigrationContractCases.Create());
cases.AddRange(IdentityUpgradeToolContractCases.Create());
cases.AddRange(IdentityPublishTreeContractCases.Create());
cases.AddRange(QualityGateContractCases.Create());
cases.AddRange(PostG6NamingContractCases.Create());
cases.AddRange(HudNodeOwnershipContractCases.Create());
cases.AddRange(ConfigMigrationContractCases.Create());
cases.AddRange(PostG6ClosureContractCases.Create());

return ContractRunner.Run(cases);

static Aud0007VisibilitySnapshot CreateVisibilitySnapshot()
{
    return CreateVisibilityAccumulator().Snapshot(2_000_000);
}

static VisibilityPerformanceAccumulator CreateVisibilityAccumulator()
{
    var accumulator = new VisibilityPerformanceAccumulator();
    accumulator.Record(VisibilityPostfixKind.Show, false, false, 10, 100, false, 0, 0);
    accumulator.Record(VisibilityPostfixKind.Hide, true, true, 20, 200, true, 30, 2);
    accumulator.Record(VisibilityPostfixKind.SetVisible, true, false, 30, 300, true, 40, 3);
    return accumulator;
}

internal class VerifiedCard;

internal sealed class DerivedVerifiedCard : VerifiedCard;

internal sealed class UnverifiedCard;
