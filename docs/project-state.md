# Project State

Last reconciled: 2026-07-31

## Product Naming

The current player-facing and active technical identity is `Damage Forecast` / `damage-forecast`, with assembly/DLL, manifest, BaseLib registration, Harmony owner, install identity, config type, and primary diagnostic prefix aligned. Current settings are owned by `DamageForecast.Settings.DamageForecastBaseLibConfig` and stored in `DamageForecast.cfg`. The isolated compatibility subsystem recognizes the former config and loader identity only as a legacy migration source for supported direct upgrade and rollback. Historical evidence below is not rewritten.

G6 provides a manifest-aware, dry-run-first local identity tool. `Plan` is the default; real `Install`/`Rollback` require explicit `-Execute`, keep backups and hash ledgers outside the Loader-scanned `mods` root, and never mutate Workshop. G6-6 completed beta matching-artifact/config smoke and the full stable rollback, old-install upgrade, clean-install, conflict-preflight, restart-persistence, diagnostic-attribution, and matching-artifact matrix.

## Positioning

Damage Forecast is a read-only Slay the Spire 2 combat HUD mod. Its primary supported surface is the local player's forecast HUD. The default display is a single total expected HP loss value:

```text
-N
```

The total is display-only composition of trusted forecast fields:

```text
N = ForecastResult.OutDamage + ForecastResult.DirectHpLoss
```

Damage Forecast does not simulate a full turn, does not call real damage or command execution paths, and does not modify combat state, saves, network state, cards, powers, relics, or room state.

## Current Target Compatibility

- The immutable stable regression target remains `v0.107.1 / 59260271`; the
  frozen comparison beta remains `v0.109.0 / c12f634d`.
- The frozen current authority is `v0.110.0 / eecc8c4d`; its exact snapshot
  includes `sts2.deps.json`, `Sentry 6.7.0.0`, and `Sentry.Godot 1.0.0.0`.
  The separately selected stock `-Current` guardrail validates this identity,
  dependency closure, generated contract deps, Release build, and ordinary
  two-file artifact. At B110-2 checkpoint all three targets passed the
  then-current `481/481` contracts with 0 warnings / 0 errors.
- The live Steam `public-beta` later advanced to unannounced BuildID `24489008`
  whose files self-report `v0.110.1 / db5d3552`. B110-3 found no
  forecast-relevant public-API, content-family, or normalized-IL drift; the
  current shared candidate passed `482/482` and built with 0 warnings / 0
  errors. No official patch notes were available, so this build is not frozen
  as a new current authority and the exact v0.110.0 gate rejects it.
- Current disposition is static/contract/build compatible. The B110 audit did
  not launch the game and does not independently claim broad runtime
  compatibility. Separate HUD I2-R10 user evidence verifies only the tested HUD
  placement/freeze scenarios on the locally installed BuildID `24489008`.

## Current HUD Behavior

- Default HUD line: total expected HP loss `-N`.
- Phase 13A adds optional incoming damage `N`, disabled by default. `N` is a separate display value and does not change the existing `-N` calculation.
- Damage display modes are `ExpectedHpLossOnly` (default), `IncomingDamageOnly`, and `Both`.
- `-N`、`N` 与护盾/生命损失明细组各自拥有独立的五值 placement：血条左侧、血条右侧、人物上方、血条下方、结束回合按钮上方；同一 placement 内仍由 `IncomingDamagePlacement` 决定 `N` / `-N` 的先后。
- Incoming damage `N` can optionally include current Block, Power/Orb pre-attack Block, relic pre-attack Block, Power HP-loss result modifiers, and relic HP-loss result modifiers.
- Advanced details: optional `🛡 N` and `♥ N`, disabled by default.
- `🛡 N`: trusted final blockable HP loss after verified block and supported HP-loss result modifiers.
- `♥ N`: trusted direct HP loss that does not go through Block.
- Unknown, unsupported, non-combat, zero-output, hidden UI, or untrusted partial values are hidden rather than guessed.
- HUD refreshes from health-bar lifecycle hooks, hand pile changes, relic add/remove/melt, selected turn lifecycle hooks, and settings changes.
- 结束回合冻结固定启用且不再显示设置开关。点击结束回合时保存最后有效 live snapshot 与可见 HUD 位置；按钮退场后冻结层保持数值和位置，取消点击、下一玩家回合或战斗结束时恢复/清理，不改变预测机制。
- 血条侧 HUD 作为本地 `NHealthBar` 子节点跟随角色血条。人物上方 placement 对普通角色使用随原生缩放变化的头部语义点，对储君使用椅子顶部；人物下方 placement 在 Buff 行发生遮挡时按实际 Buff 行高下移。
- 结束回合按钮上方 placement 的活动层直属当前 `NEndTurnButton`，冻结层位于本地 `NCombatUi`；可见 cluster 围绕按钮原生文字/图框中线排列。该路径的持续显示、防闪烁、视觉居中与点击后冻结已由用户在 v0.110.0 验证。
- Known native covering pages hide the HUD only while at least one covering screen remains open and preserve the committed forecast snapshot for immediate restore. Map and `NCardPileScreen` paths cover the map, draw pile, discard pile, and exhaust pile; this matrix is RuntimeVerified on stable v0.107.1 and beta v0.109.0. Combat end, invalid player state, or disabled HUD settings still clear the relevant display state.
- The temporary local health-bar center guide, HUD text center guide, and alignment runtime log have been removed after the alignment observation task.
- Default `HealthBarRight` HUD placement now centers the main HUD label on the same local health-bar center line used by the temporary guide. User X/Y offsets still apply after the default position is calculated.
- The default right-anchor HUD alignment now assigns the current HUD string before positioning, measures that string with `Font.GetStringSize(...)`, and centers the measured text bounds on the health-bar center line instead of centering a fixed empty label rect.
- The default right-anchor HUD target was verified against the temporary cyan guide using the same health-bar center helper while the diagnostic build was active.
- The temporary `[STS2 Party Watch][HUD Align]` runtime log was used for alignment diagnosis and has been removed.
- Runtime coordinate logs showed the vertical mismatch was caused by clamping the main HUD label's local Y position to zero. The local health-bar target center was `Y=8`, the measured main label height was `34`, and the desired label top was `-9`; clamping it to `0` produced `main.deltaY=9`. The local Y clamp has been removed for the main label.
- Post-clamp runtime logs showed `main.deltaY=0` and `guide.deltaY=0`, confirming the main `-N` control and temporary guides are on the same runtime line.
- The advanced detail HUD is now placed to the right of the main `-N` control and centered on the same forecast line. The temporary diagnostic log included detail position, center, and delta fields before the debug instrumentation was removed.

