using Godot;

namespace DamageForecast.UI;

internal sealed partial class DamageForecastHudRoot : Control
{
    public const string RootName = "DamageForecastHudRoot";
    public const string OwnershipGroup = "damage-forecast-hud-root";
    public const string ExpectedLabelName = "DamageForecastExpectedLossLabel";
    public const string IncomingLabelName = "DamageForecastIncomingDamageLabel";
    public const string DetailLabelName = "DamageForecastDetailsLabel";

    private Label? _expectedLabel;
    private Label? _incomingLabel;
    private RichTextLabel? _detailLabel;
    private RenderState? _renderState;
    private bool _dynamicAnchorFrozen;

    public void Initialize()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 50;
        ClipContents = false;
        EnsureChildren();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_renderState is not null)
        {
            Relayout();
        }
    }

    public void Apply(
        string expectedText,
        string incomingText,
        string detailText,
        HudPlacementPreset expectedPreset,
        HudPlacementPreset incomingPreset,
        HudPlacementPreset detailPreset,
        IncomingDamagePlacement incomingPlacement,
        float offsetX,
        float offsetY,
        bool? endTurnSurface,
        Func<HudLayoutRect> resolveAvailableBounds,
        Func<HudPlacementPreset, HudLayoutRect?> resolveAnchor,
        Func<HudPlacementPreset, HudLayoutAvoidance?>? resolveAvoidance = null,
        bool preserveOnAnchorFailure = false)
    {
        EnsureChildren();
        if (_expectedLabel is null || _incomingLabel is null || _detailLabel is null)
        {
            HideAll();
            return;
        }

        _expectedLabel.Text = expectedText;
        _incomingLabel.Text = incomingText;
        _detailLabel.Text = detailText;
        DamageForecastHudDisplay.ApplyMainHudStyle(_expectedLabel);
        DamageForecastHudDisplay.ApplyIncomingHudStyle(_incomingLabel);
        DamageForecastHudDisplay.ApplyDetailHudStyle(_detailLabel);
        DamageForecastHudDisplay.ApplyNumericHudAlignment(
            _expectedLabel,
            expectedPreset);
        DamageForecastHudDisplay.ApplyNumericHudAlignment(
            _incomingLabel,
            incomingPreset);
        DamageForecastHudDisplay.ApplyMainHudTextBounds(_expectedLabel);
        DamageForecastHudDisplay.ApplyHudTextBounds(_incomingLabel);
        DamageForecastHudDisplay.ApplyDetailHudTextBounds(_detailLabel);

        _renderState = new RenderState(
            expectedText,
            incomingText,
            detailText,
            expectedPreset,
            incomingPreset,
            detailPreset,
            incomingPlacement,
            offsetX,
            offsetY,
            endTurnSurface,
            resolveAvailableBounds,
            resolveAnchor,
            resolveAvoidance,
            preserveOnAnchorFailure);
        var usesDynamicAnchor = UsesDynamicAnchor(_renderState);
        SetProcess(HudDynamicAnchorTrackingPolicy.ShouldProcess(
            _dynamicAnchorFrozen,
            usesDynamicAnchor));
        if (HudDynamicAnchorTrackingPolicy.ShouldRelayoutOnApply(
                _dynamicAnchorFrozen,
                HasVisibleLayout()))
        {
            Relayout();
        }
    }

    public void HideAll()
    {
        _renderState = null;
        _dynamicAnchorFrozen = false;
        SetProcess(false);
        if (_expectedLabel is not null)
        {
            _expectedLabel.Text = string.Empty;
            _expectedLabel.Hide();
        }

        if (_incomingLabel is not null)
        {
            _incomingLabel.Text = string.Empty;
            _incomingLabel.Hide();
        }

        if (_detailLabel is not null)
        {
            _detailLabel.Text = string.Empty;
            _detailLabel.Hide();
        }

        Hide();
    }

    public void FreezeDynamicAnchor()
    {
        if (_renderState is not null)
        {
            Relayout();
        }

        _dynamicAnchorFrozen = true;
        SetProcess(false);
    }

    public void ResumeDynamicAnchor()
    {
        _dynamicAnchorFrozen = false;
        if (_renderState is null)
        {
            return;
        }

        SetProcess(UsesDynamicAnchor(_renderState));
        Relayout();
    }

    public bool CopyVisibleSnapshotTo(DamageForecastHudRoot target)
    {
        EnsureChildren();
        target.EnsureChildren();
        target.HideAll();
        if (_expectedLabel is null
            || _incomingLabel is null
            || _detailLabel is null
            || target._expectedLabel is null
            || target._incomingLabel is null
            || target._detailLabel is null)
        {
            return false;
        }

        var anyVisible = CopyVisibleControl(
                _expectedLabel,
                target._expectedLabel,
                target)
            | CopyVisibleControl(
                _incomingLabel,
                target._incomingLabel,
                target)
            | CopyVisibleControl(
                _detailLabel,
                target._detailLabel,
                target);
        target._renderState = null;
        target._dynamicAnchorFrozen = true;
        target.SetProcess(false);
        target.Visible = anyVisible;
        return anyVisible;
    }

    private void Relayout()
    {
        if (_renderState is not { } state
            || _expectedLabel is null
            || _incomingLabel is null
            || _detailLabel is null)
        {
            return;
        }

        var specifications = new[]
        {
            new ContentSpecification(
                HudLayoutContent.ExpectedHpLoss,
                _expectedLabel,
                state.ExpectedText,
                state.ExpectedPreset),
            new ContentSpecification(
                HudLayoutContent.IncomingDamage,
                _incomingLabel,
                state.IncomingText,
                state.IncomingPreset),
            new ContentSpecification(
                HudLayoutContent.Details,
                _detailLabel,
                state.DetailText,
                state.DetailPreset)
        };

        var eligible = specifications
            .Where(item => !string.IsNullOrEmpty(item.Text)
                && (state.EndTurnSurface is null
                    || (item.Preset == HudPlacementPreset.EndTurnButtonAbove) == state.EndTurnSurface.Value))
            .ToArray();
        foreach (var specification in specifications.Except(eligible))
        {
            specification.Control.Hide();
        }

        foreach (var group in eligible.GroupBy(item => item.Preset))
        {
            var anchor = state.ResolveAnchor(group.Key);
            if (anchor is null)
            {
                if (!state.PreserveOnAnchorFailure)
                {
                    foreach (var specification in group)
                    {
                        specification.Control.Hide();
                    }
                }

                continue;
            }

            var request = new HudLayoutRequest(
                anchor.Value,
                state.ResolveAvailableBounds(),
                group.Key,
                group.Select(item => new HudLayoutItem(
                    item.Content,
                    new HudLayoutSize(item.Control.Size.X, item.Control.Size.Y))).ToArray(),
                state.IncomingPlacement,
                state.OffsetX,
                state.OffsetY,
                state.ResolveAvoidance?.Invoke(group.Key));
            var result = HudLayoutEngine.Layout(request);
            foreach (var specification in group.Where(item => !result.Contains(item.Content)))
            {
                specification.Control.Hide();
            }

            foreach (var placement in result.Placements)
            {
                var specification = specifications.Single(item => item.Content == placement.Content);
                specification.Control.Position = new Vector2(
                    placement.Rect.X,
                    placement.Rect.Y);
                specification.Control.Size = new Vector2(
                    placement.Rect.Width,
                    placement.Rect.Height);
                specification.Control.Show();
            }
        }

        Visible = specifications.Any(item => item.Control.Visible);
    }

    private static bool UsesDynamicAnchor(RenderState state) =>
        new[]
        {
            (state.ExpectedText, state.ExpectedPreset),
            (state.IncomingText, state.IncomingPreset),
            (state.DetailText, state.DetailPreset)
        }.Any(item => !string.IsNullOrEmpty(item.Item1)
            && (state.EndTurnSurface is null
                || (item.Item2 == HudPlacementPreset.EndTurnButtonAbove) == state.EndTurnSurface.Value)
            && item.Item2 is HudPlacementPreset.HealthBarAbove
                or HudPlacementPreset.HealthBarBelow);

    private static bool CopyVisibleControl(
        Control source,
        Control target,
        DamageForecastHudRoot targetRoot)
    {
        if (!source.Visible || source.Size.X <= 0f || source.Size.Y <= 0f)
        {
            target.Hide();
            return false;
        }

        var transform = targetRoot.GetGlobalTransformWithCanvas().AffineInverse()
            * source.GetGlobalTransformWithCanvas();
        var sourceRect = new Rect2(Vector2.Zero, source.Size);
        var topLeft = transform * sourceRect.Position;
        var topRight = transform * new Vector2(sourceRect.End.X, sourceRect.Position.Y);
        var bottomRight = transform * sourceRect.End;
        var bottomLeft = transform * new Vector2(sourceRect.Position.X, sourceRect.End.Y);
        var left = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomRight.X, bottomLeft.X));
        var top = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomRight.Y, bottomLeft.Y));
        var right = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomRight.X, bottomLeft.X));
        var bottom = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomRight.Y, bottomLeft.Y));
        if (right <= left || bottom <= top)
        {
            target.Hide();
            return false;
        }

        if (source is Label sourceLabel && target is Label targetLabel)
        {
            targetLabel.Text = sourceLabel.Text;
        }
        else if (source is RichTextLabel sourceRichText
            && target is RichTextLabel targetRichText)
        {
            targetRichText.Text = sourceRichText.Text;
        }

        target.Position = new Vector2(left, top);
        target.Size = new Vector2(right - left, bottom - top);
        target.Show();
        return true;
    }

    private bool HasVisibleLayout() =>
        _expectedLabel?.Visible == true
        || _incomingLabel?.Visible == true
        || _detailLabel?.Visible == true;

    private void EnsureChildren()
    {
        _expectedLabel ??= FindOrCreateLabel(
            ExpectedLabelName,
            DamageForecastHudDisplay.ApplyMainHudStyle);
        _incomingLabel ??= FindOrCreateLabel(
            IncomingLabelName,
            DamageForecastHudDisplay.ApplyIncomingHudStyle);
        _detailLabel ??= FindOrCreateDetailLabel();
    }

    private Label FindOrCreateLabel(string name, Action<Label> applyStyle)
    {
        var label = GetNodeOrNull<Label>(name);
        if (label is null)
        {
            label = new Label
            {
                Name = name,
                Text = string.Empty,
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(label);
        }

        applyStyle(label);
        return label;
    }

    private RichTextLabel FindOrCreateDetailLabel()
    {
        var label = GetNodeOrNull<RichTextLabel>(DetailLabelName);
        if (label is null)
        {
            label = new RichTextLabel
            {
                Name = DetailLabelName,
                Text = string.Empty,
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(label);
        }

        DamageForecastHudDisplay.ApplyDetailHudStyle(label);
        return label;
    }

    private readonly record struct ContentSpecification(
        HudLayoutContent Content,
        Control Control,
        string Text,
        HudPlacementPreset Preset);

    private sealed record RenderState(
        string ExpectedText,
        string IncomingText,
        string DetailText,
        HudPlacementPreset ExpectedPreset,
        HudPlacementPreset IncomingPreset,
        HudPlacementPreset DetailPreset,
        IncomingDamagePlacement IncomingPlacement,
        float OffsetX,
        float OffsetY,
        bool? EndTurnSurface,
        Func<HudLayoutRect> ResolveAvailableBounds,
        Func<HudPlacementPreset, HudLayoutRect?> ResolveAnchor,
        Func<HudPlacementPreset, HudLayoutAvoidance?>? ResolveAvoidance,
        bool PreserveOnAnchorFailure);
}

internal static class HudDynamicAnchorTrackingPolicy
{
    public static bool ShouldProcess(bool isFrozen, bool usesDynamicAnchor) =>
        !isFrozen && usesDynamicAnchor;

    public static bool ShouldRelayoutOnApply(bool isFrozen, bool hasVisibleLayout) =>
        !isFrozen || !hasVisibleLayout;
}
