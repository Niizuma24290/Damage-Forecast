internal static class DifferentialScenarioMatrixContractCases
{
    public static IReadOnlyList<ContractCase> Create(
        IReadOnlySet<string> knownContractIds)
    {
        return
        [
            new(
                "DM-001",
                "DifferentialMatrix",
                "DifferentialMatrix.Manifest_IsValidOfflineContract",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixValidator.Validate(
                        DifferentialScenarioMatrixFixture.Matrix,
                        knownContractIds);
                    assert.True(
                        actual.IsValid,
                        "valid=true; errors=0",
                        $"valid={actual.IsValid}; errors=[{string.Join(",", actual.Errors)}]");
                }),
            new(
                "DM-002",
                "DifferentialMatrix",
                "DifferentialMatrix.RequiredScenarioGroups_AreComplete",
                assert =>
                {
                    var matrix = DifferentialScenarioMatrixFixture.Matrix;
                    var actual = matrix.Rows!
                        .Where(row => row.Required)
                        .Select(row => row.ScenarioGroup!)
                        .ToHashSet(StringComparer.Ordinal);
                    assert.True(
                        actual.SetEquals(DifferentialScenarioMatrixValidator.RequiredGroups()),
                        string.Join(",", DifferentialScenarioMatrixValidator.RequiredGroups().Order()),
                        string.Join(",", actual.Order()));
                }),
            new(
                "DM-003",
                "DifferentialMatrix",
                "DifferentialMatrix.ReadyComparisons_AreOnlyReviewedBurnAndDoubtMappings",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixFixture.Matrix.Rows!
                        .Where(row => row.ComparisonReadiness == "Ready")
                        .SelectMany(row => row.ProviderScenarioIds!
                            .Select(scenarioId => $"{row.TargetChannels!.Single()}:{scenarioId}"))
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                    var expected = new[]
                    {
                        "beta:turn-end.doubt.power.v1",
                        "stable:turn-end.burn.blockable.v1"
                    };
                    assert.True(
                        actual.SequenceEqual(expected, StringComparer.Ordinal),
                        string.Join(",", expected),
                        string.Join(",", actual));
                }),
            new(
                "DM-004",
                "DifferentialMatrix",
                "DifferentialMatrix.ToolUnsupportedRows_FailClosedWithReasons",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixFixture.Matrix.Rows!
                        .Where(row => row.Disposition == "Tool unsupported")
                        .ToArray();
                    assert.True(
                        actual.Length > 0
                        && actual.All(row => row.ComparisonReadiness == "Blocked")
                        && actual.All(row => row.BlockingReasonCodes is { Count: > 0 })
                        && actual.All(row => row.ProviderScenarioIds is { Count: 0 }),
                        "at least one blocked Tool unsupported row; reasons present; no provider mapping",
                        string.Join(
                            ";",
                            actual.Select(row =>
                                $"{row.Id}:{row.ComparisonReadiness}:"
                                + $"reasons={row.BlockingReasonCodes?.Count}:"
                                + $"provider={row.ProviderScenarioIds?.Count}")));
                }),
            new(
                "DM-005",
                "DifferentialMatrix",
                "DifferentialMatrix.VersionDifference_RemainsUnpairedCandidate",
                assert =>
                {
                    var row = DifferentialScenarioMatrixFixture.Matrix.Rows!
                        .Single(item => item.ScenarioGroup == "stable-beta-difference");
                    assert.True(
                        row.Disposition == "Expected version difference"
                        && row.ComparisonReadiness == "Candidate"
                        && row.ProviderScenarioIds is { Count: 0 }
                        && row.BlockingReasonCodes!.Contains(
                            "comparison.same-scenario-cross-target-pair.missing",
                            StringComparer.Ordinal),
                        "candidate=true; paired provider scenario absent; blocker present",
                        $"{row.Disposition}; {row.ComparisonReadiness}; "
                        + $"provider={row.ProviderScenarioIds?.Count}; "
                        + $"blockers={string.Join(",", row.BlockingReasonCodes ?? [])}");
                }),
            new(
                "DM-006",
                "DifferentialMatrix",
                "DifferentialMatrix.ExecutionBoundary_RemainsOfflineAndOptional",
                assert =>
                {
                    var matrix = DifferentialScenarioMatrixFixture.Matrix;
                    assert.True(
                        matrix.ExecutionMode == "OfflineOnly"
                        && !matrix.ProcessExecutionAllowed
                        && !matrix.OrdinaryGuardrailRequired
                        && !matrix.ProductionChangeOnMismatchAllowed
                        && !matrix.RuntimeVerified
                        && matrix.Rows!.All(row => !row.RuntimeVerified),
                        "offline only; no process; ordinary guardrail optional; no production mutation; runtime false",
                        $"mode={matrix.ExecutionMode}; process={matrix.ProcessExecutionAllowed}; "
                        + $"guardrail={matrix.OrdinaryGuardrailRequired}; "
                        + $"productionChange={matrix.ProductionChangeOnMismatchAllowed}; "
                        + $"runtime={matrix.RuntimeVerified}");
                }),
            new(
                "DM-007",
                "DifferentialMatrix",
                "DifferentialMatrix.ProviderCheckpoint_IsExactAndClean",
                assert =>
                {
                    var matrix = DifferentialScenarioMatrixFixture.Matrix;
                    assert.True(
                        matrix.ProviderId == "sts2sim-explicit-process"
                        && matrix.SourceRevision
                            == DifferentialScenarioMatrixValidator.ApprovedProviderRevision
                        && matrix.SourceTree == "clean",
                        "provider=sts2sim-explicit-process; approved revision; sourceTree=clean",
                        $"provider={matrix.ProviderId}; revision={matrix.SourceRevision}; "
                        + $"sourceTree={matrix.SourceTree}");
                }),
            new(
                "DM-008",
                "DifferentialMatrix",
                "DifferentialMatrix.UnreviewedReadyMapping_FailsValidation",
                assert =>
                {
                    var matrix = DifferentialScenarioMatrixFixture.Matrix;
                    var rows = matrix.Rows!.ToArray();
                    var burnIndex = Array.FindIndex(
                        rows,
                        row => row.Id == "DFM-006");
                    rows[burnIndex] = rows[burnIndex] with
                    {
                        ProviderScenarioIds = ["turn-end.unreviewed.v1"]
                    };
                    var actual = DifferentialScenarioMatrixValidator.Validate(
                        matrix with { Rows = rows },
                        knownContractIds);
                    assert.True(
                        !actual.IsValid
                        && actual.Errors.Contains(
                            "DFM-006.comparable.mappingNotReviewed",
                            StringComparer.Ordinal),
                        "valid=false; errors contains DFM-006.comparable.mappingNotReviewed",
                        $"valid={actual.IsValid}; errors=[{string.Join(",", actual.Errors)}]");
                }),
            new(
                "DM-009",
                "DifferentialMatrix",
                "DifferentialMatrix.ProcessPermission_FailsValidation",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixValidator.Validate(
                        DifferentialScenarioMatrixFixture.Matrix with
                        {
                            ProcessExecutionAllowed = true
                        },
                        knownContractIds);
                    assert.True(
                        !actual.IsValid
                        && actual.Errors.Contains(
                            "matrix.processExecutionAllowed.mustRemainFalse",
                            StringComparer.Ordinal),
                        "valid=false; process execution rejected",
                        $"valid={actual.IsValid}; errors=[{string.Join(",", actual.Errors)}]");
                }),
            new(
                "DM-010",
                "DifferentialMatrix",
                "DifferentialMatrix.UnknownContractReference_FailsValidation",
                assert =>
                {
                    var matrix = DifferentialScenarioMatrixFixture.Matrix;
                    var rows = matrix.Rows!.ToArray();
                    var blockIndex = Array.FindIndex(
                        rows,
                        row => row.Id == "DFM-001");
                    rows[blockIndex] = rows[blockIndex] with
                    {
                        ForecastContractIds = ["BK-999"]
                    };
                    var actual = DifferentialScenarioMatrixValidator.Validate(
                        matrix with { Rows = rows },
                        knownContractIds);
                    assert.True(
                        !actual.IsValid
                        && actual.Errors.Contains(
                            "DFM-001.contractReference.unknown:BK-999",
                            StringComparer.Ordinal),
                        "valid=false; unknown contract reference rejected",
                        $"valid={actual.IsValid}; errors=[{string.Join(",", actual.Errors)}]");
                }),
            new(
                "DM-011",
                "DifferentialMatrix",
                "DifferentialMatrix.OfflineEvaluation_ClassifiesAllRowsWithoutPromotion",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixEvaluator.EvaluateAll(
                        DifferentialScenarioMatrixFixture.Matrix);
                    assert.True(
                        actual.Count == 13
                        && actual.Count(result =>
                            result.IsEvaluated
                            && result.Classification == "Match"
                            && result.IsValid) == 4
                        && actual.Count(result => !result.IsEvaluated) == 9
                        && actual.All(result =>
                            result.Classification is not ("Tool suspect" or "Forecast suspect")),
                        "rows=13; evaluated Match=4; not evaluated=9; no suspect promotion",
                        DescribeEvaluations(actual));
                }),
            new(
                "DM-012",
                "DifferentialMatrix",
                "DifferentialMatrix.StableBurnFixture_MatchesForecastSemantics",
                assert => AssertMatch(assert, "DFM-006", "SemanticMatch")),
            new(
                "DM-013",
                "DifferentialMatrix",
                "DifferentialMatrix.BetaDoubtFixture_MatchesForecastSemantics",
                assert => AssertMatch(assert, "DFM-007", "SemanticMatch")),
            new(
                "DM-014",
                "DifferentialMatrix",
                "DifferentialMatrix.UnknownScenarioFixture_MatchesFailClosedBoundary",
                assert => AssertMatch(assert, "DFM-010", "UnsupportedBoundary")),
            new(
                "DM-015",
                "DifferentialMatrix",
                "DifferentialMatrix.SameSeedFixtures_MatchDeterministically",
                assert => AssertMatch(assert, "DFM-011", "Determinism")),
            new(
                "DM-016",
                "DifferentialMatrix",
                "DifferentialMatrix.BlockedAndCandidateRows_AreNotEvaluatedOrPromoted",
                assert =>
                {
                    var actual = DifferentialScenarioMatrixEvaluator.EvaluateAll(
                            DifferentialScenarioMatrixFixture.Matrix)
                        .Where(result => !result.IsEvaluated)
                        .ToArray();
                    assert.True(
                        actual.Length == 9
                        && actual.All(result => result.Classification != "Match")
                        && actual.Single(result => result.RowId == "DFM-012").Classification
                            == "Expected version difference"
                        && actual.Single(result => result.RowId == "DFM-013").Classification
                            == "Provider out of scope",
                        "nine non-evaluated rows; no Match; candidate and out-of-scope preserved",
                        DescribeEvaluations(actual));
                }),
            new(
                "DM-017",
                "DifferentialMatrix",
                "DifferentialMatrix.MissingOfflineFixture_FailsWithoutFallback",
                assert =>
                {
                    var row = DifferentialScenarioMatrixFixture.Matrix.Rows!
                        .Single(item => item.Id == "DFM-006");
                    var actual = DifferentialScenarioMatrixEvaluator.Evaluate(
                        row with
                        {
                            Evaluation = row.Evaluation! with
                            {
                                ObservationFixtures = ["does-not-exist.observation.json"]
                            }
                        });
                    assert.True(
                        actual.IsEvaluated
                        && !actual.IsValid
                        && actual.Classification == "Invalid"
                        && actual.Errors.Any(error =>
                            error.Contains("fixture.notFound", StringComparison.Ordinal)),
                        "evaluated=true; valid=false; classification=Invalid; fixture.notFound",
                        DescribeEvaluations([actual]));
                })
        ];
    }

    private static void AssertMatch(
        ContractAssert assert,
        string rowId,
        string evaluationKind)
    {
        var row = DifferentialScenarioMatrixFixture.Matrix.Rows!
            .Single(item => item.Id == rowId);
        var actual = DifferentialScenarioMatrixEvaluator.Evaluate(row);
        assert.True(
            actual.IsEvaluated
            && actual.IsValid
            && actual.Classification == "Match"
            && actual.EvaluationKind == evaluationKind,
            $"evaluated=true; valid=true; classification=Match; kind={evaluationKind}",
            DescribeEvaluations([actual]));
    }

    private static string DescribeEvaluations(
        IEnumerable<DifferentialScenarioEvaluationResult> results)
    {
        return string.Join(
            ";",
            results.Select(result =>
                $"{result.RowId}:{result.EvaluationKind}:{result.IsEvaluated}:"
                + $"{result.Classification}:errors=[{string.Join(",", result.Errors)}]:"
                + $"reasons=[{string.Join(",", result.Reasons)}]"));
    }
}
