using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using DamageForecast.Combat;
using DamageForecast.Diagnostics;
using DamageForecast.Forecast;
using DamageForecast.UI;

namespace DamageForecast.Patches;

[HarmonyPatch(typeof(NHealthBar))]
internal static class ForecastRefreshPatch
{
    internal const string MainLabelName = "DamageForecastExpectedLossLabel";
    internal const string IncomingLabelName = "DamageForecastIncomingDamageLabel";
    internal const string DetailLabelName = "DamageForecastDetailsLabel";
    private const string RootName = DamageForecastHudRoot.RootName;
    private const string RootOwnershipGroup = DamageForecastHudRoot.OwnershipGroup;
    private const string EndTurnRootName = "DamageForecastEndTurnHudRoot";
    private const string EndTurnRootOwnershipGroup = "damage-forecast-end-turn-hud-root";
    private const string FrozenEndTurnRootName = "DamageForecastFrozenEndTurnHudRoot";
    private const string FrozenEndTurnRootOwnershipGroup = "damage-forecast-frozen-end-turn-hud-root";
    private const string MainLabelOwnershipGroup = "damage-forecast-hud-expected-loss";
    private const string IncomingLabelOwnershipGroup = "damage-forecast-hud-incoming-damage";
    private const string DetailLabelOwnershipGroup = "damage-forecast-hud-details";
    private static readonly FieldInfo? CreatureField = typeof(NHealthBar).GetField("_creature", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly LocalIncomingDamageReader Reader = new();
    private static readonly LocalDamageForecast Forecast = new();
    private static readonly List<WeakReference<NHealthBar>> RegisteredBars = new();
    private static WeakReference<NEndTurnButton>? _frozenEndTurnButton;
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
    [ThreadStatic]
    private static int _lastRegisteredBarsVisited;

    internal static int LastRegisteredBarsVisitedForVisibilityProfiling => _lastRegisteredBarsVisited;
#endif

    static ForecastRefreshPatch()
    {
        DamageForecastUiSettings.Changed += RefreshRegisteredBars;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.SetCreature))]
    private static void SetCreaturePostfix(NHealthBar __instance)
    {
        Refresh(__instance, null);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.RefreshValues))]
    private static void RefreshValuesPostfix(NHealthBar __instance)
    {
        Refresh(__instance, null);
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetHpBarContainerSizeWithOffsets")]
    private static void ResizePostfix(NHealthBar __instance, Vector2 size)
    {
        Refresh(__instance, size);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.UpdateLayoutForCreatureBounds))]
    private static void UpdateLayoutForCreatureBoundsPostfix(NHealthBar __instance)
    {
        Refresh(__instance, null);
    }

    private static void Refresh(NHealthBar bar, Vector2? containerSize)
    {
        if (IsMultiplayerSummaryHealthBar(bar))
        {
            HideExisting(bar);
            return;
        }

        if (!TryGetLocalCreature(bar, out var creature) || creature is null)
        {
            HideExisting(bar);
            return;
        }

        RegisterBar(bar);

        var healthRoot = GetOrCreateRoot(bar, RootName, RootOwnershipGroup);
        var endTurnButton = HudAnchorResolver.ResolveEndTurnButton(bar);
        var endTurnRoot = endTurnButton is null
            ? null
            : GetOrCreateRoot(endTurnButton, EndTurnRootName, EndTurnRootOwnershipGroup);
        var hasFrozenEndTurnSnapshot = IsFrozenEndTurnButton(endTurnButton);
        if (healthRoot is null)
        {
            return;
        }

        ObservedHpLossBudgetTracker.Observe(creature);

        if (!DamageForecastHudVisibilityPolicy.ShouldRenderHud(bar, creature, out var temporarilyCovered))
        {
            DamageForecastHudSnapshotStore.OnVisibilityHidden(temporarilyCovered);

            healthRoot.HideAll();
            if (HudEndTurnLayerPolicy.ShouldRenderLive(hasFrozenEndTurnSnapshot))
            {
                endTurnRoot?.HideAll();
            }
            return;
        }

        var snapshot = DamageForecastHudSnapshotStore.TryGetCommitted(creature, out var committed)
            ? committed
            : DamageForecastHudSnapshotStore.ResolveDisplayResult(
                creature,
                BuildForecastHudSnapshot(creature));
        if (!DamageForecastHudDisplay.HasDisplayableSnapshot(snapshot))
        {
            healthRoot.HideAll();
            if (HudEndTurnLayerPolicy.ShouldRenderLive(hasFrozenEndTurnSnapshot))
            {
                endTurnRoot?.HideAll();
            }
            return;
        }

        var expectedText = DamageForecastHudDisplay.ShouldShowExpectedHpLoss(snapshot)
            ? DamageForecastHudDisplay.BuildMainHudDisplay(snapshot.ExpectedHpLoss)
            : string.Empty;
        var incomingText = DamageForecastHudDisplay.ShouldShowIncomingDamage(snapshot)
            ? DamageForecastHudDisplay.BuildIncomingHudDisplay(snapshot.IncomingDamage)
            : string.Empty;
        var details = DamageForecastHudDisplay.BuildHudDetails(snapshot.ExpectedHpLoss);
        ApplyRoot(
            healthRoot,
            creature,
            bar.HpBarContainer,
            containerSize,
            expectedText,
            incomingText,
            details,
            endTurnSurface: false);
        if (endTurnRoot is not null
            && HudEndTurnLayerPolicy.ShouldRenderLive(hasFrozenEndTurnSnapshot))
        {
            ApplyRoot(
                endTurnRoot,
                creature,
                bar.HpBarContainer,
                containerSize,
                expectedText,
                incomingText,
                details,
                endTurnSurface: true);
        }
        else
        {
            endTurnRoot?.HideAll();
        }
    }

    private static ForecastHudSnapshot BuildForecastHudSnapshot(Creature creature)
    {
        var expected = Forecast.Calculate(Reader.ReadForLocalCreature(creature));
        var incoming = Reader.ReadIncomingDamageForLocalCreature(creature, new IncomingDamageDisplayOptions(
            DamageForecastUiSettings.IncludeCurrentBlockInIncomingDamage,
            DamageForecastUiSettings.IncludePowerBlockInIncomingDamage,
            DamageForecastUiSettings.IncludeRelicBlockInIncomingDamage,
            DamageForecastUiSettings.IncludePowerHpLossModifiersInIncomingDamage,
            DamageForecastUiSettings.IncludeRelicHpLossModifiersInIncomingDamage));
        return new ForecastHudSnapshot(expected, incoming);
    }

    private static bool TryGetLocalCreature(NHealthBar bar, out Creature? creature)
    {
        creature = GetCreature(bar);
        if (creature?.Player is null)
        {
            return false;
        }

        try
        {
            var localPlayer = LocalContext.GetMe(creature.CombatState);
            return localPlayer is not null && creature.Player.NetId == localPlayer.NetId;
        }
        catch
        {
            return false;
        }
    }

    private static Creature? GetCreature(NHealthBar bar)
    {
        return CreatureField?.GetValue(bar) as Creature;
    }

    internal static bool TryGetRegisteredLocalCreature(
        out Player? player,
        out Creature? creature)
    {
        for (var i = RegisteredBars.Count - 1; i >= 0; i--)
        {
            if (!RegisteredBars[i].TryGetTarget(out var bar)
                || !IsUsableBar(bar))
            {
                RegisteredBars.RemoveAt(i);
                continue;
            }

            if (TryGetLocalCreature(bar, out creature) && creature?.Player is { } owner)
            {
                player = owner;
                return true;
            }
        }

        player = null;
        creature = null;
        return false;
    }

    private static bool IsMultiplayerSummaryHealthBar(NHealthBar bar)
    {
        for (Node? ancestor = bar.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (DamageForecastHudSurfacePolicy.IsExcludedMultiplayerSummaryAncestor(
                    ancestor.GetType().FullName))
            {
                return true;
            }
        }

        return false;
    }

    private static DamageForecastHudRoot? GetOrCreateRoot(
        Control parent,
        string rootName,
        string ownershipGroup)
    {
        var root = ResolveHudNode(
            parent,
            rootName,
            ownershipGroup,
            static () => new DamageForecastHudRoot
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 50,
                Visible = false
            },
            static value => value.Initialize());
        if (root is not null)
        {
            root.Position = Vector2.Zero;
            root.Size = parent.Size;
        }

        return root;
    }

    private static void ApplyRoot(
        DamageForecastHudRoot root,
        Creature creature,
        Control? healthBar,
        Vector2? containerSize,
        string expectedText,
        string incomingText,
        string details,
        bool endTurnSurface)
    {
        if (healthBar is null)
        {
            root.HideAll();
            return;
        }

        root.Apply(
            expectedText,
            incomingText,
            details,
            DamageForecastUiSettings.ExpectedHpLossPlacementPreset,
            DamageForecastUiSettings.IncomingDamagePlacementPreset,
            DamageForecastUiSettings.DetailsPlacementPreset,
            DamageForecastUiSettings.IncomingDamagePlacement,
            DamageForecastUiSettings.OffsetX,
            DamageForecastUiSettings.OffsetY,
            endTurnSurface,
            () => HudAnchorResolver.ResolveAvailableBounds(root),
            preset =>
            {
                if (preset == HudPlacementPreset.EndTurnButtonAbove)
                {
                    return HudAnchorResolver.TryResolveEndTurnButton(root, out var endAnchor)
                        ? endAnchor
                        : null;
                }

                if (preset == HudPlacementPreset.HealthBarAbove
                    && HudAnchorResolver.TryResolveCharacterAbove(
                        root,
                        creature,
                        healthBar,
                        containerSize,
                        out var creatureAnchor))
                {
                    return creatureAnchor;
                }

                return HudAnchorResolver.TryResolveHealthBar(
                    root,
                    healthBar,
                    containerSize,
                    out var healthAnchor)
                    ? healthAnchor
                    : null;
            },
            preset => preset == HudPlacementPreset.HealthBarBelow
                && HudAnchorResolver.TryResolvePowerAvoidance(root, creature, out var avoidance)
                    ? avoidance
                    : null,
            preserveOnAnchorFailure: endTurnSurface);
    }

    private static Label? GetOrCreateMainLabel(NHealthBar bar)
    {
        var parent = GetLabelParent(bar);
        if (parent is null)
        {
            return null;
        }

        return ResolveHudNode(
            parent,
            MainLabelName,
            MainLabelOwnershipGroup,
            static () => new Label
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 50,
                Text = string.Empty,
                Visible = false,
            },
            DamageForecastHudDisplay.ApplyMainHudStyle);
    }

    private static Label? GetOrCreateIncomingLabel(NHealthBar bar)
    {
        var parent = GetLabelParent(bar);
        if (parent is null)
        {
            return null;
        }

        return ResolveHudNode(
            parent,
            IncomingLabelName,
            IncomingLabelOwnershipGroup,
            static () => new Label
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 50,
                Text = string.Empty,
                Visible = false,
            },
            DamageForecastHudDisplay.ApplyIncomingHudStyle);
    }

    private static RichTextLabel? GetOrCreateDetailLabel(NHealthBar bar)
    {
        var parent = GetLabelParent(bar);
        if (parent is null)
        {
            return null;
        }

        return ResolveHudNode(
            parent,
            DetailLabelName,
            DetailLabelOwnershipGroup,
            static () => new RichTextLabel
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 50,
                Text = string.Empty,
                Visible = false,
            },
            DamageForecastHudDisplay.ApplyDetailHudStyle);
    }

    private static T? ResolveHudNode<T>(
        Control parent,
        string expectedName,
        string ownershipGroup,
        Func<T>? create,
        Action<T>? applyStyle)
        where T : Control
    {
        var children = parent.GetChildren().OfType<Node>().ToArray();
        var candidates = children.Select(child => new DamageForecastHudNodeCandidate(
            HasExpectedName: string.Equals(child.Name.ToString(), expectedName, StringComparison.Ordinal),
            HasExpectedType: child is T,
            IsOwned: child.IsInGroup(ownershipGroup))).ToArray();
        var resolution = DamageForecastHudNodeOwnershipPolicy.Resolve(candidates);
        if (resolution.FailClosed)
        {
            var conflictingTypes = children
                .Where(child => string.Equals(child.Name.ToString(), expectedName, StringComparison.Ordinal))
                .Select(child => child.GetType().FullName ?? child.GetType().Name);
            DamageForecastDiagnostics.ReportOnce(
                $"hud-node.type-conflict.{expectedName}",
                new InvalidOperationException(
                    $"Expected {typeof(T).FullName} for node '{expectedName}', found {string.Join(", ", conflictingTypes)}."));
            return null;
        }

        T? canonical;
        if (resolution.CreateNew)
        {
            if (create is null)
            {
                return null;
            }

            canonical = create();
            canonical.Name = expectedName;
            canonical.AddToGroup(ownershipGroup);
            applyStyle?.Invoke(canonical);
            parent.AddChild(canonical);
        }
        else
        {
            canonical = children[resolution.CanonicalIndex] as T;
            if (canonical is null)
            {
                return null;
            }

            canonical.Name = expectedName;
            if (!canonical.IsInGroup(ownershipGroup))
            {
                canonical.AddToGroup(ownershipGroup);
            }

            applyStyle?.Invoke(canonical);
        }

        foreach (var duplicateIndex in resolution.DuplicateOwnedIndexes)
        {
            var duplicate = children[duplicateIndex];
            if (GodotObject.IsInstanceValid(duplicate) && !duplicate.IsQueuedForDeletion())
            {
                duplicate.QueueFree();
            }
        }

        return canonical;
    }

    private static Control? GetLabelParent(NHealthBar bar)
    {
        return bar.HpBarContainer?.GetParent() as Control ?? bar;
    }

    private static void Reposition(
        NHealthBar bar,
        Label mainLabel,
        Label incomingLabel,
        RichTextLabel detailLabel,
        Vector2? containerSize)
    {
        var container = bar.HpBarContainer;
        if (container is null)
        {
            return;
        }

        var isMultiplayer = GetCreature(bar)?.CombatState?.Players.Count > 1;
        DamageForecastHudDisplay.ApplyHudPosition(
            container,
            mainLabel,
            incomingLabel,
            detailLabel,
            containerSize,
            isMultiplayer);
    }

    private static void HideExisting(NHealthBar bar)
    {
        var healthRoot = ResolveHudNode<DamageForecastHudRoot>(
            bar,
            RootName,
            RootOwnershipGroup,
            create: null,
            applyStyle: null);
        healthRoot?.HideAll();
        if (DamageForecastHudSurfacePolicy.CanHideSharedEndTurnSurface(
                IsRegisteredBar(bar)))
        {
            var endTurnButton = HudAnchorResolver.ResolveEndTurnButton(bar);
            if (endTurnButton is not null)
            {
                var liveEndTurnRoot = ResolveHudNode<DamageForecastHudRoot>(
                    endTurnButton,
                    EndTurnRootName,
                    EndTurnRootOwnershipGroup,
                    create: null,
                    applyStyle: null);
                liveEndTurnRoot?.HideAll();
                ClearFrozenEndTurnSnapshot(endTurnButton);
            }
        }

        var parent = GetLabelParent(bar);
        if (parent is null)
        {
            return;
        }

        var mainLabel = ResolveHudNode<Label>(
            parent,
            MainLabelName,
            MainLabelOwnershipGroup,
            create: null,
            applyStyle: null);
        var incomingLabel = ResolveHudNode<Label>(
            parent,
            IncomingLabelName,
            IncomingLabelOwnershipGroup,
            create: null,
            applyStyle: null);
        var detailLabel = ResolveHudNode<RichTextLabel>(
            parent,
            DetailLabelName,
            DetailLabelOwnershipGroup,
            create: null,
            applyStyle: null);
        if (mainLabel is not null)
        {
            Hide(mainLabel);
        }

        if (incomingLabel is not null)
        {
            Hide(incomingLabel);
        }

        if (detailLabel is not null)
        {
            Hide(detailLabel);
        }
    }

    private static void Hide(Label mainLabel, Label incomingLabel, RichTextLabel detailLabel)
    {
        Hide(mainLabel);
        Hide(incomingLabel);
        Hide(detailLabel);
    }

    private static void ShowOrHide(Label label)
    {
        if (string.IsNullOrEmpty(label.Text))
        {
            Hide(label);
        }
        else
        {
            label.Show();
        }
    }

    private static void Hide(Control label)
    {
        if (label is Label simpleLabel)
        {
            simpleLabel.Text = string.Empty;
        }
        else if (label is RichTextLabel richTextLabel)
        {
            richTextLabel.Text = string.Empty;
        }

        label.Hide();
    }

    private static void RegisterBar(NHealthBar bar)
    {
        if (IsRegisteredBar(bar))
        {
            return;
        }

        RegisteredBars.Add(new WeakReference<NHealthBar>(bar));
    }

    private static bool IsRegisteredBar(NHealthBar bar)
    {
        for (var i = RegisteredBars.Count - 1; i >= 0; i--)
        {
            if (!RegisteredBars[i].TryGetTarget(out var existing) || !IsUsableBar(existing))
            {
                RegisteredBars.RemoveAt(i);
                continue;
            }

            if (ReferenceEquals(existing, bar))
            {
                return true;
            }
        }

        return false;
    }

    internal static void RefreshRegisteredBars()
    {
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
        var barsVisited = 0;
#endif
        for (var i = RegisteredBars.Count - 1; i >= 0; i--)
        {
            if (!RegisteredBars[i].TryGetTarget(out var bar) || !IsUsableBar(bar))
            {
                RegisteredBars.RemoveAt(i);
                continue;
            }

#if DAMAGE_FORECAST_VISIBILITY_PROFILING
            barsVisited++;
#endif
            Refresh(bar, null);
        }
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
        _lastRegisteredBarsVisited = barsVisited;
#endif
    }

    internal static void FreezeEndTurnAnchor(NEndTurnButton button)
    {
        ClearFrozenEndTurnSnapshot(button);
        var liveRoot = ResolveLiveEndTurnRoot(button);
        var frozenRoot = GetOrCreateFrozenEndTurnRoot(button);
        if (liveRoot is null
            || frozenRoot is null
            || !liveRoot.CopyVisibleSnapshotTo(frozenRoot))
        {
            frozenRoot?.HideAll();
            return;
        }

        _frozenEndTurnButton = new WeakReference<NEndTurnButton>(button);
        liveRoot.HideAll();
    }

    internal static void ResumeEndTurnAnchor(NEndTurnButton button)
    {
        ClearFrozenEndTurnSnapshot(button);
    }

    internal static void ResumeEndTurnAnchor()
    {
        foreach (var reference in RegisteredBars)
        {
            if (!reference.TryGetTarget(out var bar) || !IsUsableBar(bar))
            {
                continue;
            }

            var button = HudAnchorResolver.ResolveEndTurnButton(bar);
            if (button is not null)
            {
                ResumeEndTurnAnchor(button);
                return;
            }
        }
    }

    internal static void HideAndClearRegisteredBars()
    {
        var bars = RegisteredBars
            .Select(reference => reference.TryGetTarget(out var bar) ? bar : null)
            .Where(bar => bar is not null && IsUsableBar(bar))
            .Cast<NHealthBar>()
            .ToArray();
        foreach (var bar in bars)
        {
            HideExisting(bar);
        }

        RegisteredBars.Clear();
        _frozenEndTurnButton = null;
    }

    private static DamageForecastHudRoot? ResolveLiveEndTurnRoot(NEndTurnButton button)
    {
        return ResolveHudNode<DamageForecastHudRoot>(
            button,
            EndTurnRootName,
            EndTurnRootOwnershipGroup,
            create: null,
            applyStyle: null);
    }

    private static DamageForecastHudRoot? GetOrCreateFrozenEndTurnRoot(
        NEndTurnButton button)
    {
        var surface = HudAnchorResolver.ResolveEndTurnSurfaceParent(button);
        return surface is null
            ? null
            : GetOrCreateRoot(
                surface,
                FrozenEndTurnRootName,
                FrozenEndTurnRootOwnershipGroup);
    }

    private static DamageForecastHudRoot? ResolveFrozenEndTurnRoot(
        NEndTurnButton button)
    {
        var surface = HudAnchorResolver.ResolveEndTurnSurfaceParent(button);
        return surface is null
            ? null
            : ResolveHudNode<DamageForecastHudRoot>(
                surface,
                FrozenEndTurnRootName,
                FrozenEndTurnRootOwnershipGroup,
                create: null,
                applyStyle: null);
    }

    private static bool IsFrozenEndTurnButton(NEndTurnButton? button)
    {
        if (button is null
            || _frozenEndTurnButton is null
            || !_frozenEndTurnButton.TryGetTarget(out var frozenButton)
            || !GodotObject.IsInstanceValid(frozenButton)
            || frozenButton.IsQueuedForDeletion())
        {
            _frozenEndTurnButton = null;
            return false;
        }

        return ReferenceEquals(button, frozenButton);
    }

    private static void ClearFrozenEndTurnSnapshot(NEndTurnButton button)
    {
        ResolveFrozenEndTurnRoot(button)?.HideAll();
        if (_frozenEndTurnButton is not null
            && _frozenEndTurnButton.TryGetTarget(out var frozenButton)
            && ReferenceEquals(button, frozenButton))
        {
            _frozenEndTurnButton = null;
        }
    }

    private static bool IsUsableBar(NHealthBar bar) =>
        GodotObject.IsInstanceValid(bar)
        && !bar.IsQueuedForDeletion()
        && bar.IsInsideTree();

    internal static void CommitFinalSnapshot(Creature creature)
    {
        var player = creature.Player;
        if (player is null)
        {
            return;
        }

        DamageForecastHudSnapshotStore.OnPlayerTurnEnding(player, creature);
    }
}

