using DamageForecast.Forecast;

namespace DamageForecast.UI;

internal static class HudSnapshotLifecyclePolicy
{
    public static HudSnapshotLifecycleState StartPlayerTurn(HudSnapshotOwnerIdentity owner) =>
        HudSnapshotLifecycleState.Empty with { Owner = owner };

    public static HudSnapshotLifecycleState CommitLatest(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner)
    {
        state = ForOwner(state, owner);
        if (state.Phase == HudSnapshotLifecyclePhase.Frozen)
        {
            return state;
        }

        return state with
        {
            CommittedSnapshot = state.LatestLiveSnapshot,
            PendingGeneration = null,
            Phase = HudSnapshotLifecyclePhase.Frozen
        };
    }

    public static HudSnapshotLifecycleState Commit(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        ForecastHudSnapshot latest,
        bool isDisplayable)
    {
        state = ForOwner(state, owner);
        if (state.Phase == HudSnapshotLifecyclePhase.Frozen)
        {
            return state;
        }

        var accepted = isDisplayable
            ? latest
            : state.LatestLiveSnapshot;
        return state with
        {
            LatestLiveSnapshot = isDisplayable ? latest : state.LatestLiveSnapshot,
            CommittedSnapshot = accepted,
            PendingGeneration = null,
            Phase = HudSnapshotLifecyclePhase.Frozen
        };
    }

    public static HudSnapshotLifecycleState PrepareEndTurn(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        long generation)
    {
        state = ForOwner(state, owner);
        return state.Phase == HudSnapshotLifecyclePhase.Frozen
            ? state
            : state with
            {
                PendingGeneration = generation,
                Phase = HudSnapshotLifecyclePhase.LocalReadyWaiting
            };
    }

    public static HudSnapshotLifecycleState ConfirmLocalReady(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        long generation)
    {
        state = ForOwner(state, owner);
        if (state.Phase == HudSnapshotLifecyclePhase.Frozen
            || state.PendingGeneration != generation)
        {
            return state;
        }

        return state with
        {
            PendingGeneration = null,
            Phase = HudSnapshotLifecyclePhase.LocalReadyWaiting
        };
    }

    public static HudSnapshotLifecycleState CancelLocalReady(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        long generation)
    {
        state = ForOwner(state, owner);
        return state.Phase == HudSnapshotLifecyclePhase.Frozen
            || state.PendingGeneration != generation
            ? state
            : state with
            {
                PendingGeneration = null,
                Phase = HudSnapshotLifecyclePhase.Live
            };
    }

    public static HudSnapshotLifecycleState CancelLocalReady(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner)
    {
        state = ForOwner(state, owner);
        return state.Phase != HudSnapshotLifecyclePhase.LocalReadyWaiting
            ? state
            : state with
            {
                PendingGeneration = null,
                Phase = HudSnapshotLifecyclePhase.Live
            };
    }

    public static HudSnapshotResolution ResolveDisplay(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        ForecastHudSnapshot latest,
        bool isDisplayable,
        bool freezeEnabled)
    {
        if (!freezeEnabled)
        {
            return new(StartPlayerTurn(owner), latest);
        }

        state = ForOwner(state, owner);
        if (state.Phase == HudSnapshotLifecyclePhase.Frozen)
        {
            return new(state, state.CommittedSnapshot ?? ForecastHudSnapshot.Hidden);
        }

        state = state with
        {
            LatestLiveSnapshot = isDisplayable
                ? latest
                : state.LatestLiveSnapshot
        };
        return new(state, isDisplayable ? latest : ForecastHudSnapshot.Hidden);
    }

    public static bool TryGetCommitted(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner,
        bool freezeEnabled,
        out ForecastHudSnapshot snapshot)
    {
        if (freezeEnabled
            && state.Owner == owner
            && state.Phase == HudSnapshotLifecyclePhase.Frozen
            && state.CommittedSnapshot is { } committed)
        {
            snapshot = committed;
            return true;
        }

        snapshot = ForecastHudSnapshot.Hidden;
        return false;
    }

    public static HudSnapshotLifecycleState OnVisibilityEvent(
        HudSnapshotLifecycleState state,
        HudVisibilityLifecycleEvent visibilityEvent) =>
        visibilityEvent == HudVisibilityLifecycleEvent.TemporarilyCovered
            ? state
            : HudSnapshotLifecycleState.Empty;

    private static HudSnapshotLifecycleState ForOwner(
        HudSnapshotLifecycleState state,
        HudSnapshotOwnerIdentity owner) =>
        state.Owner == owner ? state : StartPlayerTurn(owner);
}

internal readonly record struct HudSnapshotOwnerIdentity(
    ulong PlayerNetId,
    string CreatureStableIdentity);

internal readonly record struct HudSnapshotLifecycleState(
    HudSnapshotOwnerIdentity? Owner,
    ForecastHudSnapshot? LatestLiveSnapshot,
    ForecastHudSnapshot? CommittedSnapshot,
    long? PendingGeneration,
    HudSnapshotLifecyclePhase Phase)
{
    public bool HasPlayerEndedTurn => Phase == HudSnapshotLifecyclePhase.Frozen;

    public static HudSnapshotLifecycleState Empty =>
        new(null, null, null, null, HudSnapshotLifecyclePhase.Live);
}

internal enum HudSnapshotLifecyclePhase
{
    Live,
    LocalReadyWaiting,
    Frozen
}

internal readonly record struct HudSnapshotResolution(
    HudSnapshotLifecycleState State,
    ForecastHudSnapshot DisplaySnapshot);

internal enum HudVisibilityLifecycleEvent
{
    TemporarilyCovered,
    PermanentlyInvalidated
}
