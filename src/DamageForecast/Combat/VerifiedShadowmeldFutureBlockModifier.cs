using DamageForecast.Diagnostics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DamageForecast.Combat;

internal static class VerifiedShadowmeldFutureBlockModifier
{
    public static ShadowmeldFutureBlockModifierRead Read(Creature localCreature)
    {
        try
        {
            var power = localCreature.GetPower<ShadowmeldPower>();
            if (power is null)
            {
                return ShadowmeldFutureBlockModifierRead.Known(decimal.One);
            }

            if (power.GetType() != typeof(ShadowmeldPower)
                || power.Owner != localCreature
                || !ShadowmeldFutureBlockPolicy.TryGetExpectedMultiplier(
                    power.Amount,
                    out var expectedMultiplier))
            {
                return ShadowmeldFutureBlockModifierRead.Unknown;
            }

            var nativeMultiplier = power.ModifyBlockMultiplicative(
                localCreature,
                decimal.Zero,
                default!,
                null!,
                null!);
            return nativeMultiplier == expectedMultiplier
                ? ShadowmeldFutureBlockModifierRead.Known(nativeMultiplier)
                : ShadowmeldFutureBlockModifierRead.Unknown;
        }
        catch (Exception exception)
        {
            DamageForecastDiagnostics.ReportOnce(
                "incoming.shadowmeld-future-block",
                exception);
            return ShadowmeldFutureBlockModifierRead.Unknown;
        }
    }
}

internal readonly record struct ShadowmeldFutureBlockModifierRead(
    ShadowmeldFutureBlockModifierReadState State,
    decimal Multiplier)
{
    public bool TryApply(int baseAmount, out int modifiedAmount)
    {
        if (State != ShadowmeldFutureBlockModifierReadState.Known)
        {
            modifiedAmount = 0;
            return false;
        }

        return ShadowmeldFutureBlockPolicy.TryApplyMultiplier(
            baseAmount,
            Multiplier,
            out modifiedAmount);
    }

    public static ShadowmeldFutureBlockModifierRead Unknown =>
        new(ShadowmeldFutureBlockModifierReadState.Unknown, decimal.Zero);

    public static ShadowmeldFutureBlockModifierRead Known(decimal multiplier) =>
        new(ShadowmeldFutureBlockModifierReadState.Known, multiplier);
}

internal enum ShadowmeldFutureBlockModifierReadState
{
    Known,
    Unknown
}

internal static class ShadowmeldFutureBlockPolicy
{
    public static ShadowmeldFutureBlockContractResult Evaluate(
        ShadowmeldFutureBlockContractInput input)
    {
        if (input.CurrentBlock < 0
            || input.Grants is null
            || input.PowerState == ShadowmeldPowerContractState.Unsupported
            || (input.PowerState == ShadowmeldPowerContractState.Known
                && !input.OwnerMatches))
        {
            return ShadowmeldFutureBlockContractResult.Unknown(input.CurrentBlock);
        }

        var events = new List<UpcomingBlockEvent>();
        foreach (var grant in input.Grants)
        {
            if (string.IsNullOrWhiteSpace(grant.Source)
                || grant.NativeExecutionOrder < 0
                || grant.BaseAmount < 0
                || grant.Eligibility == ShadowmeldGrantEligibility.Unknown
                || grant.Window == ShadowmeldGrantWindow.Unknown)
            {
                return ShadowmeldFutureBlockContractResult.Unknown(input.CurrentBlock);
            }

            if (grant.Eligibility == ShadowmeldGrantEligibility.Ineligible
                || grant.Window == ShadowmeldGrantWindow.AlreadyResolved)
            {
                continue;
            }

            var amount = grant.BaseAmount;
            switch (grant.Window)
            {
                case ShadowmeldGrantWindow.WhileShadowmeldAbsent:
                    if (input.PowerState != ShadowmeldPowerContractState.Absent)
                    {
                        return ShadowmeldFutureBlockContractResult.Unknown(
                            input.CurrentBlock);
                    }

                    break;
                case ShadowmeldGrantWindow.WhileShadowmeldActive:
                    if (input.PowerState != ShadowmeldPowerContractState.Known
                        || !TryGetExpectedMultiplier(
                            grant.LayersAtGrant,
                            out var multiplier)
                        || !TryApplyMultiplier(
                            amount,
                            multiplier,
                            out amount))
                    {
                        return ShadowmeldFutureBlockContractResult.Unknown(
                            input.CurrentBlock);
                    }

                    break;
                case ShadowmeldGrantWindow.AfterShadowmeldRemoved:
                    break;
                default:
                    return ShadowmeldFutureBlockContractResult.Unknown(
                        input.CurrentBlock);
            }

            events.Add(new UpcomingBlockEvent(
                grant.Source,
                grant.NativeExecutionOrder,
                amount));
        }

        return ShadowmeldFutureBlockContractResult.Known(
            input.CurrentBlock,
            events);
    }

    internal static bool TryGetExpectedMultiplier(
        int layers,
        out decimal multiplier)
    {
        multiplier = decimal.Zero;
        if (layers is < 1 or > 30)
        {
            return false;
        }

        multiplier = 1L << layers;
        return true;
    }

    internal static bool TryApplyMultiplier(
        int baseAmount,
        decimal multiplier,
        out int modifiedAmount)
    {
        modifiedAmount = 0;
        if (baseAmount < 0
            || multiplier < decimal.One
            || decimal.Truncate(multiplier) != multiplier)
        {
            return false;
        }

        try
        {
            var product = baseAmount * multiplier;
            if (product > int.MaxValue
                || decimal.Truncate(product) != product)
            {
                return false;
            }

            modifiedAmount = decimal.ToInt32(product);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}

internal readonly record struct ShadowmeldFutureBlockContractInput(
    int CurrentBlock,
    ShadowmeldPowerContractState PowerState,
    bool OwnerMatches,
    IReadOnlyList<ShadowmeldFutureBlockGrantContractInput> Grants);

internal readonly record struct ShadowmeldFutureBlockGrantContractInput(
    string Source,
    int NativeExecutionOrder,
    int BaseAmount,
    ShadowmeldGrantWindow Window,
    ShadowmeldGrantEligibility Eligibility,
    int LayersAtGrant);

internal readonly record struct ShadowmeldFutureBlockContractResult(
    ShadowmeldFutureBlockContractState State,
    int CurrentBlock,
    IReadOnlyList<UpcomingBlockEvent> Events)
{
    public static ShadowmeldFutureBlockContractResult Known(
        int currentBlock,
        IReadOnlyList<UpcomingBlockEvent> events) =>
        new(ShadowmeldFutureBlockContractState.Known, currentBlock, events);

    public static ShadowmeldFutureBlockContractResult Unknown(
        int currentBlock) =>
        new(
            ShadowmeldFutureBlockContractState.Unknown,
            currentBlock,
            Array.Empty<UpcomingBlockEvent>());
}

internal enum ShadowmeldPowerContractState
{
    Absent,
    Known,
    Unsupported
}

internal enum ShadowmeldGrantWindow
{
    AlreadyResolved,
    WhileShadowmeldAbsent,
    WhileShadowmeldActive,
    AfterShadowmeldRemoved,
    Unknown
}

internal enum ShadowmeldGrantEligibility
{
    Eligible,
    Ineligible,
    Unknown
}

internal enum ShadowmeldFutureBlockContractState
{
    Known,
    Unknown
}
