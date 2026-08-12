using System.Runtime.CompilerServices;
using DamageForecast.Combat;
using DamageForecast.Forecast;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace DamageForecast.Diagnostics.DebugTrace;

internal enum DebugTraceEvaluationKind
{
    ExpectedHpLoss,
    IncomingDamage
}

internal enum DebugTraceValueKind
{
    ExpectedTotalHpLoss,
    IncomingDamage,
    BlockableHpLoss,
    DirectHpLoss
}

internal enum DebugTraceValueState
{
    Hidden,
    Known,
    Unknown
}

internal enum DebugTraceSourceLevel
{
    Native,
    Forecast,
    Recorded,
    Unknown
}

internal enum DebugTraceStepStatus
{
    Applied,
    Skipped,
    Unknown
}

internal enum DebugTraceLane
{
    BlockableDamage,
    DirectHpLoss,
    BlockGain,
    Modifier,
    Lifecycle
}

internal enum DebugTraceGranularity
{
    SingleEvent,
    PerHit,
    Aggregate,
    Unknown
}

internal enum DebugTraceReason
{
    None,
    NotLiveCombat,
    Dead,
    PredictedDeadBeforeAction,
    NoAttackIntent,
    DisabledBySetting,
    AlreadyIncluded,
    UnsupportedShape,
    UnsupportedOrder,
    UnsupportedGranularity,
    UnsupportedModifier,
    OwnerMismatch,
    ReadFailure,
    StaleGeneration,
    TraceNotCapturedForSnapshot
}

internal enum DebugTraceFormulaOperator
{
    Add,
    Subtract
}

internal readonly record struct DebugTraceFormulaTerm(
    string LabelKey,
    int Amount,
    DebugTraceFormulaOperator Operator);

internal readonly record struct DebugTraceStep(
    string SourceId,
    DebugTraceSourceLevel SourceLevel,
    DebugTraceStepStatus Status,
    DebugTraceReason Reason,
    int? NativeExecutionOrder,
    DebugTraceLane Lane,
    DebugTraceGranularity Granularity,
    int? InputAmount,
    int? OutputAmount);

internal sealed record DebugTraceValue(
    DebugTraceValueKind Kind,
    DebugTraceValueState State,
    int Amount,
    IReadOnlyList<DebugTraceFormulaTerm> FormulaTerms,
    IReadOnlyList<DebugTraceStep> Steps);

internal sealed record DebugTraceCapture(
    long CaptureId,
    ulong PlayerNetId,
    string CreatureStableIdentity,
    string RefreshReason,
    IReadOnlyDictionary<DebugTraceValueKind, DebugTraceValue> Values,
    bool Truncated);

internal readonly record struct DebugTraceDisplayBinding(
    long CaptureId,
    bool UsedCommittedSnapshot,
    string LifecyclePhase,
    long? PendingGeneration,
    DebugTraceReason Reason);

internal sealed class DebugTraceCaptureBuilder
{
    internal const int MaxSteps = 256;

    private readonly List<DebugTraceStep> _expectedSteps = [];
    private readonly List<DebugTraceStep> _incomingSteps = [];
    private IReadOnlyList<DebugTraceFormulaTerm>? _expectedFormula;
    private IReadOnlyList<DebugTraceFormulaTerm>? _incomingFormula;
    private int _stepCount;

    internal DebugTraceCaptureBuilder(
        ulong playerNetId,
        string creatureStableIdentity,
        string refreshReason)
    {
        PlayerNetId = playerNetId;
        CreatureStableIdentity = creatureStableIdentity;
        RefreshReason = string.IsNullOrWhiteSpace(refreshReason)
            ? "hud-refresh"
            : refreshReason;
    }

    internal ulong PlayerNetId { get; }

    internal string CreatureStableIdentity { get; }

    internal string RefreshReason { get; }

    internal DebugTraceEvaluationKind Evaluation { get; set; }

    internal bool Truncated { get; private set; }

    internal void AddStep(DebugTraceStep step)
    {
        if (_stepCount >= MaxSteps)
        {
            Truncated = true;
            return;
        }

        _stepCount++;
        StepsFor(Evaluation).Add(step);
    }

    internal void SetExpectedSimpleFormula(int blockableInput, int selectedBlock, int directHpLoss)
    {
        var normalizedInput = Math.Max(0, blockableInput);
        var consumedBlock = Math.Min(normalizedInput, Math.Max(0, selectedBlock));
        _expectedFormula =
        [
            new("BlockableInput", normalizedInput, DebugTraceFormulaOperator.Add),
            new("ConsumedBlock", consumedBlock, DebugTraceFormulaOperator.Subtract),
            new("DirectHpLoss", Math.Max(0, directHpLoss), DebugTraceFormulaOperator.Add)
        ];
    }

