# Build Environment

## Toolchain

- Use `C:\sts2\dotnet\dotnet.exe`.
- Do not use the system `dotnet` for this repository.
- Target framework: `net9.0`.
- By default, the project references STS2 runtime assemblies from:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64
```

- The default can be overridden with `-p:Sts2ReferenceRoot=<reference-root>`.
- `Sts2ReferenceRoot` must point directly at the directory containing `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll`.
- Reference snapshots are kept outside the repository. The first frozen beta snapshot is:

```text
C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.109.0-beta-c12f634d\data_sts2_windows_x86_64
```

- The first frozen stable snapshot is:

```text
C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.107.1-stable-59260271\data_sts2_windows_x86_64
```

## BaseLib compile dependency

- The compile-only BaseLib dependency is pinned to `3.3.4` and downloaded from the official BaseLib GitHub release.
- `scripts/Restore-BaseLibDependency.ps1` verifies SHA256 `C593F14EAAB504FC1D31C89DA7C029116D269F65706D9612D6F71A048E504235` before accepting the DLL.
- The verified DLL is stored under ignored `work/dependencies/BaseLib/3.3.4/`; it is never committed or copied into the published mod.
- `DamageForecast.csproj` uses a direct compile reference. Override it with `-p:BaseLibReferencePath=<verified-dll>` when necessary.
- `scripts/Build-DualTargets.ps1` bootstraps the dependency automatically when the default path is missing.

## ModLoader packaging rules confirmed for this project

Confirmed from the current StS2 mod template wiki and local BaseLib workshop install:

- Local mods are loaded from the game install `mods` directory on Windows.
- A mod is packaged as a small folder containing its manifest JSON, DLL, and optional PCK together.
- The manifest file should use the mod ID as the file stem.
- The manifest `id` determines the DLL/PCK names the game attempts to load.
- The current manifest field names use snake_case, including `has_pck`, `has_dll`, and `affects_gameplay`.
- Code-only mods set `has_dll` to `true` and `has_pck` to `false`.
- v2 has no extra dependency DLLs to ship; STS2, GodotSharp, and Harmony are loaded from the game runtime and are referenced with `Private=false`.

Reference points:

- Alchyr ModTemplate-StS2 wiki: setup, local mods, manifest, and publish rules.
- Local BaseLib install:

```text
C:\Program Files (x86)\Steam\steamapps\workshop\content\2868840\3737335127\BaseLib
├─ BaseLib.json
├─ BaseLib.dll
└─ BaseLib.pck
```

## v2 project rules

- Project: `src/DamageForecast/DamageForecast.csproj`
- Assembly name: `damage-forecast`
- Manifest: `src/DamageForecast/damage-forecast.json`
- Mod ID: `damage-forecast`
- Entry class: `DamageForecast.MainFile`
- Entry marker: `[ModInitializer(nameof(Initialize))]`
- Primary startup log marker: `[Damage Forecast] Loaded`
- Current config owner/file: `DamageForecast` / `DamageForecast.cfg`
- Compatibility input: `STS2PartyWatch.cfg` is a legacy migration source used only by the isolated migration subsystem and rollback tooling.

The publish output must not contain `.deps.json`. The ModLoader scans JSON files under mod folders as manifests, so dependency JSON files can be misread as invalid mod metadata. The project therefore sets `GenerateDependencyFile=false`.

## Commands

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ForecastGuardrails.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Restore-BaseLibDependency.ps1
C:\sts2\dotnet\dotnet.exe restore .\src\DamageForecast\DamageForecast.csproj
C:\sts2\dotnet\dotnet.exe build .\src\DamageForecast\DamageForecast.csproj -c Release --no-restore
C:\sts2\dotnet\dotnet.exe publish .\src\DamageForecast\DamageForecast.csproj -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Save-Sts2ReferenceSnapshot.ps1 -SnapshotName v0.109.0-beta-c12f634d
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Save-Sts2ReferenceSnapshot.ps1 -ReferenceRoot C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2\work\reflect\bin\Debug\net9.0 -SnapshotName v0.107.1-stable-59260271 -Version v0.107.1 -Branch v0.107.1 -Commit 59260271 -ReleaseDate 2026-06-18T15:43:56-07:00
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-DualTargets.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-DualTargets.ps1 -BetaReferenceRoot C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.109.0-beta-c12f634d\data_sts2_windows_x86_64
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-DualTargets.ps1 -StableReferenceRoot C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.107.1-stable-59260271\data_sts2_windows_x86_64 -BetaReferenceRoot C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.109.0-beta-c12f634d\data_sts2_windows_x86_64
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-IdentityPublishTrees.ps1
git diff --check
git status
```

