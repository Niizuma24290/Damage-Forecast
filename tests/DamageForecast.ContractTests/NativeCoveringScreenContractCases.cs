using System.Reflection;
using DamageForecast.Patches;
using DamageForecast.UI;
using HarmonyLib;

internal static class NativeCoveringScreenContractCases
{
    private const string TrackerPath =
        "src/DamageForecast/UI/DamageForecastNativeCoveringScreenTracker.cs";
    private const string LifecyclePatchPath =
        "src/DamageForecast/Patches/NativeCoveringScreenLifecyclePatch.cs";
    private const string VisibilityPolicyPath =
        "src/DamageForecast/UI/DamageForecastHudVisibilityPolicy.cs";
    private const string RefreshPatchPath =
        "src/DamageForecast/Patches/ForecastRefreshPatch.cs";
    private const string SnapshotPolicyPath =
        "src/DamageForecast/UI/HudSnapshotLifecyclePolicy.cs";
    private const string MapTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen";
    private const string CardPileTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.NCardPileScreen";
    private const string OrdinaryCombatUiTypeName =
        "MegaCrit.Sts2.Core.Nodes.Combat.NCombatUi";

    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "CV-001",
            "CoveringVisibility",
            "CoveringVisibility.KnownMapType_IsCovering",
            assert =>
            {
                var mapType = AccessTools.TypeByName(MapTypeName);
                assert.True(
                    mapType is not null
                    && DamageForecastNativeCoveringScreenTracker.CoveringScreenTypeNames.Contains(
                        MapTypeName,
                        StringComparer.Ordinal),
                    "stable/beta NMapScreen resolves and is explicitly classified as covering",
                    $"resolved={mapType is not null}; classified={IsClassified(MapTypeName)}");
            });

        yield return new(
            "CV-002",
            "CoveringVisibility",
            "CoveringVisibility.KnownCardPileType_IsCoveringWithDeclaredLifecycle",
            assert =>
            {
                var pileType = AccessTools.TypeByName(CardPileTypeName);
                var targets = LifecycleTargets();
                var methodNames = new[] { "_Ready", "_EnterTree", "_ExitTree" };
                var requiredMethods = methodNames
                    .Select(name => pileType is null ? null : AccessTools.DeclaredMethod(pileType, name))
                    .ToArray();
                var targetStatus = methodNames.Zip(
                    requiredMethods,
                    (name, method) => $"{name}={method is not null && targets.Contains(method)}");
                assert.True(
                    pileType is not null
                    && IsClassified(CardPileTypeName)
                    && requiredMethods.All(method => method is not null && targets.Contains(method)),
                    "NCardPileScreen resolves, is explicitly classified, and its declared open/close lifecycle is patched",
                    $"resolved={pileType is not null}; classified={IsClassified(CardPileTypeName)}; "
                    + $"targets={string.Join(',', targetStatus)}");
            });

        yield return new(
            "CV-003",
            "CoveringVisibility",
            "CoveringVisibility.UnknownCombatUi_IsNotCovering",
            assert =>
            {
                assert.True(
                    !IsClassified(OrdinaryCombatUiTypeName),
                    "ordinary NCombatUi remains outside the explicit covering-screen allowlist",
                    $"classified={IsClassified(OrdinaryCombatUiTypeName)}");
            });

        yield return new(
            "CV-004",
            "CoveringVisibility",
            "CoveringVisibility.NoCover_OrdinaryCombatRemainsVisible",
            assert =>
            {
                var policy = Read(VisibilityPolicyPath);
                assert.True(
                    policy.Contains(
                        "temporarilyCovered = DamageForecastNativeCoveringScreenTracker.HasNativeCombatCoveringScreenOpen();",
                        StringComparison.Ordinal)
                    && policy.Contains("return !temporarilyCovered;", StringComparison.Ordinal),
                    "valid ordinary combat renders whenever the covering tracker is empty",
                    "visibility policy no longer has the explicit no-cover negative control");
            });

        yield return new(
            "CV-005",
            "CoveringVisibility",
            "CoveringVisibility.OpenCover_HidesWithoutClearingSnapshot",
            assert =>
            {
                var lifecycle = Read(LifecyclePatchPath);
                var refresh = Read(RefreshPatchPath);
                var snapshot = Read(SnapshotPolicyPath);
                assert.True(
                    lifecycle.Contains(
                        "DamageForecastNativeCoveringScreenTracker.MarkOpened",
                        StringComparison.Ordinal)
                    && lifecycle.Contains("ForecastRefreshPatch.RefreshRegisteredBars();", StringComparison.Ordinal)
                    && refresh.Contains(
                        "DamageForecastHudSnapshotStore.OnVisibilityHidden(temporarilyCovered);",
                        StringComparison.Ordinal)
                    && snapshot.Contains(
                        "visibilityEvent == HudVisibilityLifecycleEvent.TemporarilyCovered",
                        StringComparison.Ordinal)
                    && snapshot.Contains("? state", StringComparison.Ordinal),
                    "open covering transition refreshes HUD while preserving temporary-cover snapshot state",
                    "open, refresh, or temporary snapshot-preservation wiring is missing");
            });

        yield return new(
            "CV-006",
            "CoveringVisibility",
            "CoveringVisibility.CloseLastCover_RestoresVisibility",
            assert =>
            {
                var tracker = Read(TrackerPath);
                var lifecycle = Read(LifecyclePatchPath);
                var policy = Read(VisibilityPolicyPath);
                assert.True(
                    tracker.Contains("ReferenceEquals(existing, node)", StringComparison.Ordinal)
                    && lifecycle.Contains(
                        "DamageForecastNativeCoveringScreenTracker.MarkClosed",
                        StringComparison.Ordinal)
                    && lifecycle.Contains("ForecastRefreshPatch.RefreshRegisteredBars();", StringComparison.Ordinal)
                    && policy.Contains("return !temporarilyCovered;", StringComparison.Ordinal),
                    "closing the last exact cover refreshes the HUD back through the ordinary visibility policy",
                    "exact close, refresh, or ordinary restore wiring is missing");
            });

        yield return new(
            "CV-007",
            "CoveringVisibility",
            "CoveringVisibility.DuplicateOpenClose_IsIdempotent",
            assert =>
            {
                var tracker = Read(TrackerPath);
                assert.True(
                    tracker.Contains(
                        "ActiveScreens.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, node))",
                        StringComparison.Ordinal)
                    && tracker.Contains("return;", StringComparison.Ordinal)
                    && tracker.Contains("ReferenceEquals(existing, node)", StringComparison.Ordinal),
                    "duplicate open is ignored and repeated exact close cannot accumulate an instance",
                    "tracker no longer contains its duplicate-instance guard and exact close removal");
            });

        yield return new(
            "CV-008",
            "CoveringVisibility",
            "CoveringVisibility.NestedCover_RemainsHiddenUntilLastClose",
            assert =>
            {
                var tracker = Read(TrackerPath);
                assert.True(
                    tracker.Contains(
                        "private static readonly List<WeakReference<Node>> ActiveScreens",
                        StringComparison.Ordinal)
                    && tracker.Contains("return ActiveScreens.Any(reference =>", StringComparison.Ordinal)
                    && tracker.Contains("ReferenceEquals(existing, node)", StringComparison.Ordinal),
                    "tracker stores multiple exact instances and remains covered while any valid visible cover remains",
                    "multi-instance list, exact close, or any-open semantics are missing");
            });

        yield return new(
            "CV-009",
            "CoveringVisibility",
            "CoveringVisibility.InvalidOrExitedCover_DoesNotLeavePermanentHide",
            assert =>
            {
                var tracker = Read(TrackerPath);
                assert.True(
                    tracker.Contains("Cleanup();", StringComparison.Ordinal)
                    && tracker.Contains("!GodotObject.IsInstanceValid(node)", StringComparison.Ordinal)
                    && tracker.Contains("!node.IsInsideTree()", StringComparison.Ordinal),
                    "invalid weak targets and exited-tree nodes are cleaned before visibility is decided",
                    "invalid or exited covering nodes can remain stale");
            });

        yield return new(
            "CV-010",
            "CoveringVisibility",
            "CoveringVisibility.MapOpenClose_AreDirectLifecycleTargets",
            assert =>
            {
                var mapType = AccessTools.TypeByName(MapTypeName);
                var open = mapType is null ? null : AccessTools.DeclaredMethod(mapType, "Open");
                var close = mapType is null ? null : AccessTools.DeclaredMethod(mapType, "Close");
                var targets = LifecycleTargets();
                var lifecycle = Read(LifecyclePatchPath);
                var openHandled = lifecycle.Contains(
                    "__originalMethod.Name is \"_Ready\" or \"_EnterTree\" or \"Open\"",
                    StringComparison.Ordinal);
                var closeHandled = lifecycle.Contains(
                    "__originalMethod.Name is \"_ExitTree\" or \"Close\"",
                    StringComparison.Ordinal);
                assert.True(
                    mapType is not null
                    && open is not null
                    && close is not null
                    && targets.Contains(open)
                    && targets.Contains(close)
                    && openHandled
                    && closeHandled,
                    "stable/beta NMapScreen.Open and Close are direct lifecycle targets with explicit tracker transitions",
                    $"resolved={mapType is not null}; openTarget={open is not null && targets.Contains(open)}; "
                    + $"closeTarget={close is not null && targets.Contains(close)}; "
                    + $"openHandled={openHandled}; closeHandled={closeHandled}");
            });
    }

    private static bool IsClassified(string typeName) =>
        DamageForecastNativeCoveringScreenTracker.CoveringScreenTypeNames.Contains(
            typeName,
            StringComparer.Ordinal);

    private static IReadOnlySet<MethodBase> LifecycleTargets()
    {
        var method = typeof(NativeCoveringScreenLifecyclePatch).GetMethod(
            "TargetMethods",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(
                typeof(NativeCoveringScreenLifecyclePatch).FullName,
                "TargetMethods");
        var targets = method.Invoke(null, null) as IEnumerable<MethodBase>
            ?? throw new InvalidOperationException("TargetMethods did not return a method sequence.");
        return targets.ToHashSet();
    }

    private static string Read(string path) => IdentityContractFixture.Read(path);
}
