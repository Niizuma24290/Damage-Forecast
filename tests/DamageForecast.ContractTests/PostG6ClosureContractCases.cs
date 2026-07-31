using System.Reflection;
using DamageForecast;
using DamageForecast.Compatibility;
using DamageForecast.Patches;
using DamageForecast.Settings;

internal static class PostG6ClosureContractCases
{
    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "C4-001",
            "PostG6Closure",
            "C4.Contract_IsExplicitlyApprovedAndComplete",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                assert.True(
                    contract.SchemaVersion == 2
                    && contract.Status == "c4-complete"
                    && contract.Approval.Gate == "C2"
                    && contract.Closure.Gate == "C4"
                    && contract.Closure.RollbackEvidence == "closed"
                    && contract.Closure.HistoricalEvidenceDisposition == "preserved",
                    "schema=2; status=c4-complete; C2 migration approval retained; C4 closure explicit",
                    $"schema={contract.SchemaVersion}; status={contract.Status}; closure={contract.Closure.Gate}");
            });

        yield return new(
            "C4-002",
            "PostG6Closure",
            "C4.OrdinaryProductionSource_ContainsNoLegacyMarkers",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var compatibilityRoot = Resolve(contract.CompatibilityRoot);
                var hits = ProductionFiles()
                    .Where(path => !IsWithin(path, compatibilityRoot))
                    .SelectMany(path => contract.OrdinaryLegacyMarkers
                        .Where(marker => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
                        .Select(marker => $"{Relative(path)}:{marker}"))
                    .ToArray();
                assert.True(
                    hits.Length == 0,
                    "ordinary production source contains zero PartyWatch/STS2PartyWatch/legacy-slug markers",
                    string.Join(',', hits));
            });

        yield return new(
            "C4-003",
            "PostG6Closure",
            "C4.CompiledAssembly_ContainsNoLegacyTypesNamespacesOrMembers",
            assert =>
            {
                var markers = PostG6PersistenceContractFixture.Contract.OrdinaryLegacyMarkers;
                var types = typeof(MainFile).Assembly.GetTypes();
                var names = types.Select(type => type.FullName ?? type.Name)
                    .Concat(types.SelectMany(type => type.GetMembers(
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .Select(member => $"{type.FullName}.{member.Name}")));
                var hits = names
                    .Where(name => markers.Any(marker => name.Contains(marker, StringComparison.Ordinal)))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                assert.True(
                    hits.Length == 0,
                    "compiled type, namespace, and member names contain no legacy marker",
                    string.Join(',', hits));
            });

        yield return new(
            "C4-004",
            "PostG6Closure",
            "C4.CompatibilitySubsystem_FileAndNamespaceBoundary_IsExact",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var compatibilityRoot = Resolve(contract.CompatibilityRoot);
                var actualFiles = Directory.EnumerateFiles(compatibilityRoot, "*.cs", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var expectedFiles = contract.CompatibilityFiles
                    .Append("HudPlacementConfigFileMigration.cs")
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var namespaceErrors = Directory.EnumerateFiles(compatibilityRoot, "*.cs", SearchOption.TopDirectoryOnly)
                    .Where(path => !File.ReadAllText(path).Contains(
                        "namespace DamageForecast.Compatibility;",
                        StringComparison.Ordinal))
                    .Select(Relative)
                    .ToArray();
                assert.True(
                    actualFiles.SequenceEqual(expectedFiles, StringComparer.Ordinal)
                    && namespaceErrors.Length == 0,
                    "compatibility file inventory and namespace ownership exactly match the C4 contract",
                    $"files={string.Join(',', actualFiles)}; namespaceErrors={string.Join(',', namespaceErrors)}");
            });

        yield return new(
            "C4-005",
            "PostG6Closure",
            "C4.RuntimeCompatibilityTokens_AreCompleteAndLocationBound",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var installer = Resolve("scripts/Install-LocalMod.ps1");
                var scanFiles = ProductionFiles().Append(installer).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var locationErrors = contract.RuntimeTokenInventory.SelectMany(entry =>
                {
                    var actual = scanFiles
                        .Where(path => File.ReadAllText(path).Contains(entry.Token, StringComparison.Ordinal))
                        .Select(Relative)
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                    var expected = entry.Locations.Order(StringComparer.Ordinal).ToArray();
                    var metadataComplete = !string.IsNullOrWhiteSpace(entry.OriginVersion)
                        && !string.IsNullOrWhiteSpace(entry.Purpose)
                        && entry.RetirementGate == "C5-or-explicit-support-drop"
                        && entry.VerificationIds.Count > 0;
                    return actual.SequenceEqual(expected, StringComparer.Ordinal) && metadataComplete
                        ? Array.Empty<string>()
                        : [$"{entry.Token}:actual={string.Join('|', actual)}:expected={string.Join('|', expected)}:metadata={metadataComplete}"];
                }).ToArray();

                var ownedFiles = Directory.EnumerateFiles(
                        Resolve(contract.CompatibilityRoot),
                        "*.cs",
                        SearchOption.TopDirectoryOnly)
                    .Append(installer);
                var uncovered = ownedFiles.SelectMany(path =>
                {
                    var remaining = File.ReadAllText(path);
                    foreach (var entry in contract.RuntimeTokenInventory)
                    {
                        remaining = remaining.Replace(entry.Token, string.Empty, StringComparison.Ordinal);
                    }
                    return contract.OrdinaryLegacyMarkers
                        .Where(marker => remaining.Contains(marker, StringComparison.Ordinal))
                        .Select(marker => $"{Relative(path)}:{marker}");
                }).ToArray();
                var descriptorFields = typeof(LegacyIdentityDescriptor)
                    .GetFields(BindingFlags.Public | BindingFlags.Static);
                var descriptorOwnership = descriptorFields.Length == 4
                    && descriptorFields.All(field => field.IsInitOnly && !field.IsLiteral);

                assert.True(
                    locationErrors.Length == 0 && uncovered.Length == 0 && descriptorOwnership,
                    "all runtime compatibility tokens have exact locations, non-inlined descriptor ownership, metadata, retirement gate, and tests",
                    $"{string.Join(',', locationErrors.Concat(uncovered))}; descriptorOwnership={descriptorOwnership}");
            });

        yield return new(
            "C4-006",
            "PostG6Closure",
            "C4.ActiveSourceAndTestPaths_ContainNoOrdinaryLegacyNames",
            assert =>
            {
                var roots = new[]
                {
                    Resolve("src/DamageForecast"),
                    Resolve("tests/DamageForecast.ContractTests")
                };
                var hits = roots.SelectMany(root => Directory.EnumerateFileSystemEntries(
                        root,
                        "*",
                        SearchOption.AllDirectories))
                    .Where(path => Path.GetFileName(path).Contains("PartyWatch", StringComparison.Ordinal)
                        || Path.GetFileName(path).Contains("STS2PartyWatch", StringComparison.Ordinal))
                    .Select(Relative)
                    .ToArray();
                assert.True(
                    hits.Length == 0,
                    "active source and test filenames/directories contain no ordinary legacy names",
                    string.Join(',', hits));
            });

        yield return new(
            "C4-007",
            "PostG6Closure",
            "C4.CurrentAuthority_DescribesCurrentConfigAndCompatibilityBoundary",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var errors = contract.CurrentAuthorityRules.SelectMany(rule =>
                {
                    var path = Resolve(rule.Path);
                    if (!File.Exists(path))
                    {
                        return new[] { $"{rule.Path}:missing" };
                    }
                    var text = File.ReadAllText(path);
                    var missing = rule.RequiredTokens
                        .Where(token => !text.Contains(token, StringComparison.Ordinal))
                        .Select(token => $"{rule.Path}:missing:{token}");
                    var forbidden = rule.ForbiddenTokens
                        .Where(token => text.Contains(token, StringComparison.Ordinal))
                        .Select(token => $"{rule.Path}:forbidden:{token}");
                    return missing.Concat(forbidden);
                }).ToArray();
                assert.True(
                    errors.Length == 0,
                    "all eight current-authority files satisfy their C4 product-fact or routing responsibilities",
                    string.Join(',', errors));
            });

        yield return new(
            "C4-008",
            "PostG6Closure",
            "C4.OrdinarySettingsUiCombatForecastAndPatches_ContainNoLegacyKeyOrFile",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var forbidden = contract.RuntimeTokenInventory.Select(entry => entry.Token).ToArray();
                var hits = contract.OrdinaryLayerRoots
                    .SelectMany(root => Directory.EnumerateFiles(Resolve(root), "*", SearchOption.AllDirectories))
                    .Where(path => Path.GetExtension(path) == ".cs")
                    .SelectMany(path => forbidden
                        .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                        .Select(token => $"{Relative(path)}:{token}"))
                    .ToArray();
                assert.True(
                    hits.Length == 0,
                    "ordinary Settings/UI/Combat/Forecast/Patches layers contain zero legacy key/file/mod literals",
                    string.Join(',', hits));
            });

        yield return new(
            "C4-009",
            "PostG6Closure",
            "C4.SchemaGraphAndCompatibilityDescriptor_Agree",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var graphAligned = contract.SchemaGraph.Count == ConfigSchemaRegistry.SupportedSchemas.Count
                    && contract.SchemaGraph.All(edge =>
                        ConfigSchemaRegistry.Get(edge.Key).Id == edge.Key
                        && ConfigSchemaRegistry.CanMigrateDirectlyToCurrent(edge.Key)
                        && edge.Value == ConfigSchemaRegistry.Current.Id);
                var descriptorAligned = contract.Legacy.Schema == LegacyIdentityDescriptor.SchemaId
                    && contract.Legacy.File == LegacyIdentityDescriptor.ConfigFileName
                    && contract.Legacy.HudKey == LegacyIdentityDescriptor.HudEnabledKey
                    && contract.Release.ModId == MainFile.ModId;
                assert.True(
                    graphAligned && descriptorAligned,
                    "machine-readable schema graph, runtime registry, descriptor, and release identity agree",
                    $"graphAligned={graphAligned}; descriptorAligned={descriptorAligned}");
            });

        yield return new(
            "C4-010",
            "PostG6Closure",
            "C4.ReleaseRuntimeAndExternalBoundaries_AreExplicit",
            assert =>
            {
                var contract = PostG6PersistenceContractFixture.Contract;
                var currentType = typeof(DamageForecastBaseLibConfig);
                var oldNodeNames = PostG6NamingContractFixture.Contract.GodotNodeMappings
                    .Select(mapping => mapping.Legacy)
                    .ToArray();
                var nodeNames = new[]
                {
                    ForecastRefreshPatch.MainLabelName,
                    ForecastRefreshPatch.IncomingLabelName,
                    ForecastRefreshPatch.DetailLabelName
                };
                assert.True(
                    contract.Release.Version == MainFile.ModVersion
                    && contract.Closure.InstalledIdentity == "one-damage-forecast-v0.3.0"
                    && contract.Closure.ActiveConfig == contract.Current.File
                    && contract.Closure.WorkshopDisposition == contract.Release.WorkshopDisposition
                    && currentType.FullName == contract.Current.Type
                    && currentType.Assembly.GetType(contract.Legacy.Type) is null
                    && !nodeNames.Intersect(oldNodeNames, StringComparer.Ordinal).Any(),
                    "v0.3.0/current config/one installed identity/external Workshop boundary/zero old node names",
                    $"version={contract.Release.Version}; config={contract.Closure.ActiveConfig}; workshop={contract.Closure.WorkshopDisposition}");
            });
    }

    private static IReadOnlyList<string> ProductionFiles()
    {
        var sourceRoot = Resolve("src/DamageForecast");
        return Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".json")
            .ToArray();
    }

    private static string Resolve(string relativePath) =>
        PostG6NamingContractFixture.Resolve(relativePath);

    private static string Relative(string path) =>
        Path.GetRelativePath(IdentityContractFixture.RepositoryRoot, path).Replace('\\', '/');

    private static bool IsWithin(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
