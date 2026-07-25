using DamageForecast.Combat;
using DamageForecast.Forecast;
using DamageForecast.UI;

internal static class FeelNoPainContractCases
{
    private static readonly HudSnapshotOwnerIdentity Owner = new(1, "feel-no-pain-owner");

    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            new ContractCase(
                "FP-001",
                "FeelNoPain",
                "NoPower_ContributesZero",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Absent,
                        0,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 5, 0);
                    AssertKnown(assert, actual.Incoming, 5, 0);
                }),
            new ContractCase(
                "FP-002",
                "FeelNoPain",
                "PowerWithNoEthereal_ContributesZero",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [NonEthereal("ordinary")],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 5, 0);
                }),
            new ContractCase(
                "FP-003",
                "FeelNoPain",
                "OneEthereal_OneVerifiedBlockGrant",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 2, 0);
                }),
            new ContractCase(
                "FP-004",
                "FeelNoPain",
                "MultipleEthereal_GrantsOncePerCard",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("first"), OrdinaryEthereal("second")],
                        [LaterBlockable("enemy", 7)]));
                    AssertKnown(assert, actual.Expected, 1, 0);
                }),
            new ContractCase(
                "FP-005",
                "FeelNoPain",
                "StackedOrUpgradedPower_UsesVerifiedNativeValue",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        4,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 6)]));
                    AssertKnown(assert, actual.Expected, 2, 0);
                }),
            new ContractCase(
                "FP-006",
                "FeelNoPain",
                "NonEtherealAndAlreadyLeftHand_AreIgnored",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [
                            NonEthereal("ordinary"),
                            OrdinaryEthereal("already-left") with { IsInHand = false }
                        ],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 5, 0);
                }),
            new ContractCase(
                "FP-007",
                "FeelNoPain",
                "TurnEndEffectEthereal_UsesEffectThenExhaustOrder",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [TurnEndEthereal("turn-end", HpLossDisplayLane.Blockable, 5)],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 7, 0);
                }),
            new ContractCase(
                "FP-008",
                "FeelNoPain",
                "BlockProtectsOnlyLaterBlockableEvents",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [
                            TurnEndEthereal("earlier", HpLossDisplayLane.Blockable, 5),
                            TurnEndNonEthereal("later", HpLossDisplayLane.Blockable, 5)
                        ],
                        []));
                    AssertEventLosses(assert, actual.Expected, ("earlier", 5), ("later", 2));
                }),
            new ContractCase(
                "FP-009",
                "FeelNoPain",
                "DirectHpLoss_DoesNotConsumeOrBenefitFromBlock",
                assert =>
                {
                    var actual = Evaluate(Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [TurnEndEthereal("direct", HpLossDisplayLane.DirectHpLoss, 4)],
                        [LaterBlockable("enemy", 5)]));
                    AssertKnown(assert, actual.Expected, 2, 4);
                    AssertEventLosses(assert, actual.Expected, ("direct", 4), ("enemy", 2));
                }),
            new ContractCase(
                "FP-010",
                "FeelNoPain",
                "ExpectedMinusN_AlwaysIncludesVerifiedPowerBlock",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]);
                    var expectedOnly = Evaluate(
                        scenario,
                        DamageDisplayMode.ExpectedHpLossOnly,
                        Options(power: false));
                    var both = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: false));
                    var incomingOnly = Evaluate(
                        scenario,
                        DamageDisplayMode.IncomingDamageOnly,
                        Options(power: false));

                    AssertKnown(assert, expectedOnly.Expected, 2, 0);
                    AssertKnown(assert, both.Expected, 2, 0);
                    AssertKnown(assert, incomingOnly.Expected, 2, 0);
                    assert.True(
                        expectedOnly.ShowExpected
                        && both.ShowExpected
                        && !incomingOnly.ShowExpected,
                        "ExpectedOnly=true; Both=true; IncomingOnly=false",
                        $"ExpectedOnly={expectedOnly.ShowExpected}; Both={both.ShowExpected}; IncomingOnly={incomingOnly.ShowExpected}");
                }),
            new ContractCase(
                "FP-011",
                "FeelNoPain",
                "IncomingN_PowerOptionOff_IgnoresPowerBlock",
                assert =>
                {
                    var actual = Evaluate(
                        Scenario(
                            FeelNoPainPowerReadState.Known,
                            3,
                            [OrdinaryEthereal("ethereal")],
                            [LaterBlockable("enemy", 5)]),
                        DamageDisplayMode.Both,
                        Options(power: false));
                    AssertKnown(assert, actual.Expected, 2, 0);
                    AssertKnown(assert, actual.Incoming, 5, 0);
                }),
            new ContractCase(
                "FP-012",
                "FeelNoPain",
                "IncomingN_PowerOptionOn_IncludesPowerBlock",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]);
                    var both = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: true));
                    var incomingOnly = Evaluate(
                        scenario,
                        DamageDisplayMode.IncomingDamageOnly,
                        Options(power: true));

                    AssertKnown(assert, both.Incoming, 2, 0);
                    AssertKnown(assert, incomingOnly.Incoming, 2, 0);
                    assert.True(
                        both.ShowIncoming && incomingOnly.ShowIncoming,
                        "Both=true; IncomingOnly=true",
                        $"Both={both.ShowIncoming}; IncomingOnly={incomingOnly.ShowIncoming}");
                }),
            new ContractCase(
                "FP-013",
                "FeelNoPain",
                "RelicAndCurrentOptions_DoNotControlFeelNoPain",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)],
                        currentBlock: 1,
                        relicBlock: 1);
                    var withoutPower = Evaluate(
                        scenario,
                        DamageDisplayMode.IncomingDamageOnly,
                        Options(current: true, power: false, relic: true));
                    var powerOnly = Evaluate(
                        scenario,
                        DamageDisplayMode.IncomingDamageOnly,
                        Options(current: false, power: true, relic: false));

                    AssertKnown(assert, withoutPower.Incoming, 3, 0);
                    AssertKnown(assert, powerOnly.Incoming, 2, 0);
                }),
            new ContractCase(
                "FP-014",
                "FeelNoPain",
                "DetailsOff_DoesNotChangeEitherCalculation",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]);
                    var detailsOff = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: true),
                        showDetails: false);
                    var detailsOn = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: true),
                        showDetails: true);
                    AssertEquivalentRoute(assert, detailsOff.Expected, detailsOn.Expected);
                    AssertEquivalentRoute(assert, detailsOff.Incoming, detailsOn.Incoming);
                }),
            new ContractCase(
                "FP-015",
                "FeelNoPain",
                "UnsupportedPower_HidesMinusNAndSelectedNOnly",
                assert =>
                {
                    var actual = Evaluate(
                        Scenario(
                            FeelNoPainPowerReadState.Unsupported,
                            0,
                            [OrdinaryEthereal("ethereal")],
                            [LaterBlockable("enemy", 5)]),
                        DamageDisplayMode.Both,
                        Options(power: true));
                    assert.Equal(RouteState.Unknown, actual.Expected.State);
                    assert.Equal(RouteState.Unknown, actual.Incoming.State);
                }),
            new ContractCase(
                "FP-016",
                "FeelNoPain",
                "UnselectedUnsupportedPower_DoesNotPoisonN",
                assert =>
                {
                    var actual = Evaluate(
                        Scenario(
                            FeelNoPainPowerReadState.Unsupported,
                            0,
                            [OrdinaryEthereal("ethereal")],
                            [LaterBlockable("enemy", 5)]),
                        DamageDisplayMode.Both,
                        Options(power: false));
                    assert.Equal(RouteState.Unknown, actual.Expected.State);
                    AssertKnown(assert, actual.Incoming, 5, 0);
                }),
            new ContractCase(
                "FP-017",
                "FeelNoPain",
                "DuplicateRefresh_DoesNotDoubleCount",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [OrdinaryEthereal("ethereal")],
                        [LaterBlockable("enemy", 5)]);
                    var first = Evaluate(scenario, DamageDisplayMode.Both, Options(power: true));
                    var second = Evaluate(scenario, DamageDisplayMode.Both, Options(power: true));
                    AssertEquivalentRoute(assert, first.Expected, second.Expected);
                    AssertEquivalentRoute(assert, first.Incoming, second.Incoming);
                    AssertKnown(assert, second.Expected, 2, 0);
                    AssertKnown(assert, second.Incoming, 2, 0);
                }),
            new ContractCase(
                "FP-018",
                "FeelNoPain",
                "FreezeSnapshot_PreservesRoutedValues",
                assert =>
                {
                    var routed = new ForecastHudSnapshot(
                        ForecastResult.KnownDamage(2, 4),
                        IncomingDamageDisplayRead.Known(2));
                    var state = HudSnapshotLifecyclePolicy.Commit(
                        HudSnapshotLifecycleState.Empty,
                        Owner,
                        routed,
                        isDisplayable: true);
                    var found = HudSnapshotLifecyclePolicy.TryGetCommitted(
                        state,
                        Owner,
                        freezeEnabled: true,
                        out var committed);
                    assert.True(
                        found && committed == routed,
                        "found=true; expected=(2 blockable, 4 direct); incoming=2",
                        $"found={found}; committed={committed}");
                }),
            new ContractCase(
                "FP-019",
                "FeelNoPain",
                "IndeterminateEtherealExhaust_FailsClosedOnlyOnSelectedRoutes",
                assert =>
                {
                    var scenario = Scenario(
                        FeelNoPainPowerReadState.Known,
                        3,
                        [
                            OrdinaryEthereal("stampede-candidate") with
                            {
                                Prediction = EtherealExhaustPrediction.Unknown
                            }
                        ],
                        [LaterBlockable("enemy", 5)]);
                    var selected = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: true));
                    var unselected = Evaluate(
                        scenario,
                        DamageDisplayMode.Both,
                        Options(power: false));

                    assert.Equal(RouteState.Unknown, selected.Expected.State);
                    assert.Equal(RouteState.Unknown, selected.Incoming.State);
                    assert.Equal(RouteState.Unknown, unselected.Expected.State);
                    AssertKnown(assert, unselected.Incoming, 5, 0);
                })
        ];
    }

    private static Projection Evaluate(
        ScenarioInput scenario,
        DamageDisplayMode displayMode = DamageDisplayMode.Both,
        IncomingDamageDisplayOptions? incomingOptions = null,
        bool showDetails = false)
    {
        var options = incomingOptions ?? Options(power: true);
        var expected = EvaluateRoute(
            scenario,
            includeCurrentBlock: true,
            includeStaticPowerBlock: true,
            includeRelicBlock: true,
            includeFeelNoPain: true);
        var incoming = EvaluateRoute(
            scenario,
            options.IncludeCurrentBlock,
            options.IncludePowerBlock,
            options.IncludeRelicBlock,
            options.IncludePowerBlock);
        var projection = ForecastHudProjectionPolicy.Project(
            new ForecastHudSnapshot(ToForecast(expected), ToIncoming(incoming)),
            displayMode,
            IncomingDamagePlacement.RightOfExpectedHpLoss);
        _ = showDetails;
        return new Projection(
            expected,
            incoming,
            projection.ShowExpectedHpLoss,
            projection.ShowIncomingDamage);
    }

    private static RouteResult EvaluateRoute(
        ScenarioInput scenario,
        bool includeCurrentBlock,
        bool includeStaticPowerBlock,
        bool includeRelicBlock,
        bool includeFeelNoPain)
    {
        var currentCards = scenario.Cards.Where(card => card.IsInHand).ToArray();
        var exhaustInputs = currentCards
            .Select((card, index) => new EtherealExhaustCardInput(
                card.Id,
                index,
                card.HasTurnEndInHandEffect,
                card.Prediction))
            .ToArray();
        var futureBlock = includeFeelNoPain
            ? VerifiedEtherealExhaustBlockPolicy.Evaluate(
                new EtherealExhaustBlockInput(
                    scenario.PowerState,
                    scenario.PowerAmount,
                    exhaustInputs))
            : EtherealExhaustBlockRead.Known(Array.Empty<UpcomingBlockEvent>());
        if (futureBlock.State != EtherealExhaustBlockReadState.Known)
        {
            return RouteResult.Unknown;
        }

        var selectedBlock = HpLossEventPolicy.SelectBlock(
            new AvailableBlockInput(
                scenario.CurrentBlock,
                scenario.StaticPowerBlock,
                scenario.RelicBlock),
            Options(
                current: includeCurrentBlock,
                power: includeStaticPowerBlock,
                relic: includeRelicBlock));
        var sourceEvents = new List<UpcomingHpLossEvent>();
        for (var index = 0; index < currentCards.Length; index++)
        {
            var damage = currentCards[index].TurnEndDamage;
            if (damage is null)
            {
                continue;
            }

            sourceEvents.Add(new UpcomingHpLossEvent(
                damage.Value.Source,
                VerifiedEtherealExhaustBlockReader.GetHandTurnEndEffectOrder(index),
                damage.Value.Lane,
                damage.Value.Amount,
                true));
        }

        sourceEvents.AddRange(scenario.LaterDamageEvents.Select((damage, index) =>
            new UpcomingHpLossEvent(
                damage.Source,
                1_000_000 + index,
                damage.Lane,
                damage.Amount,
                true)));
        var applied = HpLossEventPolicy.ApplySelectedBlock(
            sourceEvents,
            selectedBlock,
            futureBlock.Events);
        return RouteResult.Known(applied);
    }

    private static ForecastResult ToForecast(RouteResult route)
    {
        return route.State == RouteState.Known
            ? ForecastResult.KnownDamage(route.BlockableHpLoss, route.DirectHpLoss)
            : ForecastResult.Unknown;
    }

    private static IncomingDamageDisplayRead ToIncoming(RouteResult route)
    {
        return route.State == RouteState.Known
            ? IncomingDamageDisplayRead.Known(
                SaturatingAdd(route.BlockableHpLoss, route.DirectHpLoss))
            : IncomingDamageDisplayRead.Unknown;
    }

    private static int SaturatingAdd(int left, int right)
    {
        return (int)Math.Min(
            int.MaxValue,
            (long)Math.Max(0, left) + Math.Max(0, right));
    }

    private static ScenarioInput Scenario(
        FeelNoPainPowerReadState powerState,
        int powerAmount,
        IReadOnlyList<CardInput> cards,
        IReadOnlyList<DamageInput> laterDamageEvents,
        int currentBlock = 0,
        int staticPowerBlock = 0,
        int relicBlock = 0)
    {
        return new ScenarioInput(
            powerState,
            powerAmount,
            cards,
            laterDamageEvents,
            currentBlock,
            staticPowerBlock,
            relicBlock);
    }

    private static CardInput OrdinaryEthereal(string id)
    {
        return new CardInput(
            id,
            IsInHand: true,
            EtherealExhaustPrediction.Yes,
            HasTurnEndInHandEffect: false,
            TurnEndDamage: null);
    }

    private static CardInput NonEthereal(string id)
    {
        return new CardInput(
            id,
            IsInHand: true,
            EtherealExhaustPrediction.No,
            HasTurnEndInHandEffect: false,
            TurnEndDamage: null);
    }

    private static CardInput TurnEndEthereal(
        string id,
        HpLossDisplayLane lane,
        int damage)
    {
        return new CardInput(
            id,
            IsInHand: true,
            EtherealExhaustPrediction.Yes,
            HasTurnEndInHandEffect: true,
            new DamageInput(id, lane, damage));
    }

    private static CardInput TurnEndNonEthereal(
        string id,
        HpLossDisplayLane lane,
        int damage)
    {
        return new CardInput(
            id,
            IsInHand: true,
            EtherealExhaustPrediction.No,
            HasTurnEndInHandEffect: true,
            new DamageInput(id, lane, damage));
    }

    private static DamageInput LaterBlockable(string source, int amount)
    {
        return new DamageInput(source, HpLossDisplayLane.Blockable, amount);
    }

    private static IncomingDamageDisplayOptions Options(
        bool current = false,
        bool power = false,
        bool relic = false)
    {
        return new IncomingDamageDisplayOptions(current, power, relic, false, false);
    }

    private static void AssertKnown(
        ContractAssert assert,
        RouteResult actual,
        int blockableHpLoss,
        int directHpLoss)
    {
        assert.True(
            actual.State == RouteState.Known
            && actual.BlockableHpLoss == blockableHpLoss
            && actual.DirectHpLoss == directHpLoss,
            $"state=Known; blockable={blockableHpLoss}; direct={directHpLoss}",
            actual.ToString());
    }

    private static void AssertEventLosses(
        ContractAssert assert,
        RouteResult actual,
        params (string Source, int HpLoss)[] expected)
    {
        assert.Equal(expected.Length, actual.Events.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            assert.Equal(expected[index].Source, actual.Events[index].Source, $"event index={index}");
            assert.Equal(expected[index].HpLoss, actual.Events[index].VerifiedHpLoss, $"event index={index}");
        }
    }

    private static void AssertEquivalentRoute(
        ContractAssert assert,
        RouteResult expected,
        RouteResult actual)
    {
        assert.Equal(expected.State, actual.State);
        assert.Equal(expected.BlockableHpLoss, actual.BlockableHpLoss);
        assert.Equal(expected.DirectHpLoss, actual.DirectHpLoss);
        assert.Equal(expected.Events.Count, actual.Events.Count);
        for (var index = 0; index < expected.Events.Count; index++)
        {
            assert.Equal(expected.Events[index], actual.Events[index], $"event index={index}");
        }
    }

    private readonly record struct Projection(
        RouteResult Expected,
        RouteResult Incoming,
        bool ShowExpected,
        bool ShowIncoming);

    private readonly record struct ScenarioInput(
        FeelNoPainPowerReadState PowerState,
        int PowerAmount,
        IReadOnlyList<CardInput> Cards,
        IReadOnlyList<DamageInput> LaterDamageEvents,
        int CurrentBlock,
        int StaticPowerBlock,
        int RelicBlock);

    private readonly record struct CardInput(
        string Id,
        bool IsInHand,
        EtherealExhaustPrediction Prediction,
        bool HasTurnEndInHandEffect,
        DamageInput? TurnEndDamage);

    private readonly record struct DamageInput(
        string Source,
        HpLossDisplayLane Lane,
        int Amount);

    private readonly record struct RouteResult(
        RouteState State,
        int BlockableHpLoss,
        int DirectHpLoss,
        IReadOnlyList<UpcomingHpLossEvent> Events)
    {
        public static RouteResult Unknown =>
            new(RouteState.Unknown, 0, 0, Array.Empty<UpcomingHpLossEvent>());

        public static RouteResult Known(IReadOnlyList<UpcomingHpLossEvent> events)
        {
            return new RouteResult(
                RouteState.Known,
                events
                    .Where(e => e.DisplayLane == HpLossDisplayLane.Blockable)
                    .Sum(e => Math.Max(0, e.VerifiedHpLoss)),
                events
                    .Where(e => e.DisplayLane == HpLossDisplayLane.DirectHpLoss)
                    .Sum(e => Math.Max(0, e.VerifiedHpLoss)),
                events);
        }
    }

    private enum RouteState
    {
        Known,
        Unknown
    }
}