    internal void SetExpectedFinalFormula(int blockableHpLoss, int directHpLoss)
    {
        _expectedFormula =
        [
            new("FinalBlockableHpLoss", Math.Max(0, blockableHpLoss), DebugTraceFormulaOperator.Add),
            new("DirectHpLoss", Math.Max(0, directHpLoss), DebugTraceFormulaOperator.Add)
        ];
    }

    internal void SetIncomingFinalFormula(int blockableHpLoss, int directHpLoss)
    {
        _incomingFormula =
        [
            new("FinalBlockableHpLoss", Math.Max(0, blockableHpLoss), DebugTraceFormulaOperator.Add),
            new("DirectHpLoss", Math.Max(0, directHpLoss), DebugTraceFormulaOperator.Add)
        ];
    }

    internal DebugTraceCapture Seal(
        long captureId,
        ForecastResult expected,
        IncomingDamageDisplayRead incoming)
    {
        var expectedState = expected.State switch
        {
            ForecastResultState.KnownDamage => DebugTraceValueState.Known,
            ForecastResultState.Unknown => DebugTraceValueState.Unknown,
            _ => DebugTraceValueState.Hidden
        };
        var incomingState = incoming.State switch
        {
            IncomingDamageDisplayReadState.Known => DebugTraceValueState.Known,
            IncomingDamageDisplayReadState.Unknown => DebugTraceValueState.Unknown,
            _ => DebugTraceValueState.Hidden
        };

        var blockable = Math.Max(0, expected.OutDamage);
        var direct = Math.Max(0, expected.DirectHpLoss);
        var expectedTotal = SaturatingAdd(blockable, direct);
        var expectedFormula = _expectedFormula ??
        [
            new("FinalBlockableHpLoss", blockable, DebugTraceFormulaOperator.Add),
            new("DirectHpLoss", direct, DebugTraceFormulaOperator.Add)
        ];
        var incomingFormula = _incomingFormula ??
        [
            new("IncomingDamage", Math.Max(0, incoming.Damage), DebugTraceFormulaOperator.Add)
        ];

        var expectedSteps = _expectedSteps.ToArray();
        var incomingSteps = _incomingSteps.ToArray();
        var values = new Dictionary<DebugTraceValueKind, DebugTraceValue>
        {
            [DebugTraceValueKind.ExpectedTotalHpLoss] = new(
                DebugTraceValueKind.ExpectedTotalHpLoss,
                expectedState,
                expectedTotal,
                expectedFormula,
                expectedSteps),
            [DebugTraceValueKind.IncomingDamage] = new(
                DebugTraceValueKind.IncomingDamage,
                incomingState,
                Math.Max(0, incoming.Damage),
                incomingFormula,
                incomingSteps),
            [DebugTraceValueKind.BlockableHpLoss] = new(
                DebugTraceValueKind.BlockableHpLoss,
                expectedState,
                blockable,
                [new("FinalBlockableHpLoss", blockable, DebugTraceFormulaOperator.Add)],
                expectedSteps),
            [DebugTraceValueKind.DirectHpLoss] = new(
                DebugTraceValueKind.DirectHpLoss,
                expectedState,
                direct,
                [new("DirectHpLoss", direct, DebugTraceFormulaOperator.Add)],
                expectedSteps)
        };

        return new DebugTraceCapture(
            captureId,
            PlayerNetId,
            CreatureStableIdentity,
            RefreshReason,
            values,
            Truncated);
    }

    private List<DebugTraceStep> StepsFor(DebugTraceEvaluationKind evaluation) =>
        evaluation == DebugTraceEvaluationKind.ExpectedHpLoss
            ? _expectedSteps
            : _incomingSteps;

    private static int SaturatingAdd(int left, int right) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, left) + Math.Max(0, right));
}

internal static class DebugTraceRuntime
{
    [ThreadStatic]
    private static DebugTraceCaptureBuilder? _current;

    private static readonly object Sync = new();
    private static readonly Dictionary<long, DebugTraceCapture> Captures = [];
    private static long _nextCaptureId;
    private static long _latestCaptureId;
    private static long _pinnedCaptureId;
    private static bool _enabled;

    internal static bool IsEnabled => _enabled;

    internal static bool IsCapturing => _current is not null;

