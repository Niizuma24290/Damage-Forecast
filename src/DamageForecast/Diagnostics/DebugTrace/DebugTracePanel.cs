using DamageForecast.Forecast;
using DamageForecast.Patches;
using DamageForecast.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DamageForecast.Diagnostics.DebugTrace;

internal static class DebugTracePanelController
{
    private const string PanelName = "DamageForecastDebugTracePanel";

    internal static void Sync(
        NHealthBar bar,
        Creature creature,
        ForecastHudSnapshot snapshot,
        bool usedCommittedSnapshot)
    {
        var panel = bar.GetNodeOrNull<DebugTracePanel>(PanelName);
        if (panel is null)
        {
            panel = new DebugTracePanel
            {
                Name = PanelName,
                ZIndex = 100,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            bar.AddChild(panel);
            panel.Initialize();
        }

        panel.Position = Vector2.Zero;
        panel.Size = bar.Size;
        panel.Bind(
            snapshot,
            DamageForecastHudSnapshotStore.CreateDebugTraceBinding(
                creature,
                snapshot.DebugTraceCaptureId,
                usedCommittedSnapshot));
        panel.Show();
    }

    internal static void Hide(NHealthBar bar)
    {
        bar.GetNodeOrNull<DebugTracePanel>(PanelName)?.Hide();
    }

    internal static void Invalidate(NHealthBar bar)
    {
        Hide(bar);
        DebugTraceRuntime.SetEnabled(false);
        DebugTraceRuntime.Clear();
    }

    internal static void Clear()
    {
        DebugTraceRuntime.SetEnabled(false);
        DebugTraceRuntime.Clear();
    }
}

internal sealed partial class DebugTracePanel : Control
{
    private const float ButtonRightInset = 24f;
    private const float ButtonTopInset = 220f;
    private const float PanelGap = 8f;
    private const float ViewportEdgeInset = 12f;

    private readonly List<DebugTraceValueKind> _availableKinds = [];
    private Button? _toggleButton;
    private PanelContainer? _panel;
    private OptionButton? _selector;
    private RichTextLabel? _content;
    private Button? _detailsButton;
    private ForecastHudSnapshot _snapshot;
    private DebugTraceDisplayBinding _binding;
    private DebugTraceCapture? _capture;
    private DebugTraceValueKind _selectedKind = DebugTraceValueKind.ExpectedTotalHpLoss;
    private bool _showDetails;
    private bool _dragging;
    private Vector2 _dragPointerOffset;
    private Vector2? _draggedViewportPosition;
    private bool _initialized;

    internal void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        SetProcessUnhandledInput(true);

        _toggleButton = new Button
        {
            Text = "调试 Debug",
            CustomMinimumSize = new Vector2(104, 30),
            MouseFilter = MouseFilterEnum.Stop
        };
        _toggleButton.Pressed += TogglePanel;
        AddChild(_toggleButton);
    }

    private void EnsurePanel()
    {
        if (_panel is not null)
        {
            return;
        }

        _panel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(540, 360),
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 101
        };
        AddChild(_panel);

