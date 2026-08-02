namespace DamageForecast.UI;

internal static class HudLayoutEngine
{
    private const float HealthBarHorizontalPadding = 22f;
    private const float VerticalPadding = 14f;
    private const float EndTurnVerticalPadding = 2f;
    private const float EndTurnDownwardOffset = 2f;
    private const float SegmentGap = 8f;
    private const float DetailRowGap = 6f;

    public static HudLayoutResult Layout(HudLayoutRequest request)
    {
        var expected = Find(request.Items, HudLayoutContent.ExpectedHpLoss);
        var incoming = Find(request.Items, HudLayoutContent.IncomingDamage);
        var details = Find(request.Items, HudLayoutContent.Details);
        var numeric = OrderNumeric(expected, incoming, request.IncomingDamagePlacement);
        var numericRow = BuildRow(numeric, SegmentGap);
        var candidates = BuildCandidates(numericRow, details);
        var capacity = CapacityFor(request);
        var selected = candidates.FirstOrDefault(candidate => Fits(candidate.Size, capacity))
            ?? candidates[^1];
        var detailsHidden = details is not null
            && selected.Items.All(item => item.Content != HudLayoutContent.Details);
        var origin = OriginFor(request, selected.Size);
        var localItems = request.Preset == HudPlacementPreset.HealthBarLeft
            ? MirrorHorizontally(selected.Items, selected.Size.Width)
            : selected.Items;
        var placements = localItems
            .Select(item => new HudLayoutPlacement(
                item.Content,
                new HudLayoutRect(
                    origin.X + item.Rect.X,
                    origin.Y + item.Rect.Y,
                    item.Rect.Width,
                    item.Rect.Height)))
            .ToArray();
        return new HudLayoutResult(placements, detailsHidden);
    }

    private static IReadOnlyList<HudLayoutCandidateItem> MirrorHorizontally(
        IReadOnlyList<HudLayoutCandidateItem> items,
        float width) =>
        items.Select(item => item with
        {
            Rect = item.Rect with { X = width - item.Rect.Right }
        }).ToArray();

    private static HudLayoutItem? Find(
        IReadOnlyList<HudLayoutItem> items,
        HudLayoutContent content)
    {
        foreach (var item in items)
        {
            if (item.Content == content)
            {
                return item;
            }
        }

        return null;
    }

    private static IReadOnlyList<HudLayoutItem> OrderNumeric(
        HudLayoutItem? expected,
        HudLayoutItem? incoming,
        IncomingDamagePlacement placement)
    {
        if (expected is null)
        {
            return incoming is null ? [] : [incoming.Value];
        }

        if (incoming is null)
        {
            return [expected.Value];
        }

        return placement == IncomingDamagePlacement.LeftOfExpectedHpLoss
            ? [incoming.Value, expected.Value]
            : [expected.Value, incoming.Value];
    }

    private static IReadOnlyList<HudLayoutCandidate> BuildCandidates(
        HudLayoutRow numeric,
        HudLayoutItem? details)
    {
        if (details is null)
        {
            return [new HudLayoutCandidate(numeric.Items, numeric.Size)];
        }

        if (numeric.Items.Count == 0)
        {
            var item = details.Value;
            return
            [
                new HudLayoutCandidate(
                    [new HudLayoutCandidateItem(item.Content, RectAt(item.Size, 0f, 0f))],
                    item.Size)
            ];
        }

        var detail = details.Value;
        var inlineHeight = MathF.Max(numeric.Size.Height, detail.Size.Height);
        var inlineItems = numeric.Items
            .Select(item => item with
            {
                Rect = item.Rect with { Y = (inlineHeight - item.Rect.Height) * 0.5f }
            })
            .Append(new HudLayoutCandidateItem(
                detail.Content,
                RectAt(
                    detail.Size,
                    numeric.Size.Width + SegmentGap,
                    (inlineHeight - detail.Size.Height) * 0.5f)))
            .ToArray();
        var inline = new HudLayoutCandidate(
            inlineItems,
            new HudLayoutSize(
                numeric.Size.Width + SegmentGap + detail.Size.Width,
                inlineHeight));

        var wrapped = new HudLayoutCandidate(
            numeric.Items
                .Append(new HudLayoutCandidateItem(
                    detail.Content,
                    RectAt(detail.Size, 0f, numeric.Size.Height + DetailRowGap)))
                .ToArray(),
            new HudLayoutSize(
                MathF.Max(numeric.Size.Width, detail.Size.Width),
                numeric.Size.Height + DetailRowGap + detail.Size.Height));

        var numericOnly = new HudLayoutCandidate(numeric.Items, numeric.Size);
        return [inline, wrapped, numericOnly];
    }