    internal static void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    internal static DebugTraceCaptureScope BeginCapture(Creature creature, string? refreshReason)
    {
        if (!_enabled)
        {
            return DebugTraceCaptureScope.Inactive;
        }

        var creatureIdentity = creature.CombatId.HasValue
            ? $"CombatId:{creature.CombatId.Value}"
            : $"ObjectRef:{RuntimeHelpers.GetHashCode(creature)}";
        var builder = new DebugTraceCaptureBuilder(
            creature.Player?.NetId ?? 0,
            creatureIdentity,
            refreshReason ?? "hud-refresh");
        var previous = _current;
        _current = builder;
        return new DebugTraceCaptureScope(builder, previous);
    }

    internal static DebugTraceEvaluationScope BeginEvaluation(DebugTraceEvaluationKind evaluation)
    {
        if (_current is null)
        {
            return DebugTraceEvaluationScope.Inactive;
        }

        var previous = _current.Evaluation;
        _current.Evaluation = evaluation;
        return new DebugTraceEvaluationScope(_current, previous);
    }

    internal static void AddStep(
        string sourceId,
        DebugTraceSourceLevel sourceLevel,
        DebugTraceStepStatus status,
        DebugTraceReason reason,
        int? nativeExecutionOrder,
        DebugTraceLane lane,
        DebugTraceGranularity granularity,
        int? inputAmount,
        int? outputAmount)
    {
        _current?.AddStep(new DebugTraceStep(
            sourceId,
            sourceLevel,
            status,
            reason,
            nativeExecutionOrder,
            lane,
            granularity,
            inputAmount,
            outputAmount));
    }

    internal static void SetExpectedSimpleFormula(int blockableInput, int selectedBlock, int directHpLoss) =>
        _current?.SetExpectedSimpleFormula(blockableInput, selectedBlock, directHpLoss);

    internal static void SetExpectedFinalFormula(int blockableHpLoss, int directHpLoss) =>
        _current?.SetExpectedFinalFormula(blockableHpLoss, directHpLoss);

    internal static void SetIncomingFinalFormula(int blockableHpLoss, int directHpLoss) =>
        _current?.SetIncomingFinalFormula(blockableHpLoss, directHpLoss);

    internal static void RecordTimeline(
        IReadOnlyList<UpcomingHpLossEvent> sourceEvents,
        AvailableBlockInput availableBlock,
        IncomingDamageDisplayOptions options,
        int selectedBlock,
        IReadOnlyList<UpcomingBlockEvent> futureBlockEvents,
        IReadOnlyList<UpcomingHpLossEvent> outputEvents)
    {
        if (_current is null)
        {
            return;
        }

        RecordBlock("CurrentBlock", availableBlock.CurrentBlock, options.IncludeCurrentBlock);
        RecordBlock("PowerBlock", availableBlock.PowerBlock, options.IncludePowerBlock);
        RecordBlock("RelicBlock", availableBlock.RelicBlock, options.IncludeRelicBlock);

        foreach (var blockEvent in futureBlockEvents)
        {
            AddStep(
                blockEvent.Source,
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                blockEvent.NativeExecutionOrder,
                DebugTraceLane.BlockGain,
                DebugTraceGranularity.SingleEvent,
                blockEvent.Amount,
                blockEvent.Amount);
        }

        var inputs = sourceEvents
            .GroupBy(EventKey)
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(group.Select(value => Math.Max(0, value.VerifiedHpLoss))));
        foreach (var output in outputEvents)
        {
            var key = EventKey(output);
            var input = inputs.TryGetValue(key, out var queue) && queue.Count > 0
                ? queue.Dequeue()
                : Math.Max(0, output.VerifiedHpLoss);
            var isEnemy = output.Source.StartsWith("EnemyAttackIntent[", StringComparison.Ordinal);
            AddStep(
                output.Source,
                isEnemy ? DebugTraceSourceLevel.Native : DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                output.NativeExecutionOrder,
                output.DisplayLane == HpLossDisplayLane.Blockable
                    ? DebugTraceLane.BlockableDamage
                    : DebugTraceLane.DirectHpLoss,
                output.IsSingleVerifiedEvent
                    ? (isEnemy ? DebugTraceGranularity.PerHit : DebugTraceGranularity.SingleEvent)
                    : DebugTraceGranularity.Aggregate,
                input,
                Math.Max(0, output.VerifiedHpLoss));
            if (isEnemy)
            {
                AddStep(
                    $"{output.Source}.NativeModifiers",
                    DebugTraceSourceLevel.Unknown,
                    DebugTraceStepStatus.Unknown,
                    DebugTraceReason.AlreadyIncluded,
                    output.NativeExecutionOrder,
                    DebugTraceLane.Modifier,
                    DebugTraceGranularity.Unknown,
                    null,
                    null);
            }
        }