## Supported Mechanics

Status tags:

- `Implemented`: current code exists.
- `RuntimeVerified`: Steam, Workshop subscription, or task note runtime evidence exists.
- `Conditional`: implemented only under documented constraints.
- `Unsupported`: deliberately hidden or not implemented.

| Mechanism | Current status | Evidence boundary |
| --- | --- | --- |
| Enemy `AttackIntent` / DeathBlow intent damage | Implemented, RuntimeVerified | Uses native `AttackIntent.GetTotalDamage(...)`; ordinary attack scenes verified. |
| Hand turn-end blockable `DamageVar`, including current official Burn, Toxic, Decay, Infection, and Wither shapes | Implemented, RuntimeVerified, Conditional | A strict structural recognizer accepts the verified current official single self-target blockable `DamageVar` shape without a card-name allowlist. Stable/beta contracts passed 267/267, and the matching-artifact user matrix passed Burn, Toxic, Decay, Infection, Wither, supported-card combinations, and Block/no-Block scenes. External/Mod damaging cards and ambiguous or changed structures remain Unsupported rather than guessed; this is not a claim that every future official card is automatically supported. |
| Frost | Implemented, RuntimeVerified | Reads `FrostOrb.PassiveVal` as verified pre-attack Block. |
| PlatingPower | Implemented, RuntimeVerified | Reads current local `PlatingPower.Amount`. |
| FeelNoPainPower + current-hand Ethereal exhaust | Implemented, RuntimeVerified, Conditional | Uses ordered future Block from exact native `FeelNoPainPower.Amount` and deterministic native Ethereal exhaust. Default `-N` includes it; positive `N` includes it only when Power Block is selected. Pending Stampede with a playable Attack remains Unknown before auto-play. Stable/beta L1/L2 passed; L3-01–12 have aggregate cross-target coverage, not a single-target `12/12` claim. |
| Orichalcum / FakeOrichalcum | Implemented, RuntimeVerified, Conditional | Counted only when current Block is 0. |
| RippleBasin | Implemented, RuntimeVerified, Conditional | Handles already-played Attack and pending `StampedePower` attack exclusion. |
| CloakClasp | Implemented, RuntimeVerified | Reads current hand count and relic Block var. |
| Beckon / Bad Luck / Regret | Implemented, RuntimeVerified | Exact verified direct-HP-loss readers remain ahead of generic unsupported rejection. Historical individual matrices and the current matching-artifact representative direct-lane plus supported-blockable combination passed without regression. |
| ConstrictPower / DisintegrationPower self-damage | Implemented, RuntimeVerified | Treated as blockable turn-end Power damage based on code evidence and user Steam verification. |
| IntangiblePower on local player direct HP loss | Implemented, RuntimeVerified, Conditional | Applies to verified single HP-loss events before Tungsten Rod and Beating Remnant. Aggregate direct HP loss remains unsupported. |
| TungstenRod | Implemented, Conditional | Applies only to verified single HP-loss events. Aggregate enemy HP loss with Tungsten Rod returns Unknown. |
| BeatingRemnant | Implemented, Conditional | Uses observed current-window HP loss budget and verified event order. Tungsten Rod is applied before Beating Remnant in the Damage Forecast result. |
| Diamond Diadem, legacy and v0.109 | Implemented, Conditional | Capability routing selects the preserved legacy card-count damage-reduction strategy only when `CardsPlayedThisTurn` and `DiamondDiademPower` exist. v0.109 first-turn Block + Blur and unknown future mechanisms keep native attack damage. Runtime re-verification is pending. |
| Ordinary Poison pre-action enemy survival preview | Implemented, Conditional | Code integrated for ordinary enemies with readable `PoisonPower` and `AccelerantPower`; existing multi-enemy behavior retained, but the full专项 Steam matrix is not fully backfilled. |
| `HardToKillPower` / Exoskeleton Poison pre-action preview | Implemented, RuntimeVerified, Conditional | Phase 11C uses native `PoisonPower.CalculateTotalDamageNextTurn()` for capped Poison damage; user Steam verification confirmed Exoskeleton no longer hides `-N`, including with relic and curse modifiers active. |
| `SlipperyPower` / 墨宝 Poison preview | Implemented, RuntimeVerified, Conditional | Active Slippery is previewed as a per-damage-event cap: each Slippery layer caps one Poison tick to 1 HP loss and is consumed for the next tick. User Steam verification confirmed the middle 墨宝's `3x3` Intent was removed and HUD showed only the remaining `-8`. |
| `HardenedShellPower` / SewerClam Poison preview | Implemented, RuntimeVerified, Conditional | Phase 11C reads `DisplayAmount` as remaining HP-loss budget, caps Poison HP loss to that budget, and removes current Intent only when the readable budget is at least current HP and capped Poison reaches current HP. User reported the SewerClam / coral test succeeded on 2026-07-17. |
| Enemy `IntangiblePower` Poison preview | Implemented, RuntimeVerified, Conditional | Phase 11C narrow support was restored on 2026-07-18. Without HardToKill, Slippery, or HardenedShell combinations, each Poison tick contributes at most 1 HP loss and the current Intent is removed only when the readable trigger count reaches current HP. User Steam verification confirmed the exact-lethal TestSubject phase-3 boundary: 9 HP, 20 Poison, Intangible(1), opponent Accelerant(8), native 45 Intent. |
| Local HUD in multiplayer battles | Implemented, RuntimeVerified, Conditional | Workshop subscription runtime screenshot confirmed local player's `-6` HUD under the local character health bar. This is still local-player HUD, not teammate HUD. |
| Formal multiplayer HUD, teammate HUD, shared party HUD | Unsupported | Explicitly frozen. No target-aware multiplayer forecast is claimed. |

