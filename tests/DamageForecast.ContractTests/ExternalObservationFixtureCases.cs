internal static class ExternalObservationFixtureCases
{
    public static IReadOnlyList<ContractCase> Create()
    {
        return
        [
            new(
                "EO-001",
                "ExternalObservation",
                "ExternalObservation.StableBurnFixture_IsComparable",
                assert =>
                {
                    var (scenario, observation) = LoadPair(
                        "stable-burn.scenario.json",
                        "stable-burn.observation.json");
                    var actual = ExternalObservationContractValidator.Validate(scenario, observation);
                    assert.True(
                        actual.IsValid && actual.IsComparable && actual.Errors.Count == 0,
                        "valid=true; comparable=true; errors=0",
                        Describe(actual));
                }),
            new(
                "EO-002",
                "ExternalObservation",
                "ExternalObservation.BetaDoubtFixture_IsComparable",
                assert =>
                {
                    var (scenario, observation) = LoadPair(
                        "beta-doubt.scenario.json",
                        "beta-doubt.observation.json");
                    var actual = ExternalObservationContractValidator.Validate(scenario, observation);
                    assert.True(
                        actual.IsValid && actual.IsComparable && actual.Errors.Count == 0,
                        "valid=true; comparable=true; errors=0",
                        Describe(actual));
                }),
            new(
                "EO-003",
                "ExternalObservation",
                "ExternalObservation.MalformedJson_FailsWithoutThrowing",
                assert =>
                {
                    var actual = ExternalObservationFixture.LoadObservation(
                        "malformed.observation.json");
                    assert.Equal(false, actual.Success);
                    assert.Equal("fixture.json.invalid", actual.Error);
                }),
            new(
                "EO-004",
                "ExternalObservation",
                "ExternalObservation.MissingFixture_FailsWithoutProcessFallback",
                assert =>
                {
                    var actual = ExternalObservationFixture.LoadObservation(
                        "does-not-exist.observation.json");
                    assert.Equal(false, actual.Success);
                    assert.Equal("fixture.notFound", actual.Error);
                })
        ];
    }

    public static (ForecastScenario Scenario, NativeObservation Observation) LoadPair(
        string scenarioFile,
        string observationFile)
    {
        var scenario = ExternalObservationFixture.LoadScenario(scenarioFile);
        if (!scenario.Success)
            throw new InvalidOperationException($"Scenario fixture failed to load: {scenario.Error}");
        var observation = ExternalObservationFixture.LoadObservation(observationFile);
        if (!observation.Success)
            throw new InvalidOperationException($"Observation fixture failed to load: {observation.Error}");
        return (scenario.Value!, observation.Value!);
    }

    public static string Describe(ExternalObservationValidationResult result) =>
        $"valid={result.IsValid}; comparable={result.IsComparable}; "
        + $"disposition={result.Disposition}; errors=[{string.Join(",", result.Errors)}]";
}
