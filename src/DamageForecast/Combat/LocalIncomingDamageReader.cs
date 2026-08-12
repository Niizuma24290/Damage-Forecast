using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using DamageForecast.Diagnostics;
#if DAMAGE_FORECAST_DEBUG_TRACE
using DamageForecast.Diagnostics.DebugTrace;
#endif

namespace DamageForecast.Combat;

public sealed class LocalIncomingDamageReader
{
    public IncomingDamageRead Read(ICombatState? combatState)
    {
        if (combatState is null || !combatState.IsLiveCombat())
        {
            return IncomingDamageRead.Hidden;
        }

        var localPlayer = LocalContext.GetMe(combatState);
        var localCreature = localPlayer?.Creature;
        if (localCreature is null || !localCreature.IsAlive)
        {
            return IncomingDamageRead.Hidden;
        }

        return ReadKnown(combatState, localCreature);
    }

    public IncomingDamageRead ReadForLocalCreature(Creature? localCreature)
    {
        var combatState = localCreature?.CombatState;
        if (combatState is null || !combatState.IsLiveCombat())
        {
            return IncomingDamageRead.Hidden;
        }

        if (localCreature is null || !localCreature.IsAlive)
        {
            return IncomingDamageRead.Hidden;
        }

        try
        {
            var localPlayer = LocalContext.GetMe(combatState);
            if (localPlayer is null || localCreature.Player?.NetId != localPlayer.NetId)
            {
                return IncomingDamageRead.Hidden;
            }
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce("incoming.local-context.expected", exception);
            return IncomingDamageRead.Unknown;
        }

        return ReadKnown(combatState, localCreature);
    }

    internal IncomingDamageDisplayRead ReadIncomingDamageForLocalCreature(
        Creature? localCreature,
        IncomingDamageDisplayOptions options)
    {
        var combatState = localCreature?.CombatState;
        if (combatState is null || !combatState.IsLiveCombat())
        {
            return IncomingDamageDisplayRead.Hidden;
        }

        if (localCreature is null || !localCreature.IsAlive)
        {
            return IncomingDamageDisplayRead.Hidden;
        }

        try
        {
            var localPlayer = LocalContext.GetMe(combatState);
            if (localPlayer is null || localCreature.Player?.NetId != localPlayer.NetId)
            {
                return IncomingDamageDisplayRead.Hidden;
            }
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce("incoming.local-context.display", exception);
            return IncomingDamageDisplayRead.Unknown;
        }

        return ReadIncomingDamageKnown(combatState, localCreature, options);
    }

    private static IncomingDamageRead ReadKnown(ICombatState combatState, Creature localCreature)
    {
        if (localCreature.Player is null)
        {
            return IncomingDamageRead.Unknown;
        }

        var raw = 0;
        var foundDamage = false;
        var enemyAttackEvents = new List<BlockableFutureDamageEvent>();
        var enemyAttackOrder = 0;
        var enemySnapshotIndex = 0;

        foreach (var enemy in combatState.Enemies)
        {
            var snapshotIndex = enemySnapshotIndex++;
            if (enemy is null || !enemy.IsAlive || enemy.Monster?.NextMove?.Intents is null)
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                RecordSkippedEnemy(
                    enemy,
                    enemy is null
                        ? DebugTraceReason.ReadFailure
                        : !enemy.IsAlive
                            ? DebugTraceReason.Dead
                            : DebugTraceReason.NoAttackIntent);
#endif
                continue;
            }

            var attackIntents = enemy.Monster.NextMove.Intents.OfType<AttackIntent>().ToArray();
            if (attackIntents.Length == 0)
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                RecordSkippedEnemy(enemy, DebugTraceReason.NoAttackIntent);
#endif
                continue;
            }

            var enemyIntentContribution = 0;
            var intentTotals = new int[attackIntents.Length];
            for (var i = 0; i < attackIntents.Length; i++)
            {
                intentTotals[i] = attackIntents[i].GetTotalDamage(new[] { localCreature }, enemy);
                enemyIntentContribution += intentTotals[i];
            }

