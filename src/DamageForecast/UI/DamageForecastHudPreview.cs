using Godot;

namespace DamageForecast.UI;

internal sealed partial class DamageForecastHudPreview : Control
{
    private const float PreviewHeight = 190f;
    private DamageForecastHudRoot? _hudRoot;
    private ColorRect? _healthBar;
    private ColorRect? _endTurnButton;

    public DamageForecastHudPreview()
    {
        CustomMinimumSize = new Vector2(640f, PreviewHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
    }

    public override void _Ready()
    {
        _healthBar = CreateAnchor(new Color(0.55f, 0.13f, 0.15f, 1f));
        _endTurnButton = CreateAnchor(new Color(0.16f, 0.30f, 0.42f, 1f));
        _hudRoot = new DamageForecastHudRoot
        {
            Name = DamageForecastHudRoot.RootName,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_hudRoot);
        _hudRoot.Initialize();
        Resized += RefreshPreview;
        DamageForecastUiSettings.Changed += RefreshPreview;
        RefreshPreview();
    }

    public override void _ExitTree()
    {
        Resized -= RefreshPreview;
        DamageForecastUiSettings.Changed -= RefreshPreview;
    }

    private ColorRect CreateAnchor(Color color)
    {
        var anchor = new ColorRect
        {
            Color = color,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(anchor);
        return anchor;
    }

    private void RefreshPreview()
    {
        if (_hudRoot is null || _healthBar is null || _endTurnButton is null)
        {
            return;
        }

        var width = MathF.Max(CustomMinimumSize.X, Size.X);
        _hudRoot.Position = Vector2.Zero;
        _hudRoot.Size = new Vector2(width, PreviewHeight);
        var healthRect = new HudLayoutRect(
            55f,
            76f,
            Math.Clamp(width * 0.42f, 220f, 340f),
            28f);
        var endRect = new HudLayoutRect(
            MathF.Max(healthRect.Right + 110f, width - 190f),
            118f,
            150f,
            48f);
        _healthBar.Position = new Vector2(healthRect.X, healthRect.Y);
        _healthBar.Size = new Vector2(healthRect.Width, healthRect.Height);
        _endTurnButton.Position = new Vector2(endRect.X, endRect.Y);
        _endTurnButton.Size = new Vector2(endRect.Width, endRect.Height);

        _hudRoot.Apply(
            "-17",
            "24",
            "\U0001F6E1 17   \u2665 3",
            DamageForecastUiSettings.ExpectedHpLossPlacementPreset,
            DamageForecastUiSettings.IncomingDamagePlacementPreset,
            DamageForecastUiSettings.DetailsPlacementPreset,
            DamageForecastUiSettings.IncomingDamagePlacement,
            DamageForecastUiSettings.OffsetX,
            DamageForecastUiSettings.OffsetY,
            endTurnSurface: null,
            () => new HudLayoutRect(0f, 0f, width, PreviewHeight),
            preset => preset == HudPlacementPreset.EndTurnButtonAbove
                ? endRect
                : healthRect);
    }
}