[HarmonyPatch(typeof(NEndTurnButton))]
internal static class ForecastEndTurnFreezePatch
{
    private static long _nextGeneration;
    private static long? _activeGeneration;
    private static WeakReference<NEndTurnButton>? _activeButton;
    private static WeakReference<Player>? _activePlayer;
    private static WeakReference<Creature>? _activeCreature;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NEndTurnButton._Ready))]
    private static void ReadyPostfix(NEndTurnButton __instance)
    {
        HudAnchorResolver.EnsureEndTurnAnchor(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("CallReleaseLogic")]
    private static void CallReleaseLogicPrefix(NEndTurnButton __instance)
    {
        CancelActive();
        if (!ForecastRefreshPatch.TryGetRegisteredLocalCreature(
                out var player,
                out var creature)
            || player is null
            || creature is null)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _nextGeneration);
        _activeGeneration = generation;
        _activeButton = new WeakReference<NEndTurnButton>(__instance);
        _activePlayer = new WeakReference<Player>(player);
        _activeCreature = new WeakReference<Creature>(creature);
        DamageForecastHudSnapshotStore.PrepareEndTurn(player, creature, generation);
        ForecastRefreshPatch.FreezeEndTurnAnchor(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("OnDisable")]
    private static void OnDisablePostfix(NEndTurnButton __instance)
    {
        if (!TryGetActive(
                __instance,
                out var generation,
                out var player,
                out var creature))
        {
            return;
        }

        DamageForecastHudSnapshotStore.ConfirmEndTurn(player, creature, generation);
        ClearActive();
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    [HarmonyPostfix]
    [HarmonyPatch("CallReleaseLogic")]
    private static void CallReleaseLogicPostfix(NEndTurnButton __instance)
    {
        if (!TryGetActive(
                __instance,
                out var generation,
                out var player,
                out var creature))
        {
            return;
        }

        DamageForecastHudSnapshotStore.CancelEndTurn(player, creature, generation);
        ClearActive();
        ForecastRefreshPatch.ResumeEndTurnAnchor(__instance);
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    internal static void Clear()
    {
        CancelActive();
        ClearActive();
    }

    internal static bool HasLifecycleMethod(string methodName)
    {
        return AccessTools.Method(typeof(NEndTurnButton), methodName) is not null;
    }

    private static void CancelActive()
    {
        if (_activeGeneration is not { } generation
            || _activePlayer is null
            || !_activePlayer.TryGetTarget(out var player)
            || _activeCreature is null
            || !_activeCreature.TryGetTarget(out var creature))
        {
            return;
        }

        DamageForecastHudSnapshotStore.CancelEndTurn(player, creature, generation);
    }

    private static bool TryGetActive(
        NEndTurnButton button,
        out long generation,
        out Player player,
        out Creature creature)
    {
        if (_activeGeneration is { } activeGeneration
            && _activeButton is not null
            && _activeButton.TryGetTarget(out var activeButton)
            && ReferenceEquals(activeButton, button)
            && _activePlayer is not null
            && _activePlayer.TryGetTarget(out var activePlayer)
            && _activeCreature is not null
            && _activeCreature.TryGetTarget(out var activeCreature))
        {
            generation = activeGeneration;
            player = activePlayer;
            creature = activeCreature;
            return true;
        }

        generation = default;
        player = null!;
        creature = null!;
        return false;
    }

    private static void ClearActive()
    {
        _activeGeneration = null;
        _activeButton = null;
        _activePlayer = null;
        _activeCreature = null;
    }
}

[HarmonyPatch(typeof(NCombatUi))]
internal static class ForecastCombatUiReadyPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCombatUi._Ready))]
    private static void ReadyPostfix(NCombatUi __instance)
    {
        if (__instance.EndTurnButton is { } button)
        {
            HudAnchorResolver.EnsureEndTurnAnchor(button);
        }

        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    internal static bool HasReadyMethod() =>
        AccessTools.Method(typeof(NCombatUi), nameof(NCombatUi._Ready)) is not null;
}