## Phase 9B Poison Total Record

Current implementation:

- `EnemyPreActionSurvivalPreview` / `PoisonTickPreview` run inside `LocalIncomingDamageReader` before an enemy instance's current `AttackIntent` is added to the forecast.
- Enemy identity is per native instance: prefer `Creature.CombatId`, otherwise native object reference plus same-snapshot enemy index. Enemy display name is not used as a key.
- Supported ordinary path reads current `PoisonPower.Amount`, current enemy HP, current intent contribution, and opponent `AccelerantPower.Amount`.
- `AccelerantPower` is read only if it is already active at the forecast moment. Damage Forecast does not replay `Accelerant` cards or command queues.
- If supported Poison preview is predicted to kill that enemy before its action, that enemy instance's current Attack Intent is excluded from final `-N`.
- `HardToKillPower` / Exoskeleton uses native `PoisonPower.CalculateTotalDamageNextTurn()` so per-tick damage caps are reflected before deciding whether to exclude current Intent.
- `SlipperyPower` / 墨宝 now previews current Slippery layers as per-tick caps consumed by Poison damage events.
- `HardenedShellPower` / SewerClam now reads current `DisplayAmount` as remaining HP-loss budget. If the budget is unreadable or smaller than current HP, Damage Forecast keeps base Intent; otherwise it caps Poison HP loss by that budget before deciding whether current Intent is removed.
- Enemy `IntangiblePower` now has narrow support: when not combined with HardToKill, Slippery, or HardenedShell, each Poison tick is treated as at most 1 HP loss, so current Intent is removed only when `min(PoisonPower.Amount, 1 + opponent AccelerantPower sum)` reaches current HP.
- Revive / phase / hatch enemies (`TestSubject` / `AdaptablePower`, `NemesisPower`, `HatchPower` / ToughEgg) may now remove current Intent when supported Poison preview reaches current HP, because user confirmed they do not immediately attack after death, revive, or phase transition.
- Unsupported Poison-sensitive HP-loss or budget mechanics still conservatively keep base Intent instead of hiding the whole forecast.
- The preview does not execute real Poison, damage, death, removal, enemy action, RNG, save, or network behavior.