Expected publish output:

```text
src/DamageForecast/bin/Release/net9.0/publish/
├─ damage-forecast.json
└─ damage-forecast.dll

work/publish/beta/damage-forecast/
├─ damage-forecast.json
└─ damage-forecast.dll

work/publish/stable/damage-forecast/
├─ damage-forecast.json
└─ damage-forecast.dll
```

`Test-IdentityPublishTrees.ps1` is read-only. It requires each stable/beta tree
to contain exactly the target manifest and DLL at the top level, parses all
identity/version/dependency fields, reads the managed assembly name, and emits
per-file SHA256 values. Both trees must remain under `work/publish/`, and the
DLL scan rejects personal Debug Trace types, node names, and bilingual UI text.
`Build-DualTargets.ps1` also passes `DamageForecastDebugTrace=false` explicitly
to restore/build/publish. Stable/beta hash differences fail by default; using
`-ApproveHashDifference` is valid only after the difference has been explained
and separately approved. `Build-DualTargets.ps1` invokes this validator after a
two-target publish. Running either publish command still requires an explicit
publish approval; contract/build approval alone does not authorize it.

Forbidden publish/commit artifacts:

```text
*.deps.json
*.dll in git
*.pdb
*.pck
*.log
bin/
obj/
work/
NuGet cache
```

## Local install rule

Expected local install directory:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast
```

Expected install tree:

```text
mods/
└─ damage-forecast/
   ├─ damage-forecast.json
   └─ damage-forecast.dll
```

There must not be an extra nested `publish/` directory under the mod folder.

The identity-aware installer defaults to a read-only JSON plan. It parses manifests, verifies the staging tree, checks the game process and resolved paths, and reports clean-install/upgrade/conflict state:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Install-LocalMod.ps1 `
  -Mode Plan `
  -StagingDir .\work\publish\stable\damage-forecast
```

`Install` requires the reviewed plan's transaction ID, manifest SHA256, DLL SHA256, and explicit `-Execute`; any staging change after review is rejected. `Rollback` also requires explicit `-Execute`. Active staging and installation remain under the resolved `mods` root, while recoverable backups and hash ledgers must remain inside the resolved game root but strictly outside `mods`, because the game Loader recursively discovers JSON manifests below `mods`. A custom backup root inside that Loader-scanned tree is rejected before mutation. Backups are retained through runtime acceptance, and no mutation is authorized merely by running the plan. Workshop scanning is excluded unless `-IncludeWorkshop` and an explicit read-only root are supplied; Workshop mutation is never performed by this script.

Phase 9 task cards use only the explicit commands above. Do not use `STS2PartyWatch.sln`, `NuGet.Config`, or `tools/check-forbidden-files.ps1` for Phase 9.

## Dual target build baseline

- `scripts/Save-Sts2ReferenceSnapshot.ps1` copies the current installed game's or an explicit `ReferenceRoot`'s `release_info.json`, `sts2.dll`, `sts2.xml`, `GodotSharp.dll`, and `0Harmony.dll` to a repository-external snapshot and writes `manifest.json` plus `SHA256SUMS.txt`.
- With no reference-root arguments, `scripts/Build-DualTargets.ps1` reads each snapshot's `release_info.json`, selects stable `v0.107.1` and beta `v0.109.0`, and builds both targets.
- Explicit `StableReferenceRoot` and `BetaReferenceRoot` arguments remain available for one-target builds or manual overrides.
- Stable and beta outputs are independent: `work/bin/<target>/`, `work/obj/<target>/`, and `work/publish/<target>/damage-forecast/`.
- The script redirects NuGet and MSBuild scratch paths into ignored `work/` directories to avoid machine temp lock issues.
- The script fails immediately when `dotnet restore`, `build`, or `publish` returns a non-zero exit code.
- A dual-target run also fails unless both publish trees pass exact two-file,
  manifest, assembly-identity, and SHA256 comparison validation.
- Current beta baseline: v0.109.0 / branch `v0.109.0` / commit `c12f634d`, build passed with 0 warnings and 0 errors.
- Current stable baseline: v0.107.1 / branch `v0.107.1` / commit `59260271`, captured from the old `work/reflect/bin/Debug/net9.0` reference candidate; `sts2.xml` shows the stable hook surface with `Hook.BeforeTurnEnd` and 10-argument `Hook.ModifyDamage`. Build passed with 0 warnings and 0 errors.
- The shared mod manifest sets `min_game_version` to `0.107.1`, so it applies to both baselines; API selection comes from the reference DLLs, not from separate manifest JSON files.