[HarmonyPatch(typeof(CardPile))]
internal static class ForecastHandChangePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(CardPile.InvokeContentsChanged))]
    private static void InvokeContentsChangedPostfix(CardPile __instance)
    {
        if (__instance.Type == PileType.Hand)
        {
            ForecastRefreshPatch.RefreshRegisteredBars();
        }
    }
}

[HarmonyPatch(typeof(Player))]
internal static class ForecastRelicChangePatch
{
    [HarmonyPostfix]
    [HarmonyPatch("AddRelicInternal")]
    private static void AddRelicInternalPostfix()
    {
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    [HarmonyPostfix]
    [HarmonyPatch("RemoveRelicInternal")]
    private static void RemoveRelicInternalPostfix()
    {
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    [HarmonyPostfix]
    [HarmonyPatch("MeltRelicInternal")]
    private static void MeltRelicInternalPostfix()
    {
        ForecastRefreshPatch.RefreshRegisteredBars();
    }
}

[HarmonyPatch(typeof(BeatingRemnant))]
internal static class ForecastBeatingRemnantBudgetPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(BeatingRemnant.BeforeSideTurnStart))]
    private static void BeforeSideTurnStartPostfix(BeatingRemnant __instance, object[] __args)
    {
        if (__args.Length < 3 || __args[2] is not IReadOnlyList<Creature> creaturesStartingTurn)
        {
            return;
        }

        var owner = __instance.Owner;
        var ownerCreature = owner?.Creature;
        if (owner is null || ownerCreature is null || !creaturesStartingTurn.Contains(ownerCreature))
        {
            return;
        }

        ObservedHpLossBudgetTracker.ResetWindow(owner);
        ForecastRefreshPatch.RefreshRegisteredBars();
    }
}

