using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace DamageForecast.Combat;

internal static class VerifiedPreAttackBlockReader
{
    public static PreAttackBlockRead Read(Player player, Creature localCreature)
    {
        try
        {
            var powerGrants = ReadFrostBlockGrants(player);
            AddPositive(powerGrants, ReadPlatingBlock(localCreature));

            var endTurnBoundaryBlock = localCreature.Block;
            var relicGrants = new List<int>();
            AddPositive(
                relicGrants,
                ReadOrichalcumBlock(player, endTurnBoundaryBlock));
            AddPositive(
                relicGrants,
                ReadFakeOrichalcumBlock(player, endTurnBoundaryBlock));
            AddPositive(
                relicGrants,
                ReadRippleBasinBlock(player, localCreature.CombatState!));
            AddPositive(relicGrants, ReadCloakClaspBlock(player));

            if (powerGrants.Count == 0 && relicGrants.Count == 0)
            {
                return PreAttackBlockRead.Known(0, 0);
            }

            var modifier = VerifiedShadowmeldFutureBlockModifier.Read(
                localCreature);
            if (modifier.State != ShadowmeldFutureBlockModifierReadState.Known
                || !TrySumModified(powerGrants, modifier, out var powerBlock)
                || !TrySumModified(relicGrants, modifier, out var relicBlock))
            {
                return PreAttackBlockRead.Unknown;
            }

            return PreAttackBlockRead.Known(powerBlock, relicBlock);
        }
        catch
        {
            return PreAttackBlockRead.Unknown;
        }
    }

    private static List<int> ReadFrostBlockGrants(Player player)
    {
        var grants = new List<int>();
        var orbs = player.PlayerCombatState?.OrbQueue?.Orbs;
        if (orbs is null)
        {
            return grants;
        }

        foreach (var orb in orbs.OfType<FrostOrb>())
        {
            AddPositive(grants, Math.Max(0, (int)orb.PassiveVal));
        }

        return grants;
    }

    private static int ReadPlatingBlock(Creature localCreature)
    {
        var plating = localCreature.GetPower<PlatingPower>();
        return plating is null ? 0 : Math.Max(0, plating.Amount);
    }

    private static int ReadOrichalcumBlock(Player player, int currentBlock)
    {
        if (currentBlock > 0)
        {
            return 0;
        }

        var relic = FindRelic<Orichalcum>(player);
        return relic is null ? 0 : ReadBlockVar(relic);
    }

    private static int ReadFakeOrichalcumBlock(Player player, int currentBlock)
    {
        if (currentBlock > 0)
        {
            return 0;
        }

        var relic = FindRelic<FakeOrichalcum>(player);
        return relic is null ? 0 : ReadBlockVar(relic);
    }

    private static int ReadRippleBasinBlock(Player player, ICombatState combatState)
    {
        var relic = FindRelic<RippleBasin>(player);
        if (relic is null || HasPlayedAttackThisTurn(player, combatState) || HasPendingStampedeAttack(player))
        {
            return 0;
        }

        return ReadBlockVar(relic);
    }

    private static int ReadCloakClaspBlock(Player player)
    {
        var relic = FindRelic<CloakClasp>(player);
        if (relic is null)
        {
            return 0;
        }

        var handPile = CardPile.Get(PileType.Hand, player);
        return handPile is null
            ? 0
            : checked(handPile.Cards.Count * ReadBlockVar(relic));
    }

    private static bool HasPlayedAttackThisTurn(Player player, ICombatState combatState)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.HappenedThisTurn(combatState)
            && entry.CardPlay.Card.Type == CardType.Attack
            && entry.CardPlay.Card.Owner == player);
    }

    private static bool HasPendingStampedeAttack(Player player)
    {
        var stampede = player.Creature.GetPower<StampedePower>();
        if (stampede is null || stampede.Amount <= 0)
        {
            return false;
        }

        var handPile = CardPile.Get(PileType.Hand, player);
        return handPile is not null
            && handPile.Cards.Any(card =>
                card.Owner == player
                && card.Type == CardType.Attack
                && !card.Keywords.Contains(CardKeyword.Unplayable));
    }

    private static TRelic? FindRelic<TRelic>(Player player)
        where TRelic : RelicModel
    {
        return player.Relics.OfType<TRelic>().FirstOrDefault(relic => !relic.IsMelted);
    }

    private static int ReadBlockVar(RelicModel relic)
    {
        return relic.DynamicVars.Values.OfType<BlockVar>().Select(blockVar => Math.Max(0, (int)blockVar.BaseValue)).FirstOrDefault();
    }

    private static void AddPositive(List<int> grants, int amount)
    {
        if (amount > 0)
        {
            grants.Add(amount);
        }
    }

    private static bool TrySumModified(
        IReadOnlyList<int> grants,
        ShadowmeldFutureBlockModifierRead modifier,
        out int total)
    {
        total = 0;
        foreach (var grant in grants)
        {
            if (!modifier.TryApply(grant, out var modified))
            {
                total = 0;
                return false;
            }

            total = checked(total + modified);
        }

        return true;
    }
}

internal readonly record struct PreAttackBlockRead(
    PreAttackBlockReadState State,
    int PowerBlock,
    int RelicBlock)
{
    public int Block => PowerBlock + RelicBlock;

    public static PreAttackBlockRead Unknown => new(PreAttackBlockReadState.Unknown, 0, 0);

    public static PreAttackBlockRead Known(int powerBlock, int relicBlock) =>
        new(
            PreAttackBlockReadState.Known,
            Math.Max(0, powerBlock),
            Math.Max(0, relicBlock));
}

internal enum PreAttackBlockReadState
{
    Known,
    Unknown
}