Native poison-bar evidence recorded from the final task notes:

- Native `PoisonPower.CalculateTotalDamageNextTurn()` is the game's model-side total poison preview used by the native poison health-bar foreground.
- Native trigger count is `min(PoisonPower.Amount, 1 + alive opponents' AccelerantPower amount sum)`.
- Native poison preview calls `Hook.ModifyDamage(...)` per tick, so it can reflect damage-cap style powers such as `HardToKillPower` or damage-cap side of `IntangiblePower`.
- Native `NHealthBar.IsPoisonLethal(...)` and `_poisonForeground` are UI preview signals only. They do not prove enemy Intent cancellation, death lifecycle, revive, phase behavior, or HP-loss budget behavior.

Current conservative / unsupported Poison boundaries:

- `HardToKillPower` / Exoskeleton: implemented and RuntimeVerified for the Phase 11C user-provided scenes; still conditional on native `CalculateTotalDamageNextTurn()` being available.
- `SlipperyPower`: implemented and RuntimeVerified as narrow support. When active, each Slippery layer caps one Poison tick to 1 HP loss and is consumed in the preview.
- `HardenedShellPower` / SewerClam: implemented and RuntimeVerified as narrow budget support. It requires readable `DisplayAmount`; remaining budget must be at least current HP, and effective Poison HP loss is capped by remaining budget.
- Enemy `IntangiblePower`: narrow support is active and RuntimeVerified for the representative exact-lethal TestSubject phase-3 boundary. It remains conditional: HardToKill, Slippery, or HardenedShell combinations keep the already-read base Intent.
- `TestSubject` / `AdaptablePower`: supported Poison may remove the current Intent. User runtime confirmation covers the phase-3 Intangible exact-lethal scene and the established rule that revive / phase transition does not immediately execute the old attack. Other lifecycle families remain without separate captures.
- `NemesisPower`: Poison override is enabled for supported poison damage. Runtime verification is pending for this enemy family.
- `ToughEgg` / `HatchPower`: Poison override is enabled for supported poison damage. Runtime verification is pending for this enemy family.

Verification state:

- Implemented: ordinary Poison pre-action survival code path exists; Phase 11C adds native Poison preview for supported capped-damage cases, narrow Slippery tick/layer preview, narrow HardenedShell remaining-budget preview, and conservative keep for active enemy Intangible / unsupported special blockers.
- Built / publish / local install evidenced: recorded in Phase 11C task note.
- RuntimeVerified: Exoskeleton / `HardToKillPower` Phase 11C scene passed user Steam verification, including relic and curse interactions; 墨宝 / `SlipperyPower` narrow tick/layer preview passed user Steam verification; HardenedShell / SewerClam budget support passed user Steam verification.
- Pending runtime verification: dedicated family-specific captures for
  `NemesisPower` and `ToughEgg` / `HatchPower`, plus broader ordinary and
  special-combination matrices. The representative TestSubject phase-3
  Intangible exact-lethal boundary is already RuntimeVerified and must not be
  described as disabled or pending.
- DocumentedOnly: no remaining Phase 11C Poison special case is only documented, but unsupported combinations still keep base Intent.

## Front-End Capabilities

- HUD label is anchored to the local player's `NHealthBar.HpBarContainer` parent or equivalent health-bar node parent.
- Main expected-loss label, incoming-damage label, and detail label are separate nodes.
- The temporary health-bar center guide and HUD text guide were removed after the alignment observation task. The remaining HUD labels continue to use the local health-bar lifecycle and parent node.
- Native covering screen lifecycle is tracked by `DamageForecastNativeCoveringScreenTracker` and `NativeCoveringScreenLifecyclePatch`.
- Settings are defined through `DamageForecast.Settings.DamageForecastBaseLibConfig : BaseLib.Config.SimpleModConfig`, registered with `ModConfigRegistry.Register("damage-forecast", config)`, and read/write `DamageForecast.cfg`.
- The business-facing HUD code reads settings only through `DamageForecastUiSettings`; `DamageForecastSettingsAdapter` syncs BaseLib config changes into that settings surface.
- Settings include HUD enabled, local HUD in multiplayer, advanced details, freeze behavior, damage display mode, incoming damage placement, incoming damage calculation switches, anchor preset, X/Y offset, total color, shield detail color, heart detail color, and BaseLib restore-defaults behavior.
- The former Mod-owned `NModInfoContainer.Fill(Mod)` settings route has been removed.
- The current supported settings route is the BaseLib generated page under the main-menu Mod Configuration flow. Post-G6 C3 runtime verification confirmed one `Damage Forecast` entry under the active `damage-forecast` registration key, one config page, and all 18 values in `DamageForecast.cfg`.
- Phase 13A settings-page smoke verification on 2026-07-16 confirmed the installed incoming-damage settings surface is OK. The full Phase 13A combat HUD matrix is not fully runtime verified.
- The in-combat built-in `模组配置（BaseLib）` / `打开配置` entry is visible but currently unusable; this is recorded as a Phase 12A known limitation, not a Phase 12A blocker.

