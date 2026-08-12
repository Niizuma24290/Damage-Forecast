using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DamageForecast;
using DamageForecast.Settings;

internal static class IdentityContractFixture
{
    private static readonly string[] Statuses =
        ["current-baseline", "approved-migration", "migration-in-progress", "active-migrated"];
    private static readonly string[] DecisionIds =
        Enumerable.Range(1, 14).Select(index => $"G6-D{index:00}").ToArray();
    private static readonly Lazy<string> Root = new(FindRepositoryRoot);
    private static readonly Lazy<TechnicalIdentityContract> Loaded = new(LoadContract);

    public static string RepositoryRoot => Root.Value;
    public static TechnicalIdentityContract Contract => Loaded.Value;
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, Normalize(relativePath)));
    public static JsonDocument ReadManifest() => JsonDocument.Parse(Read(Contract.Packaging.ManifestPath));
    public static XDocument ReadProject() =>
        XDocument.Load(Path.Combine(RepositoryRoot, Normalize(Contract.ActiveIdentity.ProjectPath)));

    public static IReadOnlyList<string> GetTrackedFiles()
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("ls-files");
        info.ArgumentList.Add("-z");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Unable to start git ls-files.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files failed: {stderr}");
        }
        return stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/')).ToArray();
    }

    public static IReadOnlyList<string> ValidateMigrationStructure() => ValidateMigrationStructure(Contract);

    public static IReadOnlyList<string> ValidateMigrationStructure(TechnicalIdentityContract contract)
    {
        var errors = new List<string>();
        var approved = contract.Status is "approved-migration" or "migration-in-progress" or "active-migrated";
        if (contract.SchemaVersion != 2) errors.Add($"unsupported schema version: {contract.SchemaVersion}");
        if (!Statuses.Contains(contract.Status, StringComparer.Ordinal)) errors.Add($"unknown contract status: {contract.Status}");
        if (approved && new[] { contract.Approval.Gate, contract.Approval.ApprovedAt, contract.Approval.Evidence }
            .Any(string.IsNullOrWhiteSpace)) errors.Add("approved contract is missing approval evidence");

        var decisions = contract.ApprovedDecisions.Select(decision => decision.Id).ToArray();
        foreach (var duplicate in decisions.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
            errors.Add($"duplicate approved decision: {duplicate.Key}");
        if (approved)
        {
            errors.AddRange(DecisionIds.Except(decisions, StringComparer.Ordinal).Select(id => $"missing approved decision: {id}"));
            errors.AddRange(decisions.Except(DecisionIds, StringComparer.Ordinal).Select(id => $"unknown approved decision: {id}"));
        }
        foreach (var decision in contract.ApprovedDecisions.Where(decision =>
            string.IsNullOrWhiteSpace(decision.ApprovedValue) || string.IsNullOrWhiteSpace(decision.ApprovalEvidence)))
            errors.Add($"incomplete approved decision: {decision.Id}");

        ValidatePersistence(contract, errors);
        ValidateInstallTool(contract, errors);
        ValidatePublishValidation(contract, errors);
        ValidateActiveLifecycle(contract, errors);
        ValidateLegacyAllowlist(contract, errors);
        var expected = ExpectedFields(contract);
        var approvedDecisionIds = decisions.ToHashSet(StringComparer.Ordinal);
        foreach (var field in contract.MigrationFields)
        {
            if (!expected.Remove(field.Field, out var values))
            {
                errors.Add($"unexpected or duplicate migration field: {field.Field}");
                continue;
            }
            if (field.LegacyValue != values.Legacy) errors.Add($"legacy value mismatch: {field.Field}");
            if (approved && field.TargetValue != values.Target) errors.Add($"approved target mismatch: {field.Field}");
            if (new[]
                {
                    field.PersistenceImpact, field.InstalledArtifactImpact, field.CompatibilityOwner,
                    field.CompatibilityLifetime, field.RollbackBehavior, field.StaticVerification,
                    field.BuildVerification, field.RuntimeVerification, field.ApprovalEvidence
                }.Any(string.IsNullOrWhiteSpace)) errors.Add($"incomplete migration evidence columns: {field.Field}");
            if (field.DecisionIds.Count == 0) errors.Add($"missing decision reference: {field.Field}");
            errors.AddRange(field.DecisionIds.Where(id => !approvedDecisionIds.Contains(id))
                .Select(id => $"unknown decision reference {id}: {field.Field}"));

            if (contract.Status == "current-baseline")
            {
                if (field.Disposition != "undecided" || field.TargetValue is not null)
                    errors.Add($"baseline contract prematurely chooses target: {field.Field}");
                continue;
            }
            if (field.Disposition == "must-change"
                && (string.IsNullOrWhiteSpace(field.TargetValue) || field.TargetValue == field.LegacyValue))
                errors.Add($"must-change target is empty or unchanged: {field.Field}");
            else if (field.Disposition == "must-remain" && field.TargetValue != field.LegacyValue)
                errors.Add($"must-remain target changed: {field.Field}");
            else if (field.Disposition == "compatibility-alias"
                && (string.IsNullOrWhiteSpace(field.TargetValue) || field.CompatibilityOwner is "" or "none"
                    || field.CompatibilityLifetime is "" or "not-applicable"))
                errors.Add($"compatibility alias is missing target, owner, or lifetime: {field.Field}");
            else if (field.Disposition is not ("must-change" or "must-remain" or "compatibility-alias"))
                errors.Add($"invalid approved migration disposition: {field.Field}");
        }
        errors.AddRange(expected.Keys.Select(field => $"missing migration field: {field}"));
        return errors;
    }

    public static RepositoryIdentitySnapshot CaptureRepositoryIdentity()
    {
        var contract = Contract;
        var active = contract.ActiveIdentity;
        var target = contract.ApprovedTargetIdentity;
        using var manifest = ReadManifest();
        var project = ReadProject();
        var source = Read("src/DamageForecast/MainFile.cs");
        var configType = typeof(DamageForecastBaseLibConfig);
        var configOwner = configType.Namespace?.Split('.')[0] ?? string.Empty;
        var manifestDirectory = Path.GetDirectoryName(Path.Combine(RepositoryRoot, Normalize(contract.Packaging.ManifestPath)))
            ?? throw new InvalidOperationException("Manifest path has no parent directory.");
        var ids = Directory.EnumerateFiles(manifestDirectory, "*.json")
            .Select(path => JsonDocument.Parse(File.ReadAllText(path)))
            .Select(document =>
            {
                using (document) return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            }).ToArray();
        var assembly = project.Descendants("AssemblyName").Single().Value;
        return new(
            manifest.RootElement.GetProperty("id").GetString() ?? "",
            Path.GetFileNameWithoutExtension(contract.Packaging.ManifestPath),
            manifest.RootElement.GetProperty("version").GetString() ?? "",
            assembly,
            assembly + ".dll",
            project.Descendants("RootNamespace").Single().Value,
            Select(Read(contract.Packaging.BuildScriptPath).Replace('\\', '/'), active.ProjectPath, target.ProjectPath),
            Select(Read("scripts/Test-ForecastGuardrails.ps1").Replace('\\', '/'), active.TestProjectPath, target.TestProjectPath),
            CallIdentity(source, "ModConfigRegistry\\.Register", active.ModId, target.ModId),
            CallIdentity(source, "new Harmony", active.ModId, target.ModId),
            Select(source, active.DiagnosticPrefix, target.DiagnosticPrefix),
            InstallIdentity(Read(contract.Packaging.InstallScriptPath), active.InstallDirectoryName, target.InstallDirectoryName),
            configOwner, configOwner + ".cfg", configType.FullName ?? "", contract.Packaging.PublishWhitelist,
            ids.Count(id => id == contract.LegacyIdentity.ModId), ids.Count(id => id == target.ModId));
    }

    public static IReadOnlyList<string> ValidateRepositoryAlignment() =>
        ValidateRepositoryAlignment(Contract, CaptureRepositoryIdentity());

    public static IReadOnlyList<string> ValidateRepositoryAlignment(
        TechnicalIdentityContract contract,
        RepositoryIdentitySnapshot snapshot)
    {
        var errors = new List<string>();
        var expected = contract.ActiveIdentity;
        var pairs = new (string Name, string Expected, string Actual)[]
        {
            ("manifest id", expected.ModId, snapshot.ManifestId), ("manifest stem", expected.ManifestStem, snapshot.ManifestStem),
            ("manifest version", expected.ManifestVersion, snapshot.ManifestVersion), ("assembly name", expected.AssemblyName, snapshot.AssemblyName),
            ("DLL name", expected.DllName, snapshot.DllName), ("root namespace", expected.RootNamespace, snapshot.RootNamespace),
            ("project path", expected.ProjectPath, snapshot.ProjectPath), ("test project path", expected.TestProjectPath, snapshot.TestProjectPath),
            ("BaseLib registration key", expected.BaseLibRegistrationKey, snapshot.BaseLibRegistrationKey),
            ("Harmony owner", expected.HarmonyOwner, snapshot.HarmonyOwner), ("diagnostic prefix", expected.DiagnosticPrefix, snapshot.DiagnosticPrefix),
            ("install directory", expected.InstallDirectoryName, snapshot.InstallDirectoryName),
            ("persistence owner root namespace", expected.PersistenceOwnerRootNamespace, snapshot.PersistenceOwnerRootNamespace),
            ("persistence file", expected.PersistenceFileName, snapshot.PersistenceFileName),
            ("config compatibility type", expected.ConfigCompatibilityType, snapshot.ConfigCompatibilityType)
        };
        errors.AddRange(pairs.Where(pair => pair.Expected != pair.Actual)
            .Select(pair => $"{pair.Name} points at a different contract state: expected={pair.Expected}; actual={pair.Actual}"));
        var whitelist = new[] { expected.DllName, expected.ManifestStem + ".json" }.Order().ToArray();
        if (!snapshot.PublishWhitelist.Order().SequenceEqual(whitelist, StringComparer.Ordinal))
            errors.Add("publish whitelist points at a different contract state");
        var selectedCount = contract.Status == "active-migrated" ? snapshot.TargetManifestCount : snapshot.LegacyManifestCount;
        var inactiveCount = contract.Status == "active-migrated" ? snapshot.LegacyManifestCount : snapshot.TargetManifestCount;
        if (selectedCount != 1 || inactiveCount != 0)
            errors.Add($"manifest identity is duplicate or half-migrated: expected={selectedCount}; inactive={inactiveCount}");
        return errors;
    }

    private static void ValidatePersistence(TechnicalIdentityContract contract, ICollection<string> errors)
    {
        var p = contract.PersistenceContract;
        var active = contract.ActiveIdentity;
        var target = contract.ApprovedTargetIdentity;
        if (p.BaseLibRegistrationRole != "registry-key-only") errors.Add("BaseLib registration role must be registry-key-only");
        var pairs = new[]
        {
            (contract.LegacyIdentity.BaseLibRegistrationKey, p.LegacyRegistrationKey),
            (target.BaseLibRegistrationKey, p.ApprovedTargetRegistrationKey),
            (active.PersistenceOwnerRootNamespace, p.ConfigOwnerRootNamespace), (target.PersistenceOwnerRootNamespace, p.ConfigOwnerRootNamespace),
            (active.PersistenceFileName, p.ConfigFileName), (target.PersistenceFileName, p.ConfigFileName),
            (active.ConfigCompatibilityType, p.ConfigCompatibilityType), (target.ConfigCompatibilityType, p.ConfigCompatibilityType),
            (p.ConfigOwnerRootNamespace + ".cfg", p.ConfigFileName)
        };
        if (pairs.Any(pair => pair.Item1 != pair.Item2)) errors.Add("persistence contract disagrees with active or approved identity");
        if (p.LegacyRegistrationKey == p.ConfigFileName || p.ApprovedTargetRegistrationKey == p.ConfigFileName)
            errors.Add("BaseLib registration key must not be treated as the persistence file");
        if (p.CompatibilityLifetime != "permanent" || new[]
            { p.CompatibilityOwner, p.ContinuityStrategy, p.RollbackStrategy }.Any(string.IsNullOrWhiteSpace))
            errors.Add("permanent persistence compatibility ownership is incomplete");
    }

    private static void ValidateLegacyAllowlist(TechnicalIdentityContract contract, ICollection<string> errors)
    {
        if (contract.Status == "active-migrated" && contract.LegacyAllowlist.Count == 0)
            errors.Add("active-migrated contract is missing legacy allowlist entries");
        foreach (var duplicate in contract.LegacyAllowlist.GroupBy(entry => entry.Token, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
            errors.Add($"duplicate legacy allowlist token: {duplicate.Key}");
        foreach (var entry in contract.LegacyAllowlist)
        {
            if (new[] { entry.Token, entry.Owner, entry.Purpose, entry.Lifetime }.Any(string.IsNullOrWhiteSpace)
                || entry.Locations.Count == 0 || entry.Locations.Any(string.IsNullOrWhiteSpace))
                errors.Add($"incomplete legacy allowlist entry: {entry.Token}");
        }

        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STS2PartyWatch.Settings"] = "permanent",
            ["sts2-party-watch-v2"] = "permanent-detection-and-rollback",
            ["[STS2 Party Watch]"] = "through-v0.2.x"
        };
        foreach (var (token, lifetime) in required)
        {
            var entry = contract.LegacyAllowlist.SingleOrDefault(candidate => candidate.Token == token);
            if (entry is null) errors.Add($"missing required legacy allowlist token: {token}");
            else if (entry.Lifetime != lifetime) errors.Add($"legacy allowlist lifetime mismatch: {token}");
        }
    }

    private static void ValidateInstallTool(TechnicalIdentityContract contract, ICollection<string> errors)
    {
        var tool = contract.InstallToolContract;
        var expectedModes = new[] { "Plan", "Install", "Rollback" };
        if (tool.DefaultMode != "Plan" || tool.ExecutionSwitch != "Execute"
            || tool.PlanBinding != "transaction-and-staging-sha256"
            || !tool.Modes.SequenceEqual(expectedModes, StringComparer.Ordinal)
            || tool.BackupRootPolicy != "inside-game-root-outside-loader-mods-root"
            || tool.LoaderScanModel != "recursive-json-under-mods-root"
            || tool.UnsafeBackupDisposition != "reject"
            || tool.BackupRetention != "through-runtime-acceptance"
            || tool.WorkshopDefault != "excluded" || !tool.RealExecutionRequiresSeparateApproval)
            errors.Add("install tool contract must remain dry-run-first, recoverable, and separately approved");
    }

    private static void ValidatePublishValidation(TechnicalIdentityContract contract, ICollection<string> errors)
    {
        var publish = contract.PublishValidationContract;
        if (publish.ValidatorScriptPath != "scripts/Test-IdentityPublishTrees.ps1"
            || publish.PublishRoot != "work/publish"
            || !publish.Targets.SequenceEqual(["stable", "beta"], StringComparer.Ordinal)
            || publish.ExactFileCount != 2 || publish.HashAlgorithm != "SHA256"
            || publish.HashDifferencePolicy != "reject-unless-explicitly-approved"
            || !publish.ForbiddenAssemblyMarkers.SequenceEqual(
                ["DebugTrace", "DamageForecastDebugTracePanel", "Calculation Debug", "调试 Debug", "计算调试"],
                StringComparer.Ordinal)
            || !publish.ActualPublishRequiresSeparateApproval)
            errors.Add("publish validation contract must enforce approved-root two-file trees, Debug exclusion, SHA256 comparison, and separate approval");
    }

    private static void ValidateActiveLifecycle(TechnicalIdentityContract contract, ICollection<string> errors)
    {
        var legacy = IdentityValues(contract.LegacyIdentity);
        var active = IdentityValues(contract.ActiveIdentity);
        var target = IdentityValues(contract.ApprovedTargetIdentity);
        foreach (var field in active.Keys)
        {
            if (active[field] != legacy[field] && active[field] != target[field])
                errors.Add($"active identity has an unapproved value: {field}");
            if (contract.Status == "approved-migration" && active[field] != legacy[field])
                errors.Add($"approved-migration activated a target prematurely: {field}");
            if (contract.Status == "active-migrated" && active[field] != target[field])
                errors.Add($"active-migrated did not activate target: {field}");
        }

        var groups = new Dictionary<string, string[]>
        {
            ["compiler-preparation"] = ["RootNamespace", "ProjectPath", "TestProjectPath"],
            ["load-identity-cutover"] = ["ModId", "ManifestStem", "AssemblyName", "DllName", "BaseLibRegistrationKey",
                "HarmonyOwner", "DiagnosticPrefix", "InstallDirectoryName", "ManifestVersion"]
        };
        foreach (var (group, fields) in groups)
        {
            var states = fields.Select(field => active[field] == target[field] ? "target" : "legacy").Distinct().ToArray();
            if (states.Length != 1) errors.Add($"atomic identity group is split: {group}");
        }
    }

    private static Dictionary<string, (string Legacy, string Target)> ExpectedFields(TechnicalIdentityContract contract)
    {
        var a = contract.LegacyIdentity;
        var t = contract.ApprovedTargetIdentity;
        return new(StringComparer.Ordinal)
        {
            ["ModId"] = (a.ModId, t.ModId), ["ManifestStem"] = (a.ManifestStem, t.ManifestStem),
            ["AssemblyName"] = (a.AssemblyName, t.AssemblyName), ["DllName"] = (a.DllName, t.DllName),
            ["RootNamespace"] = (a.RootNamespace, t.RootNamespace), ["ProjectPath"] = (a.ProjectPath, t.ProjectPath),
            ["TestProjectPath"] = (a.TestProjectPath, t.TestProjectPath),
            ["BaseLibRegistrationKey"] = (a.BaseLibRegistrationKey, t.BaseLibRegistrationKey),
            ["HarmonyOwner"] = (a.HarmonyOwner, t.HarmonyOwner), ["DiagnosticPrefix"] = (a.DiagnosticPrefix, t.DiagnosticPrefix),
            ["InstallDirectoryName"] = (a.InstallDirectoryName, t.InstallDirectoryName),
            ["PersistenceOwnerRootNamespace"] = (a.PersistenceOwnerRootNamespace, t.PersistenceOwnerRootNamespace),
            ["PersistenceFileName"] = (a.PersistenceFileName, t.PersistenceFileName),
            ["ConfigCompatibilityType"] = (a.ConfigCompatibilityType, t.ConfigCompatibilityType),
            ["ManifestVersion"] = (a.ManifestVersion, t.ManifestVersion)
        };
    }

    private static Dictionary<string, string> IdentityValues(TechnicalIdentity identity) => new(StringComparer.Ordinal)
    {
        ["ModId"] = identity.ModId, ["ManifestStem"] = identity.ManifestStem, ["AssemblyName"] = identity.AssemblyName,
        ["DllName"] = identity.DllName, ["RootNamespace"] = identity.RootNamespace, ["ProjectPath"] = identity.ProjectPath,
        ["TestProjectPath"] = identity.TestProjectPath, ["BaseLibRegistrationKey"] = identity.BaseLibRegistrationKey,
        ["HarmonyOwner"] = identity.HarmonyOwner, ["DiagnosticPrefix"] = identity.DiagnosticPrefix,
        ["InstallDirectoryName"] = identity.InstallDirectoryName,
        ["PersistenceOwnerRootNamespace"] = identity.PersistenceOwnerRootNamespace,
        ["PersistenceFileName"] = identity.PersistenceFileName, ["ConfigCompatibilityType"] = identity.ConfigCompatibilityType,
        ["ManifestVersion"] = identity.ManifestVersion
    };

    private static TechnicalIdentityContract LoadContract() =>
        JsonSerializer.Deserialize<TechnicalIdentityContract>(
            Read("tests/DamageForecast.ContractTests/identity-contract.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Identity contract deserialized to null.");

    private static string FindRepositoryRoot()
    {
        foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(seed); directory is not null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                    && File.Exists(Path.Combine(directory.FullName, "src", "DamageForecast", "DamageForecast.csproj")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    private static string CallIdentity(string source, string call, string active, string target)
    {
        var match = Regex.Match(source, call + @"\s*\(\s*(?<value>[^,\)]+)");
        if (!match.Success) return "<unresolved>";
        var value = match.Groups["value"].Value.Trim();
        return value == "ModId" ? MainFile.ModId : Select(value, active, target);
    }

    private static string InstallIdentity(string script, string active, string target)
    {
        var match = Regex.Match(script, @"\$modId\s*=\s*""(?<value>[^""]+)""");
        return match.Success ? match.Groups["value"].Value : Select(script, active, target);
    }

    private static string Select(string text, string active, string target)
    {
        if (active == target)
            return text.Contains(active, StringComparison.Ordinal) ? active : "<unresolved>";
        return (text.Contains(active, StringComparison.Ordinal), text.Contains(target, StringComparison.Ordinal)) switch
        { (true, false) => active, (false, true) => target, _ => "<ambiguous>" };
    }
    private static string Normalize(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}

internal sealed record TechnicalIdentityContract(int SchemaVersion, string Status, IdentityApprovalContract Approval,
    PermanentBehaviorContract PermanentBehavior, TechnicalIdentity LegacyIdentity, TechnicalIdentity ActiveIdentity,
    TechnicalIdentity ApprovedTargetIdentity,
    PersistenceIdentityContract PersistenceContract, InstallToolIdentityContract InstallToolContract,
    PublishValidationIdentityContract PublishValidationContract,
    PackagingIdentityContract Packaging, IReadOnlyList<string> PersistentSettings,
    IReadOnlyList<string> L0ScanScope, IReadOnlyList<string> ExcludedScanPrefixes,
    IReadOnlyList<LegacyAllowlistEntry> LegacyAllowlist,
    IReadOnlyList<ApprovedIdentityDecision> ApprovedDecisions, IReadOnlyList<MigrationIdentityField> MigrationFields);
internal sealed record IdentityApprovalContract(string Gate, string ApprovedAt, string Evidence);
internal sealed record PermanentBehaviorContract(string EnglishProductName, string SimplifiedChineseProductName,
    string MultiplayerBoundary, string WorkshopDisposition, string HistoricalEvidenceDisposition);
internal sealed record TechnicalIdentity(string ModId, string ManifestStem, string AssemblyName, string DllName,
    string RootNamespace, string ProjectPath, string TestProjectPath, string BaseLibRegistrationKey, string HarmonyOwner,
    string DiagnosticPrefix, string InstallDirectoryName, string PersistenceOwnerRootNamespace, string PersistenceFileName,
    string ConfigCompatibilityType, string ManifestVersion);
internal sealed record PersistenceIdentityContract(string BaseLibRegistrationRole, string LegacyRegistrationKey,
    string ApprovedTargetRegistrationKey, string ConfigStorageRole, string ConfigOwnerRootNamespace, string ConfigFileName,
    string ConfigCompatibilityType, string CompatibilityOwner, string CompatibilityLifetime, string ContinuityStrategy,
    string RollbackStrategy);
internal sealed record InstallToolIdentityContract(string DefaultMode, string ExecutionSwitch, string PlanBinding, IReadOnlyList<string> Modes,
    string BackupRootPolicy, string LoaderScanModel, string UnsafeBackupDisposition,
    string BackupRetention, string WorkshopDefault, bool RealExecutionRequiresSeparateApproval);
internal sealed record PublishValidationIdentityContract(string ValidatorScriptPath, string PublishRoot,
    IReadOnlyList<string> Targets, int ExactFileCount, string HashAlgorithm, string HashDifferencePolicy,
    IReadOnlyList<string> ForbiddenAssemblyMarkers, bool ActualPublishRequiresSeparateApproval);
internal sealed record PackagingIdentityContract(string ManifestPath, string BuildScriptPath, string InstallScriptPath,
    bool HasDll, bool HasPck, string DependencyId, string DependencyMinVersion, string MinGameVersion,
    IReadOnlyList<string> PublishWhitelist);
internal sealed record ApprovedIdentityDecision(string Id, string ApprovedValue, string ApprovalEvidence);
internal sealed record LegacyAllowlistEntry(string Token, IReadOnlyList<string> Locations, string Owner, string Purpose, string Lifetime);
internal sealed record MigrationIdentityField(string Field, string LegacyValue, string? TargetValue, string Disposition,
    string PersistenceImpact, string InstalledArtifactImpact, string CompatibilityOwner, string CompatibilityLifetime,
    string RollbackBehavior, string StaticVerification, string BuildVerification, string RuntimeVerification,
    IReadOnlyList<string> DecisionIds, string ApprovalEvidence);
internal sealed record RepositoryIdentitySnapshot(string ManifestId, string ManifestStem, string ManifestVersion,
    string AssemblyName, string DllName, string RootNamespace, string ProjectPath, string TestProjectPath,
    string BaseLibRegistrationKey, string HarmonyOwner, string DiagnosticPrefix, string InstallDirectoryName,
    string PersistenceOwnerRootNamespace, string PersistenceFileName, string ConfigCompatibilityType,
    IReadOnlyList<string> PublishWhitelist, int LegacyManifestCount, int TargetManifestCount);
