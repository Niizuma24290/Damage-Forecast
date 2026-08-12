using System.Text;

namespace DamageForecast.Diagnostics.DebugTrace;

internal static class DebugTraceFormatter
{
    internal const int MaxCopyCharacters = 64 * 1024;

    internal static string ValueLabel(DebugTraceValueKind kind) => kind switch
    {
        DebugTraceValueKind.ExpectedTotalHpLoss => "预计掉血 -N / Expected HP Loss",
        DebugTraceValueKind.IncomingDamage => "来袭伤害 N / Incoming Damage",
        DebugTraceValueKind.BlockableHpLoss => "可格挡掉血 🛡 / Blockable HP Loss",
        DebugTraceValueKind.DirectHpLoss => "直接掉血 ♥ / Direct HP Loss",
        _ => "未知数值 / Unknown Value"
    };

    internal static string BuildCalculation(DebugTraceCapture capture, DebugTraceValueKind kind)
    {
        if (!capture.Values.TryGetValue(kind, out var value))
        {
            return TraceUnavailable(DebugTraceReason.TraceNotCapturedForSnapshot);
        }

        if (value.State == DebugTraceValueState.Unknown)
        {
            return $"{PlayerValueLabel(kind)}：暂时无法可靠计算\n"
                + "结论：当前信息不足，为避免误导，不显示猜测数值。\n\n"
                + CopyHelpText;
        }

        if (value.State == DebugTraceValueState.Hidden)
        {
            return $"{PlayerValueLabel(kind)}：本次没有需要显示的数值。\n"
                + "结论：当前快照未显示这一项。";
        }

        var builder = new StringBuilder();
        builder.Append(PlayerValueLabel(kind)).Append('：').Append(value.Amount)
            .Append("\n计算：").Append(FormatFormula(value.FormulaTerms))
            .Append(" = ").Append(value.Amount)
            .Append("\n说明：");

        if (value.FormulaTerms.Count == 0)
        {
            builder.Append("\n没有可拆分的计算项");
        }
        else
        {
            foreach (var term in value.FormulaTerms)
            {
                builder.Append("\n• ").Append(PlayerTermLabel(term.LabelKey))
                    .Append("：").Append(term.Amount);
            }
        }

        builder.Append("\n结论：").Append(PlayerConclusion(kind, value.Amount));
        if (capture.Truncated)
        {
            builder.Append("\n注意：详细记录达到上限，但上面的最终数值仍来自本次正式预测。");
        }

        builder.Append("\n\n").Append(CopyHelpText);
        return builder.ToString();
    }

