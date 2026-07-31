using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DamageForecast.UI;

internal static class HudAnchorResolver
{
    internal const string EndTurnAnchorMarkerName = "DamageForecastEndTurnAnchorMarker";
    private static readonly FieldInfo? PowerCreatureField = typeof(NPowerContainer).GetField(
        "_creature",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? HealthBarCreatureField = typeof(NHealthBar).GetField(
        "_creature",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CombatUiStateField = typeof(NCombatUi).GetField(
        "_state",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? EndTurnButtonCombatUiField = typeof(NEndTurnButton).GetField(
        "_combatUi",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? EndTurnButtonImageField = typeof(NEndTurnButton).GetField(
        "_image",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? EndTurnButtonLabelField = typeof(NEndTurnButton).GetField(
        "_label",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static WeakReference<Creature>? _creatureEntity;
    private static WeakReference<NCreature>? _creatureNode;
    private static WeakReference<NPowerContainer>? _powerContainer;

    public static bool TryResolveHealthBar(
        DamageForecastHudRoot root,
        Control healthBar,
        Vector2? sizeOverride,
        out HudLayoutRect anchor)
    {
        var size = sizeOverride ?? healthBar.Size;
        return TryTransformRect(root, healthBar, new Rect2(Vector2.Zero, size), out anchor);
    }

    public static bool TryResolveEndTurnButton(
        DamageForecastHudRoot root,
        out HudLayoutRect anchor)
    {
        var button = ResolveEndTurnButton(root);
        if (button is not null
            && ReferenceEquals(root.GetParent(), button)
            && HasEndTurnAnchorMarker(button))
        {
            HudLayoutRect? labelAnchor = null;
            if (EndTurnButtonLabelField?.GetValue(button) is Control label
                && IsUsable(label)
                && TryTransformRect(
                    root,
                    label,
                    new Rect2(Vector2.Zero, label.Size),
                    out var transformedLabel))
            {
                labelAnchor = transformedLabel;
            }

            HudLayoutRect? imageAnchor = null;
            if (EndTurnButtonImageField?.GetValue(button) is Control image
                && IsUsable(image)
                && TryTransformRect(
                    root,
                    image,
                    new Rect2(Vector2.Zero, image.Size),
                    out var transformedImage))
            {
                imageAnchor = transformedImage;
            }

            anchor = HudEndTurnLocalAnchorPolicy.Resolve(
                new HudLayoutSize(button.Size.X, button.Size.Y),
                labelAnchor,
                imageAnchor);
            return anchor.Width > 0f && anchor.Height > 0f;
        }

        anchor = default;
        return false;
    }

    public static bool TryResolveCharacterAbove(
        DamageForecastHudRoot root,
        Creature creature,
        Control healthBar,
        Vector2? sizeOverride,
        out HudLayoutRect anchor)
    {
        if (!TryResolveHealthBar(root, healthBar, sizeOverride, out var healthBarAnchor))
        {
            anchor = default;
            return false;
        }

        var creatureNode = ResolveCreatureNode(root, creature);
        HudAnchorPoint? semanticAnchor = null;
        var visuals = creatureNode?.Visuals;
        if (creature.Player?.Character is not Regent
            && visuals?.TalkPosition is Node2D talkPosition
            && TryTransformPoint(root, talkPosition, out var transformedTalkPosition))
        {
            semanticAnchor = transformedTalkPosition;
        }

        HudLayoutRect? visualsAnchor = null;
        var bounds = visuals?.Bounds;
        if (bounds is not null
            && TryTransformRect(
                root,
                bounds,
                new Rect2(Vector2.Zero, bounds.Size),
                out var transformedBounds))
        {
            visualsAnchor = transformedBounds;
        }

        anchor = HudCharacterAboveAnchorPolicy.Resolve(
            healthBarAnchor,
            semanticAnchor,
            visualsAnchor);
        return true;
    }

    public static bool TryResolvePowerAvoidance(
        DamageForecastHudRoot root,
        Creature creature,
        out HudLayoutAvoidance avoidance)
    {
        var container = ResolvePowerContainer(root, creature);
        if (container is null)
        {
            avoidance = default;
            return false;
        }

        var visiblePowerRects = container.GetChildren()
            .OfType<Control>()
            .Where(control => GodotObject.IsInstanceValid(control)
                && control.IsInsideTree()
                && control.IsVisibleInTree()
                && control.Size.X > 0f
                && control.Size.Y > 0f)
            .Select(control => TryTransformRect(
                    root,
                    control,
                    new Rect2(Vector2.Zero, control.Size),
                    out var rect)
                ? rect
                : (HudLayoutRect?)null)
            .Where(rect => rect is not null)
            .Select(rect => rect!.Value)
            .ToArray();
        if (visiblePowerRects.Length == 0)
        {
            avoidance = default;
            return false;
        }

        var rect = UnionOf(visiblePowerRects);
        var rowHeight = visiblePowerRects.Max(item => item.Height);
        avoidance = new HudLayoutAvoidance(rect, rowHeight);
        return rowHeight > 0f;
    }

    public static HudLayoutRect ResolveAvailableBounds(Control root)
    {
        var viewportRect = root.GetViewportRect();
        var inverse = root.GetGlobalTransformWithCanvas().AffineInverse();
        return BoundsOf(
            inverse * viewportRect.Position,
            inverse * new Vector2(viewportRect.End.X, viewportRect.Position.Y),
            inverse * viewportRect.End,
            inverse * new Vector2(viewportRect.Position.X, viewportRect.End.Y));
    }

    public static NEndTurnButton? ResolveEndTurnButton(Node context)
    {
        var owner = ResolveCurrentCombatUi(context);
        var button = owner?.EndTurnButton;
        return HudEndTurnAnchorOwnershipPolicy.CanUse(
                ownerUsable: IsUsable(owner),
                buttonUsable: IsUsable(button),
                ownerMatchesContext: owner is not null
                    && OwnerMatchesContextCombat(owner, context),
                buttonBelongsToOwner: button is not null
                    && ReferenceEquals(FindAncestor<NCombatUi>(button), owner)
                    && ReferenceEquals(EndTurnButtonCombatUiField?.GetValue(button), owner),
                markerPresent: button is not null && HasEndTurnAnchorMarker(button))
            ? button
            : null;
    }

    public static void EnsureEndTurnAnchor(NEndTurnButton button)
    {
        if (!IsUsable(button))
        {
            return;
        }

        if (button.GetNodeOrNull<Node>(EndTurnAnchorMarkerName) is not null)
        {
            return;
        }

        button.AddChild(new Node
        {
            Name = EndTurnAnchorMarkerName
        });
    }

    public static Control? ResolveEndTurnSurfaceParent(NEndTurnButton button)
    {
        var owner = ResolveCurrentCombatUi(button);
        return ReferenceEquals(owner?.EndTurnButton, button)
            && HasEndTurnAnchorMarker(button)
            ? owner
            : null;
    }

    public static void Clear()
    {
        _creatureEntity = null;
        _creatureNode = null;
        _powerContainer = null;
    }

    private static NCreature? ResolveCreatureNode(Node context, Creature creature)
    {
        if (_creatureEntity is not null
            && _creatureEntity.TryGetTarget(out var cachedEntity)
            && ReferenceEquals(cachedEntity, creature)
            && _creatureNode is not null
            && _creatureNode.TryGetTarget(out var cachedNode)
            && IsUsable(cachedNode)
            && ReferenceEquals(cachedNode.Entity, creature))
        {
            return cachedNode;
        }

        var root = context.GetTree()?.Root;
        var found = root is null
            ? null
            : FindDescendants<NCreature>(root)
                .FirstOrDefault(node => IsUsable(node) && ReferenceEquals(node.Entity, creature));
        _creatureEntity = found is null ? null : new WeakReference<Creature>(creature);
        _creatureNode = found is null ? null : new WeakReference<NCreature>(found);
        _powerContainer = null;
        return found;
    }

    private static NPowerContainer? ResolvePowerContainer(Node context, Creature creature)
    {
        if (_powerContainer is not null
            && _powerContainer.TryGetTarget(out var cached)
            && IsUsable(cached)
            && ReferenceEquals(PowerCreatureField?.GetValue(cached), creature))
        {
            return cached;
        }

        var creatureNode = ResolveCreatureNode(context, creature);
        Node? root = creatureNode is not null
            ? creatureNode
            : context.GetTree()?.Root;
        var found = root is null
            ? null
            : FindDescendants<NPowerContainer>(root)
                .FirstOrDefault(container =>
                    IsUsable(container)
                    && ReferenceEquals(PowerCreatureField?.GetValue(container), creature));
        if (found is null && creatureNode is not null)
        {
            var treeRoot = context.GetTree()?.Root;
            found = treeRoot is null
                ? null
                : FindDescendants<NPowerContainer>(treeRoot)
                    .FirstOrDefault(container =>
                        IsUsable(container)
                        && ReferenceEquals(PowerCreatureField?.GetValue(container), creature));
        }

        _powerContainer = found is null ? null : new WeakReference<NPowerContainer>(found);
        return found;
    }

    private static bool TryTransformRect(
        Control target,
        Control source,
        Rect2 sourceRect,
        out HudLayoutRect result)
    {
        if (!GodotObject.IsInstanceValid(target)
            || !GodotObject.IsInstanceValid(source)
            || !target.IsInsideTree()
            || !source.IsInsideTree())
        {
            result = default;
            return false;
        }

        var transform = target.GetGlobalTransformWithCanvas().AffineInverse()
            * source.GetGlobalTransformWithCanvas();
        result = BoundsOf(
            transform * sourceRect.Position,
            transform * new Vector2(sourceRect.End.X, sourceRect.Position.Y),
            transform * sourceRect.End,
            transform * new Vector2(sourceRect.Position.X, sourceRect.End.Y));
        return result.Width > 0f && result.Height > 0f;
    }

    private static bool TryTransformPoint(
        Control target,
        Node2D source,
        out HudAnchorPoint result)
    {
        if (!GodotObject.IsInstanceValid(target)
            || !GodotObject.IsInstanceValid(source)
            || !target.IsInsideTree()
            || !source.IsInsideTree())
        {
            result = default;
            return false;
        }

        var transformed = target.GetGlobalTransformWithCanvas().AffineInverse()
            * source.GetGlobalTransformWithCanvas()
            * Vector2.Zero;
        result = new HudAnchorPoint(transformed.X, transformed.Y);
        return true;
    }

    private static HudLayoutRect BoundsOf(params Vector2[] points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new HudLayoutRect(left, top, right - left, bottom - top);
    }

    private static HudLayoutRect UnionOf(IReadOnlyList<HudLayoutRect> rects)
    {
        var left = rects.Min(rect => rect.Left);
        var top = rects.Min(rect => rect.Top);
        var right = rects.Max(rect => rect.Right);
        var bottom = rects.Max(rect => rect.Bottom);
        return new HudLayoutRect(left, top, right - left, bottom - top);
    }

    private static T? FindAncestor<T>(Node node)
        where T : Node
    {
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            if (current is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private static T? FindSelfOrAncestor<T>(Node node)
        where T : Node
    {
        return node is T typed ? typed : FindAncestor<T>(node);
    }

    private static NCombatUi? ResolveCurrentCombatUi(Node context)
    {
        var nearest = FindSelfOrAncestor<NCombatUi>(context);
        if (IsUsable(nearest) && OwnerMatchesContextCombat(nearest!, context))
        {
            return nearest;
        }

        var state = ResolveContextCombatState(context);
        var root = context.GetTree()?.Root;
        if (state is null || root is null)
        {
            return null;
        }

        var matches = FindDescendants<NCombatUi>(root)
            .Where(IsUsable)
            .Where(owner => ReferenceEquals(CombatUiStateField?.GetValue(owner), state))
            .Where(owner => IsUsable(owner.EndTurnButton))
            .Where(owner => ReferenceEquals(
                EndTurnButtonCombatUiField?.GetValue(owner.EndTurnButton),
                owner))
            .Where(owner => HasEndTurnAnchorMarker(owner.EndTurnButton))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static object? ResolveContextCombatState(Node context)
    {
        var healthBar = FindSelfOrAncestor<NHealthBar>(context);
        if (healthBar is not null
            && HealthBarCreatureField?.GetValue(healthBar) is Creature creature)
        {
            return creature.CombatState;
        }

        return null;
    }

    private static bool OwnerMatchesContextCombat(NCombatUi owner, Node context)
    {
        var nearest = FindSelfOrAncestor<NCombatUi>(context);
        if (nearest is not null)
        {
            return ReferenceEquals(nearest, owner);
        }

        var contextState = ResolveContextCombatState(context);
        return contextState is not null
            && ReferenceEquals(CombatUiStateField?.GetValue(owner), contextState);
    }

    private static bool HasEndTurnAnchorMarker(NEndTurnButton button) =>
        button.GetNodeOrNull<Node>(EndTurnAnchorMarkerName) is { } marker
        && ReferenceEquals(marker.GetParent(), button);

    private static IEnumerable<T> FindDescendants<T>(Node node)
        where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T typed)
            {
                yield return typed;
            }

            if (child is Node childNode)
            {
                foreach (var nested in FindDescendants<T>(childNode))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool IsUsable(GodotObject? value) =>
        value is Node node
        && GodotObject.IsInstanceValid(value)
        && !node.IsQueuedForDeletion()
        && node.IsInsideTree();
}

internal static class HudEndTurnAnchorOwnershipPolicy
{
    public static bool CanUse(
        bool ownerUsable,
        bool buttonUsable,
        bool ownerMatchesContext,
        bool buttonBelongsToOwner,
        bool markerPresent) =>
        ownerUsable
        && buttonUsable
        && ownerMatchesContext
        && buttonBelongsToOwner
        && markerPresent;
}