    private static HudLayoutRow BuildRow(
        IReadOnlyList<HudLayoutItem> items,
        float gap)
    {
        var x = 0f;
        var height = items.Count == 0 ? 0f : items.Max(item => item.Size.Height);
        var placements = new List<HudLayoutCandidateItem>(items.Count);
        foreach (var item in items)
        {
            placements.Add(new HudLayoutCandidateItem(
                item.Content,
                RectAt(item.Size, x, (height - item.Size.Height) * 0.5f)));
            x += item.Size.Width + gap;
        }

        if (placements.Count > 0)
        {
            x -= gap;
        }

        return new HudLayoutRow(placements, new HudLayoutSize(x, height));
    }

    private static HudLayoutSize CapacityFor(HudLayoutRequest request)
    {
        var bounds = request.AvailableBounds;
        var anchor = request.Anchor;
        return request.Preset switch
        {
            HudPlacementPreset.HealthBarRight => new(
                MathF.Max(0f, bounds.Right - anchor.Right - HealthBarHorizontalPadding),
                bounds.Height),
            HudPlacementPreset.HealthBarLeft => new(
                MathF.Max(0f, anchor.Left - bounds.Left - HealthBarHorizontalPadding),
                bounds.Height),
            HudPlacementPreset.HealthBarBelow => new(
                bounds.Width,
                MathF.Max(0f, bounds.Bottom - anchor.Bottom - VerticalPadding)),
            HudPlacementPreset.EndTurnButtonAbove => new(
                bounds.Width,
                MathF.Max(
                    0f,
                    anchor.Top - bounds.Top - EndTurnVerticalPadding + EndTurnDownwardOffset)),
            _ => new(
                bounds.Width,
                MathF.Max(0f, anchor.Top - bounds.Top - VerticalPadding))
        };
    }

    private static HudLayoutPoint OriginFor(
        HudLayoutRequest request,
        HudLayoutSize cluster)
    {
        var anchor = request.Anchor;
        var bounds = request.AvailableBounds;
        var raw = request.Preset switch
        {
            HudPlacementPreset.HealthBarRight => new HudLayoutPoint(
                anchor.Right + HealthBarHorizontalPadding,
                anchor.CenterY - (cluster.Height * 0.5f)),
            HudPlacementPreset.HealthBarLeft => new HudLayoutPoint(
                anchor.Left - HealthBarHorizontalPadding - cluster.Width,
                anchor.CenterY - (cluster.Height * 0.5f)),
            HudPlacementPreset.HealthBarBelow => new HudLayoutPoint(
                anchor.CenterX - (cluster.Width * 0.5f),
                anchor.Bottom + VerticalPadding),
            HudPlacementPreset.EndTurnButtonAbove => new HudLayoutPoint(
                anchor.CenterX - (cluster.Width * 0.5f),
                anchor.Top - EndTurnVerticalPadding - cluster.Height + EndTurnDownwardOffset),
            _ => new HudLayoutPoint(
                anchor.CenterX - (cluster.Width * 0.5f),
                anchor.Top - VerticalPadding - cluster.Height)
        };
        raw = new HudLayoutPoint(
            raw.X + request.OffsetX,
            raw.Y + request.OffsetY);
        raw = AvoidObstacleBelow(request, cluster, raw);
        return request.Preset is HudPlacementPreset.HealthBarLeft
            or HudPlacementPreset.HealthBarRight
            ? raw with { Y = Clamp(raw.Y, bounds.Top, bounds.Bottom - cluster.Height) }
            : raw with { X = Clamp(raw.X, bounds.Left, bounds.Right - cluster.Width) };
    }

    private static HudLayoutPoint AvoidObstacleBelow(
        HudLayoutRequest request,
        HudLayoutSize cluster,
        HudLayoutPoint origin)
    {
        if (request.Preset != HudPlacementPreset.HealthBarBelow
            || request.Avoidance is not { } avoidance
            || avoidance.RowHeight <= 0f)
        {
            return origin;
        }

        var rect = new HudLayoutRect(origin.X, origin.Y, cluster.Width, cluster.Height);
        var maximumSteps = Math.Max(
            1,
            (int)MathF.Ceiling((avoidance.Rect.Height + cluster.Height) / avoidance.RowHeight) + 1);
        for (var step = 0; step < maximumSteps && Intersects(rect, avoidance.Rect); step++)
        {
            rect = rect with { Y = rect.Y + avoidance.RowHeight };
        }

        return new HudLayoutPoint(origin.X, rect.Y);
    }

    private static bool Intersects(HudLayoutRect left, HudLayoutRect right) =>
        left.Left < right.Right
        && left.Right > right.Left
        && left.Top < right.Bottom
        && left.Bottom > right.Top;

    private static bool Fits(HudLayoutSize size, HudLayoutSize capacity) =>
        size.Width <= capacity.Width && size.Height <= capacity.Height;

    private static HudLayoutRect RectAt(HudLayoutSize size, float x, float y) =>
        new(x, y, MathF.Max(0f, size.Width), MathF.Max(0f, size.Height));