## Multiplayer Definition

`LocalSinglePlayerHud` is the current HUD product surface. It can be shown in single-player combat and, when `ShowLocalHudInMultiplayer` is true, in multiplayer combat for the local player only.

Current multiplayer behavior:

- Shows one local HUD anchored to the local player's health bar when the local forecast is trusted.
- Does not display remote teammate forecasts.
- Does not create a party/shared HUD.
- Does not send network messages.
- Does not modify multiplayer state.
- Hides numeric output when the local target or damage cannot be trusted.

`FormalMultiplayerHud` remains frozen future work.

## Known Limitations

- Ordinary Poison pre-action survival preview still needs a full dedicated Steam runtime matrix for ordinary Poison, multiple enemies, and same-name enemies, although user reports the existing multi-enemy behavior is currently correct.
- `HardToKillPower` / Exoskeleton Poison interaction is implemented and RuntimeVerified in Phase 11C, but remains conditional on native poison preview availability.
- `SlipperyPower` / 墨宝 is implemented and RuntimeVerified as narrow tick/layer Poison preview.
- Enemy `IntangiblePower` narrow Poison cancellation was restored on 2026-07-18 and RuntimeVerified for the representative TestSubject phase-3 exact-lethal equality boundary. The full special-combination matrix is not separately captured.
- `SewerClam` / `HardenedShellPower` Poison cancellation is implemented only when current `DisplayAmount` is readable and enough remaining budget exists; user runtime verification passed on 2026-07-17.
- `TestSubject` / `AdaptablePower`, `NemesisPower`, and `ToughEgg` / `HatchPower` are enabled for supported Poison override based on user-confirmed revive / phase behavior, but still need dedicated runtime verification.
- Tungsten Rod with aggregate enemy HP loss remains unsupported because per-hit or per-event granularity is required.
- Generic external/Mod hand-card damage remains unsupported. Current official Burn, Toxic, Decay, Infection, and Wither shapes are RuntimeVerified through the strict structural recognizer, but changed/ambiguous IL, non-self targets, multiple damage calls/vars, unsupported props, and future official shapes outside that contract fail closed.
- Legacy Diamond Diadem aggregate damage without verified per-hit rounding keeps native attack damage; it no longer makes the whole HUD Unknown. The v0.109 first-turn Block + Blur mechanism never applies the legacy damage modifier or its Stampede correction.
- Settings persistence is handled by BaseLib config.
- The health-bar center guide, HUD text center guide, `[HUD Align]` runtime log, and `PartyWatchHudDebugGuide` helper were removed in commit `070774b70ef07a5ead50f3e82ad60f1a6a3c6c0f`, Built and locally installed. The main `-N` alignment is RuntimeVerified by post-clamp logs. Post-removal smoke verification passed: user confirmed the HUD view is OK, and latest log inspection found no temporary guide or `[HUD Align]` debug noise. Installed DLL SHA256: `A7E87950FDF19C8BE3985894F988B009CDA188BB341733EC27C415F5E5B4A02D`.
- Workshop state must not be described as a public release unless a public publish is explicitly recorded.
- Historical Phase 12A used BaseLib's raw automatic labels. Phase 12B later
  completed the approved friendly English/Simplified Chinese labels, language
  selection, and HUD color polish while preserving BaseLib's automatic layout.
- The in-combat built-in `模组配置（BaseLib）` / `打开配置` route is currently unusable from the native in-combat settings panel. The main-menu Mod Configuration route is the verified supported route for Phase 12A.

## Phase 11A v1.08 Beta Smoke State

Phase 11A quick compatibility smoke retargeted the local development build to the installed STS2 v1.08 beta / `v0.108.0` game API (`release_info.json` version `v0.108.0`, branch `v0.108.0`, commit `58694f64`).

Result boundary:

