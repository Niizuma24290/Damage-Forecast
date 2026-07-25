using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal sealed record DifferentialScenarioMatrix(
    int SchemaVersion,
    string? Status,
    string? Gate,
    string? ExecutionMode,
    string? ProviderId,
    string? SourceRevision,
    string? SourceTree,
    bool RuntimeVerified,
    bool ProcessExecutionAllowed,
    bool OrdinaryGuardrailRequired,
    bool ProductionChangeOnMismatchAllowed,
    IReadOnlyList<DifferentialScenarioMatrixRow>? Rows);

internal sealed record DifferentialScenarioMatrixRow(
    string? Id,
    string? ScenarioGroup,
    string? Title,
    bool Required,
    string? Disposition,
    string? ComparisonReadiness,
    IReadOnlyList<string>? TargetChannels,
    IReadOnlyList<string>? ForecastContractIds,
    IReadOnlyList<string>? ProviderScenarioIds,
    IReadOnlyList<string>? EvidenceRefs,
    string? EvidenceLevel,
    bool RuntimeVerified,
    IReadOnlyList<string>? BlockingReasonCodes,
    DifferentialScenarioEvaluationBinding? Evaluation);

internal sealed record DifferentialScenarioEvaluationBinding(
    string? Kind,
    string? ScenarioFixture,
    IReadOnlyList<string>? ObservationFixtures);

