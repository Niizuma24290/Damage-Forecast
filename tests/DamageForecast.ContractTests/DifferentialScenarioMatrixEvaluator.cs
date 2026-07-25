internal sealed record DifferentialScenarioEvaluationResult(
    string RowId,
    string EvaluationKind,
    bool IsEvaluated,
    string Classification,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Reasons)
{
    public bool IsValid => Errors.Count == 0;
}

internal static class DifferentialScenarioMatrixEvaluator
{
    public static IReadOnlyList<DifferentialScenarioEvaluationResult> EvaluateAll(
        DifferentialScenarioMatrix matrix)
    {
        return matrix.Rows?.Select(Evaluate).ToArray() ?? [];
    }

    public static DifferentialScenarioEvaluationResult Evaluate(
        DifferentialScenarioMatrixRow row)
    {
        var rowId = row.Id ?? "<missing>";
        return row.ComparisonReadiness switch
        {
            "Ready" or "BoundaryOnly" => EvaluateBinding(row, rowId),
            "Blocked" => NotEvaluated(row, rowId, row.Disposition ?? "Invalid"),
            "Candidate" => NotEvaluated(row, rowId, "Expected version difference"),
            "OutOfScope" => NotEvaluated(row, rowId, "Provider out of scope"),
            _ => Invalid(rowId, row.Evaluation?.Kind ?? "None", "evaluation.readiness.invalid")
        };
    }

    private static DifferentialScenarioEvaluationResult EvaluateBinding(
        DifferentialScenarioMatrixRow row,
        string rowId)
    {
        var binding = row.Evaluation;
        if (binding?.ScenarioFixture is null
            || binding.ObservationFixtures is not { Count: > 0 })
        {
            return Invalid(rowId, binding?.Kind ?? "None", "evaluation.binding.missing");
        }

        var scenarioLoad = ExternalObservationFixture.LoadScenario(binding.ScenarioFixture);
        if (!scenarioLoad.Success)
        {
            return Invalid(
                rowId,
                binding.Kind ?? "None",
                $"evaluation.scenario.{scenarioLoad.Error}");
        }

        var observations = new List<NativeObservation>();
        foreach (var fixture in binding.ObservationFixtures)
        {
            var observationLoad = ExternalObservationFixture.LoadObservation(fixture);
            if (!observationLoad.Success)
            {
                return Invalid(
                    rowId,
                    binding.Kind ?? "None",
                    $"evaluation.observation.{observationLoad.Error}:{fixture}");
            }
            observations.Add(observationLoad.Value!);
        }

        return binding.Kind switch
        {
            "SemanticMatch" => EvaluateSemanticMatch(
                rowId,
                scenarioLoad.Value!,
                observations.Single()),
            "UnsupportedBoundary" => EvaluateUnsupportedBoundary(
                rowId,
                scenarioLoad.Value!,
                observations.Single()),
            "Determinism" => EvaluateDeterminism(
                rowId,
                scenarioLoad.Value!,
                observations),
            _ => Invalid(rowId, binding.Kind ?? "None", "evaluation.kind.invalid")
        };
    }

    private static DifferentialScenarioEvaluationResult EvaluateSemanticMatch(
        string rowId,
        ForecastScenario scenario,
        NativeObservation observation)
    {
        var errors = ValidateObservationPair(scenario, observation);
        if (errors.Count == 0)
        {
            var expected = scenario.OrderedInputs!;
            var actual = observation.Events!;
            if (expected.Count != actual.Count)
            {
                errors.Add("evaluation.semantic.eventCount.mismatch");
            }
            else
            {
                for (var index = 0; index < expected.Count; index++)
                {
                    if (!SemanticEventMatches(expected[index], actual[index]))
                        errors.Add($"evaluation.semantic.event.mismatch:{index}");
                }
            }

            var expectedBlockable = expected
                .Where(item => item.Lane == "Blockable")
                .Sum(item => item.Amount!.Value);
            var expectedDirect = expected
                .Where(item => item.Lane == "DirectHpLoss")
                .Sum(item => item.Amount!.Value);
            if (observation.BlockableTotal != expectedBlockable)
                errors.Add("evaluation.semantic.blockableTotal.mismatch");
            if (observation.DirectHpLossTotal != expectedDirect)
                errors.Add("evaluation.semantic.directHpLossTotal.mismatch");
        }

        return Evaluated(rowId, "SemanticMatch", errors);
    }

    private static DifferentialScenarioEvaluationResult EvaluateUnsupportedBoundary(
        string rowId,
        ForecastScenario scenario,
        NativeObservation observation)
    {
        var validation = ExternalObservationContractValidator.Validate(scenario, observation);
        var errors = validation.Errors
            .Select(error => $"evaluation.external.{error}")
            .ToList();
        if (validation.IsValid
            && (validation.IsComparable
                || validation.Disposition != "Unsupported"
                || observation.Unsupported is not { Count: > 0 }
                || observation.Unsupported.Any(item => item.FailClosed != true)))
        {
            errors.Add("evaluation.unsupportedBoundary.notFailClosed");
        }
        return Evaluated(rowId, "UnsupportedBoundary", errors);
    }

    private static DifferentialScenarioEvaluationResult EvaluateDeterminism(
        string rowId,
        ForecastScenario scenario,
        IReadOnlyList<NativeObservation> observations)
    {
        var errors = new List<string>();
        if (observations.Count != 2)
        {
            errors.Add("evaluation.determinism.observationCount.invalid");
            return Evaluated(rowId, "Determinism", errors);
        }

        errors.AddRange(ValidateObservationPair(scenario, observations[0]));
        errors.AddRange(ValidateObservationPair(scenario, observations[1]));
        errors.AddRange(
            ExternalObservationContractValidator.ValidateDeterminism(
                    observations[0],
                    observations[1])
                .Select(error => $"evaluation.{error}"));
        return Evaluated(rowId, "Determinism", errors);
    }

    private static List<string> ValidateObservationPair(
        ForecastScenario scenario,
        NativeObservation observation)
    {
        var validation = ExternalObservationContractValidator.Validate(scenario, observation);
        var errors = validation.Errors
            .Select(error => $"evaluation.external.{error}")
            .ToList();
        if (validation.IsValid && !validation.IsComparable)
            errors.Add("evaluation.external.notComparable");
        return errors;
    }

    private static bool SemanticEventMatches(
        OrderedScenarioInput expected,
        OrderedObservedEvent actual)
    {
        return actual.Status == "Observed"
            && actual.SourceId == expected.SourceId
            && actual.Phase == expected.Phase
            && actual.Order == expected.Order
            && actual.Lane == expected.Lane
            && actual.Granularity == expected.Granularity
            && actual.Amount == expected.Amount;
    }

    private static DifferentialScenarioEvaluationResult Evaluated(
        string rowId,
        string kind,
        IReadOnlyList<string> errors)
    {
        return new(
            rowId,
            kind,
            true,
            errors.Count == 0 ? "Match" : "Invalid",
            errors,
            []);
    }

    private static DifferentialScenarioEvaluationResult NotEvaluated(
        DifferentialScenarioMatrixRow row,
        string rowId,
        string classification)
    {
        return new(
            rowId,
            "None",
            false,
            classification,
            [],
            row.BlockingReasonCodes ?? []);
    }

    private static DifferentialScenarioEvaluationResult Invalid(
        string rowId,
        string kind,
        string error)
    {
        return new(rowId, kind, true, "Invalid", [error], []);
    }
}