- Built against v1.08: Release build passed with `C:\sts2\dotnet\dotnet.exe`, 0 warnings, 0 errors.
- Required code changes: `Hook.BeforeTurnEnd` retargeted to `Hook.BeforeSideTurnEnd`; read-only `Hook.ModifyDamage(...)` preview calls now pass the v0.108.0 nullable `CardPlay` argument; registered HUD bars refresh after local `Hook.AfterPlayerTurnStart`; known covering screens refresh HUD bars after `Show` / `Hide` / `set_Visible`.
- Local development artifact installed after the covering-screen hide fix: `mods\sts2-party-watch-v2\sts2-party-watch-v2.dll`, SHA256 `781B555A5161BBEFDAB19A74A20D3301211B297880CECB854DBB1486BC1A85D0`, timestamp `2026-07-04 01:30:54`.
- Load smoke verified: running `SlayTheSpire2.exe` loaded `sts2-party-watch-v2.dll` from the intended local mods directory.
- Core HUD smoke verified: first v1.08 runtime screenshot showed no HUD; after adding the local `AfterPlayerTurnStart` refresh, user screenshot confirmed `-13` next to the local health bar against a 13 enemy attack intent.
- Core damage update smoke verified: user screenshot confirmed a reduced `-8` forecast after block.
- HUD lifecycle smoke verified: user confirmed turn-start refresh works normally and end-turn freeze behavior is good.
- Covering-screen hide smoke verified after the visibility refresh fix: user confirmed the HUD now hides when opening deck, settings, and map screens.
- Settings entry smoke: not reverified from the final HUD screenshot; the native `NModInfoContainer.Fill(Mod)` patch target remains present in v0.108.0 and builds.
- Not reverified in Phase 11A: full v1.08 compatibility, formal Poison matrix, Workshop release/upload, formal multiplayer HUD, teammate HUD, and shared party HUD.

Key Phase 11A lesson: confirm the loaded artifact path first, reflect the installed v1.08 API instead of guessing from v1.07 names, and treat HUD display failures separately from covering-screen visibility failures. The successful compatibility fixes live in `ForecastRefreshPatch`, the three read-only `Hook.ModifyDamage(...)` callers, `NativeCoveringScreenLifecyclePatch`, and `PartyWatchNativeCoveringScreenTracker`.

Phase 11A is closed as a partial v1.08 smoke. Do not describe this as full v1.08 certification.

## Phase 11B Stable DLL Initialization State

Phase 11B repaired stable branch loading after the local game was switched back to public stable `v0.107.1` (`release_info.json` version `v0.107.1`, branch `v0.107.1`, commit `59260271`).

Result boundary:

- Root cause: stable API incompatibility introduced by the v0.108.0 beta retarget. Stable `Hook` does not expose `BeforeSideTurnEnd`, and stable `Hook.ModifyDamage(...)` does not expose the beta 11-argument signature with nullable `CardPlay`.
- BuiltAgainstStable: Release build passed with `C:\sts2\dotnet\dotnet.exe`, 0 warnings, 0 errors.
- Required code changes: turn-end final snapshot commit is shared and attached through optional Harmony patches for `BeforeTurnEnd` or `BeforeSideTurnEnd`; read-only damage previews now call `HookDamageCompat`, which detects the current 10-argument or 11-argument `Hook.ModifyDamage(...)` signature.
- Local development artifact installed: `mods\sts2-party-watch-v2\sts2-party-watch-v2.dll`, SHA256 `4B324F5B76A96322B3AD660F21EE04DA241D4DA83B26CC64F25235606FBA899A`, timestamp `2026-07-04 02:08:28`.
- StableLoadVerified: user launched stable after the repaired local artifact was installed and reported success.
- StableSettingsSmokeVerified: not reverified in Phase 11B.
- StableHudSmokeVerified: not reverified in Phase 11B.
- Beta after the compatibility wrapper: intended to remain supported through optional hook/signature detection, but not rerun after the local install was switched back to stable.

Key Phase 11B lesson: prove the failure layer first, then isolate version drift behind small compatibility points. The successful fix lives in `ForecastRefreshPatch`, `HookDamageCompat`, and the three read-only `Hook.ModifyDamage(...)` forecast callers.

## Phase 11E Dual Target Build Baseline

Phase 11E is closed as `BuildVerified / RuntimeUnverified`.

- Frozen reference snapshots exist for stable v0.107.1 and beta v0.109.0 outside the repository.
- `scripts/Build-DualTargets.ps1` reads snapshot `release_info.json` files and rebuilds both targets with one no-argument command.
- Both targets build and publish with 0 warnings and 0 errors into independent `work/publish/stable/` and `work/publish/beta/` directories.
- The shared mod manifest has `min_game_version` `0.107.1`; no target-specific JSON is required for this baseline.
- No matching-branch installation, loaded DLL path/hash proof, startup smoke, or HUD smoke was performed for these two generated artifacts.
- Do not describe this baseline as stable/beta runtime compatibility. Runtime verification must be recorded separately for each matching game branch.

## Phase 12A BaseLib Config State

Phase 12A migrated the Party Watch settings baseline to BaseLib automatic `SimpleModConfig`.

Result boundary:

