using DamageForecast.Combat;
using DamageForecast.Diagnostics.DebugTrace;
using DamageForecast.Forecast;
using DamageForecast.UI;

internal static class DebugTraceContractCases
{
    public static IReadOnlyList<ContractCase> Create() =>
    [
        new(
            "DT-001",
            "DebugTrace",
            "DebugTrace.SimpleExpectedFormula_UsesConsumedBlockAndMatchesDisplayedValue",
            assert =>
            {
                var builder = Builder();
                builder.SetExpectedSimpleFormula(blockableInput: 0, selectedBlock: 5, directHpLoss: 3);
                var capture = builder.Seal(
                    1,
                    ForecastResult.KnownDamage(0, 3),
                    IncomingDamageDisplayRead.Hidden);
                var value = capture.Values[DebugTraceValueKind.ExpectedTotalHpLoss];
                var arithmetic = Evaluate(value.FormulaTerms);
                var text = DebugTraceFormatter.BuildCalculation(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                assert.Equal(3, arithmetic, "formula arithmetic");
                assert.Equal(3, value.Amount, "displayed amount");
                assert.True(
                    text.Contains("0 - 0 + 3 = 3", StringComparison.Ordinal),
                    "only consumed Block is subtracted after max(0, ...) semantics",
                    text);
            }),
        new(
            "DT-002",
            "DebugTrace",
            "DebugTrace.UnknownNativeModifier_AppearsOnlyInDetails",
            assert =>
            {
                var builder = Builder();
                builder.AddStep(new DebugTraceStep(
                    "EnemyAttackIntent[1].NativeModifiers",
                    DebugTraceSourceLevel.Unknown,
                    DebugTraceStepStatus.Unknown,
                    DebugTraceReason.AlreadyIncluded,
                    10,
                    DebugTraceLane.Modifier,
                    DebugTraceGranularity.Unknown,
                    null,
                    null));
                builder.SetExpectedFinalFormula(12, 0);
                var capture = builder.Seal(
                    2,
                    ForecastResult.KnownDamage(12, 0),
                    IncomingDamageDisplayRead.Hidden);
                var calculation = DebugTraceFormatter.BuildCalculation(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                var details = DebugTraceFormatter.BuildDetails(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                assert.True(
                    !calculation.Contains("NativeModifiers", StringComparison.Ordinal),
                    "unknown modifier excluded from applied formula",
                    calculation);
                assert.True(
                    details.Contains("Already included", StringComparison.Ordinal)
                    && details.Contains("未知 Unknown", StringComparison.Ordinal),
                    "unknown native decomposition retained in details",
                    details);
            }),
        new(
            "DT-003",
            "DebugTrace",
            "DebugTrace.StepBudget_TruncatesExplicitlyAt256",
            assert =>
            {
                var builder = Builder();
                for (var i = 0; i < 300; i++)
                {
                    builder.AddStep(Step($"source-{i}"));
                }

                var capture = builder.Seal(
                    3,
                    ForecastResult.KnownDamage(1, 0),
                    IncomingDamageDisplayRead.Hidden);
                var value = capture.Values[DebugTraceValueKind.ExpectedTotalHpLoss];
                assert.Equal(DebugTraceCaptureBuilder.MaxSteps, value.Steps.Count, "step cap");
                assert.Equal(true, capture.Truncated, "truncation must be explicit");
            }),
        new(
            "DT-004",
            "DebugTrace",
            "DebugTrace.Formatter_IsBilingualAndCopyIsBounded",
            assert =>
            {
                var builder = Builder();
                for (var i = 0; i < DebugTraceCaptureBuilder.MaxSteps; i++)
                {
                    builder.AddStep(Step(new string('x', 400) + i));
                }

                builder.SetExpectedFinalFormula(7, 2);
                var capture = builder.Seal(
                    4,
                    ForecastResult.KnownDamage(7, 2),
                    IncomingDamageDisplayRead.Hidden);
                var copy = DebugTraceFormatter.BuildCopyText(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                assert.True(
                    copy.Contains("预计掉血", StringComparison.Ordinal)
                    && copy.Contains("Expected HP Loss", StringComparison.Ordinal),
                    "Chinese primary and English helper labels",
                    copy[..Math.Min(copy.Length, 200)]);
                assert.True(
                    copy.Length <= DebugTraceFormatter.MaxCopyCharacters,
                    $"copy <= {DebugTraceFormatter.MaxCopyCharacters}",
                    $"length={copy.Length}");
            }),
        new(
            "DT-005",
            "DebugTrace",
            "DebugTrace.UncapturedFrozenSnapshot_RefusesRecalculationClaim",
            assert =>
            {
                var text = DebugTraceFormatter.TraceUnavailable(
                    DebugTraceReason.TraceNotCapturedForSnapshot);
                assert.True(
                    text.Contains("未记录", StringComparison.Ordinal)
                    && text.Contains("不能重新计算", StringComparison.Ordinal),
                    "uncaptured frozen snapshot is explicit and fail-closed",
                    text);
            }),
        new(
            "DT-006",
            "DebugTrace",
            "DebugTrace.CompileIsolation_DefaultRemovesWholeDirectory",
            assert =>
            {
                var project = IdentityContractFixture.Read(
                    "src/DamageForecast/DamageForecast.csproj");
                var snapshot = IdentityContractFixture.Read(
                    "src/DamageForecast/Forecast/ForecastHudSnapshot.cs");
                var actual = project.Contains(
                        "<DamageForecastDebugTrace Condition=\"'$(DamageForecastDebugTrace)' == ''\">false</DamageForecastDebugTrace>",
                        StringComparison.Ordinal)
                    && project.Contains(
                        "<Compile Remove=\"Diagnostics\\DebugTrace\\**\\*.cs\" />",
                        StringComparison.Ordinal)
                    && snapshot.Contains("#if DAMAGE_FORECAST_DEBUG_TRACE", StringComparison.Ordinal);
                assert.True(
                    actual,
                    "default false plus whole-directory compile removal and token guard",
                    "one or more compile-isolation markers missing");
            }),
        new(
            "DT-007",
            "DebugTrace",
            "DebugTrace.IncomingFormula_UsesFinalBlockableAndDirectLanes",
            assert =>
            {
                var builder = Builder();
                builder.SetIncomingFinalFormula(5, 4);
                var capture = builder.Seal(
                    7,
                    ForecastResult.Hidden,
                    IncomingDamageDisplayRead.Known(9));
                var value = capture.Values[DebugTraceValueKind.IncomingDamage];
                assert.Equal(9, Evaluate(value.FormulaTerms), "incoming formula arithmetic");
                assert.Equal(9, value.Amount, "incoming displayed amount");
            }),
        new(
            "DT-008",
            "DebugTrace",
            "DebugTrace.CaptureToken_FollowsCommittedFrozenSnapshot",
            assert =>
            {
                var owner = new HudSnapshotOwnerIdentity(7, "CombatId:trace-owner");
                var snapshot = new ForecastHudSnapshot(
                    ForecastResult.KnownDamage(8, 1),
                    IncomingDamageDisplayRead.Known(12),
                    DebugTraceCaptureId: 42);
                var live = HudSnapshotLifecyclePolicy.ResolveDisplay(
                    HudSnapshotLifecyclePolicy.StartPlayerTurn(owner),
                    owner,
                    snapshot,
                    isDisplayable: true,
                    freezeEnabled: true).State;
                var waiting = HudSnapshotLifecyclePolicy.PrepareEndTurn(live, owner, 9);
                var frozen = HudSnapshotLifecyclePolicy.CommitLatest(waiting, owner);
                var found = HudSnapshotLifecyclePolicy.TryGetCommitted(
                    frozen,
                    owner,
                    freezeEnabled: true,
                    out var committed);
                assert.True(found, "committed snapshot exists", "missing committed snapshot");
                assert.Equal(42L, committed.DebugTraceCaptureId, "capture token preserved");
            }),
        new(
            "DT-009",
            "DebugTrace",
            "DebugTrace.OffPath_ReturnsBeforeTimelineAllocationAndPanelIsLazy",
            assert =>
            {
                var data = IdentityContractFixture.Read(
                    "src/DamageForecast/Diagnostics/DebugTrace/DebugTraceData.cs");
                var panel = IdentityContractFixture.Read(
                    "src/DamageForecast/Diagnostics/DebugTrace/DebugTracePanel.cs");
                var guard = data.IndexOf("if (_current is null)", StringComparison.Ordinal);
                var timelineAllocation = data.IndexOf("var inputs = sourceEvents", StringComparison.Ordinal);
                var initialize = panel.IndexOf("internal void Initialize()", StringComparison.Ordinal);
                var ensure = panel.IndexOf("private void EnsurePanel()", StringComparison.Ordinal);
                var panelCreation = panel.IndexOf("_panel = new PanelContainer", StringComparison.Ordinal);
                assert.True(
                    guard >= 0 && timelineAllocation > guard,
                    "off-path guard precedes timeline allocation",
                    $"guard={guard}; allocation={timelineAllocation}");
                assert.True(
                    initialize >= 0 && ensure > initialize && panelCreation > ensure,
                    "Initialize creates button; panel subtree is inside lazy EnsurePanel",
                    $"initialize={initialize}; ensure={ensure}; panel={panelCreation}");
            }),
        new(
            "DT-010",
            "DebugTrace",
            "DebugTrace.OwnerSwitch_ClearsPreviousOwnerCapture",
            assert =>
            {
                DebugTraceRuntime.Clear();
                var first = Builder().Seal(
                    101,
                    ForecastResult.KnownDamage(1, 0),
                    IncomingDamageDisplayRead.Hidden);
                var second = new DebugTraceCaptureBuilder(
                    2,
                    "CombatId:other-owner",
                    "contract").Seal(
                        102,
                        ForecastResult.KnownDamage(2, 0),
                        IncomingDamageDisplayRead.Hidden);
                DebugTraceRuntime.CacheCapture(first);
                DebugTraceRuntime.CacheCapture(second);
                assert.Equal(false, DebugTraceRuntime.TryGetCapture(101, out _), "old owner evicted");
                assert.Equal(true, DebugTraceRuntime.TryGetCapture(102, out _), "new owner retained");
                DebugTraceRuntime.Clear();
            }),
        new(
            "DT-011",
            "DebugTrace",
            "DebugTrace.Binding_FailsClosedForMissingOrWrongOwnerCapture",
            assert =>
            {
                var store = IdentityContractFixture.Read(
                    "src/DamageForecast/UI/DamageForecastHudSnapshotStore.cs");
                assert.True(
                    store.Contains("? DebugTraceReason.StaleGeneration", StringComparison.Ordinal)
                    && store.Contains("? DebugTraceReason.OwnerMismatch", StringComparison.Ordinal)
                    && store.Contains("capture.PlayerNetId != owner.PlayerNetId", StringComparison.Ordinal),
                    "binding distinguishes stale generation and owner mismatch",
                    "one or more fail-closed binding checks missing");
            }),
        new(
            "DT-012",
            "DebugTrace",
            "DebugTrace.EntryUsesViewportTopRightAndPanelTitleIsDraggable",
            assert =>
            {
                var panel = IdentityContractFixture.Read(
                    "src/DamageForecast/Diagnostics/DebugTrace/DebugTracePanel.cs");
                assert.True(
                    panel.Contains("ButtonRightInset", StringComparison.Ordinal)
                    && panel.Contains(
                        "private const float ButtonTopInset = 220f;",
                        StringComparison.Ordinal)
                    && panel.Contains("HudAnchorResolver.ResolveAvailableBounds(this)", StringComparison.Ordinal)
                    && panel.Contains("title.GuiInput += HandleTitleGuiInput", StringComparison.Ordinal)
                    && panel.Contains("ClampPanelPosition(desiredLocalPosition)", StringComparison.Ordinal)
                    && panel.Contains("_draggedViewportPosition", StringComparison.Ordinal),
                    "entry uses viewport bounds and title drag is clamped and retained",
                    "one or more viewport or drag markers missing");
            }),
        new(
            "DT-013",
            "DebugTrace",
            "DebugTrace.QClosesOpenPanelAndEscapeIsNotCaptured",
            assert =>
            {
                var panel = IdentityContractFixture.Read(
                    "src/DamageForecast/Diagnostics/DebugTrace/DebugTracePanel.cs");
                assert.True(
                    panel.Contains("keyEvent.Keycode == Key.Q", StringComparison.Ordinal)
                    && panel.Contains("keyEvent.PhysicalKeycode == Key.Q", StringComparison.Ordinal)
                    && !panel.Contains("Key.Escape", StringComparison.Ordinal),
                    "Q closes only while the panel is open and Escape remains untouched",
                    "Q binding missing or Escape is still captured");
            }),
        new(
            "DT-014",
            "DebugTrace",
            "DebugTrace.DefaultViewIsPlayerReadableChineseAndTechnicalFieldsStayInDetails",
            assert =>
            {
                var builder = Builder();
                builder.SetExpectedSimpleFormula(blockableInput: 26, selectedBlock: 20, directHpLoss: 0);
                var capture = builder.Seal(
                    14,
                    ForecastResult.KnownDamage(6, 0),
                    IncomingDamageDisplayRead.Hidden);
                var calculation = DebugTraceFormatter.BuildCalculation(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                var details = DebugTraceFormatter.BuildDetails(
                    capture,
                    DebugTraceValueKind.ExpectedTotalHpLoss);
                assert.True(
                    calculation.Contains("计算：26 - 20 + 0 = 6", StringComparison.Ordinal)
                    && calculation.Contains("结论：本回合预计失去 6 点生命。", StringComparison.Ordinal)
                    && calculation.Contains("复制完整诊断", StringComparison.Ordinal)
                    && !calculation.Contains("Capture:", StringComparison.Ordinal)
                    && !calculation.Contains("generation=", StringComparison.Ordinal),
                    "default view explains the result in Chinese without developer lifecycle fields",
                    calculation);
                assert.True(
                    details.Contains("Capture: 14", StringComparison.Ordinal)
                    && details.Contains("Owner:", StringComparison.Ordinal),
                    "developer details retain technical evidence",
                    details);
            }),
        new(
            "DT-015",
            "DebugTrace",
            "DebugTrace.PanelLabelsExposeDeveloperDetailsAndFullDiagnosticCopy",
            assert =>
            {
                var panel = IdentityContractFixture.Read(
                    "src/DamageForecast/Diagnostics/DebugTrace/DebugTracePanel.cs");
                assert.True(
                    panel.Contains("开发者详情 Details", StringComparison.Ordinal)
                    && panel.Contains("复制完整诊断 Copy", StringComparison.Ordinal)
                    && panel.Contains("BuildCopyText(_capture, _selectedKind, _binding)", StringComparison.Ordinal),
                    "technical content is explicitly separated and copied with snapshot binding",
                    "one or more DT-2C player-facing labels or full-copy markers missing");
            })
    ];

    private static DebugTraceCaptureBuilder Builder() =>
        new(1, "CombatId:debug-contract", "contract");

    private static DebugTraceStep Step(string source) =>
        new(
            source,
            DebugTraceSourceLevel.Forecast,
            DebugTraceStepStatus.Applied,
            DebugTraceReason.None,
            null,
            DebugTraceLane.BlockableDamage,
            DebugTraceGranularity.SingleEvent,
            1,
            1);

    private static int Evaluate(IReadOnlyList<DebugTraceFormulaTerm> terms) =>
        terms.Sum(term => term.Operator == DebugTraceFormulaOperator.Subtract
            ? -term.Amount
            : term.Amount);
}
