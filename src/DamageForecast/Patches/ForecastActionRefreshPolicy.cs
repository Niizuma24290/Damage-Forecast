using DamageForecast.UI;

namespace DamageForecast.Patches;

internal static class ForecastActionRefreshPolicy
{
    public static bool ShouldRefresh(
        HudSnapshotLifecyclePhase phase,
        bool isCompletedPlayCard)
    {
        return phase == HudSnapshotLifecyclePhase.LocalReadyWaiting
            || (phase == HudSnapshotLifecyclePhase.Live
                && isCompletedPlayCard);
    }
}
