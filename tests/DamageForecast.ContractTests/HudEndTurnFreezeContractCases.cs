using DamageForecast.Combat;
using DamageForecast.Forecast;
using DamageForecast.Patches;
using DamageForecast.UI;

internal static class HudEndTurnFreezeContractCases
{
    private static readonly HudSnapshotOwnerIdentity OwnerA = new(1, "creature-a");
    private static readonly HudSnapshotOwnerIdentity OwnerB = new(2, "creature-b");

    public static IEnumerable<ContractCase> Create()
    {
        yield return new(
            "HF-001",
            "HudFreeze",
            "HudFreeze.HiddenRefresh_PreservesLastValidLiveCandidate",
            assert =>
            {
                var live = Observe(Snapshot(8));
                var hidden = HudSnapshotLifecyclePolicy.ResolveDisplay(
                    live,
                    OwnerA,
                    ForecastHudSnapshot.Hidden,
                    isDisplayable: false,
                    freezeEnabled: true);
                assert.True(
                    hidden.State.LatestLiveSnapshot == Snapshot(8)
                    && hidden.DisplaySnapshot == ForecastHudSnapshot.Hidden,
                    "lastValid=8; display=Hidden",
                    hidden.ToString());
            });

        yield return new(
            "HF-002",
            "HudFreeze",
            "HudFreeze.HiddenTurnEndReevaluation_CommitsLastValidLive",
            assert =>
            {
                var live = Observe(Snapshot(8));
                var committed = HudSnapshotLifecyclePolicy.Commit(
                    live,
                    OwnerA,
                    ForecastHudSnapshot.Hidden,
                    isDisplayable: false);
                assert.True(
                    committed.CommittedSnapshot == Snapshot(8)
                    && committed.LatestLiveSnapshot == Snapshot(8)
                    && committed.HasPlayerEndedTurn,
                    "live=8; committed=8; ended=true",
                    committed.ToString());
            });

        yield return new(
            "HF-003",
            "HudFreeze",
            "HudFreeze.Prepare_CapturesClickBeforeSnapshot",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var later = HudSnapshotLifecyclePolicy.ResolveDisplay(
                    prepared,
                    OwnerA,
                    Snapshot(3),
                    isDisplayable: true,
                    freezeEnabled: true).State;
                assert.True(
                    later.PendingSnapshot == Snapshot(8)
                    && later.LatestLiveSnapshot == Snapshot(3)
                    && later.PendingGeneration == 11,
                    "pending=8; latest=3; generation=11",
                    later.ToString());
            });