    private static float Clamp(float value, float minimum, float maximum) =>
        maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);

    private readonly record struct HudLayoutPoint(float X, float Y);

    private sealed record HudLayoutRow(
        IReadOnlyList<HudLayoutCandidateItem> Items,
        HudLayoutSize Size);

    private sealed record HudLayoutCandidate(
        IReadOnlyList<HudLayoutCandidateItem> Items,
        HudLayoutSize Size);

    private readonly record struct HudLayoutCandidateItem(
        HudLayoutContent Content,
        HudLayoutRect Rect);
}

internal static class HudCharacterAboveAnchorPolicy
{
    public static HudLayoutRect Resolve(
        HudLayoutRect healthBar,
        HudAnchorPoint? semanticPoint,
        HudLayoutRect? visuals)
    {
        if (semanticPoint is { } point)
        {
            var top = MathF.Min(healthBar.Top, point.Y);
            return new HudLayoutRect(
                point.X - 0.5f,
                top,
                1f,
                MathF.Max(1f, healthBar.Top - top));
        }

        if (visuals is { } visualRect)
        {
            var top = MathF.Min(healthBar.Top, visualRect.Top);
            return new HudLayoutRect(
                visualRect.CenterX - 0.5f,
                top,
                1f,
                MathF.Max(1f, healthBar.Top - top));
        }

        return new HudLayoutRect(
            healthBar.CenterX - 0.5f,
            healthBar.Top,
            1f,
            1f);
    }
}

internal readonly record struct HudAnchorPoint(float X, float Y);

internal readonly record struct HudLayoutSize(float Width, float Height);

internal readonly record struct HudAffineTransform2D(
    float XAxisX,
    float XAxisY,
    float YAxisX,
    float YAxisY,
    float OriginX,
    float OriginY)
{
    public HudAnchorPoint Transform(HudAnchorPoint point) =>
        new(
            (XAxisX * point.X) + (YAxisX * point.Y) + OriginX,
            (XAxisY * point.X) + (YAxisY * point.Y) + OriginY);
}

internal static class HudEndTurnAnchorTransferPolicy
{
    public static HudLayoutRect Convert(
        HudLayoutRect liveAnchor,
        HudAffineTransform2D liveToFrozen)
    {
        var points = new[]
        {
            liveToFrozen.Transform(new HudAnchorPoint(liveAnchor.Left, liveAnchor.Top)),
            liveToFrozen.Transform(new HudAnchorPoint(liveAnchor.Right, liveAnchor.Top)),
            liveToFrozen.Transform(new HudAnchorPoint(liveAnchor.Right, liveAnchor.Bottom)),
            liveToFrozen.Transform(new HudAnchorPoint(liveAnchor.Left, liveAnchor.Bottom))
        };
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new HudLayoutRect(left, top, right - left, bottom - top);
    }
}

internal static class HudEndTurnLocalAnchorPolicy
{
    public static HudLayoutRect Resolve(
        HudLayoutSize buttonSize,
        HudLayoutRect? labelAnchor = null,
        HudLayoutRect? imageAnchor = null)
    {
        var buttonAnchor = new HudLayoutRect(
            0f,
            0f,
            Math.Max(0f, buttonSize.Width),
            Math.Max(0f, buttonSize.Height));
        var horizontalReference = labelAnchor is { Width: > 0f } label
            ? label
            : imageAnchor is { Width: > 0f } image
                ? image
                : (HudLayoutRect?)null;
        return horizontalReference is { } reference
            ? buttonAnchor with
            {
                X = reference.X,
                Width = reference.Width
            }
            : buttonAnchor;
    }
}

internal readonly record struct HudLayoutRect(
    float X,
    float Y,
    float Width,
    float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public float CenterX => X + (Width * 0.5f);
    public float CenterY => Y + (Height * 0.5f);
}

internal readonly record struct HudLayoutItem(
    HudLayoutContent Content,
    HudLayoutSize Size);

internal sealed record HudLayoutRequest(
    HudLayoutRect Anchor,
    HudLayoutRect AvailableBounds,
    HudPlacementPreset Preset,
    IReadOnlyList<HudLayoutItem> Items,
    IncomingDamagePlacement IncomingDamagePlacement,
    float OffsetX = 0f,
    float OffsetY = 0f,
    HudLayoutAvoidance? Avoidance = null);

internal readonly record struct HudLayoutAvoidance(
    HudLayoutRect Rect,
    float RowHeight);

internal readonly record struct HudLayoutPlacement(
    HudLayoutContent Content,
    HudLayoutRect Rect);

internal sealed record HudLayoutResult(
    IReadOnlyList<HudLayoutPlacement> Placements,
    bool DetailsHidden)
{
    public HudLayoutRect RectFor(HudLayoutContent content) =>
        Placements.Single(placement => placement.Content == content).Rect;

    public bool Contains(HudLayoutContent content) =>
        Placements.Any(placement => placement.Content == content);
}