            var survivalPreview = EnemyPreActionSurvivalPreview.Preview(enemy, snapshotIndex, enemyIntentContribution);
            if (!survivalPreview.WillExecuteIntentFor(survivalPreview.State.Identity.StableIdentity))
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                DebugTraceRuntime.AddStep(
                    survivalPreview.State.Identity.StableIdentity,
                    DebugTraceSourceLevel.Forecast,
                    DebugTraceStepStatus.Skipped,
                    DebugTraceReason.PredictedDeadBeforeAction,
                    null,
                    DebugTraceLane.BlockableDamage,
                    DebugTraceGranularity.SingleEvent,
                    enemyIntentContribution,
                    0);
#endif
                continue;
            }

            for (var i = 0; i < attackIntents.Length; i++)
            {
                foundDamage = true;
                var attackIntent = attackIntents[i];
                var totalDamage = intentTotals[i];
                var attackEvents = ReadEnemyAttackEvents(
                    localCreature.Player,
                    attackIntent,
                    localCreature,
                    enemy,
                    survivalPreview.State.Identity,
                    enemyAttackOrder,
                    totalDamage,
                    out var modificationState);
                if (modificationState != EnemyDamageModificationState.Supported)
                {
                    return ReadKnownWithUnsupportedEnemyDamage(localCreature);
                }

                raw += attackEvents.Sum(e => e.Amount);
                enemyAttackEvents.AddRange(attackEvents);
                enemyAttackOrder++;
            }
        }

        var futurePowerBlock = VerifiedEtherealExhaustBlockReader.Read(
            localCreature.Player,
            localCreature);
        var hasHpLossResultModifier =
            HasVerifiedHpLossResultModifier(localCreature.Player, localCreature);
        if (hasHpLossResultModifier
            || futurePowerBlock.State != EtherealExhaustBlockReadState.Known
            || futurePowerBlock.Events.Count > 0)
        {
            return ReadKnownWithOrderedEvents(
                localCreature.Player,
                localCreature,
                enemyAttackEvents,
                foundDamage,
                futurePowerBlock,
                hasHpLossResultModifier);
        }

        if (TryReadHandTurnEndDamage(localCreature.Player, localCreature, out var handTurnEndDamage))
        {
            raw += handTurnEndDamage;
            foundDamage = foundDamage || handTurnEndDamage > 0;
        }
        else
        {
            return IncomingDamageRead.Unknown;
        }

        if (VerifiedTurnEndPowerDamageReader.TryRead(localCreature, out var turnEndPowerEvents, out var turnEndPowerDamage))
        {
            raw += turnEndPowerDamage;
            foundDamage = foundDamage || turnEndPowerDamage > 0;
        }
        else
        {
            return IncomingDamageRead.Unknown;
        }

        if (!VerifiedFixedTurnEndHpLossReader.TryRead(localCreature.Player, out var directHpLoss))
        {
            return IncomingDamageRead.Unknown;
        }

        if (!foundDamage && directHpLoss <= 0)
        {
            return IncomingDamageRead.Hidden;
        }

        if (!foundDamage)
        {
#if DAMAGE_FORECAST_DEBUG_TRACE
            RecordSimpleExpectedTrace(
                enemyAttackEvents,
                handTurnEndDamage,
                turnEndPowerEvents,
                localCreature.Block,
                PreAttackBlockRead.Known(0, 0),
                directHpLoss,
                raw);
#endif
            return IncomingDamageRead.Known(raw, localCreature.Block, directHpLoss);
        }

        var preAttackBlock = VerifiedPreAttackBlockReader.Read(localCreature.Player, localCreature);
        if (preAttackBlock.State != PreAttackBlockReadState.Known)
        {
            return directHpLoss > 0
                ? IncomingDamageRead.UnknownDirect(directHpLoss)
                : IncomingDamageRead.Unknown;
        }

#if DAMAGE_FORECAST_DEBUG_TRACE
        RecordSimpleExpectedTrace(
            enemyAttackEvents,
            handTurnEndDamage,
            turnEndPowerEvents,
            localCreature.Block,
            preAttackBlock,
            directHpLoss,
            raw);
