using DamageForecast.Combat;

internal static class ShadowmeldFutureBlockContractCases
{
    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            new ContractCase(
                "SM-001",
                "ShadowmeldFutureBlock",
                "Absent.FutureGrant_RemainsBaseAmount",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            currentBlock: 4,
                            ShadowmeldPowerContractState.Absent,
                            Grant("PlatingPower", 10, 3, ShadowmeldGrantWindow.WhileShadowmeldAbsent)));

                    AssertKnown(assert, actual, 4, ("PlatingPower", 10, 3));
                }),
            new ContractCase(
                "SM-002",
                "ShadowmeldFutureBlock",
                "Active.CurrentBlock_IsNeverRemultiplied",
                assert =>
                {
                    var actual = Evaluate(Input(7, ShadowmeldPowerContractState.Known));

                    AssertKnown(assert, actual, 7);
                }),
            new ContractCase(
                "SM-003",
                "ShadowmeldFutureBlock",
                "Active.OneLayer_FutureGrantDoubles",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("FrostOrb", 20, 5, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)));

                    AssertKnown(assert, actual, 0, ("FrostOrb", 20, 10));
                }),
            new ContractCase(
                "SM-004",
                "ShadowmeldFutureBlock",
                "Active.TwoLayers_FutureGrantQuadruplesWithoutChangingCurrentBlock",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            8,
                            ShadowmeldPowerContractState.Known,
                            Grant("FrostOrb", 20, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 2)));

                    AssertKnown(assert, actual, 8, ("FrostOrb", 20, 12));
                }),
            new ContractCase(
                "SM-005",
                "ShadowmeldFutureBlock",
                "Active.Stacking_AppliesLayersAtEachGrantWithoutRetroactivity",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            6,
                            ShadowmeldPowerContractState.Known,
                            Grant("BeforeSecondShadowmeld", 10, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1),
                            Grant("AfterSecondShadowmeld", 20, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 2)));

                    AssertKnown(
                        assert,
                        actual,
                        6,
                        ("BeforeSecondShadowmeld", 10, 6),
                        ("AfterSecondShadowmeld", 20, 12));
                }),
            new ContractCase(
                "SM-006",
                "ShadowmeldFutureBlock",
                "Removed.LaterGrant_IsNotMultiplied",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("NextTurnGrant", 30, 4, ShadowmeldGrantWindow.AfterShadowmeldRemoved)));

                    AssertKnown(assert, actual, 0, ("NextTurnGrant", 30, 4));
                }),
            new ContractCase(
                "SM-007",
                "ShadowmeldFutureBlock",
                "Plating.ActiveOneLayer_DoublesGrant",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("PlatingPower", 40, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)));

                    AssertKnown(assert, actual, 0, ("PlatingPower", 40, 6));
                }),
            new ContractCase(
                "SM-008",
                "ShadowmeldFutureBlock",
                "Orichalcum.ZeroBlockEligible_DoublesGrant",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("Orichalcum", 50, 6, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)));

                    AssertKnown(assert, actual, 0, ("Orichalcum", 50, 12));
                }),
            new ContractCase(
                "SM-009",
                "ShadowmeldFutureBlock",
                "Orichalcum.EndTurnSnapshotNonZero_IneligibleDoesNotGrant",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            2,
                            ShadowmeldPowerContractState.Known,
                            Grant(
                                "Orichalcum",
                                50,
                                6,
                                ShadowmeldGrantWindow.WhileShadowmeldActive,
                                ShadowmeldGrantEligibility.Ineligible,
                                layersAtGrant: 1)));

                    AssertKnown(assert, actual, 2);
                }),
            new ContractCase(
                "SM-010",
                "ShadowmeldFutureBlock",
                "PlatingWithOrichalcum.EndTurnSnapshotZero_BothGrantsRemainEligible",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("PlatingPower", 40, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1),
                            Grant(
                                "Orichalcum",
                                50,
                                6,
                                ShadowmeldGrantWindow.WhileShadowmeldActive,
                                ShadowmeldGrantEligibility.Eligible,
                                layersAtGrant: 1)));

                    AssertKnown(
                        assert,
                        actual,
                        0,
                        ("PlatingPower", 40, 6),
                        ("Orichalcum", 50, 12));
                }),
            new ContractCase(
                "SM-011",
                "ShadowmeldFutureBlock",
                "FeelNoPain.ActiveGrant_PreservesTimelineOrder",
                assert =>
                {
                    var transformed = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("FeelNoPainPower[card]", 1, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)));
                    AssertKnown(assert, transformed, 0, ("FeelNoPainPower[card]", 1, 6));

                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Damage("earlier", 0, 5), Damage("later", 2, 8)],
                        selectedBlock: 0,
                        transformed.Events);

                    assert.Equal(5, actual[0].VerifiedHpLoss, "future Block must not protect earlier damage");
                    assert.Equal(2, actual[1].VerifiedHpLoss, "doubled future Block protects only later damage");
                }),
            new ContractCase(
                "SM-012",
                "ShadowmeldFutureBlock",
                "UnsupportedPowerShape_FailsClosed",
                assert => AssertUnknown(
                    assert,
                    Input(
                        5,
                        ShadowmeldPowerContractState.Unsupported,
                        Grant("FrostOrb", 20, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)))),
            new ContractCase(
                "SM-013",
                "ShadowmeldFutureBlock",
                "OwnerMismatch_FailsClosed",
                assert => AssertUnknown(
                    assert,
                    Input(
                        5,
                        ShadowmeldPowerContractState.Known,
                        ownerMatches: false,
                        Grant("FrostOrb", 20, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)))),
            new ContractCase(
                "SM-014",
                "ShadowmeldFutureBlock",
                "UnknownGrantWindow_FailsClosed",
                assert => AssertUnknown(
                    assert,
                    Input(
                        5,
                        ShadowmeldPowerContractState.Known,
                        Grant("UnknownTiming", 20, 3, ShadowmeldGrantWindow.Unknown, layersAtGrant: 1)))),
            new ContractCase(
                "SM-015",
                "ShadowmeldFutureBlock",
                "UnknownGrantEligibility_FailsClosed",
                assert => AssertUnknown(
                    assert,
                    Input(
                        5,
                        ShadowmeldPowerContractState.Known,
                        Grant(
                            "UnknownEligibility",
                            20,
                            3,
                            ShadowmeldGrantWindow.WhileShadowmeldActive,
                            ShadowmeldGrantEligibility.Unknown,
                            layersAtGrant: 1)))),
            new ContractCase(
                "SM-016",
                "ShadowmeldFutureBlock",
                "AlreadyResolvedBeforeActivation_IsCurrentBlockOnly",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            9,
                            ShadowmeldPowerContractState.Known,
                            Grant("ResolvedBeforeShadowmeld", 0, 4, ShadowmeldGrantWindow.AlreadyResolved)));

                    AssertKnown(assert, actual, 9);
                }),
            new ContractCase(
                "SM-017",
                "ShadowmeldFutureBlock",
                "ActivePowerWithNonPositiveLayers_FailsClosed",
                assert => AssertUnknown(
                    assert,
                    Input(
                        0,
                        ShadowmeldPowerContractState.Known,
                        Grant("InvalidLayer", 20, 3, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 0)))),
            new ContractCase(
                "SM-018",
                "ShadowmeldFutureBlock",
                "MultipleFutureGrants_PreserveSourceAndNativeOrderMetadata",
                assert =>
                {
                    var actual = Evaluate(
                        Input(
                            0,
                            ShadowmeldPowerContractState.Known,
                            Grant("LaterInput", 80, 2, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1),
                            Grant("EarlierInput", 10, 4, ShadowmeldGrantWindow.WhileShadowmeldActive, layersAtGrant: 1)));

                    AssertKnown(
                        assert,
                        actual,
                        0,
                        ("LaterInput", 80, 4),
                        ("EarlierInput", 10, 8));
                })
        ];
    }

    private static ShadowmeldFutureBlockContractResult Evaluate(
        ShadowmeldFutureBlockContractInput input) =>
        ShadowmeldFutureBlockPolicy.Evaluate(input);

    private static ShadowmeldFutureBlockContractInput Input(
        int currentBlock,
        ShadowmeldPowerContractState powerState,
        params ShadowmeldFutureBlockGrantContractInput[] grants)
    {
        return new ShadowmeldFutureBlockContractInput(
            currentBlock,
            powerState,
            OwnerMatches: true,
            grants);
    }

    private static ShadowmeldFutureBlockContractInput Input(
        int currentBlock,
        ShadowmeldPowerContractState powerState,
        bool ownerMatches,
        params ShadowmeldFutureBlockGrantContractInput[] grants)
    {
        return new ShadowmeldFutureBlockContractInput(
            currentBlock,
            powerState,
            ownerMatches,
            grants);
    }

    private static ShadowmeldFutureBlockGrantContractInput Grant(
        string source,
        int nativeExecutionOrder,
        int baseAmount,
        ShadowmeldGrantWindow window,
        ShadowmeldGrantEligibility eligibility = ShadowmeldGrantEligibility.Eligible,
        int layersAtGrant = 0)
    {
        return new ShadowmeldFutureBlockGrantContractInput(
            source,
            nativeExecutionOrder,
            baseAmount,
            window,
            eligibility,
            layersAtGrant);
    }

    private static UpcomingHpLossEvent Damage(string source, int order, int amount)
    {
        return new UpcomingHpLossEvent(
            source,
            order,
            HpLossDisplayLane.Blockable,
            amount,
            IsSingleVerifiedEvent: true);
    }

    private static void AssertKnown(
        ContractAssert assert,
        ShadowmeldFutureBlockContractResult actual,
        int expectedCurrentBlock,
        params (string Source, int NativeExecutionOrder, int Amount)[] expectedEvents)
    {
        assert.Equal(ShadowmeldFutureBlockContractState.Known, actual.State);
        assert.Equal(expectedCurrentBlock, actual.CurrentBlock);
        assert.Equal(expectedEvents.Length, actual.Events.Count);
        for (var index = 0; index < expectedEvents.Length; index++)
        {
            var expected = expectedEvents[index];
            var actualEvent = actual.Events[index];
            assert.Equal(expected.Source, actualEvent.Source, $"event index={index}");
            assert.Equal(expected.NativeExecutionOrder, actualEvent.NativeExecutionOrder, $"event index={index}");
            assert.Equal(expected.Amount, actualEvent.Amount, $"event index={index}");
        }
    }

    private static void AssertUnknown(
        ContractAssert assert,
        ShadowmeldFutureBlockContractInput input)
    {
        var actual = Evaluate(input);
        assert.Equal(ShadowmeldFutureBlockContractState.Unknown, actual.State);
        assert.Equal(input.CurrentBlock, actual.CurrentBlock, "current Block remains native-final even on Unknown future grant");
        assert.Equal(0, actual.Events.Count, "Unknown result must not expose partial future Block");
    }
}