- Implemented: `PartyWatchBaseLibConfig` static properties define the automatic BaseLib config surface.
- Implemented: `PartyWatchSettingsAdapter` listens for BaseLib config changes and applies them to `PartyWatchUiSettings`.
- Implemented: `MainFile.Initialize()` registers the config with BaseLib before Harmony patches run.
- Removed: the old Party Watch-owned `PartyWatchSettingsPatch.cs` route.
- Dependency: project pins BaseLib `3.3.4` as a direct compile-only reference restored from the official release with a required SHA256 check; manifest requires `BaseLib` `min_version` `3.3.4`.
- Build/publish: Release build and publish passed with 0 warnings and 0 errors in Phase 12A notes.
- RuntimeVerified: user screenshot and report on 2026-07-03 confirmed the main-menu Mod Configuration flow shows `STS2PartyWatch`, opens the generated page, and controls are usable.
- Known limitation: the in-combat built-in BaseLib config entry is visible but currently unusable; this is recorded and deferred.
- Out of scope for Phase 12A: friendly labels/localization polish, language selector, HUD color polish, Poison logic changes, and formal multiplayer HUD work.

## Phase 12B BaseLib Config Polish State

Phase 12B is implemented, built, published, and locally installed for user runtime verification.

Task note:

```text
docs/task-notes/phase-12b-config-localization-language-color.md
```

Implemented scope:

- Friendly English / Simplified Chinese labels for Party Watch BaseLib config groups, settings, and closed dropdown values.
- Persisted Party Watch-only `ConfigLanguage` setting with `English` and `简体中文`.
- Existing `TotalExpectedLossColor` is reused as the player-facing `HUD Color`; no duplicate HUD color setting was added.
- BaseLib's automatic rows and controls are still used through `GenerateOptionFromProperty(...)`.
- BaseLib's section headers, restore-defaults button, focus-neighbor setup, scrolling, left mod list, and fullscreen shell remain in use.

Layout rule:

- Continue using BaseLib's automatic page layout, rows, controls, scrolling, spacing, and fullscreen shell.
- Do not implement the hand-drawn/mockup text layout because it can wrap poorly and produce row alignment problems.
- Do not replace the BaseLib page with a fully custom right-side UI.

If an older note uses "Phase 12B" to mean a custom full right-side UI, that custom-UI idea is postponed behind this Phase 12B task.

Current evidence:

- Release build passed with 0 warnings and 0 errors.
- Publish succeeded.
- Local install copied only `sts2-party-watch-v2.dll` and `sts2-party-watch-v2.json` to the Party Watch mod directory.

Runtime status:

- RuntimeVerified through Phase 12C-B G4 from `Main Menu -> Mod Configuration -> Damage Forecast`.
- English and Simplified Chinese titles, labels, closed values, and live language switching passed user runtime verification.
- Language persistence passed a complete restart cycle: the user selected English, closed the game, reopened it, and confirmed English remained selected.

Known Phase 12B limitations:

- The language selector is a standard BaseLib row, not a top-right header control. This preserves BaseLib alignment and avoids fragile custom header layout.
- Hover descriptions are not implemented in this pass because BaseLib's documented hover path expects `settings_ui.json` localization resources, while Party Watch remains code-only.
- Dropdown popup item text still needs runtime verification; closed dropdown text is relabeled after BaseLib row generation and config changes.

## Phase 12C-B Surface Rename Runtime State

- G3: complete / StaticVerified / BuildVerified / ContractVerified.
- G4: complete / Installed / RuntimeVerified.
- Player-facing name: `Damage Forecast` / `伤害预测`.
- Left BaseLib Mod list identity: stable `Damage Forecast`.
- BaseLib page title: follows this Mod's English or Simplified Chinese config language, including first open after launch.
- Technical identities remain unchanged: `sts2-party-watch-v2`, `STS2PartyWatch`, `PartyWatch*`, assembly/project/file names, persistence keys, paths, and `[STS2 Party Watch]` diagnostics.
- Loader evidence: one `Damage Forecast [sts2-party-watch-v2]` initialization and one matching installed manifest.
- Config persistence: RuntimeVerified across full restart.
- Core single-player HUD: RuntimeVerified after the rename/title fixes.
- Local-player multiplayer boundary: unchanged and covered by the earlier Workshop subscription runtime evidence; no formal teammate/shared HUD is claimed.
- Final installed DLL: 112,128 bytes; SHA256 `1BAC85248FD83B62BE9867FD3A2F8F3B54D676D96D3F6B25F71B720A62D544F0`.
- Final manifest: 375 bytes; SHA256 `A0CCDE08C2C7C0DB1C5D3BFC374C7523DFF0ECF492D00E6F154BF1AE1DAC5E11`.
- Verification: stable/beta Release builds 0 warnings / 0 errors; 28/28 contract assertions pass on each frozen target.
- Runtime BaseLib: Workshop v3.3.7; frozen build/contract baseline remains v3.3.4.
- G5: complete / CommitVerified / PushVerified / TagVerified.
- The complete 36-commit development history was fast-forward pushed without squash or history rewrite; the audited product/source tip is `0de7d74`.
- Repository closure is recorded in `docs/task-notes/phase-12c-g5-repository-closure.md` and identified remotely by the annotated tag `g5-repository-closure`.
- Workshop item `3755598583` was not updated during G5.

