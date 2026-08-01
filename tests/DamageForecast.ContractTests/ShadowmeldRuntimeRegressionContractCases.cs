using DamageForecast.Combat;
using DamageForecast.Forecast;
using DamageForecast.Patches;
using DamageForecast.UI;

internal static class ShadowmeldRuntimeRegressionContractCases
{
    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            new ContractCase(
                "SM-019",
                "ShadowmeldRuntimeRegression",
                "CurrentEight.ActiveOneLayer.PlatingSix.AttackTwentySix_LosesSix",
                assert => AssertLoss(assert, layers: 1, plating: 6, attack: 26, expectedLoss: 6)),
            new ContractCase(
                "SM-020",
                "ShadowmeldRuntimeRegression",
                "Absent.CurrentEight.PlatingSix.AttackTwentySix_LosesTwelve",
                assert =>
                {
                    var forecast = new LocalDamageForecast().Calculate(
                        IncomingDamageRead.Known(rawDamage: 26, effectiveBlock: 8 + 6, directHpLoss: 0));
                    assert.Equal(ForecastResultState.KnownDamage, forecast.State);
                    assert.Equal(12, forecast.OutDamage);
                }),
            new ContractCase(
                "SM-021",
                "ShadowmeldRuntimeRegression",
                "CurrentEight.ActiveTwoLayers.PlatingSix.AttackThirtyEight_LosesSix",
                assert => AssertLoss(assert, layers: 2, plating: 6, attack: 38, expectedLoss: 6)),
            new ContractCase(
                "SM-022",
                "ShadowmeldRuntimeRegression",
                "Live.CompletedPlayCard_RefreshesFinalState",
                assert => assert.Equal(
                    true,
                    ForecastActionRefreshPolicy.ShouldRefresh(
                        HudSnapshotLifecyclePhase.Live,
                        isCompletedPlayCard: true),
                    "completed PlayCardAction must replace the early hand-change snapshot")),
            new ContractCase(
                "SM-023",
                "ShadowmeldRuntimeRegression",
                "Live.NonCardAction_DoesNotBroadenRefreshPolicy",
                assert => assert.Equal(
                    false,
                    ForecastActionRefreshPolicy.ShouldRefresh(
                        HudSnapshotLifecyclePhase.Live,
                        isCompletedPlayCard: false))),
            new ContractCase(
                "SM-024",
                "ShadowmeldRuntimeRegression",
                "Waiting.AnyCompletedAction_PreservesExistingRefresh",
                assert => assert.Equal(
                    true,
                    ForecastActionRefreshPolicy.ShouldRefresh(
                        HudSnapshotLifecyclePhase.LocalReadyWaiting,
                        isCompletedPlayCard: false))),
            new ContractCase(
                "SM-025",
                "ShadowmeldRuntimeRegression",
                "Frozen.CompletedPlayCard_DoesNotReplaceCommittedSnapshot",
                assert => assert.Equal(
                    false,
                    ForecastActionRefreshPolicy.ShouldRefresh(
                        HudSnapshotLifecyclePhase.Frozen,
                        isCompletedPlayCard: true)))
        ];
    }

    private static void AssertLoss(
        ContractAssert assert,
        int layers,
        int plating,
        int attack,
        int expectedLoss)
    {
        var transformed = ShadowmeldFutureBlockPolicy.Evaluate(
            new ShadowmeldFutureBlockContractInput(
                CurrentBlock: 8,
                ShadowmeldPowerContractState.Known,
                OwnerMatches: true,
                [new ShadowmeldFutureBlockGrantContractInput(
                    "PlatingPower",
                    NativeExecutionOrder: 40,
                    BaseAmount: plating,
                    ShadowmeldGrantWindow.WhileShadowmeldActive,
                    ShadowmeldGrantEligibility.Eligible,
                    LayersAtGrant: layers)]));
        assert.Equal(ShadowmeldFutureBlockContractState.Known, transformed.State);

        var effectiveBlock = transformed.CurrentBlock
            + transformed.Events.Sum(blockEvent => blockEvent.Amount);
        var forecast = new LocalDamageForecast().Calculate(
            IncomingDamageRead.Known(attack, effectiveBlock, directHpLoss: 0));

        assert.Equal(ForecastResultState.KnownDamage, forecast.State);
        assert.Equal(expectedLoss, forecast.OutDamage);
    }
}
