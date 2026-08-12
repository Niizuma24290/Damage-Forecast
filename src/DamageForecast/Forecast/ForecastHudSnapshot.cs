using DamageForecast.Combat;

namespace DamageForecast.Forecast;

internal readonly record struct ForecastHudSnapshot(
    ForecastResult ExpectedHpLoss,
    IncomingDamageDisplayRead IncomingDamage
#if DAMAGE_FORECAST_DEBUG_TRACE
    , long DebugTraceCaptureId = 0
#endif
    )
{
    public static ForecastHudSnapshot Hidden => new(ForecastResult.Hidden, IncomingDamageDisplayRead.Hidden);
}