        AddStep(
            "SelectedBlock",
            DebugTraceSourceLevel.Forecast,
            DebugTraceStepStatus.Applied,
            DebugTraceReason.None,
            null,
            DebugTraceLane.BlockGain,
            DebugTraceGranularity.Aggregate,
            selectedBlock,
            selectedBlock);
    }

    internal static void RecordModifier(
        string sourceId,
        int before,
        int after,
        bool supported)
    {
        if (_current is null)
        {
            return;
        }

        AddStep(
            sourceId,
            DebugTraceSourceLevel.Forecast,
            supported ? DebugTraceStepStatus.Applied : DebugTraceStepStatus.Unknown,
            supported ? DebugTraceReason.None : DebugTraceReason.UnsupportedModifier,
            null,
            DebugTraceLane.Modifier,
            DebugTraceGranularity.Aggregate,
            before,
            supported ? after : null);
    }

    internal static bool TryGetCapture(long captureId, out DebugTraceCapture capture)
    {
        lock (Sync)
        {
            if (captureId > 0 && Captures.TryGetValue(captureId, out capture!))
            {
                _pinnedCaptureId = captureId;
                TrimLocked();
                return true;
            }
        }

        capture = null!;
        return false;
    }

    internal static void Clear()
    {
        lock (Sync)
        {
            Captures.Clear();
            _latestCaptureId = 0;
            _pinnedCaptureId = 0;
        }
    }

    internal static void CacheCapture(DebugTraceCapture capture)
    {
        lock (Sync)
        {
            if (Captures.Values.Any(existing =>
                    existing.PlayerNetId != capture.PlayerNetId
                    || !string.Equals(
                        existing.CreatureStableIdentity,
                        capture.CreatureStableIdentity,
                        StringComparison.Ordinal)))
            {
                Captures.Clear();
                _latestCaptureId = 0;
                _pinnedCaptureId = 0;
            }

            Captures[capture.CaptureId] = capture;
            _latestCaptureId = capture.CaptureId;
            TrimLocked();
        }
    }

    private static void RecordBlock(string sourceId, int amount, bool enabled)
    {
        if (amount <= 0 && enabled)
        {
            return;
        }

        AddStep(
            sourceId,
            sourceId == "CurrentBlock"
                ? DebugTraceSourceLevel.Native
                : DebugTraceSourceLevel.Forecast,
            enabled ? DebugTraceStepStatus.Applied : DebugTraceStepStatus.Skipped,
            enabled ? DebugTraceReason.None : DebugTraceReason.DisabledBySetting,
            null,
            DebugTraceLane.BlockGain,
            DebugTraceGranularity.Aggregate,
            Math.Max(0, amount),
            enabled ? Math.Max(0, amount) : 0);
    }

    private static string EventKey(UpcomingHpLossEvent value) =>
        $"{value.Source}\u001f{value.NativeExecutionOrder}\u001f{value.DisplayLane}";

    private static long Store(DebugTraceCaptureBuilder builder, ForecastResult expected, IncomingDamageDisplayRead incoming)
    {
        var captureId = Interlocked.Increment(ref _nextCaptureId);
        var capture = builder.Seal(captureId, expected, incoming);
        CacheCapture(capture);

        return captureId;
    }

    private static void TrimLocked()
    {
        foreach (var key in Captures.Keys
                     .Where(key => key != _latestCaptureId && key != _pinnedCaptureId)
                     .ToArray())
        {
            Captures.Remove(key);
        }
    }

    internal sealed class DebugTraceCaptureScope : IDisposable
    {
        internal static readonly DebugTraceCaptureScope Inactive = new(null, null);
        private readonly DebugTraceCaptureBuilder? _builder;
        private readonly DebugTraceCaptureBuilder? _previous;
        private bool _disposed;

        internal DebugTraceCaptureScope(
            DebugTraceCaptureBuilder? builder,
            DebugTraceCaptureBuilder? previous)
        {
            _builder = builder;
            _previous = previous;
        }

        internal long Seal(ForecastResult expected, IncomingDamageDisplayRead incoming) =>
            _builder is null ? 0 : Store(_builder, expected, incoming);

        public void Dispose()
        {
            if (_builder is null || _disposed)
            {
                return;
            }

            _disposed = true;
            _current = _previous;
        }
    }

    internal sealed class DebugTraceEvaluationScope : IDisposable
    {
        internal static readonly DebugTraceEvaluationScope Inactive = new(null, default);
        private readonly DebugTraceCaptureBuilder? _builder;
        private readonly DebugTraceEvaluationKind _previous;
        private bool _disposed;

        internal DebugTraceEvaluationScope(
            DebugTraceCaptureBuilder? builder,
            DebugTraceEvaluationKind previous)
        {
            _builder = builder;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_builder is null || _disposed)
            {
                return;
            }

            _disposed = true;
            _builder.Evaluation = _previous;
        }
    }
}
