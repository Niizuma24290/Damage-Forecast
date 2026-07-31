using Godot;
using HarmonyLib;
using BaseLib.Config;
using MegaCrit.Sts2.Core.Modding;
using DamageForecast.Compatibility;
using DamageForecast.Identity;
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
using DamageForecast.Diagnostics;
#endif
using DamageForecast.Settings;

namespace DamageForecast;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    internal const string ModId = "damage-forecast";
    internal const string ModVersion = "v0.3.0";
    internal const string DiagnosticPrefix = "[Damage Forecast]";
    private static bool _initialized;
    private static bool _initializing;

    public static void Initialize()
    {
        if (_initialized || _initializing)
        {
            return;
        }

        _initializing = true;
        try
        {
            var conflicts = LegacyIdentityConflictDetector.FindLoadedLegacyAssemblyNames();
            if (conflicts.Count > 0)
            {
                var message = $"{DiagnosticPrefix} Disabled because legacy identity {LegacyIdentityDescriptor.ModId} is also loaded: {string.Join(", ", conflicts)}";
                GD.Print(message);
                Console.Error.WriteLine(message);
                _initialized = true;
                return;
            }

            var migrationOptions = ConfigMigrationOptions.CreateDefault(ModVersion, "runtime");
            var migration = CompatibilityBootstrap.Run(migrationOptions);
            if (!migration.MayRegisterCurrentConfig)
            {
                var message = $"{DiagnosticPrefix} Config migration blocked startup: {migration.Message}";
                GD.Print(message);
                Console.Error.WriteLine(message);
                _initialized = true;
                return;
            }

            Console.WriteLine(
                $"{DiagnosticPrefix} Config migration status={migration.Status} grade={migration.Grade}");
            var hudPlacementMigration = HudPlacementConfigFileMigration.Run(migrationOptions);
            if (!hudPlacementMigration.MayContinue)
            {
                var message = $"{DiagnosticPrefix} HUD placement config migration blocked startup: {hudPlacementMigration.Message}";
                GD.Print(message);
                Console.Error.WriteLine(message);
                _initialized = true;
                return;
            }

            Console.WriteLine(
                $"{DiagnosticPrefix} HUD placement config status={hudPlacementMigration.Status}");
            var config = new DamageForecastBaseLibConfig();
            DamageForecastSettingsAdapter.Bind(config);
            ModConfigRegistry.Register(ModId, config);
            DamageForecastSettingsAdapter.ApplyCurrent();

            new Harmony(ModId).PatchAll(typeof(MainFile).Assembly);
#if DAMAGE_FORECAST_VISIBILITY_PROFILING
            Aud0007VisibilityProfiler.Start();
#endif
            GD.Print($"{DiagnosticPrefix} Loaded");
            Console.WriteLine($"{DiagnosticPrefix} Loaded");
            _initialized = true;
        }
        finally
        {
            _initializing = false;
        }
    }
}
