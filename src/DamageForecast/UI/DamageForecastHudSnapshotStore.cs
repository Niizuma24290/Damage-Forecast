using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using DamageForecast.Forecast;

namespace DamageForecast.UI;

internal static class DamageForecastHudSnapshotStore
{
    private static HudSnapshotLifecycleState _state = HudSnapshotLifecycleState.Empty;

    internal static bool IsLocalReadyWaiting =>
        _state.Phase == HudSnapshotLifecyclePhase.LocalReadyWaiting;

    public static void OnPlayerSideTurnStarted(Player player, Creature creature)
    {
        _state = HudSnapshotLifecyclePolicy.StartPlayerTurn(IdentityOf(player, creature));
    }

    public static void OnPlayerTurnEnding(Player player, Creature creature)
    {
        _state = HudSnapshotLifecyclePolicy.CommitLatest(_state, IdentityOf(player, creature));
    }

    public static void PrepareEndTurn(Player player, Creature creature, long generation)
    {
        _state = HudSnapshotLifecyclePolicy.PrepareEndTurn(
            _state,
            IdentityOf(player, creature),
            generation);
    }

    public static void ConfirmLocalReady(Player player, Creature creature, long generation)
    {
        _state = HudSnapshotLifecyclePolicy.ConfirmLocalReady(
            _state,
            IdentityOf(player, creature),
            generation);
    }

    public static void CancelLocalReady(Player player, Creature creature, long generation)
    {
        _state = HudSnapshotLifecyclePolicy.CancelLocalReady(
            _state,
            IdentityOf(player, creature),
            generation);
    }

    public static void CancelLocalReady(Player player, Creature creature)
    {
        _state = HudSnapshotLifecyclePolicy.CancelLocalReady(
            _state,
            IdentityOf(player, creature));
    }

    public static void OnPlayerTurnEnding(Player player, Creature creature, ForecastHudSnapshot latest)
    {
        _state = HudSnapshotLifecyclePolicy.Commit(
            _state,
            IdentityOf(player, creature),
            latest,
            HasDisplayableDamage(latest));
    }

    public static void Clear()
    {
        _state = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
            _state,
            HudVisibilityLifecycleEvent.PermanentlyInvalidated);
    }

    public static void OnVisibilityHidden(bool temporarilyCovered)
    {
        _state = HudSnapshotLifecyclePolicy.OnVisibilityEvent(
            _state,
            temporarilyCovered
                ? HudVisibilityLifecycleEvent.TemporarilyCovered
                : HudVisibilityLifecycleEvent.PermanentlyInvalidated);
    }

    public static bool TryGetCommitted(Creature creature, out ForecastHudSnapshot result)
    {
        if (creature.Player is { } player)
        {
            return HudSnapshotLifecyclePolicy.TryGetCommitted(
                _state,
                IdentityOf(player, creature),
                DamageForecastUiSettings.FreezeHudWithinPlayerTurn,
                out result);
        }

        result = ForecastHudSnapshot.Hidden;
        return false;
    }

    public static ForecastHudSnapshot ResolveDisplayResult(Creature creature, ForecastHudSnapshot latest)
    {
        if (creature.Player is not { } player)
        {
            Clear();
            return ForecastHudSnapshot.Hidden;
        }

        var resolution = HudSnapshotLifecyclePolicy.ResolveDisplay(
            _state,
            IdentityOf(player, creature),
            latest,
            HasDisplayableDamage(latest),
            DamageForecastUiSettings.FreezeHudWithinPlayerTurn);
        _state = resolution.State;
        return resolution.DisplaySnapshot;
    }

    private static bool HasDisplayableDamage(ForecastHudSnapshot result)
    {
        return DamageForecastHudDisplay.HasDisplayableSnapshot(result);
    }

    private static HudSnapshotOwnerIdentity IdentityOf(Player player, Creature creature)
    {
        var creatureIdentity = creature.CombatId.HasValue
            ? $"CombatId:{creature.CombatId.Value}"
            : $"ObjectRef:{RuntimeHelpers.GetHashCode(creature)}";
        return new HudSnapshotOwnerIdentity(player.NetId, creatureIdentity);
    }
}
