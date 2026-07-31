namespace DamageForecast.UI;

internal static class DamageForecastHudSurfacePolicy
{
    private const string MultiplayerPlayerStateTypeName =
        "MegaCrit.Sts2.Core.Nodes.Multiplayer.NMultiplayerPlayerState";
    private const string MultiplayerPlayerStateContainerTypeName =
        "MegaCrit.Sts2.Core.Nodes.Multiplayer.NMultiplayerPlayerStateContainer";

    public static bool IsExcludedMultiplayerSummaryAncestor(string? typeFullName)
    {
        return string.Equals(
                typeFullName,
                MultiplayerPlayerStateTypeName,
                StringComparison.Ordinal)
            || string.Equals(
                typeFullName,
                MultiplayerPlayerStateContainerTypeName,
                StringComparison.Ordinal);
    }

    public static bool CanHideSharedEndTurnSurface(bool isRegisteredLocalBar)
    {
        return isRegisteredLocalBar;
    }
}

internal enum HudEndTurnLayerParent
{
    EndTurnButton,
    CombatUi
}

internal static class HudEndTurnLayerPolicy
{
    public static HudEndTurnLayerParent LiveParent =>
        HudEndTurnLayerParent.EndTurnButton;

    public static HudEndTurnLayerParent FrozenParent =>
        HudEndTurnLayerParent.CombatUi;

    public static bool ShouldRenderLive(bool hasFrozenSnapshot) =>
        !hasFrozenSnapshot;

    public static bool ShouldPreserveFrozen(bool hasFrozenSnapshot) =>
        hasFrozenSnapshot;
}
