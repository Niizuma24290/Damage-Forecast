internal static class ExternalObservationValidationCases
{
    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            InvalidCase(
                "EO-005",
                "ExternalObservation.UnknownSchema_FailsClosed",
                "unknown-schema.scenario.json",
                "stable-burn.observation.json",
                "scenario.schemaVersion.unsupported"),
            InvalidCase(
                "EO-006",
                "ExternalObservation.DirtySource_FailsClosed",
                "stable-burn.scenario.json",
                "dirty-source.observation.json",
                "metadata.sourceDirty.notClean"),
            InvalidCase(
                "EO-007",
                "ExternalObservation.CrossTargetMetadata_FailsClosed",
                "stable-burn.scenario.json",
                "target-mismatch.observation.json",
                "metadata.gameChannel.mismatch"),
            InvalidCase(
                "EO-008",
                "ExternalObservation.DuplicateEventId_FailsClosed",
                "stable-burn.scenario.json",
                "duplicate-event.observation.json",
                "observation.event.eventId.duplicate"),
            InvalidCase(
                "EO-009",
                "ExternalObservation.PartialResultCannotPublishTotals",
                "stable-burn.scenario.json",
                "partial-with-totals.observation.json",
                "observation.partialTotals.present"),
            new(
                "EO-010",
                "ExternalObservation",
                "ExternalObservation.ExplicitUnsupported_IsValidButNotComparable",
                assert =>
                {
                    var (scenario, observation) = ExternalObservationFixtureCases.LoadPair(
                        "stable-burn.scenario.json",
                        "unsupported-attack-intent.observation.json");
                    var actual = ExternalObservationContractValidator.Validate(scenario, observation);
                    assert.True(
                        actual.IsValid
                        && !actual.IsComparable
                        && actual.Disposition == "Unsupported"
                        && actual.Errors.Count == 0,
                        "valid=true; comparable=false; disposition=Unsupported; errors=0",
                        ExternalObservationFixtureCases.Describe(actual));
                }),
            InvalidCase(
                "EO-011",
                "ExternalObservation.UnsupportedWithoutFailClosedFlag_IsInvalid",
                "stable-burn.scenario.json",
                "unsafe-unsupported.observation.json",
                "observation.unsupported.failClosed.required"),
            new(
                "EO-012",
                "ExternalObservation",
                "ExternalObservation.RepeatIgnoresRunEnvelopeAndMatchesSemantics",
                assert =>
                {
                    var first = LoadObservation("stable-burn.observation.json");
                    var second = LoadObservation("stable-burn-repeat.observation.json");
                    var actual = ExternalObservationContractValidator.ValidateDeterminism(first, second);
                    assert.Equal(0, actual.Count, string.Join(",", actual));
                }),
            new(
                "EO-013",
                "ExternalObservation",
                "ExternalObservation.DeterministicMismatch_FailsClosed",
                assert =>
                {
                    var first = LoadObservation("stable-burn.observation.json");
                    var second = LoadObservation("deterministic-mismatch.observation.json");
                    var actual = ExternalObservationContractValidator.ValidateDeterminism(first, second);
                    assert.True(
                        actual.Contains("determinism.output.mismatch", StringComparer.Ordinal),
                        "determinism.output.mismatch",
                        string.Join(",", actual));
                }),
            InvalidCase(
                "EO-014",
                "ExternalObservation.CompleteTotalsMustMatchObservedEvents",
                "stable-burn.scenario.json",
                "total-mismatch.observation.json",
                "observation.blockableTotal.mismatch")
        ];
    }

    private static ContractCase InvalidCase(
        string id,
        string name,
        string scenarioFile,
        string observationFile,
        string expectedError)
    {
        return new(
            id,
            "ExternalObservation",
            name,
            assert =>
            {
                var (scenario, observation) = ExternalObservationFixtureCases.LoadPair(
                    scenarioFile,
                    observationFile);
                var actual = ExternalObservationContractValidator.Validate(scenario, observation);
                assert.True(
                    !actual.IsValid
                    && !actual.IsComparable
                    && actual.Errors.Contains(expectedError, StringComparer.Ordinal),
                    $"valid=false; comparable=false; errors contains {expectedError}",
                    ExternalObservationFixtureCases.Describe(actual));
            });
    }

    private static NativeObservation LoadObservation(string fileName)
    {
        var result = ExternalObservationFixture.LoadObservation(fileName);
        return result.Success
            ? result.Value!
            : throw new InvalidOperationException($"Observation fixture failed to load: {result.Error}");
    }
}