#endif
        return IncomingDamageRead.Known(raw, localCreature.Block + preAttackBlock.Block, directHpLoss);
    }

    private static IncomingDamageDisplayRead ReadIncomingDamageKnown(
        ICombatState combatState,
        Creature localCreature,
        IncomingDamageDisplayOptions options)
    {
        var player = localCreature.Player;
        if (player is null)
        {
            return IncomingDamageDisplayRead.Unknown;
        }

        var events = new List<UpcomingHpLossEvent>();
        var foundDamage = false;
        var enemyAttackOrder = 0;
        var enemySnapshotIndex = 0;

        foreach (var enemy in combatState.Enemies)
        {
            var snapshotIndex = enemySnapshotIndex++;
            if (enemy is null || !enemy.IsAlive || enemy.Monster?.NextMove?.Intents is null)
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                RecordSkippedEnemy(
                    enemy,
                    enemy is null
                        ? DebugTraceReason.ReadFailure
                        : !enemy.IsAlive
                            ? DebugTraceReason.Dead
                            : DebugTraceReason.NoAttackIntent);
#endif
                continue;
            }

            var attackIntents = enemy.Monster.NextMove.Intents.OfType<AttackIntent>().ToArray();
            if (attackIntents.Length == 0)
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                RecordSkippedEnemy(enemy, DebugTraceReason.NoAttackIntent);
#endif
                continue;
            }

            var enemyIntentContribution = 0;
            var intentTotals = new int[attackIntents.Length];
            for (var i = 0; i < attackIntents.Length; i++)
            {
                intentTotals[i] = attackIntents[i].GetTotalDamage(new[] { localCreature }, enemy);
                enemyIntentContribution += intentTotals[i];
            }

            var survivalPreview = EnemyPreActionSurvivalPreview.Preview(enemy, snapshotIndex, enemyIntentContribution);
            if (!survivalPreview.WillExecuteIntentFor(survivalPreview.State.Identity.StableIdentity))
            {
#if DAMAGE_FORECAST_DEBUG_TRACE
                DebugTraceRuntime.AddStep(
                    survivalPreview.State.Identity.StableIdentity,
                    DebugTraceSourceLevel.Forecast,
                    DebugTraceStepStatus.Skipped,
                    DebugTraceReason.PredictedDeadBeforeAction,
                    null,
                    DebugTraceLane.BlockableDamage,
                    DebugTraceGranularity.SingleEvent,
                    enemyIntentContribution,
                    0);
#endif
                continue;
            }

            for (var i = 0; i < attackIntents.Length; i++)
            {
                var attackEvents = ReadEnemyAttackEvents(
                    player,
                    attackIntents[i],
                    localCreature,
                    enemy,
                    survivalPreview.State.Identity,
                    enemyAttackOrder,
                    intentTotals[i],
                    out var modificationState);
                if (modificationState != EnemyDamageModificationState.Supported)
                {
                    return IncomingDamageDisplayRead.Unknown;
                }

                foreach (var attackEvent in attackEvents)
                {
                    foundDamage = foundDamage || attackEvent.Amount > 0;
                    events.Add(new UpcomingHpLossEvent(
                        attackEvent.Source,
                        attackEvent.NativeExecutionOrder,
                        HpLossDisplayLane.Blockable,
                        attackEvent.Amount,
                        attackEvent.IsSingleVerifiedEvent));
                }

                enemyAttackOrder++;
            }
        }

        if (!TryReadOrderedHandTurnEndEvents(player, localCreature, out var handEvents, out _))
        {
            return IncomingDamageDisplayRead.Unknown;
        }

        foreach (var handEvent in handEvents)
        {
            foundDamage = foundDamage || handEvent.Amount > 0;
            events.Add(new UpcomingHpLossEvent(
                handEvent.Source,
                handEvent.NativeExecutionOrder,
                handEvent.DisplayLane,
                handEvent.Amount,
                handEvent.IsSingleVerifiedEvent));
        }

        if (!VerifiedTurnEndPowerDamageReader.TryRead(localCreature, out var powerEvents, out _))
        {
            return IncomingDamageDisplayRead.Unknown;
        }

        foreach (var powerEvent in powerEvents)
        {
            foundDamage = foundDamage || powerEvent.Amount > 0;
            events.Add(new UpcomingHpLossEvent(
                powerEvent.Source,
                powerEvent.NativeExecutionOrder,
                HpLossDisplayLane.Blockable,
                powerEvent.Amount,
                powerEvent.IsSingleVerifiedEvent));
        }

        if (!foundDamage)
        {
            return IncomingDamageDisplayRead.Hidden;
        }

        var powerBlock = 0;
        var relicBlock = 0;
        var futurePowerBlockEvents = Array.Empty<UpcomingBlockEvent>();

        if (options.IncludePowerBlock || options.IncludeRelicBlock)
        {
            var preAttackBlock = VerifiedPreAttackBlockReader.Read(player, localCreature);
            if (preAttackBlock.State != PreAttackBlockReadState.Known)
            {
                return IncomingDamageDisplayRead.Unknown;
            }

            if (options.IncludePowerBlock
                && events.Any(e =>
                    e.DisplayLane == HpLossDisplayLane.Blockable
                    && e.VerifiedHpLoss > 0))
            {
                powerBlock = preAttackBlock.PowerBlock;
                var futurePowerBlock = VerifiedEtherealExhaustBlockReader.Read(
                    player,
                    localCreature);
                if (futurePowerBlock.State != EtherealExhaustBlockReadState.Known)
                {
                    return IncomingDamageDisplayRead.Unknown;
                }

                futurePowerBlockEvents = futurePowerBlock.Events.ToArray();
            }

            if (options.IncludeRelicBlock)
            {
                relicBlock = preAttackBlock.RelicBlock;
            }
        }

        var availableBlock = new AvailableBlockInput(
            localCreature.Block,
            powerBlock,
            relicBlock);
        var selectedBlock = HpLossEventPolicy.SelectBlock(
            availableBlock,
            options);
        var hpLossEvents = HpLossEventPolicy.ApplySelectedBlock(
            events,
            selectedBlock,
            futurePowerBlockEvents);
