using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using DamageForecast.Diagnostics;

namespace DamageForecast.Combat;

internal static class VerifiedEtherealExhaustBlockReader
{
    public static EtherealExhaustBlockRead Read(Player player, Creature localCreature)
    {
        try
        {
            if (player.Creature != localCreature)
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            var power = localCreature.GetPower<FeelNoPainPower>();
            if (power is null)
            {
                return VerifiedEtherealExhaustBlockPolicy.Evaluate(
                    new EtherealExhaustBlockInput(
                        FeelNoPainPowerReadState.Absent,
                        BlockPerExhaust: 0,
                        Array.Empty<EtherealExhaustCardInput>()));
            }

            if (power.GetType() != typeof(FeelNoPainPower)
                || power.Owner != localCreature)
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            var blockPerExhaust = Math.Max(0, power.Amount);
            if (blockPerExhaust == 0)
            {
                return VerifiedEtherealExhaustBlockPolicy.Evaluate(
                    new EtherealExhaustBlockInput(
                        FeelNoPainPowerReadState.Known,
                        blockPerExhaust,
                        Array.Empty<EtherealExhaustCardInput>()));
            }

            var handPile = CardPile.Get(PileType.Hand, player);
            if (handPile is null)
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            var hasCurrentEthereal = handPile.Cards.Any(
                card => card.Keywords.Contains(CardKeyword.Ethereal));
            if (hasCurrentEthereal && HasPendingStampedeAutoPlay(player, localCreature, handPile))
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            var cards = new List<EtherealExhaustCardInput>();
            for (var index = 0; index < handPile.Cards.Count; index++)
            {
                var card = handPile.Cards[index];
                if (!card.Keywords.Contains(CardKeyword.Ethereal))
                {
                    continue;
                }

                var prediction = card.GetType().Assembly == typeof(CardModel).Assembly
                    && card.Owner == player
                        ? EtherealExhaustPrediction.Yes
                        : EtherealExhaustPrediction.Unknown;
                cards.Add(new EtherealExhaustCardInput(
                    $"{card.GetType().FullName ?? card.GetType().Name}[{index}]",
                    index,
                    card.HasTurnEndInHandEffect,
                    prediction));
            }

            var futureBlock = VerifiedEtherealExhaustBlockPolicy.Evaluate(
                new EtherealExhaustBlockInput(
                    FeelNoPainPowerReadState.Known,
                    blockPerExhaust,
                    cards));
            return ApplyShadowmeld(localCreature, futureBlock);
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce("incoming.feel-no-pain", exception);
            return EtherealExhaustBlockRead.Unknown;
        }
    }

    internal static int GetHandTurnEndEffectOrder(int handIndex)
    {
        return checked(handIndex * 2);
    }

    private static EtherealExhaustBlockRead ApplyShadowmeld(
        Creature localCreature,
        EtherealExhaustBlockRead futureBlock)
    {
        if (futureBlock.State != EtherealExhaustBlockReadState.Known
            || futureBlock.Events.Count == 0)
        {
            return futureBlock;
        }

        var modifier = VerifiedShadowmeldFutureBlockModifier.Read(
            localCreature);
        if (modifier.State != ShadowmeldFutureBlockModifierReadState.Known)
        {
            return EtherealExhaustBlockRead.Unknown;
        }

        var events = new List<UpcomingBlockEvent>(futureBlock.Events.Count);
        foreach (var blockEvent in futureBlock.Events)
        {
            if (!modifier.TryApply(blockEvent.Amount, out var modifiedAmount))
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            events.Add(blockEvent with { Amount = modifiedAmount });
        }

        return EtherealExhaustBlockRead.Known(events);
    }

    private static bool HasPendingStampedeAutoPlay(
        Player player,
        Creature localCreature,
        CardPile handPile)
    {
        var phase = player.PlayerCombatState?.Phase;
        if (phase is not (PlayerTurnPhase.Play or PlayerTurnPhase.AutoPostPlay))
        {
            return false;
        }

        var stampede = localCreature.GetPower<StampedePower>();
        if (stampede is null || stampede.Amount <= 0)
        {
            return false;
        }

        return handPile.Cards.Any(card =>
            card.Type == CardType.Attack
            && !card.Keywords.Contains(CardKeyword.Unplayable));
    }
}

internal static class VerifiedEtherealExhaustBlockPolicy
{
    private const int OrdinaryEtherealOrderBase = -1_000_000;

    public static EtherealExhaustBlockRead Evaluate(EtherealExhaustBlockInput input)
    {
        if (input.PowerState == FeelNoPainPowerReadState.Absent)
        {
            return EtherealExhaustBlockRead.Known(Array.Empty<UpcomingBlockEvent>());
        }

        if (input.PowerState != FeelNoPainPowerReadState.Known)
        {
            return EtherealExhaustBlockRead.Unknown;
        }

        var blockPerExhaust = Math.Max(0, input.BlockPerExhaust);
        if (blockPerExhaust == 0)
        {
            return EtherealExhaustBlockRead.Known(Array.Empty<UpcomingBlockEvent>());
        }

        var events = new List<UpcomingBlockEvent>();
        foreach (var card in input.Cards)
        {
            if (card.HandIndex < 0
                || card.Prediction == EtherealExhaustPrediction.Unknown)
            {
                return EtherealExhaustBlockRead.Unknown;
            }

            if (card.Prediction != EtherealExhaustPrediction.Yes)
            {
                continue;
            }

            var order = card.HasTurnEndInHandEffect
                ? checked(VerifiedEtherealExhaustBlockReader.GetHandTurnEndEffectOrder(card.HandIndex) + 1)
                : checked(OrdinaryEtherealOrderBase + card.HandIndex);
            events.Add(new UpcomingBlockEvent(
                $"FeelNoPainPower[{card.Source}]",
                order,
                blockPerExhaust));
        }

        return EtherealExhaustBlockRead.Known(events);
    }
}

internal readonly record struct EtherealExhaustBlockInput(
    FeelNoPainPowerReadState PowerState,
    int BlockPerExhaust,
    IReadOnlyList<EtherealExhaustCardInput> Cards);

internal readonly record struct EtherealExhaustCardInput(
    string Source,
    int HandIndex,
    bool HasTurnEndInHandEffect,
    EtherealExhaustPrediction Prediction);

internal readonly record struct UpcomingBlockEvent(
    string Source,
    int NativeExecutionOrder,
    int Amount);

internal readonly record struct EtherealExhaustBlockRead(
    EtherealExhaustBlockReadState State,
    IReadOnlyList<UpcomingBlockEvent> Events)
{
    public static EtherealExhaustBlockRead Unknown =>
        new(EtherealExhaustBlockReadState.Unknown, Array.Empty<UpcomingBlockEvent>());

    public static EtherealExhaustBlockRead Known(IReadOnlyList<UpcomingBlockEvent> events) =>
        new(EtherealExhaustBlockReadState.Known, events);
}

internal enum FeelNoPainPowerReadState
{
    Absent,
    Known,
    Unsupported
}

internal enum EtherealExhaustPrediction
{
    No,
    Yes,
    Unknown
}

internal enum EtherealExhaustBlockReadState
{
    Known,
    Unknown
}