    internal static string BuildDetails(
        DebugTraceCapture capture,
        DebugTraceValueKind kind,
        DebugTraceDisplayBinding? binding = null)
    {
        if (!capture.Values.TryGetValue(kind, out var value))
        {
            return TraceUnavailable(DebugTraceReason.TraceNotCapturedForSnapshot);
        }

        var builder = new StringBuilder();
        builder.Append("Capture: ").Append(capture.CaptureId)
            .Append("\nOwner: ").Append(capture.PlayerNetId).Append(':').Append(capture.CreatureStableIdentity)
            .Append("\nRefresh: ").Append(capture.RefreshReason)
            .Append("\nValue: ").Append(ValueLabel(kind)).Append(" = ").Append(value.Amount)
            .Append("\nState: ").Append(value.State);

        if (binding is { } displayBinding)
        {
            builder.Append("\nDisplay: ")
                .Append(displayBinding.UsedCommittedSnapshot ? "Committed" : "Live")
                .Append(" | phase=").Append(displayBinding.LifecyclePhase)
                .Append(" | generation=")
                .Append(displayBinding.PendingGeneration?.ToString() ?? "none");
        }

        builder.Append("\n\n采用/跳过/未知 / Applied, skipped, unknown:\n");
        if (value.Steps.Count == 0)
        {
            builder.Append("（没有结构化步骤 / No structured steps recorded）");
        }
        else
        {
            foreach (var step in value.Steps)
            {
                builder.Append(StatusMarker(step.Status)).Append(' ')
                    .Append(step.SourceId)
                    .Append(" | ").Append(step.SourceLevel)
                    .Append(" | ").Append(step.Lane)
                    .Append(" | ").Append(step.Granularity);
                if (step.NativeExecutionOrder is { } order)
                {
                    builder.Append(" | order=").Append(order);
                }

                if (step.InputAmount is { } input)
                {
                    builder.Append(" | in=").Append(input);
                }

                if (step.OutputAmount is { } output)
                {
                    builder.Append(" | out=").Append(output);
                }

                if (step.Reason != DebugTraceReason.None)
                {
                    builder.Append(" | ").Append(ReasonLabel(step.Reason));
                }

                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    internal static string BuildCopyText(
        DebugTraceCapture capture,
        DebugTraceValueKind kind,
        DebugTraceDisplayBinding? binding = null)
    {
        var text = BuildCalculation(capture, kind)
            + "\n\n===== 开发者详情 / Developer Details =====\n"
            + BuildDetails(capture, kind, binding);
        if (text.Length <= MaxCopyCharacters)
        {
            return text;
        }

        const string suffix = "\n… 已截断 / Truncated";
        return text[..(MaxCopyCharacters - suffix.Length)] + suffix;
    }

    internal static string TraceUnavailable(DebugTraceReason reason) =>
        reason switch
        {
            DebugTraceReason.OwnerMismatch => "这份记录不属于当前玩家，已停止显示，避免混用。\nOwner mismatch",
            DebugTraceReason.StaleGeneration => "这份记录已经过期，已停止显示。\nStale generation",
            _ => "这份画面快照未记录对应的计算过程，不能重新计算后冒充旧过程。\n"
                + "请在当前画面重新打开调试面板。\nTrace was not captured for this snapshot"
        };

    internal static string ReasonLabel(DebugTraceReason reason) => reason switch
    {
        DebugTraceReason.NotLiveCombat => "非进行中战斗 / Not live combat",
        DebugTraceReason.Dead => "已死亡 / Dead",
        DebugTraceReason.PredictedDeadBeforeAction => "行动前预计死亡 / Predicted dead before action",
        DebugTraceReason.NoAttackIntent => "没有攻击意图 / No attack intent",
        DebugTraceReason.DisabledBySetting => "设置已关闭 / Disabled by setting",
        DebugTraceReason.AlreadyIncluded => "已包含在原生最终值 / Already included",
        DebugTraceReason.UnsupportedShape => "不支持的形状 / Unsupported shape",
        DebugTraceReason.UnsupportedOrder => "顺序未知 / Unsupported order",
        DebugTraceReason.UnsupportedGranularity => "粒度未知 / Unsupported granularity",
        DebugTraceReason.UnsupportedModifier => "修正未知 / Unsupported modifier",
        DebugTraceReason.OwnerMismatch => "玩家身份不匹配 / Owner mismatch",
        DebugTraceReason.ReadFailure => "读取失败 / Read failure",
        DebugTraceReason.StaleGeneration => "快照代次已过期 / Stale generation",
        DebugTraceReason.TraceNotCapturedForSnapshot => "此快照未记录 / Trace not captured",
        _ => reason.ToString()
    };

    private const string CopyHelpText =
        "如果显示与游戏结果不一致，请点击“复制完整诊断”，然后把内容发给 Codex。";

    private static string PlayerValueLabel(DebugTraceValueKind kind) => kind switch
    {
        DebugTraceValueKind.ExpectedTotalHpLoss => "预计掉血",
        DebugTraceValueKind.IncomingDamage => "来袭伤害",
        DebugTraceValueKind.BlockableHpLoss => "可格挡掉血",
        DebugTraceValueKind.DirectHpLoss => "直接掉血",
        _ => "未知数值"
    };

    private static string PlayerConclusion(DebugTraceValueKind kind, int amount) => kind switch
    {
        DebugTraceValueKind.ExpectedTotalHpLoss => $"本回合预计失去 {amount} 点生命。",
        DebugTraceValueKind.IncomingDamage => $"预计承受 {amount} 点来袭伤害。",
        DebugTraceValueKind.BlockableHpLoss => $"其中 {amount} 点为格挡结算后的掉血。",
        DebugTraceValueKind.DirectHpLoss => $"其中 {amount} 点会直接损失生命。",
        _ => $"最终结果为 {amount}。"
    };

    private static string FormatFormula(IReadOnlyList<DebugTraceFormulaTerm> terms)
    {
        if (terms.Count == 0)
        {
            return "0";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < terms.Count; i++)
        {
            var term = terms[i];
            if (i > 0)
            {
                builder.Append(term.Operator == DebugTraceFormulaOperator.Subtract ? " - " : " + ");
            }
            else if (term.Operator == DebugTraceFormulaOperator.Subtract)
            {
                builder.Append('-');
            }

            builder.Append(term.Amount);
        }

        return builder.ToString();
    }

    private static string PlayerTermLabel(string key) => key switch
    {
        "BlockableInput" => "可格挡伤害合计",
        "ConsumedBlock" => "实际使用格挡",
        "FinalBlockableHpLoss" => "格挡结算后的伤害",
        "DirectHpLoss" => "直接掉血",
        "IncomingDamage" => "来袭伤害",
        _ => key
    };

    private static string StatusMarker(DebugTraceStepStatus status) => status switch
    {
        DebugTraceStepStatus.Applied => "[采用 Applied]",
        DebugTraceStepStatus.Skipped => "[跳过 Skipped]",
        _ => "[未知 Unknown]"
    };
}