#if DAMAGE_FORECAST_DEBUG_TRACE
        DebugTraceRuntime.RecordTimeline(
            events,
            availableBlock,
            options,
            selectedBlock,
            futurePowerBlockEvents,
            hpLossEvents);
#endif
        if (options.IncludePowerHpLossModifiers || options.IncludeRelicHpLossModifiers)
        {
            var modified = VerifiedHpLossResultModifier.Apply(
                player,
                localCreature,
                hpLossEvents,
                ObservedHpLossBudgetTracker.GetSpent(player),
                options.IncludePowerHpLossModifiers,
                options.IncludeRelicHpLossModifiers);
            var supported = modified.State == HpLossResultModificationState.Supported;
#if DAMAGE_FORECAST_DEBUG_TRACE
            var beforeModifier = hpLossEvents.Sum(e => Math.Max(0, e.VerifiedHpLoss));
            DebugTraceRuntime.RecordModifier(
                "HpLossResultModifiers",
                beforeModifier,
                modified.BlockableHpLoss + modified.DirectHpLoss,
                supported);
            if (supported)
            {
                DebugTraceRuntime.SetIncomingFinalFormula(
                    modified.BlockableHpLoss,
                    modified.DirectHpLoss);
            }
#endif
            return supported
                ? IncomingDamageDisplayRead.Known(modified.BlockableHpLoss + modified.DirectHpLoss)
                : IncomingDamageDisplayRead.Unknown;
        }

        var blockableHpLoss = hpLossEvents
            .Where(e => e.DisplayLane == HpLossDisplayLane.Blockable)
            .Sum(e => Math.Max(0, e.VerifiedHpLoss));
        var directHpLoss = hpLossEvents
            .Where(e => e.DisplayLane == HpLossDisplayLane.DirectHpLoss)
            .Sum(e => Math.Max(0, e.VerifiedHpLoss));
#if DAMAGE_FORECAST_DEBUG_TRACE
        DebugTraceRuntime.SetIncomingFinalFormula(blockableHpLoss, directHpLoss);