        yield return new(
            "HF-004",
            "HudFreeze",
            "HudFreeze.MatchingConfirmation_CommitsCapturedSnapshot",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var confirmed = HudSnapshotLifecyclePolicy.ConfirmEndTurn(
                    prepared,
                    OwnerA,
                    generation: 11);
                assert.True(
                    confirmed.CommittedSnapshot == Snapshot(8)
                    && confirmed.PendingSnapshot is null
                    && confirmed.PendingGeneration is null
                    && confirmed.HasPlayerEndedTurn,
                    "committed=8; pending=null; ended=true",
                    confirmed.ToString());
            });

        yield return new(
            "HF-005",
            "HudFreeze",
            "HudFreeze.WrongOwnerOrGeneration_CannotConfirm",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var wrongGeneration = HudSnapshotLifecyclePolicy.ConfirmEndTurn(
                    prepared,
                    OwnerA,
                    generation: 12);
                var wrongOwner = HudSnapshotLifecyclePolicy.ConfirmEndTurn(
                    prepared,
                    OwnerB,
                    generation: 11);
                assert.True(
                    !wrongGeneration.HasPlayerEndedTurn
                    && wrongGeneration.PendingSnapshot == Snapshot(8)
                    && !wrongOwner.HasPlayerEndedTurn
                    && wrongOwner.PendingSnapshot is null,
                    "wrong generation preserves pending; wrong owner resets without commit",
                    $"generation={wrongGeneration}; owner={wrongOwner}");
            });

        yield return new(
            "HF-006",
            "HudFreeze",
            "HudFreeze.CancelledRelease_DoesNotFreeze",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var cancelled = HudSnapshotLifecyclePolicy.CancelEndTurn(
                    prepared,
                    OwnerA,
                    generation: 11);
                assert.True(
                    !cancelled.HasPlayerEndedTurn
                    && cancelled.CommittedSnapshot is null
                    && cancelled.PendingSnapshot is null
                    && cancelled.PendingGeneration is null,
                    "ended=false; committed=null; pending=null",
                    cancelled.ToString());
            });

        yield return new(
            "HF-007",
            "HudFreeze",
            "HudFreeze.FallbackCommit_CannotOverwriteExistingCommit",
            assert =>
            {
                var committed = HudSnapshotLifecyclePolicy.ConfirmEndTurn(
                    HudSnapshotLifecyclePolicy.PrepareEndTurn(
                        Observe(Snapshot(8)),
                        OwnerA,
                        generation: 11),
                    OwnerA,
                    generation: 11);
                var protectedState = HudSnapshotLifecyclePolicy.Commit(
                    committed,
                    OwnerA,
                    Snapshot(3),
                    isDisplayable: true);
                assert.True(
                    protectedState.CommittedSnapshot == Snapshot(8)
                    && protectedState.LatestLiveSnapshot == Snapshot(8),
                    "committed and live stay at accepted click-before snapshot",
                    protectedState.ToString());
            });

        yield return new(
            "HF-008",
            "HudFreeze",
            "HudFreeze.NextTurnAndPermanentInvalidation_ClearPendingAndCommitted",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var confirmed = HudSnapshotLifecyclePolicy.ConfirmEndTurn(
                    prepared,
                    OwnerA,
                    generation: 11);
                var nextTurn = HudSnapshotLifecyclePolicy.StartPlayerTurn(OwnerA);
                var invalidated = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
                    confirmed,
                    HudVisibilityLifecycleEvent.PermanentlyInvalidated);
                assert.True(
                    nextTurn.LatestLiveSnapshot is null
                    && nextTurn.CommittedSnapshot is null
                    && nextTurn.PendingSnapshot is null
                    && !nextTurn.HasPlayerEndedTurn
                    && invalidated == HudSnapshotLifecycleState.Empty,
                    "next turn and permanent invalidation clear all snapshot phases",
                    $"next={nextTurn}; invalidated={invalidated}");
            });

        yield return new(
            "HF-009",
            "HudFreeze",
            "HudFreeze.TemporaryCover_PreservesPendingCandidate",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var covered = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
                    prepared,
                    HudVisibilityLifecycleEvent.TemporarilyCovered);
                assert.Equal(prepared, covered);
            });

        yield return new(
            "HF-010",
            "HudFreeze",
            "HudFreeze.TurnHookFallback_CommitsClickBeforePendingSnapshot",
            assert =>
            {
                var prepared = HudSnapshotLifecyclePolicy.PrepareEndTurn(
                    Observe(Snapshot(8)),
                    OwnerA,
                    generation: 11);
                var later = HudSnapshotLifecyclePolicy.ResolveDisplay(
                    prepared,
                    OwnerA,
                    Snapshot(3),
                    isDisplayable: true,
                    freezeEnabled: true).State;
                var committed = HudSnapshotLifecyclePolicy.CommitLatest(later, OwnerA);
                assert.True(
                    committed.CommittedSnapshot == Snapshot(8)
                    && committed.LatestLiveSnapshot == Snapshot(3)
                    && committed.HasPlayerEndedTurn,
                    "turn-hook fallback accepts pending=8 without rebuilding",
                    committed.ToString());
            });

        yield return new(
            "HF-011",
            "HudFreeze",
            "HudFreeze.LiveAndFrozenLayers_HaveSeparateParents",
            assert =>
            {
                assert.True(
                    HudEndTurnLayerPolicy.LiveParent
                        == HudEndTurnLayerParent.EndTurnButton
                    && HudEndTurnLayerPolicy.FrozenParent
                        == HudEndTurnLayerParent.CombatUi,
                    "live=EndTurnButton; frozen=CombatUi",
                    $"live={HudEndTurnLayerPolicy.LiveParent}; frozen={HudEndTurnLayerPolicy.FrozenParent}");
            });

        yield return new(
            "HF-012",
            "HudFreeze",
            "HudFreeze.FrozenSnapshot_SuppressesLiveLayerUntilResume",
            assert =>
            {
                var liveBeforeClick =
                    HudEndTurnLayerPolicy.ShouldRenderLive(hasFrozenSnapshot: false);
                var liveAfterClick =
                    HudEndTurnLayerPolicy.ShouldRenderLive(hasFrozenSnapshot: true);
                var preserveAfterClick =
                    HudEndTurnLayerPolicy.ShouldPreserveFrozen(hasFrozenSnapshot: true);
                assert.True(
                    liveBeforeClick && !liveAfterClick && preserveAfterClick,
                    "before=true; after=false; preserve=true",
                    $"before={liveBeforeClick}; after={liveAfterClick}; preserve={preserveAfterClick}");
            });

        yield return new(
            "HF-013",
            "HudFreeze",
            "HudFreeze.EndTurnLifecyclePatchTargets_AreAvailable",
            assert =>
            {
                var ready = ForecastEndTurnFreezePatch.HasLifecycleMethod("_Ready");
                var combatUiReady = ForecastCombatUiReadyPatch.HasReadyMethod();
                var release = ForecastEndTurnFreezePatch.HasLifecycleMethod("CallReleaseLogic");
                var disable = ForecastEndTurnFreezePatch.HasLifecycleMethod("OnDisable");
                assert.True(
                    ready && combatUiReady && release && disable,
                    "button ready, combat UI ready, release, and disable targets available",
                    $"buttonReady={ready}; combatUiReady={combatUiReady}; release={release}; disable={disable}");
            });
    }

    private static HudSnapshotLifecycleState Observe(ForecastHudSnapshot snapshot) =>
        HudSnapshotLifecyclePolicy.ResolveDisplay(
            HudSnapshotLifecyclePolicy.StartPlayerTurn(OwnerA),
            OwnerA,
            snapshot,
            isDisplayable: true,
            freezeEnabled: true).State;

    private static ForecastHudSnapshot Snapshot(int total) =>
        new(
            ForecastResult.KnownDamage(total, 0),
            IncomingDamageDisplayRead.Hidden);
}
