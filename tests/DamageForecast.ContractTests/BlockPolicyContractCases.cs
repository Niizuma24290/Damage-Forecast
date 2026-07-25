using DamageForecast.Combat;

internal static class BlockPolicyContractCases
{
    public static IReadOnlyList<ContractCase> Create()
    {
        var cases = new List<ContractCase>();
        var combinations = new[]
        {
            (Id: "BK-001", Current: false, Power: false, Relic: false, Expected: 0),
            (Id: "BK-002", Current: true, Power: false, Relic: false, Expected: 2),
            (Id: "BK-003", Current: false, Power: true, Relic: false, Expected: 3),
            (Id: "BK-004", Current: false, Power: false, Relic: true, Expected: 5),
            (Id: "BK-005", Current: true, Power: true, Relic: false, Expected: 5),
            (Id: "BK-006", Current: true, Power: false, Relic: true, Expected: 7),
            (Id: "BK-007", Current: false, Power: true, Relic: true, Expected: 8),
            (Id: "BK-008", Current: true, Power: true, Relic: true, Expected: 10)
        };

        foreach (var combination in combinations)
        {
            var captured = combination;
            cases.Add(new ContractCase(
                captured.Id,
                "BlockPolicy",
                $"BlockSelection.Current{captured.Current}.Power{captured.Power}.Relic{captured.Relic}",
                assert =>
                {
                    var actual = HpLossEventPolicy.SelectBlock(
                        new AvailableBlockInput(2, 3, 5),
                        Options(captured.Current, captured.Power, captured.Relic));
                    assert.Equal(captured.Expected, actual);
                }));
        }

        cases.AddRange(
        [
            new ContractCase(
                "BK-009",
                "BlockPolicy",
                "BlockSelection.NegativeInputs_ClampToZero",
                assert => assert.Equal(
                    0,
                    HpLossEventPolicy.SelectBlock(
                        new AvailableBlockInput(-2, -3, -5),
                        Options(true, true, true)))),
            new ContractCase(
                "BK-010",
                "BlockPolicy",
                "BlockSelection.Overflow_ClampsToIntMax",
                assert => assert.Equal(
                    int.MaxValue,
                    HpLossEventPolicy.SelectBlock(
                        new AvailableBlockInput(int.MaxValue, int.MaxValue, int.MaxValue),
                        Options(true, true, true)))),
            new ContractCase(
                "BK-011",
                "BlockPolicy",
                "BlockConsumption_BlockableOnly_PreservesDirectLane",
                assert =>
                {
                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Event("blockable", 0, HpLossDisplayLane.Blockable, 6), Event("direct", 1, HpLossDisplayLane.DirectHpLoss, 4)],
                        3);
                    AssertEvents(assert, actual, ("blockable", 3), ("direct", 4));
                }),
            new ContractCase(
                "BK-012",
                "BlockPolicy",
                "BlockConsumption_UsesExplicitNativeOrder",
                assert =>
                {
                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Event("later", 2, HpLossDisplayLane.Blockable, 5), Event("earlier", 1, HpLossDisplayLane.Blockable, 4)],
                        6);
                    AssertEvents(assert, actual, ("earlier", 0), ("later", 3));
                }),
            new ContractCase(
                "BK-013",
                "BlockPolicy",
                "BlockConsumption.EqualOrder_PreservesInputOrder",
                assert =>
                {
                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Event("first", 1, HpLossDisplayLane.Blockable, 4), Event("second", 1, HpLossDisplayLane.Blockable, 4)],
                        5);
                    AssertEvents(assert, actual, ("first", 0), ("second", 3));
                }),
            new ContractCase(
                "BK-014",
                "BlockPolicy",
                "BlockConsumption.NegativeAmount_DoesNotConsumeBlock",
                assert =>
                {
                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Event("negative", 0, HpLossDisplayLane.Blockable, -4), Event("positive", 1, HpLossDisplayLane.Blockable, 5)],
                        3);
                    AssertEvents(assert, actual, ("negative", 0), ("positive", 2));
                }),
            new ContractCase(
                "BK-015",
                "BlockPolicy",
                "BlockConsumption.FutureBlockUsesNativeOrder",
                assert =>
                {
                    var actual = HpLossEventPolicy.ApplySelectedBlock(
                        [Event("earlier", 0, HpLossDisplayLane.Blockable, 5), Event("later", 2, HpLossDisplayLane.Blockable, 5)],
                        0,
                        [new UpcomingBlockEvent("FeelNoPain", 1, 3)]);
                    AssertEvents(assert, actual, ("earlier", 5), ("later", 2));
                }),
            new ContractCase(
                "BK-016",
                "BlockPolicy",
                "BlockConsumption.ProductionReader_UsesDoubledHandOrder",
                assert => assert.Equal(
                    8,
                    VerifiedEtherealExhaustBlockReader.GetHandTurnEndEffectOrder(4)))
        ]);

        return cases;
    }

    private static IncomingDamageDisplayOptions Options(bool current, bool power, bool relic)
    {
        return new IncomingDamageDisplayOptions(current, power, relic, false, false);
    }

    private static UpcomingHpLossEvent Event(
        string source,
        int order,
        HpLossDisplayLane lane,
        int amount)
    {
        return new UpcomingHpLossEvent(source, order, lane, amount, true);
    }

    private static void AssertEvents(
        ContractAssert assert,
        IReadOnlyList<UpcomingHpLossEvent> actual,
        params (string Source, int Amount)[] expected)
    {
        assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            assert.Equal(expected[index].Source, actual[index].Source, $"event index={index}");
            assert.Equal(expected[index].Amount, actual[index].VerifiedHpLoss, $"event index={index}");
        }
    }
}