### Current beta v0.110.0 compatibility evidence

- The frozen current authority is `v0.110.0 / eecc8c4d /
  2026-07-30T19:54:36-07:00`, stored under
  `STS2-reference-snapshots/v0.110.0-beta-eecc8c4d`. It is an additional
  compatibility target; it does not replace frozen stable `v0.107.1` or
  frozen beta `v0.109.0`.
- Its `sts2.dll` SHA256 is
  `7A2592364FDC6FF4C42BB5F1FF41F9FA12155F84DE772E203ACE1B088EBB607D`.
  The snapshot also freezes `sts2.deps.json`, `Sentry.dll 6.7.0.0`, and
  `Sentry.Godot.dll 1.0.0.0` with managed identity and SHA256 evidence.
- `Save-Sts2ReferenceSnapshot.ps1` fails closed on missing or mismatched current
  runtime dependencies. `Test-ForecastGuardrails.ps1 -Current` locks the exact
  `v0.110.0 / eecc8c4d` identity, fingerprints all direct/runtime references,
  and verifies that the generated contract `.deps.json` contains both Sentry
  runtime assets.
- At B110-2 checkpoint, stable `v0.107.1`, frozen beta `v0.109.0`, and frozen
  current `v0.110.0` each passed the then-current `481/481` contracts and
  Release build with 0 warnings / 0 errors. Historical B110-0 `477/477`
  investigation-host evidence is superseded by this stock guardrail closure.
- The B110-0 source snapshot's current-target Release build passes with
  0 warnings / 0 errors, no runtime shadow owners, and a strict two-file
  artifact. DLL SHA256 is
  `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`;
  manifest SHA256 is
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`.
  These hashes are checkpoint evidence, not a claim about later shared-worktree
  candidates.
- The live Steam `public-beta` later advanced to unannounced BuildID `24489008`
  whose files self-report `v0.110.1 / db5d3552`. B110-3 found no forecast-path
  API or normalized-IL drift and the shared candidate passed `482/482`, but
  this unannounced build is not a current snapshot authority and is rejected by
  the exact `-Current` identity gate.
- Runtime dependencies belong only to snapshot/contract execution. Never copy
  Sentry DLLs or `.deps.json` into the mod's strict two-file published tree.

## Contract tests

The authoritative local quality gate is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ForecastGuardrails.ps1
```

With no arguments, the script verifies the pinned BaseLib dependency, discovers
the frozen stable `v0.107.1` and beta `v0.109.0` reference roots, runs the same
named contract set against both targets, performs independent Release builds,
then runs `git diff --check` and tracked/working-tree forbidden-artifact review.
It prints per-target case summaries, durations, exit codes, and one final
`QUALITY_GATE` result. For development only, pass `-Target stable` or
`-Target beta`; closure requires the default dual-target run.

The default authoritative gate still targets the two frozen stable/beta
snapshots. Use the separate `-Current` switch for the frozen exact
`v0.110.0 / eecc8c4d` target; do not pass `-Current` together with `-Target`.

The gate writes only ignored build/intermediate data below
`work/forecast-guardrails/`. It does not publish, install, launch the game, or
operate Workshop. Historical audit notes retain their earlier case counts as
evidence of their original dates. Contract/build success does not replace the
separate runtime evidence matrix for Harmony, Godot UI, combat lifecycle, or
BaseLib integration.

## Not verified by the quality-gate command alone

- Steam launch.
- Game Mod list visibility.
- Runtime Loaded log.
- HUD creation, hiding, placement, or attack forecast behavior.

G6 supplies the separate identity-migration runtime evidence: beta v0.109.0
matching-artifact/config smoke passed, and stable v0.107.1 rollback,
old-install upgrade, clean install, isolated conflict preflight, restart
persistence, diagnostic attribution, and matching-artifact checks passed. The
final installed two-file tree uses DLL SHA256
`FB863FF75EA33D6B3F7F9B1471E12E33D5FEBD75595EB680A2D0A49369FDE880`
and manifest SHA256
`09D4DC4EFA54BAA11AB3B3B1E2660FF8DB15D5EA153103AB837AAD22DBEB68C5`.
Those runtime observations do not broaden the quality gate into an installer
or launcher.
