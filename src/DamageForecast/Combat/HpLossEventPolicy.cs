namespace DamageForecast.Combat;

internal readonly record struct AvailableBlockInput(
    int CurrentBlock,
    int PowerBlock,
    int RelicBlock);

internal static class HpLossEventPolicy
{
    public static int SelectBlock(
        AvailableBlockInput available,
        IncomingDamageDisplayOptions options)
    {
        long selected = 0;
        if (options.IncludeCurrentBlock)
        {
            selected += Math.Max(0, available.CurrentBlock);
        }

        if (options.IncludePowerBlock)
        {
            selected += Math.Max(0, available.PowerBlock);
        }

        if (options.IncludeRelicBlock)
        {
            selected += Math.Max(0, available.RelicBlock);
        }

        return (int)Math.Min(int.MaxValue, selected);
    }

    public static List<UpcomingHpLossEvent> ApplySelectedBlock(
        IReadOnlyList<UpcomingHpLossEvent> sourceEvents,
        int selectedBlock)
    {
        return ApplySelectedBlock(
            sourceEvents,
            selectedBlock,
            Array.Empty<UpcomingBlockEvent>());
    }

    public static List<UpcomingHpLossEvent> ApplySelectedBlock(
        IReadOnlyList<UpcomingHpLossEvent> sourceEvents,
        int selectedBlock,
        IReadOnlyList<UpcomingBlockEvent> futureBlockEvents)
    {
        var remainingBlock = Math.Max(0, selectedBlock);
        var hpLossEvents = new List<UpcomingHpLossEvent>(sourceEvents.Count);
        var timeline = new List<BlockTimelineEntry>(
            sourceEvents.Count + futureBlockEvents.Count);
        timeline.AddRange(sourceEvents.Select((hpLossEvent, index) =>
            BlockTimelineEntry.Damage(hpLossEvent, index)));
        timeline.AddRange(futureBlockEvents.Select((blockEvent, index) =>
            BlockTimelineEntry.Block(blockEvent, index)));

        foreach (var entry in timeline
                     .OrderBy(entry => entry.NativeExecutionOrder)
                     .ThenBy(entry => entry.Kind)
                     .ThenBy(entry => entry.InputIndex))
        {
            if (entry.Kind == BlockTimelineEntryKind.Block)
            {
                remainingBlock = SaturatingAdd(
                    remainingBlock,
                    entry.BlockEvent.Amount);
                continue;
            }

            var sourceEvent = entry.HpLossEvent;
            if (sourceEvent.DisplayLane == HpLossDisplayLane.Blockable)
            {
                var amount = Math.Max(0, sourceEvent.VerifiedHpLoss);
                var hpLoss = Math.Max(0, amount - remainingBlock);
                remainingBlock = Math.Max(0, remainingBlock - amount);
                hpLossEvents.Add(sourceEvent with { VerifiedHpLoss = hpLoss });
            }
            else
            {
                hpLossEvents.Add(sourceEvent);
            }
        }

        return hpLossEvents;
    }

    private static int SaturatingAdd(int left, int right)
    {
        return (int)Math.Min(
            int.MaxValue,
            (long)Math.Max(0, left) + Math.Max(0, right));
    }

    private readonly record struct BlockTimelineEntry(
        int NativeExecutionOrder,
        BlockTimelineEntryKind Kind,
        int InputIndex,
        UpcomingHpLossEvent HpLossEvent,
        UpcomingBlockEvent BlockEvent)
    {
        public static BlockTimelineEntry Damage(
            UpcomingHpLossEvent hpLossEvent,
            int inputIndex) =>
            new(
                hpLossEvent.NativeExecutionOrder,
                BlockTimelineEntryKind.Damage,
                inputIndex,
                hpLossEvent,
                default);

        public static BlockTimelineEntry Block(
            UpcomingBlockEvent blockEvent,
            int inputIndex) =>
            new(
                blockEvent.NativeExecutionOrder,
                BlockTimelineEntryKind.Block,
                inputIndex,
                default,
                blockEvent);
    }

    private enum BlockTimelineEntryKind
    {
        Damage,
        Block
    }
}
