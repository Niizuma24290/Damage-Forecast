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

    public static HudEndTurnLayerVisibility ResolveVisibility(
        bool hasFrozenSnapshot,
        bool hudVisible) =>
        hudVisible
            ? new HudEndTurnLayerVisibility(
                RenderLive: !hasFrozenSnapshot,
                RenderFrozen: hasFrozenSnapshot)
            : new HudEndTurnLayerVisibility(
                RenderLive: false,
                RenderFrozen: false);
}

internal readonly record struct HudEndTurnLayerVisibility(
    bool RenderLive,
    bool RenderFrozen);

internal static class HudEndTurnAnchorHandoffPolicy
{
    public static HudEndTurnAnchorHandoffDecision Resolve(
        bool rootsReady,
        bool anchorConverted,
        bool snapshotCopied) =>
        new(
            CommitFrozen: rootsReady && anchorConverted && snapshotCopied,
            SuppressLive: true);
}

internal readonly record struct HudEndTurnAnchorHandoffDecision(
    bool CommitFrozen,
    bool SuppressLive);
