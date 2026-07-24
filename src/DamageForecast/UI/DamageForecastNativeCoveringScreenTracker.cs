using Godot;

namespace DamageForecast.UI;

internal static class DamageForecastNativeCoveringScreenTracker
{
    internal const string MapScreenTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen";
    internal const string CardPileScreenTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.NCardPileScreen";

    internal static readonly string[] CoveringScreenTypeNames =
    [
        "MegaCrit.Sts2.Core.Nodes.CommonUi.NSettingsScreenPopup",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCombatPileCardSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckCardSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckEnchantSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckTransformSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen",
        MapScreenTypeName,
        CardPileScreenTypeName,
        "MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.NRewardsScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu.NPauseMenu",
        "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen",
        "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NFakeMerchantInventory",
        "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory",
    ];

    private static readonly List<WeakReference<Node>> ActiveScreens = new();

    public static void MarkOpened(Node node)
    {
        if (!IsNativeCombatCoveringScreen(node))
        {
            return;
        }

        Cleanup();
        if (ActiveScreens.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, node)))
        {
            return;
        }

        ActiveScreens.Add(new WeakReference<Node>(node));
    }

    public static void MarkClosed(Node node)
    {
        for (var i = ActiveScreens.Count - 1; i >= 0; i--)
        {
            if (!ActiveScreens[i].TryGetTarget(out var existing)
                || !GodotObject.IsInstanceValid(existing)
                || ReferenceEquals(existing, node))
            {
                ActiveScreens.RemoveAt(i);
            }
        }
    }

#if DAMAGE_FORECAST_VISIBILITY_PROFILING
    internal static bool IsTrackedOpenForVisibilityProfiling(Node node)
    {
        for (var i = ActiveScreens.Count - 1; i >= 0; i--)
        {
            if (ActiveScreens[i].TryGetTarget(out var existing)
                && GodotObject.IsInstanceValid(existing)
                && ReferenceEquals(existing, node))
            {
                return true;
            }
        }

        return false;
    }
#endif

    public static bool HasNativeCombatCoveringScreenOpen()
    {
        Cleanup();
        return ActiveScreens.Any(reference =>
            reference.TryGetTarget(out var node)
            && GodotObject.IsInstanceValid(node)
            && node.IsInsideTree()
            && (node is not CanvasItem canvasItem || canvasItem.IsVisibleInTree()));
    }

    public static bool IsNativeCombatCoveringScreen(Node node)
    {
        for (var type = node.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName is { } fullName && CoveringScreenTypeNames.Contains(fullName))
            {
                return true;
            }
        }

        return false;
    }

    private static void Cleanup()
    {
        for (var i = ActiveScreens.Count - 1; i >= 0; i--)
        {
            if (!ActiveScreens[i].TryGetTarget(out var node)
                || !GodotObject.IsInstanceValid(node)
                || !node.IsInsideTree())
            {
                ActiveScreens.RemoveAt(i);
            }
        }
    }
}