#endif
        return IncomingDamageDisplayRead.Known(blockableHpLoss + directHpLoss);
    }

    private static IncomingDamageRead ReadKnownWithUnsupportedEnemyDamage(Creature localCreature)
    {
        var player = localCreature.Player;
        if (player is null || HasVerifiedHpLossRelic(player))
        {
            return IncomingDamageRead.Unknown;
        }

        if (HasActiveIntangiblePower(localCreature))
        {
            if (!VerifiedFixedTurnEndHpLossReader.TryReadEvents(player, out var directEvents))
            {
                return IncomingDamageRead.Unknown;
            }

            var modified = VerifiedHpLossResultModifier.Apply(player, localCreature, directEvents, 0);
            return modified.State == HpLossResultModificationState.Supported && modified.DirectHpLoss > 0
                ? IncomingDamageRead.UnknownDirect(modified.DirectHpLoss)
                : IncomingDamageRead.Unknown;
        }

        if (!VerifiedFixedTurnEndHpLossReader.TryRead(player, out var directHpLoss))
        {
            return IncomingDamageRead.Unknown;
        }

        return directHpLoss > 0
            ? IncomingDamageRead.UnknownDirect(directHpLoss)
            : IncomingDamageRead.Unknown;
    }

    private static IncomingDamageRead ReadKnownWithOrderedEvents(
        Player player,
        Creature localCreature,
        IReadOnlyList<BlockableFutureDamageEvent> enemyAttackEvents,
        bool foundEnemyAttack,
        EtherealExhaustBlockRead futurePowerBlock,
        bool applyHpLossResultModifiers)
    {
        if (!TryReadOrderedHandTurnEndEvents(player, localCreature, out var handEvents, out var handBlockableRaw))
        {
            return IncomingDamageRead.Unknown;
        }

        if (!VerifiedTurnEndPowerDamageReader.TryRead(localCreature, out var powerEvents, out var powerBlockableRaw))
        {
            return IncomingDamageRead.Unknown;
        }

        if (handEvents.Count == 0 && powerEvents.Count == 0 && !foundEnemyAttack)
        {
            return IncomingDamageRead.Hidden;
        }

        var blockableRaw = SaturatingAdd(
            SaturatingAdd(handBlockableRaw, powerBlockableRaw),
            enemyAttackEvents.Sum(e => Math.Max(0, e.Amount)));
        var selectedBlock = Math.Max(0, localCreature.Block);
        var preAttackBlock = PreAttackBlockRead.Known(0, 0);
        if (blockableRaw > 0)
        {
            if (futurePowerBlock.State != EtherealExhaustBlockReadState.Known)
            {
                return IncomingDamageRead.Unknown;
            }

            preAttackBlock = VerifiedPreAttackBlockReader.Read(player, localCreature);
            if (preAttackBlock.State != PreAttackBlockReadState.Known)
            {
                return IncomingDamageRead.Unknown;
            }

            selectedBlock = SaturatingAdd(selectedBlock, preAttackBlock.Block);
        }

        var sourceEvents = new List<UpcomingHpLossEvent>(
            handEvents.Count + powerEvents.Count + enemyAttackEvents.Count);
        foreach (var handEvent in handEvents)
        {
            sourceEvents.Add(new UpcomingHpLossEvent(
                handEvent.Source,
                handEvent.NativeExecutionOrder,
                handEvent.DisplayLane,
                handEvent.Amount,
                handEvent.IsSingleVerifiedEvent));
        }

        foreach (var powerEvent in powerEvents)
        {
            sourceEvents.Add(new UpcomingHpLossEvent(
                powerEvent.Source,
                powerEvent.NativeExecutionOrder,
                HpLossDisplayLane.Blockable,
                powerEvent.Amount,
                powerEvent.IsSingleVerifiedEvent));
        }

        foreach (var enemyEvent in enemyAttackEvents)
        {
            sourceEvents.Add(new UpcomingHpLossEvent(
                enemyEvent.Source,
                enemyEvent.NativeExecutionOrder,
                HpLossDisplayLane.Blockable,
                enemyEvent.Amount,
                enemyEvent.IsSingleVerifiedEvent));
        }

        var hpLossEvents = HpLossEventPolicy.ApplySelectedBlock(
            sourceEvents,
            selectedBlock,
            futurePowerBlock.State == EtherealExhaustBlockReadState.Known
                ? futurePowerBlock.Events
                : Array.Empty<UpcomingBlockEvent>());
#if DAMAGE_FORECAST_DEBUG_TRACE
        var expectedOptions = new IncomingDamageDisplayOptions(
            IncludeCurrentBlock: true,
            IncludePowerBlock: true,
            IncludeRelicBlock: true,
            IncludePowerHpLossModifiers: true,
            IncludeRelicHpLossModifiers: true);
        DebugTraceRuntime.RecordTimeline(
            sourceEvents,
            new AvailableBlockInput(
                localCreature.Block,
                preAttackBlock.PowerBlock,
                preAttackBlock.RelicBlock),
            expectedOptions,
            selectedBlock,
            futurePowerBlock.State == EtherealExhaustBlockReadState.Known
                ? futurePowerBlock.Events
                : Array.Empty<UpcomingBlockEvent>(),
            hpLossEvents);
#endif
        if (!applyHpLossResultModifiers)
        {
            var blockableHpLoss = hpLossEvents
                .Where(e => e.DisplayLane == HpLossDisplayLane.Blockable)
                .Sum(e => Math.Max(0, e.VerifiedHpLoss));
            var directHpLoss = hpLossEvents
                .Where(e => e.DisplayLane == HpLossDisplayLane.DirectHpLoss)
                .Sum(e => Math.Max(0, e.VerifiedHpLoss));
#if DAMAGE_FORECAST_DEBUG_TRACE
            DebugTraceRuntime.SetExpectedFinalFormula(blockableHpLoss, directHpLoss);
#endif
            return IncomingDamageRead.Known(blockableHpLoss, 0, directHpLoss);
        }

        var modified = VerifiedHpLossResultModifier.Apply(
            player,
            localCreature,
            hpLossEvents,
            ObservedHpLossBudgetTracker.GetSpent(player));
        if (modified.State == HpLossResultModificationState.Supported)
        {
#if DAMAGE_FORECAST_DEBUG_TRACE
            DebugTraceRuntime.RecordModifier(
                "HpLossResultModifiers",
                hpLossEvents.Sum(e => Math.Max(0, e.VerifiedHpLoss)),
                modified.BlockableHpLoss + modified.DirectHpLoss,
                supported: true);
            DebugTraceRuntime.SetExpectedFinalFormula(
                modified.BlockableHpLoss,
                modified.DirectHpLoss);
#endif
            return modified.BlockableHpLoss > 0 || modified.DirectHpLoss > 0
                ? IncomingDamageRead.Known(modified.BlockableHpLoss, 0, modified.DirectHpLoss)
                : IncomingDamageRead.Hidden;
        }

#if DAMAGE_FORECAST_DEBUG_TRACE
        DebugTraceRuntime.RecordModifier(
            "HpLossResultModifiers",
            hpLossEvents.Sum(e => Math.Max(0, e.VerifiedHpLoss)),
            modified.BlockableHpLoss + modified.DirectHpLoss,
            supported: false);
#endif

        if (modified.State == HpLossResultModificationState.UnsupportedBecauseAggregateEnemyHpLossWithTungstenRod
            && modified.DirectHpLoss > 0)
        {
            return IncomingDamageRead.UnknownDirect(modified.DirectHpLoss);
        }

        return IncomingDamageRead.Unknown;
    }

    private static bool HasVerifiedHpLossResultModifier(Player player, Creature localCreature)
    {
        return HasActiveIntangiblePower(localCreature) || HasVerifiedHpLossRelic(player);
    }

    private static bool HasActiveIntangiblePower(Creature localCreature)
    {
        return localCreature.GetPower<IntangiblePower>()?.Amount > 0;
    }

    private static bool HasVerifiedHpLossRelic(Player player)
    {
        return player.Relics.Any(relic => !relic.IsMelted && (relic is TungstenRod || relic is BeatingRemnant));
    }

    private static IReadOnlyList<BlockableFutureDamageEvent> ReadEnemyAttackEvents(
        Player player,
        AttackIntent attackIntent,
        Creature localCreature,
        Creature enemy,
        EnemyInstanceIdentity enemyIdentity,
        int enemyAttackOrder,
        int totalDamage,
        out EnemyDamageModificationState modificationState)
    {
        var events = new List<BlockableFutureDamageEvent>();
        var orderBase = 1_000_000 + (enemyAttackOrder * 1_000);
        var repeats = attackIntent.Repeats;
        if (repeats > 0)
        {
            var singleDamage = attackIntent.GetSingleDamage(new[] { localCreature }, enemy);
            if (singleDamage >= 0 && singleDamage * repeats == totalDamage)
            {
                for (var i = 0; i < repeats; i++)
                {
                    var modified = VerifiedEnemyDamageModifier.ApplyDiamondDiadem(
                        player,
                        localCreature,
                        attackIntent,
                        enemy,
                        singleDamage,
                        true);
                    if (modified.State != EnemyDamageModificationState.Supported)
                    {
                        modificationState = modified.State;
                        return events;
                    }

                    events.Add(new BlockableFutureDamageEvent(
                        $"EnemyAttackIntent[{enemyIdentity.StableIdentity}:{enemyIdentity.SnapshotIndex}:{enemyAttackOrder}:{i}]",
                        orderBase + i,
                        modified.Amount,
                        true));
                }

                modificationState = EnemyDamageModificationState.Supported;
                return events;
            }
        }

        var aggregateModified = VerifiedEnemyDamageModifier.ApplyDiamondDiadem(
            player,
            localCreature,
            attackIntent,
            enemy,
            totalDamage,
            false);
        if (aggregateModified.State != EnemyDamageModificationState.Supported)
        {
            modificationState = aggregateModified.State;
            return events;
        }

        events.Add(new BlockableFutureDamageEvent(
            $"EnemyAttackIntent[{enemyIdentity.StableIdentity}:{enemyIdentity.SnapshotIndex}:{enemyAttackOrder}]",
            orderBase,
            aggregateModified.Amount,
            false));
        modificationState = EnemyDamageModificationState.Supported;
        return events;
    }

    private static bool TryReadOrderedHandTurnEndEvents(
        Player player,
        Creature localCreature,
        out List<HandTurnEndHpLossEvent> events,
        out int blockableRaw)
    {
        events = new List<HandTurnEndHpLossEvent>();
        blockableRaw = 0;
        var handPile = CardPile.Get(PileType.Hand, player);
        if (handPile is null)
        {
            return true;
        }

        try
        {
            var handCount = handPile.Cards.Count;
            for (var i = 0; i < handPile.Cards.Count; i++)
            {
                var card = handPile.Cards[i];
                var nativeExecutionOrder =
                    VerifiedEtherealExhaustBlockReader.GetHandTurnEndEffectOrder(i);
                var hasVerifiedDirectHpLoss = VerifiedFixedTurnEndHpLossReader.TryReadEvent(
                    card,
                    handCount,
                    nativeExecutionOrder,
                    out var directHpLossEvent);
                var genericAccepted = false;
                DamageVar? damageVar = null;
                if (!hasVerifiedDirectHpLoss)
                {
                    genericAccepted =
                        CardTurnEndDamageInspector.TryGetVerifiedSingleBlockableDamageVar(card, out damageVar);
                }

                var classification = HandCardDamageClassificationPolicy.Classify(
                    new HandCardDamageClassificationInput(
                        genericAccepted,
                        damageVar is not null,
                        hasVerifiedDirectHpLoss));
                if (classification == HandCardDamageClassification.UnsupportedDamage)
                {
                    return false;
                }

                if (classification == HandCardDamageClassification.VerifiedBlockable)
                {
                    var damage = GetModifiedIncomingCardDamage(player, localCreature, card, damageVar!);
                    blockableRaw = SaturatingAdd(blockableRaw, damage);
                    events.Add(new HandTurnEndHpLossEvent(
                        card.GetType().Name,
                        nativeExecutionOrder,
                        HpLossDisplayLane.Blockable,
                        damage,
                        true));
                }
                else if (classification == HandCardDamageClassification.VerifiedDirect)
                {
                    events.Add(new HandTurnEndHpLossEvent(
                        directHpLossEvent.Source,
                        directHpLossEvent.NativeExecutionOrder,
                        directHpLossEvent.DisplayLane,
                        directHpLossEvent.VerifiedHpLoss,
                        directHpLossEvent.IsSingleVerifiedEvent));
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce("incoming.hand-events", exception);
            events.Clear();
            blockableRaw = 0;
            return false;
        }
    }

    private static bool TryReadHandTurnEndDamage(Player player, Creature localCreature, out int damage)
    {
        damage = 0;
        var handPile = CardPile.Get(PileType.Hand, player);
        if (handPile is null)
        {
            return true;
        }

        try
        {
            var handCount = handPile.Cards.Count;
            for (var i = 0; i < handPile.Cards.Count; i++)
            {
                var card = handPile.Cards[i];
                var hasVerifiedDirectHpLoss = VerifiedFixedTurnEndHpLossReader.TryReadEvent(
                    card,
                    handCount,
                    i,
                    out _);
                var genericAccepted = false;
                DamageVar? damageVar = null;
                if (!hasVerifiedDirectHpLoss)
                {
                    genericAccepted =
                        CardTurnEndDamageInspector.TryGetVerifiedSingleBlockableDamageVar(card, out damageVar);
                }

                var classification = HandCardDamageClassificationPolicy.Classify(
                    new HandCardDamageClassificationInput(
                        genericAccepted,
                        damageVar is not null,
                        hasVerifiedDirectHpLoss));
                if (classification == HandCardDamageClassification.UnsupportedDamage)
                {
                    return false;
                }

                if (classification == HandCardDamageClassification.VerifiedBlockable)
                {
                    damage += GetModifiedIncomingCardDamage(player, localCreature, card, damageVar!);
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce("incoming.hand-total", exception);
            damage = 0;
            return false;
        }
    }

    private static int GetModifiedIncomingCardDamage(Player player, Creature localCreature, CardModel card, DamageVar damageVar)
    {
        var modified = HookDamageCompat.ModifyDamage(
            player.RunState,
            localCreature.CombatState!,
            localCreature,
            localCreature,
            damageVar.BaseValue,
            damageVar.Props,
            card,
            ModifyDamageHookType.All,
            CardPreviewMode.None);

        return Math.Max(0, (int)modified);
    }

    private static int SaturatingAdd(int left, int right)
    {
        return (int)Math.Min(
            int.MaxValue,
            (long)Math.Max(0, left) + Math.Max(0, right));
    }

#if DAMAGE_FORECAST_DEBUG_TRACE
    private static void RecordSkippedEnemy(Creature? enemy, DebugTraceReason reason)
    {
        if (!DebugTraceRuntime.IsCapturing)
        {
            return;
        }

        DebugTraceRuntime.AddStep(
            enemy?.Monster?.GetType().Name ?? enemy?.GetType().Name ?? "Enemy[null]",
            DebugTraceSourceLevel.Native,
            DebugTraceStepStatus.Skipped,
            reason,
            null,
            DebugTraceLane.BlockableDamage,
            DebugTraceGranularity.Unknown,
            null,
            null);
    }

    private static void RecordSimpleExpectedTrace(
        IReadOnlyList<BlockableFutureDamageEvent> enemyAttackEvents,
        int handTurnEndDamage,
        IReadOnlyList<VerifiedTurnEndPowerDamageEvent> powerEvents,
        int currentBlock,
        PreAttackBlockRead preAttackBlock,
        int directHpLoss,
        int rawDamage)
    {
        if (!DebugTraceRuntime.IsCapturing)
        {
            return;
        }

        foreach (var enemyEvent in enemyAttackEvents)
        {
            DebugTraceRuntime.AddStep(
                enemyEvent.Source,
                DebugTraceSourceLevel.Native,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                enemyEvent.NativeExecutionOrder,
                DebugTraceLane.BlockableDamage,
                enemyEvent.IsSingleVerifiedEvent
                    ? DebugTraceGranularity.PerHit
                    : DebugTraceGranularity.Aggregate,
                enemyEvent.Amount,
                enemyEvent.Amount);
            DebugTraceRuntime.AddStep(
                $"{enemyEvent.Source}.NativeModifiers",
                DebugTraceSourceLevel.Unknown,
                DebugTraceStepStatus.Unknown,
                DebugTraceReason.AlreadyIncluded,
                enemyEvent.NativeExecutionOrder,
                DebugTraceLane.Modifier,
                DebugTraceGranularity.Unknown,
                null,
                null);
        }

        if (handTurnEndDamage > 0)
        {
            DebugTraceRuntime.AddStep(
                "HandTurnEndDamage",
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                null,
                DebugTraceLane.BlockableDamage,
                DebugTraceGranularity.Aggregate,
                handTurnEndDamage,
                handTurnEndDamage);
        }

        foreach (var powerEvent in powerEvents)
        {
            DebugTraceRuntime.AddStep(
                powerEvent.Source,
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                powerEvent.NativeExecutionOrder,
                DebugTraceLane.BlockableDamage,
                powerEvent.IsSingleVerifiedEvent
                    ? DebugTraceGranularity.SingleEvent
                    : DebugTraceGranularity.Aggregate,
                powerEvent.Amount,
                powerEvent.Amount);
        }

        if (directHpLoss > 0)
        {
            DebugTraceRuntime.AddStep(
                "DirectTurnEndHpLoss",
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                null,
                DebugTraceLane.DirectHpLoss,
                DebugTraceGranularity.Aggregate,
                directHpLoss,
                directHpLoss);
        }

        DebugTraceRuntime.AddStep(
            "CurrentBlock",
            DebugTraceSourceLevel.Native,
            DebugTraceStepStatus.Applied,
            DebugTraceReason.None,
            null,
            DebugTraceLane.BlockGain,
            DebugTraceGranularity.Aggregate,
            Math.Max(0, currentBlock),
            Math.Max(0, currentBlock));
        if (preAttackBlock.PowerBlock > 0)
        {
            DebugTraceRuntime.AddStep(
                "PowerBlock",
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                null,
                DebugTraceLane.BlockGain,
                DebugTraceGranularity.Aggregate,
                preAttackBlock.PowerBlock,
                preAttackBlock.PowerBlock);
        }

        if (preAttackBlock.RelicBlock > 0)
        {
            DebugTraceRuntime.AddStep(
                "RelicBlock",
                DebugTraceSourceLevel.Forecast,
                DebugTraceStepStatus.Applied,
                DebugTraceReason.None,
                null,
                DebugTraceLane.BlockGain,
                DebugTraceGranularity.Aggregate,
                preAttackBlock.RelicBlock,
                preAttackBlock.RelicBlock);
        }

        DebugTraceRuntime.SetExpectedSimpleFormula(
            rawDamage,
            SaturatingAdd(currentBlock, preAttackBlock.Block),
            directHpLoss);
    }
#endif

    private readonly record struct HandTurnEndHpLossEvent(
        string Source,
        int NativeExecutionOrder,
        HpLossDisplayLane DisplayLane,
        int Amount,
        bool IsSingleVerifiedEvent);

    private readonly record struct BlockableFutureDamageEvent(
        string Source,
        int NativeExecutionOrder,
        int Amount,
        bool IsSingleVerifiedEvent);

}