## Post-G6 Product State

- Active identity: `Damage Forecast` / `damage-forecast` / `DamageForecast`.
- Active assembly, DLL stem, manifest ID/stem, install directory, BaseLib registration key, Harmony owner, namespace, and primary diagnostic prefix are aligned with the new identity.
- Current config identity is `DamageForecast.Settings.DamageForecastBaseLibConfig` / `DamageForecast.cfg`; strict V2 contains 20 ordered keys with three independent placement values and fixed freeze. Strict V1's 18 keys migrate transactionally to V2; rollback is allowed only when all three placements can be represented losslessly by the legacy anchor.
- The compatibility subsystem under `src/DamageForecast/Compatibility/` owns the legacy migration source descriptors and direct schema graph. Ordinary Settings/UI/Combat/Forecast/Patches code contains no legacy config key, file, or Mod ID literal.
- Current local install is the RuntimeVerified I2-R10 build on v0.110.0 at `mods/damage-forecast`, with DLL SHA256 `3A7240BF22293B4F64EAADAA7BF720DCECC449A54AF515EE9029E64F41B19270` and manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`; the active tree is strictly the two manifest/DLL files.
- Runtime depth: stable v0.107.1 full migration/rollback/re-upgrade matrix PASS; beta v0.109.0 matching-artifact, config continuity, one-load/one-page/one-HUD smoke PASS; the prior stable matching artifact passed the damaging Status/Curse hand matrix, supported/direct combinations, and Block/no-Block scenes with a fresh one-load/no-attributable-error log. The native covering-screen fix is RuntimeVerified on both stable and beta for ordinary combat, map, draw pile, discard pile, and exhaust pile with immediate restore after close. Feel No Pain L3-01–12 has aggregate cross-target coverage: stable passed L3-01–09/L3-11/L3-12, beta passed L3-10; this is not a single-target complete matrix.
- Workshop identity remains external and unchanged; repository-root rename remains unperformed.
- The former damage-dealing Status/Curse hand-card HUD-hidden defect is fixed and RuntimeVerified for Burn, Toxic, Decay, Infection, Wither, a representative verified-direct card, non-damaging Status/Curse, supported-card combinations, and Block/no-Block scenes. Support is structural and fail-closed, not an unrestricted future-card or Mod-card compatibility claim.

## Release State

- Branch: `main`.
- Feel No Pain implementation checkpoint:
  `a72fff5f251b2b13d0033509a67755700cdf9fba`; the docs-only local commit
  containing its FP5 Final Closure Record is the closure marker. Neither local
  checkpoint was pushed, tagged, published, installed, or sent to Workshop by
  this closure Gate.
- Remote: `https://github.com/Niizuma24290/Damage-Forecast.git`.
- GitHub: the repository is `Damage-Forecast`; HC5 implementation/verification/authority checkpoint `739b4f5` was pushed to `origin/main`, followed only by its docs-only remote evidence record.
- Post-G6 C1-C4 implementation checkpoint: `8d3c734`; final closure marker: annotated tag `post-g6-c1-c4-closure`.
- Damage-dealing Status/Curse HUD closure checkpoint: `739b4f5`; annotated marker tag: `hc5-damaging-status-curse-hud-closure`.
- Published markers: `milestone-poison-preview`, `milestone-local-multiplayer-hud`, `milestone-hud-alignment-cleanup`, `phase-12c-audit-complete`, `v0.1.0`, `g5-repository-closure`, `post-g6-c1-c4-closure`, and `hc5-damaging-status-curse-hud-closure`.
- Workshop workspace: prepared under ignored `work/`.
- Workshop item status from task notes: private / subscription test work exists, including item `3755598583`, uploaded DLL hash, cover/preview upload records, and local multiplayer HUD subscription validation. The repository state is not documented as a public Workshop release.
- Not committed by policy: DLL, PDB, PCK, logs, `bin/`, `obj/`, `publish/`, `work/`, uploader files, cover assets, `mod_id.txt`, and game directory files.

## Phase Numbering

- Phase 9: historical single-player release cleanup and legacy `phase-9*` notes.
- Phase 10: Workshop upload / private subscription-test milestone.
- Phase 11: all new supplements, maintenance fixes, runtime verification backfills, and documentation updates after Workshop upload.
- Do not create new Phase 9 subphases. Use `docs/task-notes/phase-11-supplements.md` or `phase-11-<topic>.md`.