        var layout = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(520, 340),
            MouseFilter = MouseFilterEnum.Pass
        };
        _panel.AddChild(layout);

        var title = new Label
        {
            Text = "预测计算说明 / Forecast Explanation",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Stop
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.GuiInput += HandleTitleGuiInput;
        layout.AddChild(title);

        _selector = new OptionButton
        {
            CustomMinimumSize = new Vector2(500, 34)
        };
        _selector.ItemSelected += SelectValue;
        layout.AddChild(_selector);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(500, 235),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        layout.AddChild(scroll);

        _content = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(485, 225),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _content.AddThemeFontSizeOverride("normal_font_size", 16);
        scroll.AddChild(_content);

        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        layout.AddChild(actions);

        _detailsButton = ActionButton("开发者详情 Details", ToggleDetails);
        actions.AddChild(_detailsButton);
        actions.AddChild(ActionButton("复制完整诊断 Copy", CopyCurrent));
        actions.AddChild(ActionButton("关闭 Close", ClosePanel));
    }

    internal void Bind(ForecastHudSnapshot snapshot, DebugTraceDisplayBinding binding)
    {
        _snapshot = snapshot;
        _binding = binding;
        PositionControls();

        _capture = binding.Reason == DebugTraceReason.None
            && DebugTraceRuntime.TryGetCapture(binding.CaptureId, out var capture)
                ? capture
                : null;
        RebuildSelector();
        if (_panel?.Visible == true)
        {
            Render();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_panel?.Visible == true
            && @event is InputEventKey { Pressed: true, Echo: false } keyEvent
            && (keyEvent.Keycode == Key.Q || keyEvent.PhysicalKeycode == Key.Q))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePanel()
    {
        EnsurePanel();
        if (_panel is null)
        {
            return;
        }

        if (_panel.Visible)
        {
            ClosePanel();
            return;
        }

        _showDetails = false;
        DebugTraceRuntime.SetEnabled(true);
        _panel.Show();
        PositionControls();
        RebuildSelector();
        Render();

        if (_binding.UsedCommittedSnapshot
            && _binding.CaptureId <= 0)
        {
            return;
        }

        ForecastRefreshPatch.RefreshRegisteredBars();
    }

    private void ClosePanel()
    {
        _panel?.Hide();
        _showDetails = false;
        _dragging = false;
        DebugTraceRuntime.SetEnabled(false);
    }

    private void HandleTitleGuiInput(InputEvent @event)
    {
        if (_panel is null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: true
            }:
                _dragging = true;
                _dragPointerOffset = GetViewport().GetMousePosition()
                    - GetGlobalTransformWithCanvas() * _panel.Position;
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: false
            }:
                _dragging = false;
                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseMotion when _dragging:
                var desiredViewportPosition = GetViewport().GetMousePosition() - _dragPointerOffset;
                var desiredLocalPosition = GetGlobalTransformWithCanvas().AffineInverse()
                    * desiredViewportPosition;
                _panel.Position = ClampPanelPosition(desiredLocalPosition);
                _draggedViewportPosition = GetGlobalTransformWithCanvas() * _panel.Position;
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void ToggleDetails()
    {
        _showDetails = !_showDetails;
        Render();
    }

    private void CopyCurrent()
    {
        var text = _capture is null
            ? DebugTraceFormatter.TraceUnavailable(
                _binding.Reason == DebugTraceReason.None
                    ? DebugTraceReason.TraceNotCapturedForSnapshot
                    : _binding.Reason)
            : DebugTraceFormatter.BuildCopyText(_capture, _selectedKind, _binding);
        DisplayServer.ClipboardSet(text);
    }

    private void SelectValue(long index)
    {
        if (index >= 0 && index < _availableKinds.Count)
        {
            _selectedKind = _availableKinds[(int)index];
            _showDetails = false;
            Render();
        }
    }

    private void RebuildSelector()
    {
        if (_selector is null)
        {
            return;
        }

        var projection = ForecastHudProjectionPolicy.Project(
            _snapshot,
            DamageForecastUiSettings.DamageDisplayMode,
            DamageForecastUiSettings.IncomingDamagePlacement);
        _availableKinds.Clear();
        if (projection.ShowExpectedHpLoss)
        {
            _availableKinds.Add(DebugTraceValueKind.ExpectedTotalHpLoss);
            if (DamageForecastUiSettings.ShowBreakdownDetails
                && projection.ExpectedBlockableHpLoss > 0)
            {
                _availableKinds.Add(DebugTraceValueKind.BlockableHpLoss);
            }

            if (DamageForecastUiSettings.ShowBreakdownDetails
                && projection.ExpectedDirectHpLoss > 0)
            {
                _availableKinds.Add(DebugTraceValueKind.DirectHpLoss);
            }
        }

        if (projection.ShowIncomingDamage)
        {
            _availableKinds.Add(DebugTraceValueKind.IncomingDamage);
        }

        if (_availableKinds.Count == 0)
        {
            _availableKinds.Add(DebugTraceValueKind.ExpectedTotalHpLoss);
        }

        if (!_availableKinds.Contains(_selectedKind))
        {
            _selectedKind = _availableKinds[0];
        }

        _selector.Clear();
        for (var i = 0; i < _availableKinds.Count; i++)
        {
            var kind = _availableKinds[i];
            _selector.AddItem(DebugTraceFormatter.ValueLabel(kind), (int)kind);
            if (kind == _selectedKind)
            {
                _selector.Select(i);
            }
        }
    }

    private void Render()
    {
        if (_content is null || _detailsButton is null)
        {
            return;
        }

        if (_capture is null)
        {
            _content.Text = DebugTraceFormatter.TraceUnavailable(
                _binding.Reason == DebugTraceReason.None
                    ? DebugTraceReason.TraceNotCapturedForSnapshot
                    : _binding.Reason);
        }
        else
        {
            _content.Text = _showDetails
                ? DebugTraceFormatter.BuildDetails(_capture, _selectedKind, _binding)
                : DebugTraceFormatter.BuildCalculation(_capture, _selectedKind);
        }

        _detailsButton.Text = _showDetails
            ? "返回简易说明 Back"
            : "开发者详情 Details";
    }

    private void PositionControls()
    {
        if (_toggleButton is null)
        {
            return;
        }

        var viewportBounds = HudAnchorResolver.ResolveAvailableBounds(this);
        var buttonX = MathF.Max(
            viewportBounds.Left + ViewportEdgeInset,
            viewportBounds.Right - _toggleButton.CustomMinimumSize.X - ButtonRightInset);
        var buttonY = MathF.Min(
            viewportBounds.Bottom - _toggleButton.CustomMinimumSize.Y - ViewportEdgeInset,
            viewportBounds.Top + ButtonTopInset);
        _toggleButton.Position = new Vector2(buttonX, buttonY);
        if (_panel is null)
        {
            return;
        }

        var desiredPosition = _draggedViewportPosition is { } draggedViewportPosition
            ? GetGlobalTransformWithCanvas().AffineInverse() * draggedViewportPosition
            : new Vector2(
                buttonX - _panel.CustomMinimumSize.X + _toggleButton.CustomMinimumSize.X,
                buttonY + _toggleButton.CustomMinimumSize.Y + PanelGap);
        _panel.Position = ClampPanelPosition(desiredPosition);
        if (_draggedViewportPosition is not null)
        {
            _draggedViewportPosition = GetGlobalTransformWithCanvas() * _panel.Position;
        }
    }

    private Vector2 ClampPanelPosition(Vector2 desiredPosition)
    {
        if (_panel is null)
        {
            return desiredPosition;
        }

        var viewportBounds = HudAnchorResolver.ResolveAvailableBounds(this);
        var panelWidth = MathF.Max(_panel.Size.X, _panel.CustomMinimumSize.X);
        var panelHeight = MathF.Max(_panel.Size.Y, _panel.CustomMinimumSize.Y);
        var minX = viewportBounds.Left + ViewportEdgeInset;
        var minY = viewportBounds.Top + ViewportEdgeInset;
        var maxX = MathF.Max(minX, viewportBounds.Right - panelWidth - ViewportEdgeInset);
        var maxY = MathF.Max(minY, viewportBounds.Bottom - panelHeight - ViewportEdgeInset);
        return new Vector2(
            Math.Clamp(desiredPosition.X, minX, maxX),
            Math.Clamp(desiredPosition.Y, minY, maxY));
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(155, 32)
        };
        button.Pressed += action;
        return button;
    }
}
