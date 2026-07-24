using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal sealed class ForecastScenario
{
    public int SchemaVersion { get; init; }
    public string? ScenarioId { get; init; }
    public string? TargetChannel { get; init; }
    public long? Seed { get; init; }
    public List<string>? RequestedCapabilities { get; init; }
    public JsonElement InitialState { get; init; }
    public List<OrderedScenarioInput>? OrderedInputs { get; init; }
    public string? ExpectedEvidenceLevel { get; init; }
}

internal sealed class OrderedScenarioInput
{
    public string? EventId { get; init; }
    public string? SourceId { get; init; }
    public string? Phase { get; init; }
    public int? Order { get; init; }
    public string? Lane { get; init; }
    public string? Granularity { get; init; }
    public long? Amount { get; init; }
}

internal sealed class OrderedObservedEvent
{
    public string? EventId { get; init; }
    public string? SourceId { get; init; }
    public string? Phase { get; init; }
    public int? Order { get; init; }
    public string? Lane { get; init; }
    public string? Granularity { get; init; }
    public long? Amount { get; init; }
    public string? Status { get; init; }
    public string? ReasonCode { get; init; }
    public string? ProviderDetail { get; init; }
}

internal sealed class NativeObservation
{
    public string? ScenarioId { get; init; }
    public string? Status { get; init; }
    public List<OrderedObservedEvent>? Events { get; init; }
    public long? BlockableTotal { get; init; }
    public long? DirectHpLossTotal { get; init; }
    public List<UnsupportedObservation>? Unsupported { get; init; }
    public ObservationMetadata? Metadata { get; init; }
    public string? RawProviderOutputHash { get; init; }
}

internal sealed class ObservationMetadata
{
    public int SchemaVersion { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderVersion { get; init; }
    public string? SourceRevision { get; init; }
    public bool? SourceDirty { get; init; }
    public string? GameChannel { get; init; }
    public string? GameVersion { get; init; }
    public string? GameCommit { get; init; }
    public string? GameAssemblySha256 { get; init; }
    public string? AdapterVersion { get; init; }
    public string? ProviderArtifactSha256 { get; init; }
    public string? EvidenceLevel { get; init; }
    public bool? RuntimeVerified { get; init; }
    public long? Seed { get; init; }
    public string? RunId { get; init; }
    public string? GeneratedAtUtc { get; init; }
    public string? CapabilityManifestSha256 { get; init; }
    public string? UnsupportedRegistrySha256 { get; init; }
}

internal sealed class UnsupportedObservation
{
    public string? Scope { get; init; }
    public string? ReasonCode { get; init; }
    public string? ProviderMechanismId { get; init; }
    public string? Detail { get; init; }
    public bool? FailClosed { get; init; }
}

internal sealed record ExternalObservationLoadResult<T>(T? Value, string? Error)
    where T : class
{
    public bool Success => Value is not null && Error is null;
}

internal sealed record ExternalObservationValidationResult(
    IReadOnlyList<string> Errors,
    bool IsValid,
    bool IsComparable,
    string Disposition);

internal static class ExternalObservationFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ExternalObservationLoadResult<ForecastScenario> LoadScenario(string fileName) =>
        Load<ForecastScenario>(fileName);

    public static ExternalObservationLoadResult<NativeObservation> LoadObservation(string fileName) =>
        Load<NativeObservation>(fileName);

    internal static string SerializeForComparison<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static ExternalObservationLoadResult<T> Load<T>(string fileName)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return new(null, "fixture.fileName.invalid");
        }

        var path = Path.Combine(
            IdentityContractFixture.RepositoryRoot,
            "tests",
            "DamageForecast.ContractTests",
            "fixtures",
            "external-observation",
            fileName);
        if (!File.Exists(path))
        {
            return new(null, "fixture.notFound");
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            return value is null
                ? new(null, "fixture.json.null")
                : new(value, null);
        }
        catch (JsonException)
        {
            return new(null, "fixture.json.invalid");
        }
        catch (NotSupportedException)
        {
            return new(null, "fixture.json.unsupported");
        }
    }
}

internal static class ExternalObservationContractValidator
{
    private const int SupportedSchemaVersion = 1;

    private static readonly HashSet<string> TargetChannels =
        new(["stable", "beta"], StringComparer.Ordinal);

    private static readonly HashSet<string> Lanes =
        new(["Blockable", "DirectHpLoss", "NonDamage"], StringComparer.Ordinal);