internal sealed record DifferentialScenarioMatrixValidationResult(
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

internal static class DifferentialScenarioMatrixFixture
{
    private const string FixturePath =
        "tests/DamageForecast.ContractTests/fixtures/external-observation/"
        + "differential-scenario-matrix.v1.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly Lazy<DifferentialScenarioMatrix> Loaded = new(Load);

    public static DifferentialScenarioMatrix Matrix => Loaded.Value;

    private static DifferentialScenarioMatrix Load() =>
        JsonSerializer.Deserialize<DifferentialScenarioMatrix>(
            IdentityContractFixture.Read(FixturePath),
            JsonOptions)
        ?? throw new InvalidOperationException(
            "Differential scenario matrix deserialized to null.");
}

internal static class DifferentialScenarioMatrixValidator
{
    internal const string ApprovedProviderRevision =
        "42396191e4bd66ca8ab27cd9b9b9f4f537966978";

    private static readonly HashSet<string> RequiredScenarioGroups =
        new(
        [
            "current-block-only",
            "power-relic-block-order",
            "direct-hp-loss",
            "hp-loss-modifiers",
            "poison",
            "status-curse-turn-end",
            "discard-retain-ethereal",
            "unknown-unsupported",
            "same-seed-repeat",
            "stable-beta-difference"
        ], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedDispositions =
        new(
        [
            "Comparable",
            "Expected version difference",
            "Tool unsupported",
            "Tool suspect",
            "Forecast suspect",
            "Needs runtime evidence",
            "Provider out of scope"
        ], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedReadiness =
        new(["Ready", "Blocked", "Candidate", "BoundaryOnly", "OutOfScope"], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedChannels =
        new(["stable", "beta"], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedEvidenceLevels =
        new(["L2", "L2-boundary", "L2-reference-only", "None"], StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedEvaluationKinds =
        new(["SemanticMatch", "UnsupportedBoundary", "Determinism"], StringComparer.Ordinal);

    private static readonly Dictionary<string, string> ReviewedComparableMappings =
        new(StringComparer.Ordinal)
        {
            ["turn-end.burn.blockable.v1"] = "stable",
            ["turn-end.doubt.power.v1"] = "beta"
        };

    private static readonly Regex MatrixIdPattern =
        new("^DFM-[0-9]{3}$", RegexOptions.CultureInvariant);

    private static readonly Regex StableIdPattern =
        new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant);

    private static readonly Regex ContractIdPattern =
        new("^[A-Z][A-Z0-9]*-[0-9]{3}$", RegexOptions.CultureInvariant);

    private static readonly Regex FixtureNamePattern =
        new("^[a-z0-9][a-z0-9.-]*\\.json$", RegexOptions.CultureInvariant);

    private static readonly Regex SourceRevisionPattern =
        new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

    public static DifferentialScenarioMatrixValidationResult Validate(
        DifferentialScenarioMatrix? matrix,
        IReadOnlySet<string>? knownContractIds = null)
    {
        var errors = new List<string>();
        if (matrix is null)
        {
            return new(["matrix.missing"]);
        }

        ValidateEnvelope(matrix, errors);
        ValidateRows(matrix.Rows, errors);
        if (matrix.Rows is not null && knownContractIds is not null)
            ValidateReferences(matrix.Rows, knownContractIds, errors);
        return new(errors);
    }

    public static IReadOnlySet<string> RequiredGroups() => RequiredScenarioGroups;

    private static void ValidateEnvelope(
        DifferentialScenarioMatrix matrix,
        ICollection<string> errors)
    {
        if (matrix.SchemaVersion != 1)
            errors.Add("matrix.schemaVersion.unsupported");
        if (matrix.Status != "df-s3b-offline-manifest")
            errors.Add("matrix.status.invalid");
        if (matrix.Gate != "DF-S3B")
            errors.Add("matrix.gate.invalid");
        if (matrix.ExecutionMode != "OfflineOnly")
            errors.Add("matrix.executionMode.mustBeOfflineOnly");
        if (matrix.ProviderId != "sts2sim-explicit-process")
            errors.Add("matrix.providerId.invalid");
        if (matrix.SourceRevision is null
            || !SourceRevisionPattern.IsMatch(matrix.SourceRevision)
            || matrix.SourceRevision != ApprovedProviderRevision)
            errors.Add("matrix.sourceRevision.notApproved");
        if (matrix.SourceTree != "clean")
            errors.Add("matrix.sourceTree.mustBeClean");
        if (matrix.RuntimeVerified)
            errors.Add("matrix.runtimeVerified.mustRemainFalse");
        if (matrix.ProcessExecutionAllowed)
            errors.Add("matrix.processExecutionAllowed.mustRemainFalse");
        if (matrix.OrdinaryGuardrailRequired)
            errors.Add("matrix.ordinaryGuardrailRequired.mustRemainFalse");
        if (matrix.ProductionChangeOnMismatchAllowed)
            errors.Add("matrix.productionChangeOnMismatchAllowed.mustRemainFalse");
    }

    private static void ValidateRows(
        IReadOnlyList<DifferentialScenarioMatrixRow>? rows,
        ICollection<string> errors)
    {
        if (rows is not { Count: > 0 })
        {
            errors.Add("matrix.rows.missing");
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row is null)
            {
                errors.Add("matrix.row.missing");
                continue;
            }
            ValidateRow(row, ids, errors);
        }

        var presentRequiredGroups = rows
            .Where(row => row is not null && row.Required && row.ScenarioGroup is not null)
            .Select(row => row.ScenarioGroup!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var missing in RequiredScenarioGroups.Except(presentRequiredGroups))
            errors.Add($"matrix.requiredGroup.missing:{missing}");
    }

    private static void ValidateReferences(
        IReadOnlyList<DifferentialScenarioMatrixRow> rows,
        IReadOnlySet<string> knownContractIds,
        ICollection<string> errors)
    {
        foreach (var row in rows)
        {
            if (row is null)
                continue;
            foreach (var contractId in (row.ForecastContractIds ?? [])
                         .Concat(row.EvidenceRefs ?? []))
            {
                if (!knownContractIds.Contains(contractId))
                    errors.Add($"{row.Id ?? "row"}.contractReference.unknown:{contractId}");
            }
        }
    }

    private static void ValidateRow(
        DifferentialScenarioMatrixRow row,
        ISet<string> ids,
        ICollection<string> errors)
    {
        var prefix = row.Id is { Length: > 0 } ? row.Id : "row";
        if (row.Id is null || !MatrixIdPattern.IsMatch(row.Id))
            errors.Add($"{prefix}.id.invalid");
        else if (!ids.Add(row.Id))
            errors.Add($"{prefix}.id.duplicate");
        if (row.ScenarioGroup is null || !StableIdPattern.IsMatch(row.ScenarioGroup))
            errors.Add($"{prefix}.scenarioGroup.invalid");
        if (string.IsNullOrWhiteSpace(row.Title))
            errors.Add($"{prefix}.title.missing");
        if (row.Disposition is null || !AllowedDispositions.Contains(row.Disposition))
            errors.Add($"{prefix}.disposition.invalid");
        if (row.ComparisonReadiness is null || !AllowedReadiness.Contains(row.ComparisonReadiness))
            errors.Add($"{prefix}.comparisonReadiness.invalid");
        if (row.EvidenceLevel is null || !AllowedEvidenceLevels.Contains(row.EvidenceLevel))
            errors.Add($"{prefix}.evidenceLevel.invalid");
        if (row.RuntimeVerified)
            errors.Add($"{prefix}.runtimeVerified.mustRemainFalse");

        ValidateStableList(prefix, "targetChannels", row.TargetChannels, AllowedChannels, errors);
        ValidateContractIds(prefix, "forecastContractIds", row.ForecastContractIds, errors);
        ValidateStableList(prefix, "providerScenarioIds", row.ProviderScenarioIds, null, errors);
        ValidateContractIds(prefix, "evidenceRefs", row.EvidenceRefs, errors);
        ValidateStableList(prefix, "blockingReasonCodes", row.BlockingReasonCodes, null, errors);

        ValidateDisposition(row, prefix, errors);
        ValidateEvaluationBinding(row, prefix, errors);
    }

    private static void ValidateDisposition(
        DifferentialScenarioMatrixRow row,
        string prefix,
        ICollection<string> errors)
    {
        var blockers = row.BlockingReasonCodes ?? [];
        var providerScenarios = row.ProviderScenarioIds ?? [];
        var forecastContracts = row.ForecastContractIds ?? [];
        var channels = row.TargetChannels ?? [];

        switch (row.Disposition)
        {
            case "Comparable" when row.ComparisonReadiness == "Ready":
                if (blockers.Count != 0)
                    errors.Add($"{prefix}.comparable.blockersPresent");
                if (forecastContracts.Count == 0)
                    errors.Add($"{prefix}.comparable.forecastEvidenceMissing");
                if (providerScenarios.Count == 0)
                    errors.Add($"{prefix}.comparable.providerScenarioMissing");
                if (row.EvidenceLevel != "L2")
                    errors.Add($"{prefix}.comparable.evidenceLevel.invalid");
                ValidateReviewedComparableMappings(prefix, providerScenarios, channels, errors);
                break;

            case "Comparable" when row.ComparisonReadiness == "BoundaryOnly":
                if (row.ScenarioGroup is not ("unknown-unsupported" or "same-seed-repeat"))
                    errors.Add($"{prefix}.boundaryOnly.group.invalid");
                if (row.EvidenceLevel != "L2-boundary")
                    errors.Add($"{prefix}.boundaryOnly.evidenceLevel.invalid");
                break;

            case "Tool unsupported":
                RequireBlockedWithReasons(row, prefix, errors);
                if (providerScenarios.Count != 0)
                    errors.Add($"{prefix}.toolUnsupported.providerScenarioPresent");
                break;

            case "Needs runtime evidence":
                RequireBlockedWithReasons(row, prefix, errors);
                if (!blockers.Contains(
                        "runtime.live-game-parity.unverified",
                        StringComparer.Ordinal))
                    errors.Add($"{prefix}.runtimeEvidence.reasonMissing");
                break;

            case "Expected version difference":
                if (row.ComparisonReadiness != "Candidate")
                    errors.Add($"{prefix}.versionDifference.mustRemainCandidate");
                if (!channels.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(["stable", "beta"]))
                    errors.Add($"{prefix}.versionDifference.targets.invalid");
                if (providerScenarios.Count != 0)
                    errors.Add($"{prefix}.versionDifference.unpairedScenarioClaim");
                if (!blockers.Contains(
                        "comparison.same-scenario-cross-target-pair.missing",
                        StringComparer.Ordinal))
                    errors.Add($"{prefix}.versionDifference.pairBlockerMissing");
                break;

            case "Provider out of scope":
                if (row.Required)
                    errors.Add($"{prefix}.providerOutOfScope.mustBeOptional");
                if (row.ComparisonReadiness != "OutOfScope" || blockers.Count == 0)
                    errors.Add($"{prefix}.providerOutOfScope.boundaryInvalid");
                break;

            case "Tool suspect":
            case "Forecast suspect":
                if (row.ComparisonReadiness != "Candidate"
                    || (row.EvidenceRefs?.Count ?? 0) == 0)
                    errors.Add($"{prefix}.suspect.requiresEvidence");
                break;

            case "Comparable":
                errors.Add($"{prefix}.comparable.readiness.invalid");
                break;
        }
    }

    private static void ValidateEvaluationBinding(
        DifferentialScenarioMatrixRow row,
        string prefix,
        ICollection<string> errors)
    {
        var binding = row.Evaluation;
        var needsBinding = row.ComparisonReadiness is "Ready" or "BoundaryOnly";
        if (!needsBinding)
        {
            if (binding is not null)
                errors.Add($"{prefix}.evaluation.notAllowed");
            return;
        }

        if (binding is null)
        {
            errors.Add($"{prefix}.evaluation.missing");
            return;
        }
        if (binding.Kind is null || !AllowedEvaluationKinds.Contains(binding.Kind))
            errors.Add($"{prefix}.evaluation.kind.invalid");
        if (binding.ScenarioFixture is null
            || !FixtureNamePattern.IsMatch(binding.ScenarioFixture))
            errors.Add($"{prefix}.evaluation.scenarioFixture.invalid");
        if (binding.ObservationFixtures is not { Count: > 0 })
        {
            errors.Add($"{prefix}.evaluation.observationFixtures.missing");
            return;
        }
        if (binding.ObservationFixtures.Any(
                fixture => fixture is null || !FixtureNamePattern.IsMatch(fixture)))
            errors.Add($"{prefix}.evaluation.observationFixtures.invalid");
        if (binding.ObservationFixtures.Distinct(StringComparer.Ordinal).Count()
            != binding.ObservationFixtures.Count)
            errors.Add($"{prefix}.evaluation.observationFixtures.duplicate");

        var expectedKind = row.ComparisonReadiness == "Ready"
            ? "SemanticMatch"
            : row.ScenarioGroup == "unknown-unsupported"
                ? "UnsupportedBoundary"
                : row.ScenarioGroup == "same-seed-repeat"
                    ? "Determinism"
                    : null;
        if (binding.Kind != expectedKind)
            errors.Add($"{prefix}.evaluation.kind.mismatch");
        var expectedObservationCount = binding.Kind == "Determinism" ? 2 : 1;
        if (binding.ObservationFixtures.Count != expectedObservationCount)
            errors.Add($"{prefix}.evaluation.observationCount.invalid");
    }

    private static void ValidateReviewedComparableMappings(
        string prefix,
        IReadOnlyList<string> providerScenarios,
        IReadOnlyList<string> channels,
        ICollection<string> errors)
    {
        if (providerScenarios.Count != 1
            || channels.Count != 1
            || !ReviewedComparableMappings.TryGetValue(
                providerScenarios[0],
                out var reviewedChannel)
            || channels[0] != reviewedChannel)
        {
            errors.Add($"{prefix}.comparable.mappingNotReviewed");
        }
    }

    private static void RequireBlockedWithReasons(
        DifferentialScenarioMatrixRow row,
        string prefix,
        ICollection<string> errors)
    {
        if (row.ComparisonReadiness != "Blocked")
            errors.Add($"{prefix}.blocked.readiness.invalid");
        if ((row.BlockingReasonCodes?.Count ?? 0) == 0)
            errors.Add($"{prefix}.blocked.reasonMissing");
    }

    private static void ValidateStableList(
        string prefix,
        string field,
        IReadOnlyList<string>? values,
        IReadOnlySet<string>? allowlist,
        ICollection<string> errors)
    {
        if (values is null)
        {
            errors.Add($"{prefix}.{field}.missing");
            return;
        }
        if (field == "targetChannels" && values.Count == 0)
            errors.Add($"{prefix}.{field}.empty");
        if (values.Any(value => string.IsNullOrWhiteSpace(value)
                || !StableIdPattern.IsMatch(value)))
            errors.Add($"{prefix}.{field}.invalid");
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            errors.Add($"{prefix}.{field}.duplicate");
        if (allowlist is not null && values.Any(value => !allowlist.Contains(value)))
            errors.Add($"{prefix}.{field}.unsupported");
    }

    private static void ValidateContractIds(
        string prefix,
        string field,
        IReadOnlyList<string>? values,
        ICollection<string> errors)
    {
        if (values is null)
        {
            errors.Add($"{prefix}.{field}.missing");
            return;
        }
        if (values.Any(value => string.IsNullOrWhiteSpace(value)
                || !ContractIdPattern.IsMatch(value)))
            errors.Add($"{prefix}.{field}.invalid");
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            errors.Add($"{prefix}.{field}.duplicate");
    }
}
