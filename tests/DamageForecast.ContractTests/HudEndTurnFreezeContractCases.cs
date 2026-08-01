using MegaCrit.Sts2.Core.GameActions;
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
                var hidden = Resolve(live, Snapshot(0), isDisplayable: false);
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
                var committed = HudSnapshotLifecyclePolicy.Commit(
                    Observe(Snapshot(8)),
                    OwnerA,
                    ForecastHudSnapshot.Hidden,
                    isDisplayable: false);
                assert.True(
                    committed.CommittedSnapshot == Snapshot(8)
                    && committed.LatestLiveSnapshot == Snapshot(8)
                    && committed.Phase == HudSnapshotLifecyclePhase.Frozen,
                    "live=8; committed=8; phase=Frozen",
                    committed.ToString());
            });

        yield return new(
            "HF-003",
            "HudFreeze",
            "HudFreeze.Prepare_EntersLocalReadyWaitingWithoutCapturingSnapshot",
            assert =>
            {
                var prepared = Prepare(Observe(Snapshot(8)), generation: 11);
                var later = Resolve(prepared, Snapshot(3)).State;
                assert.True(
                    later.LatestLiveSnapshot == Snapshot(3)
                    && later.CommittedSnapshot is null
                    && later.PendingGeneration == 11
                    && later.Phase == HudSnapshotLifecyclePhase.LocalReadyWaiting,
                    "latest=3; committed=null; generation=11; phase=LocalReadyWaiting",
                    later.ToString());
            });

        yield return new(
            "HF-004",
            "HudFreeze",
            "HudFreeze.ButtonDisable_ConfirmsLocalReadyWithoutFreezing",
            assert =>
            {
                var confirmed = HudSnapshotLifecyclePolicy.ConfirmLocalReady(
                    Prepare(Observe(Snapshot(8)), generation: 11),
                    OwnerA,
                    generation: 11);
                var later = Resolve(confirmed, Snapshot(5)).State;
                assert.True(
                    later.LatestLiveSnapshot == Snapshot(5)
                    && later.CommittedSnapshot is null
                    && later.PendingGeneration is null
                    && later.Phase == HudSnapshotLifecyclePhase.LocalReadyWaiting,
                    "latest=5; committed=null; pending=null; phase=LocalReadyWaiting",
                    later.ToString());
            });

        yield return new(
            "HF-005",
            "HudFreeze",
            "HudFreeze.WrongOwnerOrGeneration_CannotConfirmLocalReady",
            assert =>
            {
                var prepared = Prepare(Observe(Snapshot(8)), generation: 11);
                var wrongGeneration = HudSnapshotLifecyclePolicy.ConfirmLocalReady(
                    prepared,
                    OwnerA,
                    generation: 12);
                var wrongOwner = HudSnapshotLifecyclePolicy.ConfirmLocalReady(
                    prepared,
                    OwnerB,
                    generation: 11);
                assert.True(
                    wrongGeneration == prepared
                    && wrongOwner.Owner == OwnerB
                    && wrongOwner.LatestLiveSnapshot is null
                    && wrongOwner.CommittedSnapshot is null
                    && wrongOwner.Phase == HudSnapshotLifecyclePhase.Live,
                    "wrong generation preserves waiting; wrong owner resets to a clean live state",
                    $"generation={wrongGeneration}; owner={wrongOwner}");
            });

        yield return new(
            "HF-006",
            "HudFreeze",
            "HudFreeze.CancelledRelease_ReturnsToLiveWithoutFreezing",
            assert =>
            {
                var cancelled = HudSnapshotLifecyclePolicy.CancelLocalReady(
                    Prepare(Observe(Snapshot(8)), generation: 11),
                    OwnerA,
                    generation: 11);
                assert.True(
                    cancelled.LatestLiveSnapshot == Snapshot(8)
                    && cancelled.CommittedSnapshot is null
                    && cancelled.PendingGeneration is null
                    && cancelled.Phase == HudSnapshotLifecyclePhase.Live,
                    "latest=8; committed=null; pending=null; phase=Live",
                    cancelled.ToString());
            });

        yield return new(
            "HF-007",
            "HudFreeze",
            "HudFreeze.FinalBoundary_FreezesOnceAndCannotBeOverwritten",
            assert =>
            {
                var waiting = Resolve(Prepare(Observe(Snapshot(8)), 11), Snapshot(3)).State;
                var committed = HudSnapshotLifecyclePolicy.CommitLatest(waiting, OwnerA);
                var protectedState = HudSnapshotLifecyclePolicy.Commit(
                    committed,
                    OwnerA,
                    Snapshot(1),
                    isDisplayable: true);
                assert.True(
                    committed.CommittedSnapshot == Snapshot(3)
                    && protectedState == committed
                    && protectedState.Phase == HudSnapshotLifecyclePhase.Frozen,
                    "final boundary commits latest=3 exactly once",
                    protectedState.ToString());
            });

        yield return new(
            "HF-008",
            "HudFreeze",
            "HudFreeze.NextTurnAndPermanentInvalidation_ClearWaitingAndCommitted",
            assert =>
            {
                var waiting = Prepare(Observe(Snapshot(8)), generation: 11);
                var frozen = HudSnapshotLifecyclePolicy.CommitLatest(waiting, OwnerA);
                var nextTurn = HudSnapshotLifecyclePolicy.StartPlayerTurn(OwnerA);
                var invalidated = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
                    frozen,
                    HudVisibilityLifecycleEvent.PermanentlyInvalidated);
                assert.True(
                    nextTurn.LatestLiveSnapshot is null
                    && nextTurn.CommittedSnapshot is null
                    && nextTurn.PendingGeneration is null
                    && nextTurn.Phase == HudSnapshotLifecyclePhase.Live
                    && invalidated == HudSnapshotLifecycleState.Empty,
                    "next turn and permanent invalidation clear all snapshot phases",
                    $"next={nextTurn}; invalidated={invalidated}");
            });

        yield return new(
            "HF-009",
            "HudFreeze",
            "HudFreeze.TemporaryCover_PreservesLocalReadyWaitingState",
            assert =>
            {
                var waiting = Prepare(Observe(Snapshot(8)), generation: 11);
                var covered = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
                    waiting,
                    HudVisibilityLifecycleEvent.TemporarilyCovered);
                assert.Equal(waiting, covered);
            });

        yield return new(
            "HF-010",
            "HudFreeze",
            "HudFreeze.TurnHook_CommitsLastValidWaitingSnapshot",
            assert =>
            {
                var waiting = Resolve(Prepare(Observe(Snapshot(8)), 11), Snapshot(3)).State;
                var committed = HudSnapshotLifecyclePolicy.CommitLatest(waiting, OwnerA);
                assert.True(
                    committed.CommittedSnapshot == Snapshot(3)
                    && committed.LatestLiveSnapshot == Snapshot(3)
                    && committed.Phase == HudSnapshotLifecyclePhase.Frozen,
                    "turn hook accepts last valid live=3",
                    committed.ToString());
            });

        yield return new(
            "HF-011",
            "HudFreeze",
            "HudFreeze.WaitingTeammateBlock_UpdateReachesFinalCommit",
            assert => AssertWaitingUpdateCommits(assert, before: 12, after: 5));

        yield return new(
            "HF-012",
            "HudFreeze",
            "HudFreeze.WaitingEnemyAttackModifier_UpdateReachesFinalCommit",
            assert => AssertWaitingUpdateCommits(assert, before: 8, after: 11));

        yield return new(
            "HF-013",
            "HudFreeze",
            "HudFreeze.WaitingEnemyDeath_RemovedIntentReachesFinalCommit",
            assert => AssertWaitingUpdateCommits(assert, before: 18, after: 7));

        yield return new(
            "HF-014",
            "HudFreeze",
            "HudFreeze.RepeatReadyCancel_DoesNotRestoreStaleSnapshotOrPolluteGeneration",
            assert =>
            {
                var first = Prepare(Observe(Snapshot(8)), generation: 11);
                var confirmed = HudSnapshotLifecyclePolicy.ConfirmLocalReady(first, OwnerA, 11);
                var cancelled = HudSnapshotLifecyclePolicy.CancelLocalReady(confirmed, OwnerA);
                var updated = Resolve(cancelled, Snapshot(5)).State;
                var second = Prepare(updated, generation: 12);
                var wrongCancel = HudSnapshotLifecyclePolicy.CancelLocalReady(second, OwnerA, 11);
                var final = HudSnapshotLifecyclePolicy.CommitLatest(wrongCancel, OwnerA);
                assert.True(
                    wrongCancel.PendingGeneration == 12
                    && final.CommittedSnapshot == Snapshot(5)
                    && final.Phase == HudSnapshotLifecyclePhase.Frozen,
                    "generation=12 remains authoritative; final=5",
                    $"waiting={wrongCancel}; final={final}");
            });

        yield return new(
            "HF-015",
            "HudFreeze",
            "HudFreeze.LiveAndStableLayers_HaveSeparateParents",
            assert =>
            {
                assert.True(
                    HudEndTurnLayerPolicy.LiveParent == HudEndTurnLayerParent.EndTurnButton
                    && HudEndTurnLayerPolicy.FrozenParent == HudEndTurnLayerParent.CombatUi,
                    "live=EndTurnButton; stable=CombatUi",
                    $"live={HudEndTurnLayerPolicy.LiveParent}; stable={HudEndTurnLayerPolicy.FrozenParent}");
            });

        yield return new(
            "HF-016",
            "HudFreeze",
            "HudFreeze.StableSnapshot_SuppressesLiveLayerUntilResume",
            assert =>
            {
                var liveBeforeClick = HudEndTurnLayerPolicy.ShouldRenderLive(false);
                var liveAfterClick = HudEndTurnLayerPolicy.ShouldRenderLive(true);
                var preserveAfterClick = HudEndTurnLayerPolicy.ShouldPreserveFrozen(true);
                assert.True(
                    liveBeforeClick && !liveAfterClick && preserveAfterClick,
                    "before=true; after=false; preserve=true",
                    $"before={liveBeforeClick}; after={liveAfterClick}; preserve={preserveAfterClick}");
            });

        yield return new(
            "HF-017",
            "HudFreeze",
            "HudFreeze.LifecycleAndActionRefreshTargets_AreAvailable",
            assert =>
            {
                var ready = ForecastEndTurnFreezePatch.HasLifecycleMethod("_Ready");
                var combatUiReady = ForecastCombatUiReadyPatch.HasReadyMethod();
                var release = ForecastEndTurnFreezePatch.HasLifecycleMethod("CallReleaseLogic");
                var disable = ForecastEndTurnFreezePatch.HasLifecycleMethod("OnDisable");
                var unended = ForecastEndTurnFreezePatch.HasLifecycleMethod("AfterPlayerUnendedTurn");
                var afterAction = typeof(ActionExecutor).GetEvent(nameof(ActionExecutor.AfterActionExecuted));
                assert.True(
                    ready && combatUiReady && release && disable && unended && afterAction is not null,
                    "button lifecycle and action-completion refresh targets available",
                    $"buttonReady={ready}; combatUiReady={combatUiReady}; release={release}; disable={disable}; unended={unended}; afterAction={afterAction is not null}");
            });
    }

    private static void AssertWaitingUpdateCommits(ContractAssert assert, int before, int after)
    {
        var waiting = HudSnapshotLifecyclePolicy.ConfirmLocalReady(
            Prepare(Observe(Snapshot(before)), generation: 11),
            OwnerA,
            generation: 11);
        var updated = Resolve(waiting, Snapshot(after)).State;
        var committed = HudSnapshotLifecyclePolicy.CommitLatest(updated, OwnerA);
        assert.True(
            updated.Phase == HudSnapshotLifecyclePhase.LocalReadyWaiting
            && committed.CommittedSnapshot == Snapshot(after),
            $"waiting live and final committed both use {after}",
            $"waiting={updated}; committed={committed}");
    }

    private static HudSnapshotLifecycleState Prepare(
        HudSnapshotLifecycleState state,
        long generation) =>
        HudSnapshotLifecyclePolicy.PrepareEndTurn(state, OwnerA, generation);

    private static HudSnapshotResolution Resolve(
        HudSnapshotLifecycleState state,
        ForecastHudSnapshot snapshot,
        bool isDisplayable = true) =>
        HudSnapshotLifecyclePolicy.ResolveDisplay(
            state,
            OwnerA,
            snapshot,
            isDisplayable,
            freezeEnabled: true);

    private static HudSnapshotLifecycleState Observe(ForecastHudSnapshot snapshot) =>
        Resolve(HudSnapshotLifecyclePolicy.StartPlayerTurn(OwnerA), snapshot).State;

    private static ForecastHudSnapshot Snapshot(int total) =>
        new(
            ForecastResult.KnownDamage(total, 0),
            IncomingDamageDisplayRead.Hidden);
}