[HarmonyPatch(typeof(Hook))]
internal static class ForecastTurnLifecyclePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Hook.BeforeSideTurnStart))]
    private static void BeforeSideTurnStartPostfix(
        ICombatState combatState,
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!IsPlayerSide(side)
            || !TryGetLocalParticipant(combatState, participants, out var creature)
            || creature is null)
        {
            return;
        }

        var player = creature.Player;
        if (player is null)
        {
            return;
        }

        DamageForecastHudSnapshotStore.OnPlayerSideTurnStarted(player, creature);
        ForecastRefreshPatch.ResumeEndTurnAnchor();
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Hook.AfterPlayerTurnStart))]
    private static void AfterPlayerTurnStartPostfix(
        ICombatState combatState,
        PlayerChoiceContext choiceContext,
        Player player)
    {
        try
        {
            var localPlayer = LocalContext.GetMe(combatState);
            var creature = player.Creature;
            if (localPlayer is null
                || creature is null
                || player.NetId != localPlayer.NetId)
            {
                return;
            }

            DamageForecastHudSnapshotStore.OnPlayerSideTurnStarted(player, creature);
            ForecastRefreshPatch.ResumeEndTurnAnchor();
            ForecastRefreshPatch.RefreshRegisteredBars();
        }
        catch
        {
        }
    }

    internal static void CommitTurnEndSnapshot(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!IsPlayerSide(side)
            || !TryGetLocalParticipant(combatState, participants, out var creature)
            || creature is null)
        {
            return;
        }

        var player = creature.Player;
        if (player is null)
        {
            return;
        }

        ForecastRefreshPatch.CommitFinalSnapshot(creature);
        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Hook.AfterCombatEnd))]
    private static void AfterCombatEndPostfix()
    {
        ForecastEndTurnFreezePatch.Clear();
        DamageForecastHudSnapshotStore.Clear();
        ForecastRefreshPatch.HideAndClearRegisteredBars();
        HudAnchorResolver.Clear();
        ObservedHpLossBudgetTracker.Clear();
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
        Aud0007VisibilityProfiler.Dump("combat-end", reset: true);
#endif
    }

    internal static bool HasHookMethod(string methodName)
    {
        return AccessTools.Method(typeof(Hook), methodName) is not null;
    }

    private static bool TryGetLocalParticipant(
        ICombatState combatState,
        IEnumerable<Creature> participants,
        out Creature? creature)
    {
        creature = null;
        try
        {
            var localPlayer = LocalContext.GetMe(combatState);
            var localCreature = localPlayer?.Creature;
            if (localPlayer is null || localCreature is null)
            {
                return false;
            }

            foreach (var participant in participants)
            {
                if (ReferenceEquals(participant, localCreature)
                    || participant.Player?.NetId == localPlayer.NetId)
                {
                    creature = participant;
                    return participant.Player is not null;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsPlayerSide(CombatSide side)
    {
        return side.ToString().Contains("Player", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(Hook))]
internal static class ForecastStableTurnEndPatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ForecastTurnLifecyclePatch.HasHookMethod("BeforeTurnEnd");
    }

    [HarmonyPostfix]
    [HarmonyPatch("BeforeTurnEnd")]
    private static void BeforeTurnEndPostfix(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        ForecastTurnLifecyclePatch.CommitTurnEndSnapshot(combatState, side, participants);
    }
}

[HarmonyPatch(typeof(Hook))]
internal static class ForecastBetaTurnEndPatch
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        return ForecastTurnLifecyclePatch.HasHookMethod("BeforeSideTurnEnd");
    }

    [HarmonyPostfix]
    [HarmonyPatch("BeforeSideTurnEnd")]
    private static void BeforeSideTurnEndPostfix(
        ICombatState combatState,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        ForecastTurnLifecyclePatch.CommitTurnEndSnapshot(combatState, side, participants);
    }
}