    private static readonly HashSet<string> Granularities =
        new(["SingleEvent", "PerHit", "Aggregate"], StringComparer.Ordinal);

    private static readonly HashSet<string> ObservationStatuses =
        new(["Complete", "Partial", "Unsupported", "ProviderFailure"], StringComparer.Ordinal);

    private static readonly HashSet<string> EventStatuses =
        new(["Observed", "Unsupported", "Unavailable"], StringComparer.Ordinal);

    private static readonly Regex StableIdPattern =
        new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);

    private static readonly Regex SourceRevisionPattern =
        new("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);

    private static readonly Regex GameCommitPattern =
        new("^[0-9a-fA-F]{8,40}$", RegexOptions.CultureInvariant);

    private static readonly Regex Sha256Pattern =
        new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

    public static ExternalObservationValidationResult Validate(
        ForecastScenario? scenario,
        NativeObservation? observation)
    {
        var errors = new List<string>();
        if (scenario is null)
        {
            errors.Add("scenario.missing");
        }
        else
        {
            ValidateScenario(scenario, errors);
        }

        if (observation is null)
        {
            errors.Add("observation.missing");
        }
        else if (scenario is not null)
        {
            ValidateObservation(scenario, observation, errors);
        }

        if (errors.Count > 0)
        {
            return new(errors, false, false, "Invalid");
        }

        var comparable = observation!.Status == "Complete";
        return new(
            errors,
            true,
            comparable,
            comparable ? "Comparable" : observation.Status ?? "Invalid");
    }

    public static IReadOnlyList<string> ValidateDeterminism(
        NativeObservation? first,
        NativeObservation? second)
    {
        if (first is null || second is null)
        {
            return ["determinism.observation.missing"];
        }

        var firstFingerprint = DeterminismFingerprint(first);
        var secondFingerprint = DeterminismFingerprint(second);
        return string.Equals(firstFingerprint, secondFingerprint, StringComparison.Ordinal)
            ? []
            : ["determinism.output.mismatch"];
    }

    private static void ValidateScenario(ForecastScenario scenario, ICollection<string> errors)
    {
        if (scenario.SchemaVersion != SupportedSchemaVersion)
            errors.Add("scenario.schemaVersion.unsupported");
        if (string.IsNullOrWhiteSpace(scenario.ScenarioId)
            || !StableIdPattern.IsMatch(scenario.ScenarioId))
            errors.Add("scenario.scenarioId.invalid");
        if (scenario.TargetChannel is null || !TargetChannels.Contains(scenario.TargetChannel))
            errors.Add("scenario.targetChannel.invalid");
        if (!scenario.Seed.HasValue)
            errors.Add("scenario.seed.missing");
        if (scenario.RequestedCapabilities is not { Count: > 0 })
        {
            errors.Add("scenario.requestedCapabilities.missing");
        }
        else
        {
            if (scenario.RequestedCapabilities.Any(string.IsNullOrWhiteSpace))
                errors.Add("scenario.requestedCapabilities.invalid");
            if (scenario.RequestedCapabilities.Distinct(StringComparer.Ordinal).Count()
                != scenario.RequestedCapabilities.Count)
                errors.Add("scenario.requestedCapabilities.duplicate");
        }
        if (scenario.InitialState.ValueKind != JsonValueKind.Object)
            errors.Add("scenario.initialState.invalid");
        if (scenario.OrderedInputs is null)
        {
            errors.Add("scenario.orderedInputs.missing");
        }
        else
        {
            ValidateScenarioInputs(scenario.OrderedInputs, errors);
        }
        if (scenario.ExpectedEvidenceLevel != "L2")
            errors.Add("scenario.expectedEvidenceLevel.invalid");
    }

    private static void ValidateScenarioInputs(
        IReadOnlyList<OrderedScenarioInput> inputs,
        ICollection<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var positions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            ValidateOrderedCore(
                "scenario.input",
                input.EventId,
                input.SourceId,
                input.Phase,
                input.Order,
                input.Lane,
                input.Granularity,
                input.Amount,
                ids,
                positions,
                errors);
        }
    }

    private static void ValidateObservation(
        ForecastScenario scenario,
        NativeObservation observation,
        ICollection<string> errors)
    {
        if (!string.Equals(observation.ScenarioId, scenario.ScenarioId, StringComparison.Ordinal))
            errors.Add("observation.scenarioId.mismatch");
        if (observation.Status is null || !ObservationStatuses.Contains(observation.Status))
            errors.Add("observation.status.invalid");
        if (observation.Events is null)
        {
            errors.Add("observation.events.missing");
        }
        else
        {
            ValidateObservedEvents(observation.Events, errors);
        }
        if (observation.Unsupported is null)
        {
            errors.Add("observation.unsupported.missing");
        }
        else
        {
            ValidateUnsupported(observation.Unsupported, errors);
        }
        if (observation.Metadata is null)
        {
            errors.Add("observation.metadata.missing");
        }
        else
        {
            ValidateMetadata(scenario, observation.Metadata, errors);
        }
        ValidateSha256(
            observation.RawProviderOutputHash,
            "observation.rawProviderOutputHash.invalid",
            errors);

        if (observation.Status == "Complete")
        {
            ValidateCompleteObservation(observation, errors);
        }
        else if (observation.Status is "Partial" or "Unsupported" or "ProviderFailure")
        {
            if (observation.BlockableTotal.HasValue || observation.DirectHpLossTotal.HasValue)
                errors.Add("observation.partialTotals.present");
            if (observation.Unsupported is not { Count: > 0 })
                errors.Add("observation.failClosedReason.missing");
        }
    }

    private static void ValidateObservedEvents(
        IReadOnlyList<OrderedObservedEvent> events,
        ICollection<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var positions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observedEvent in events)
        {
            ValidateOrderedCore(
                "observation.event",
                observedEvent.EventId,
                observedEvent.SourceId,
                observedEvent.Phase,
                observedEvent.Order,
                observedEvent.Lane,
                observedEvent.Granularity,
                observedEvent.Amount,
                ids,
                positions,
                errors);
            if (observedEvent.Status is null || !EventStatuses.Contains(observedEvent.Status))
                errors.Add("observation.event.status.invalid");
            if (string.IsNullOrWhiteSpace(observedEvent.ReasonCode))
                errors.Add("observation.event.reasonCode.missing");
        }
    }

    private static void ValidateOrderedCore(
        string prefix,
        string? eventId,
        string? sourceId,
        string? phase,
        int? order,
        string? lane,
        string? granularity,
        long? amount,
        ISet<string> ids,
        ISet<string> positions,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(eventId) || !StableIdPattern.IsMatch(eventId))
        {
            errors.Add($"{prefix}.eventId.invalid");
        }
        else if (!ids.Add(eventId))
        {
            errors.Add($"{prefix}.eventId.duplicate");
        }
        if (string.IsNullOrWhiteSpace(sourceId) || !StableIdPattern.IsMatch(sourceId))
            errors.Add($"{prefix}.sourceId.invalid");
        if (string.IsNullOrWhiteSpace(phase))
            errors.Add($"{prefix}.phase.missing");
        if (!order.HasValue || order.Value < 0)
        {
            errors.Add($"{prefix}.order.invalid");
        }
        else if (!string.IsNullOrWhiteSpace(phase) && !positions.Add($"{phase}\u001f{order.Value}"))
        {
            errors.Add($"{prefix}.order.duplicate");
        }
        if (lane is null || !Lanes.Contains(lane))
            errors.Add($"{prefix}.lane.invalid");
        if (granularity is null || !Granularities.Contains(granularity))
            errors.Add($"{prefix}.granularity.invalid");
        if (!amount.HasValue || amount.Value < 0)
            errors.Add($"{prefix}.amount.invalid");
    }

    private static void ValidateUnsupported(
        IReadOnlyList<UnsupportedObservation> unsupported,
        ICollection<string> errors)
    {
        foreach (var entry in unsupported)
        {
            if (string.IsNullOrWhiteSpace(entry.Scope))
                errors.Add("observation.unsupported.scope.missing");
            if (string.IsNullOrWhiteSpace(entry.ReasonCode))
                errors.Add("observation.unsupported.reasonCode.missing");
            if (entry.FailClosed != true)
                errors.Add("observation.unsupported.failClosed.required");
        }
    }

    private static void ValidateMetadata(
        ForecastScenario scenario,
        ObservationMetadata metadata,
        ICollection<string> errors)
    {
        if (metadata.SchemaVersion != scenario.SchemaVersion)
            errors.Add("metadata.schemaVersion.mismatch");
        if (string.IsNullOrWhiteSpace(metadata.ProviderId))
            errors.Add("metadata.providerId.missing");
        if (string.IsNullOrWhiteSpace(metadata.ProviderVersion))
            errors.Add("metadata.providerVersion.missing");
        if (metadata.SourceRevision is null || !SourceRevisionPattern.IsMatch(metadata.SourceRevision))
            errors.Add("metadata.sourceRevision.invalid");
        if (metadata.SourceDirty != false)
            errors.Add("metadata.sourceDirty.notClean");
        if (!string.Equals(metadata.GameChannel, scenario.TargetChannel, StringComparison.Ordinal))
            errors.Add("metadata.gameChannel.mismatch");
        if (string.IsNullOrWhiteSpace(metadata.GameVersion))
            errors.Add("metadata.gameVersion.missing");
        if (metadata.GameCommit is null || !GameCommitPattern.IsMatch(metadata.GameCommit))
            errors.Add("metadata.gameCommit.invalid");
        ValidateSha256(metadata.GameAssemblySha256, "metadata.gameAssemblySha256.invalid", errors);
        if (string.IsNullOrWhiteSpace(metadata.AdapterVersion))
            errors.Add("metadata.adapterVersion.missing");
        ValidateSha256(metadata.ProviderArtifactSha256, "metadata.providerArtifactSha256.invalid", errors);
        if (metadata.EvidenceLevel is not ("L2" or "L2-max"))
            errors.Add("metadata.evidenceLevel.invalid");
        if (metadata.RuntimeVerified != false)
            errors.Add("metadata.runtimeVerified.mustRemainFalse");
        if (metadata.Seed != scenario.Seed)
            errors.Add("metadata.seed.mismatch");
        if (string.IsNullOrWhiteSpace(metadata.RunId))
            errors.Add("metadata.runId.missing");
        if (!DateTimeOffset.TryParse(
                metadata.GeneratedAtUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out _))
            errors.Add("metadata.generatedAtUtc.invalid");
        ValidateSha256(
            metadata.CapabilityManifestSha256,
            "metadata.capabilityManifestSha256.invalid",
            errors);
        ValidateSha256(
            metadata.UnsupportedRegistrySha256,
            "metadata.unsupportedRegistrySha256.invalid",
            errors);
    }

    private static void ValidateCompleteObservation(
        NativeObservation observation,
        ICollection<string> errors)
    {
        if (!observation.BlockableTotal.HasValue || !observation.DirectHpLossTotal.HasValue)
        {
            errors.Add("observation.completeTotals.missing");
            return;
        }
        if (observation.Unsupported is { Count: > 0 })
            errors.Add("observation.completeUnsupported.present");
        if (observation.Events?.Any(item => item.Status != "Observed") == true)
            errors.Add("observation.completeEvent.notObserved");

        try
        {
            var blockable = observation.Events?
                .Where(item => item.Status == "Observed" && item.Lane == "Blockable")
                .Aggregate(0L, (total, item) => checked(total + item.Amount!.Value)) ?? 0;
            var direct = observation.Events?
                .Where(item => item.Status == "Observed" && item.Lane == "DirectHpLoss")
                .Aggregate(0L, (total, item) => checked(total + item.Amount!.Value)) ?? 0;
            if (blockable != observation.BlockableTotal)
                errors.Add("observation.blockableTotal.mismatch");
            if (direct != observation.DirectHpLossTotal)
                errors.Add("observation.directHpLossTotal.mismatch");
        }
        catch (OverflowException)
        {
            errors.Add("observation.totals.overflow");
        }
    }

    private static void ValidateSha256(
        string? value,
        string error,
        ICollection<string> errors)
    {
        if (value is null || !Sha256Pattern.IsMatch(value))
            errors.Add(error);
    }

    private static string DeterminismFingerprint(NativeObservation observation)
    {
        var metadata = observation.Metadata;
        return ExternalObservationFixture.SerializeForComparison(new
        {
            observation.ScenarioId,
            observation.Status,
            observation.Events,
            observation.BlockableTotal,
            observation.DirectHpLossTotal,
            observation.Unsupported,
            observation.RawProviderOutputHash,
            Metadata = metadata is null
                ? null
                : new
                {
                    metadata.SchemaVersion,
                    metadata.ProviderId,
                    metadata.ProviderVersion,
                    metadata.SourceRevision,
                    metadata.SourceDirty,
                    metadata.GameChannel,
                    metadata.GameVersion,
                    metadata.GameCommit,
                    metadata.GameAssemblySha256,
                    metadata.AdapterVersion,
                    metadata.ProviderArtifactSha256,
                    metadata.EvidenceLevel,
                    metadata.RuntimeVerified,
                    metadata.Seed,
                    metadata.CapabilityManifestSha256,
                    metadata.UnsupportedRegistrySha256
                }
        });
    }
}
