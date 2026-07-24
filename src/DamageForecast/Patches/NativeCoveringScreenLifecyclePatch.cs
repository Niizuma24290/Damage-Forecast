using System.Reflection;
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
using System.Diagnostics;
using DamageForecast.Diagnostics;
#endif
using Godot;
using HarmonyLib;
using DamageForecast.UI;

namespace DamageForecast.Patches;

[HarmonyPatch]
internal static class NativeCoveringScreenLifecyclePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var seen = new HashSet<MethodBase>();
        foreach (var typeName in DamageForecastNativeCoveringScreenTracker.CoveringScreenTypeNames)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type is null)
            {
                continue;
            }

            var ready = AccessTools.DeclaredMethod(type, "_Ready");
            if (ready is not null && seen.Add(ready))
            {
                yield return ready;
            }

            var enter = AccessTools.DeclaredMethod(type, "_EnterTree");
            if (enter is not null && seen.Add(enter))
            {
                yield return enter;
            }

            var exit = AccessTools.DeclaredMethod(type, "_ExitTree");
            if (exit is not null && seen.Add(exit))
            {
                yield return exit;
            }

            if (!string.Equals(
                    typeName,
                    DamageForecastNativeCoveringScreenTracker.MapScreenTypeName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var open = AccessTools.DeclaredMethod(type, "Open");
            if (open is not null && seen.Add(open))
            {
                yield return open;
            }

            var close = AccessTools.DeclaredMethod(type, "Close");
            if (close is not null && seen.Add(close))
            {
                yield return close;
            }
        }
    }

    private static void Postfix(Node __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name is "_Ready" or "_EnterTree" or "Open")
        {
            DamageForecastNativeCoveringScreenTracker.MarkOpened(__instance);
        }
        else if (__originalMethod.Name is "_ExitTree" or "Close")
        {
            DamageForecastNativeCoveringScreenTracker.MarkClosed(__instance);
        }

        ForecastRefreshPatch.RefreshRegisteredBars();
    }
}

#if !DAMAGE_FORECAST_DISABLE_GLOBAL_VISIBILITY_PATCHES
[HarmonyPatch(typeof(CanvasItem))]
internal static class NativeCoveringScreenVisibilityPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(CanvasItem.Show))]
    private static void ShowPostfix(CanvasItem __instance)
    {
        UpdateCoveringScreenVisibility(
            __instance,
            visible: true
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
            , VisibilityPostfixKind.Show
#endif
        );
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CanvasItem.Hide))]
    private static void HidePostfix(CanvasItem __instance)
    {
        UpdateCoveringScreenVisibility(
            __instance,
            visible: false
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
            , VisibilityPostfixKind.Hide
#endif
        );
    }

    [HarmonyPostfix]
    [HarmonyPatch("set_Visible")]
    private static void SetVisiblePostfix(CanvasItem __instance, bool value)
    {
        UpdateCoveringScreenVisibility(
            __instance,
            value
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
            , VisibilityPostfixKind.SetVisible
#endif
        );
    }

    private static void UpdateCoveringScreenVisibility(
        CanvasItem instance,
        bool visible
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
        , VisibilityPostfixKind kind
#endif
    )
    {
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
        var callbackStartedAt = Stopwatch.GetTimestamp();
        var typeCheckStartedAt = Stopwatch.GetTimestamp();
        var matched = DamageForecastNativeCoveringScreenTracker.IsNativeCombatCoveringScreen(instance);
        var typeCheckTicks = Stopwatch.GetTimestamp() - typeCheckStartedAt;
        var transitioned = false;
        var refreshed = false;
        var refreshTicks = 0L;
        var barsVisited = 0;

        if (matched)
        {
            var wasTrackedOpen = DamageForecastNativeCoveringScreenTracker.IsTrackedOpenForVisibilityProfiling(instance);
            if (visible)
            {
                DamageForecastNativeCoveringScreenTracker.MarkOpened(instance);
                transitioned = !wasTrackedOpen;
            }
            else
            {
                DamageForecastNativeCoveringScreenTracker.MarkClosed(instance);
                transitioned = wasTrackedOpen;
            }

            var refreshStartedAt = Stopwatch.GetTimestamp();
            ForecastRefreshPatch.RefreshRegisteredBars();
            refreshTicks = Stopwatch.GetTimestamp() - refreshStartedAt;
            barsVisited = ForecastRefreshPatch.LastRegisteredBarsVisitedForVisibilityProfiling;
            refreshed = true;
        }

        Aud0007VisibilityProfiler.Record(
            kind,
            matched,
            transitioned,
            typeCheckTicks,
            Stopwatch.GetTimestamp() - callbackStartedAt,
            refreshed,
            refreshTicks,
            barsVisited);

        if (matched && !visible && transitioned)
        {
            Aud0007VisibilityProfiler.Dump("cover-close", reset: false);
        }
#else
        if (!DamageForecastNativeCoveringScreenTracker.IsNativeCombatCoveringScreen(instance))
        {
            return;
        }

        if (visible)
        {
            DamageForecastNativeCoveringScreenTracker.MarkOpened(instance);
        }
        else
        {
            DamageForecastNativeCoveringScreenTracker.MarkClosed(instance);
        }

        ForecastRefreshPatch.RefreshRegisteredBars();
#endif
    }
}
#endif
