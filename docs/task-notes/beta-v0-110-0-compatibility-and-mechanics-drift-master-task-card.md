# Damage Forecast — Beta v0.110.0 兼容性与机制漂移调查主任务卡

日期：2026-07-31

Type: Platform Compatibility / API And Content Drift Investigation
Area: Platform
Touches: Mechanics, Forecast Core, Combat UI, Governance
Priority Tag: P1
Queue: Next
Depends on: B110-0 合同与构建前确认当前 HUD Session 已停止源码/测试写入；B110-1 更新某张任务卡前确认其活跃 owner 已停止写入

## Current Control

Classification: CHECKPOINT_TASK
State: Closed
Last completed: B110-4 — Authority Sync And Local Git Checkpoint
Next: None / Monitor official hotfix notes or a later public-beta build as a new task
Approved: B110-0、B110-1、B110-2、B110-3、B110-3C、B110-4
Evidence: 本卡 `B110-0 Audit Checkpoint — 2026-07-31`、`B110-1 Checkpoint — 2026-07-31`、
`B110-2 Checkpoint — 2026-07-31`、`B110-3 Checkpoint — 2026-07-31`、
`B110-3C Checkpoint — 2026-07-31`、`B110-4 Closure — 2026-08-01`
Repository: Closed / Implementation Checkpoint `bb99210` / Closure Checkpoint This Commit

## Goal

以仍保持 `v0.107.1` 的 stable 为不变回归目标，调查新 beta `v0.110.0`
相对既有 beta 基线的版本、程序集、API、依赖与游戏内容/机制漂移，验证当前
Damage Forecast 合同和构建状态，并在证据完成后只更新真正受影响的任务卡。

## Current facts and unknowns

- 本机 beta 身份为 `v0.110.0 / eecc8c4d`；stable 仍以冻结的 `v0.107.1 / 59260271` 为当前回归目标。
- 仓外冻结 beta 基线为 `v0.109.0 / c12f634d`；若没有可验证的 v0.109.1 精确旧 artifact，不得伪造逐版本差异。
- HUD I2-R4 已记录 v0.110.0 合同/构建和条件 `Sentry` 引用证据，但不等于全 Mod API、依赖、内容机制或运行时兼容调查。
- 新增、删除或改变的卡牌、遗物、Power、状态/诅咒、敌人与遭遇尚未形成可信差异清单。
- 当前仓库为共享 dirty worktree；调查必须区分 committed baseline、其他 Session candidate 与本任务证据。

## Scope and boundaries

Included:
- 核对 beta/stable `release_info.json`、主程序集与依赖身份、文件增删和直接使用 API 的签名/类型变化。
- 比较预测相关卡牌、遗物、Power、状态/诅咒、敌人、Intent 与遭遇机制；区分新增、删除、数值/文本变化和可执行语义变化。
- 按当前构建 Authority 运行 v0.110.0、冻结 beta v0.109.0 与 stable v0.107.1 的现有合同、Release build 和产物检查。
- 形成影响矩阵，并在 B110-1 更新受影响的候选/当前任务卡与必要 Authority。

Excluded:
- 在调查 Gate 内修复产品代码、重构 Forecast Engine、扩展机制支持或改写构建脚本。
- 安装 Mod、启动游戏、声明 `RuntimeVerified`、Git、push、tag、Workshop 或发布。
- 制作完整游戏百科、逐资源文件清单，或仅凭本地化文本推断原生执行顺序。
- 把 v0.109.x 历史证据改写成 v0.110.0 证据，或用新 beta 替换 stable v0.107.1 回归目标。

Preserved:
- stable 与旧 beta 证据保留原日期和原目标；新结果只作为新增 target-specific 证据。
- API/机制差异必须来自可复核 artifact、签名、IL/方法体、资源键或官方资料；不确定项保持 `Unknown`。
- 不自动调整任何任务的 `Priority Tag`、`Queue`、`Approved` 或 Gate 顺序。

## Gate B110-0 — Evidence, Contracts And Build Audit

Goal: 建立 v0.110.0 相对既有 beta/stable 基线的可复核差异与兼容影响矩阵。
Allowed: 只读检查当前/冻结 artifact、Authority、源码与任务卡；运行现有合同、构建和产物检查；只回填本卡增量证据。
Deliverable: 精确版本/哈希、程序集与依赖 diff、相关 API diff、游戏内容/机制 diff、合同/构建结果、受影响文件与任务卡清单。
Verification: 每项变化标记 `Unchanged / Added / Removed / Signature Drift / Semantic Drift / Unknown`，并分开记录 stable、旧 beta 与当前 beta。
Pass: 能判断当前 Mod 是兼容、条件兼容、需修复还是证据不足，并为每张受影响任务卡给出具体更新理由。
Stop: 不修产品代码、不更新其他任务卡；报告 B110-0 结果并等待 B110-1 单独批准。

### B110-0 Checkpoint — 2026-07-31

Prerequisite and worktree:
- HUD 活跃卡仍停在 `I2-R4 Installed / Runtime Verification Pending`；本 Gate 开始与结束时，
  其源码/测试文件时间戳均未继续变化，满足本 Gate 的 owner 停写前置条件。
- 仓库是共享 dirty worktree；本 Gate 未清理、覆盖或暂存其他 Session 的变更。
- 本 Gate 只向本卡写入增量证据；未改产品源码、测试、脚本、其他任务卡或 Authority，
  未安装/启动游戏，未执行 Git、publish、Workshop 或发布操作。

Exact target identity:

| Target | Release identity | `sts2.dll` length / SHA256 | `sts2.xml` SHA256 |
|---|---|---|---|
| stable regression | `v0.107.1 / 59260271 / 2026-06-18T15:43:56-07:00` | `9364480 / A1F9E653F1E28E4076558FEE1E60D218619CB7E057B887C6417F62C62C6D7A52` | `940CCC0CD6C2BE3D75AE831A1B91A3375DE571D94FDF896F45B26761148ECCCE` |
| frozen beta baseline | `v0.109.0 / c12f634d / 2026-07-17T02:31:41+00:00` | `9609216 / EE45848FF6319DFC7AF2538D3A52D05D82BEF35EE4C5FD0400DC9EFE8F9054AA` | `5B2FFB64D65061621A10A437FE57F1BC2DB9B33E79DA5B8E2EFD7EF0EA672E89` |
| current beta | `v0.110.0 / eecc8c4d / 2026-07-30T19:54:36-07:00` | `9718784 / 7A2592364FDC6FF4C42BB5F1FF41F9FA12155F84DE772E203ACE1B088EBB607D` | `AA30E1107798FEA8F670764270A1F913ABE8A87A3CDC915755C26419C6A55A9D` |

Assembly and dependency drift:
- `GodotSharp.dll` 与 `0Harmony.dll` 在三个目标间均为 `Unchanged`。
- 当前 beta 为 `Added`：`sts2.deps.json`
  (`B4DAEBC073D305D38992016352E3F5AD0542891436C40D05733CA5DBC9A708B6`)、
  `Sentry.dll 6.7.0`
  (`4F1619B048D0B0F604265075BC5311F9F2E4A0ECDC09662009B7EE11D50C216C`)、
  `Sentry.Godot.dll 1.0.0`
  (`BEF522B322662F6DBF280891E23E2001483107F8BB981F1692C264928753112A`)；
  `sts2.dll` 直接引用并通过模块初始化调用 `SentryAutoInit`。
- 两份冻结 snapshot 没有保存 `.deps.json`/Sentry 文件，因此只能判定
  “snapshot dependency closure 不包含它们”；不能据此反推历史安装目录中一定不存在，历史状态为 `Unknown`。

API and executable-drift result:
- `v0.109.0 -> v0.110.0`：程序集总类型 `9586 -> 9736`；公共类型 `Added 102 /
  Removed 4`，公共成员 `Added 441 / Removed 146`。代表性 `Signature Drift`
  包括 `CombatId?`/`CombatManager` 接口、输入热键接口、`DynamicVar` 可转换表面，
  以及新增 `PoisonPower.Trigger()`。
- Damage Forecast 当前直接读取的核心族为 `Unchanged`：`AttackIntent`、
  `DeathBlowIntent`、`DamageVar`、`HpLossVar`、Burn/Toxic/Decay/Infection/Wither、
  Frost、Plating、Feel No Pain、Intangible、Tungsten Rod、相关 relic/curse、
  `Creature`、`NHealthBar` 与 `NCombatUi`。未发现要求修改产品源码的签名断裂。
- `NEndTurnButton` 公共签名为 `Unchanged`，但热键从 `accept` 转为 `endTurn`，
  为 `Semantic Drift`；现有 HUD 锚点/释放合同仍通过。

Content and mechanics drift:

| Comparison | Evidence classification | Forecast-relevant result |
|---|---|---|
| `v0.107.1 -> v0.109.0` | 历史内容 `Added/Removed` | 顶层内容净变化：卡牌 `+18/-0`、遗物 `+2/-0`、Power `+9/-1`、遭遇 `+3/-0`；Monster/Intent 类型无增删。本 Gate 保留它为历史基线，不改写成 v0.110.0 变化。 |
| `v0.109.0 -> v0.110.0` | `Added/Removed` | 新增 `Sidestep`；移除 `Scare`、`OutbreakPower`；遗物、Monster、Encounter、Intent 无顶层类型增删。 |
| `Outbreak` | `Semantic Drift` | 从“对自身施加 `OutbreakPower`”改为“对所有可命中敌人施加 `PoisonPower`，随后逐敌调用 `PoisonPower.Trigger()`”；构造参数与动态变量也改变。属于未来出牌假设预测的明确新边界。 |
| `PoisonPower` | `Signature Drift` / behavior preserved | 新增公共 `Trigger()`；原 `CalculateTotalDamageNextTurn()` 签名与方法体保持，回合开始逻辑改为委托给 `Trigger()`。现有下一回合 Poison 预测算法未被证明失效。 |
| `ToughEgg.Hatch` | `Semantic Drift` | 随机 HP 上界从排他变为包含（传给 `NextInt` 的最大值 `+1`）；当前预测不模拟孵化，真实运行边界继续为 `Unknown`。 |
| 其余检测到的方法体漂移 | `Semantic Drift`, no current direct hook | 另有卡牌 21、Power 9、遗物 8 个家族的方法体变化；静态证据未证明它们穿透当前 native-intent 读取边界，故不扩大为已支持/已失效声明。 |

Contracts, build and artifact verification:

| Target | Existing guardrail | Release build / artifact |
|---|---|---|
| stable `v0.107.1` | `477/477 PASS` | `0 warnings / 0 errors`；shadow owner `0`；artifact check PASS |
| frozen beta `v0.109.0` | `477/477 PASS` | `0 warnings / 0 errors`；shadow owner `0`；artifact check PASS |
| current beta `v0.110.0` | 默认入口在首个用例前因 `Sentry.Godot, Version=1.0.0.0` 无法由生成的 contract `.deps.json` 解析而 FAIL；使用只读临时 host 显式解析同一精确依赖后，同一已编译合同为 `477/477 PASS` | `0 warnings / 0 errors`；shadow owner `0`；精确两文件产物 PASS |

- 三目标从同一当前源码构建出的 `damage-forecast.dll` 均为 `407552` bytes，
  SHA256 均为 `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`；
  manifest SHA256 均为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- 当前 contract 输出目录虽复制了两个 Sentry DLL，但生成的
  `DamageForecast.ContractTests.deps.json` 未登记它们；默认 .NET host 因而无法解析。
  这证明现有 v0.110.0 guardrail/snapshot 依赖闭包不完整，而不是合同断言失败。

Disposition and impact matrix:
- B110-0 结论：当前 Mod 为 **Conditionally Compatible**。三目标可从同一源码构建且产物一致；
  v0.110.0 的 477 项合同在精确依赖可解析时全过，但 stock guardrail 不能独立复现该结果。
  未启动游戏，`RuntimeVerified=false`。
- B110-1 文档候选：`docs/build-environment.md`（精确依赖与 guardrail truth）、
  `docs/mechanics-evidence.md`（Outbreak/Poison/ToughEgg target-specific 证据）、
  `docs/project-state.md`（条件兼容且未运行时验证）。
- B110-1 任务卡候选：
  `damage-forecast-hud-placement-implementation-master-task-card.md`
  （更正“stock v0.110.0 contract 已通过”的可复现性限定）与
  `card-play-hypothetical-forecast-preview-master-task-card.md`
  （登记 Outbreak 的即时群体 Poison/Trigger 边界）。
- 当前证据没有证明 Forecast Engine 产品源码需要兼容修复，也没有充分理由改动
  timeline/architecture 卡；它们不进入 B110-1 默认写入范围。
- 若要让默认 v0.110.0 guardrail 可独立通过，后续需另行批准兼容修复 Gate，候选文件仅为
  `scripts/Save-Sts2ReferenceSnapshot.ps1`、`scripts/Test-ForecastGuardrails.ps1` 与
  `tests/DamageForecast.ContractTests/DamageForecast.ContractTests.csproj`；
  该修复不属于 B110-1 文档 Gate，B110-0 未实施。

## Gate B110-1 — Impacted Task Cards And Authority Update

Goal: 依据 B110-0 证据更新真正受影响的当前/候选任务卡与必要 Authority。
Allowed: 只改已证明受影响的任务卡、`docs/project-state.md`、`docs/build-environment.md`、`docs/mechanics-evidence.md` 和必要路由；修改前复核文件 owner 与最新内容。
Pass: 每项更新都能追溯到 B110-0；历史 Gate 不重写，未受影响卡不触碰，Priority/Queue/Approved 不改变。
Stop: 完成文档影响更新后停止；如需兼容修复，提出最小后续 Gate 并等待单独批准。

### B110-1 Checkpoint — 2026-07-31

- 修改前复核确认三份 Authority 自 2026-07-24/25 未继续写入；
  future card-play 卡停在 `Proposed / Approval Pending`。
- 本 Gate 执行期间，另一活跃 owner 于 17:02 再次写入
  `ForecastRefreshPatch.cs`，并于 17:06 后继续生成合同构建输出；
  因此 HUD owner 停写前置条件不能视为持续成立。
- 该并行候选的本地 Release DLL SHA256 已变为
  `5E8C06B94364A88A55549240078FA612E11D89AA484C2B5045E672E9F917EEF8`，
  与已安装/B110-0 checkpoint 的 `9021C7E5...` 不同；本任务不解释、不安装也不覆盖
  该候选，Authority 中的 `9021C7E5...` 已明确限定为 B110-0 checkpoint 证据。
- 已更新 `docs/project-state.md`：登记精确三目标、`Conditionally Compatible`、
  stock v0.110.0 Sentry dependency-closure 缺口、当前 I2-R4 安装身份和
  `RuntimeVerified=false`。
- 已更新 `docs/build-environment.md`：登记 v0.110.0 精确依赖、stock contract
  failure 与 investigation-only host `477/477` 的区别，并保持 published mod
  严格两文件、不携带 Sentry DLL。
- 已更新 `docs/mechanics-evidence.md`：只新增 v0.110.0 target-specific
  Outbreak/Poison/ToughEgg/Sidestep/Scare 证据，不改写 stable 或旧 beta 运行时记录。
- 发现并行写入后先撤回 HUD 卡增量；待并行构建结束、源码/测试时间戳稳定且重新读取
  最新 HUD 卡后，才追加 contract 可复现性限定。其运行时下一步、Priority、Queue、
  Approved 与安装证据均未改变。
- 已更新 future card-play 卡：将 Outbreak 的多目标即时 Poison/Trigger 登记为
  CP-0 默认 Unsupported 反例；该卡仍为 `Later / Approved: No`。
- timeline/architecture 卡没有被证明需要变更，保持未触碰。B110-1 未改产品源码、
  测试或脚本，未构建、安装、启动游戏或执行 Git/Workshop/release。

## Gate B110-2 — v0.110.0 Guardrail Dependency Closure

Status: Complete / Verified

Goal: 让 stock current-target guardrail 自行解析 v0.110.0 精确运行时依赖，同时保持
stable v0.107.1、frozen beta v0.109.0 和 published mod 两文件边界不变。

Allowed:
- 最小修改 `scripts/Save-Sts2ReferenceSnapshot.ps1`、
  `scripts/Test-ForecastGuardrails.ps1` 与
  `tests/DamageForecast.ContractTests/DamageForecast.ContractTests.csproj`。
- 增加缺失/错版本依赖的 fail-closed 检查，运行三目标合同、Release build、
  artifact/forbidden-file 与 `git diff --check` 验证。
- 只向本卡回填该 Gate 增量证据。

Pass:
- stock current-target invocation 不依赖调查用 custom host，当前 discovered contracts 全部 PASS。
- 默认 stable/frozen-beta 双目标仍全部 PASS，三目标 Release build 均
  0 warning / 0 error。
- current snapshot/runtime dependency identity可复核；缺失或哈希/版本不匹配时明确失败。
- published mod 仍严格只有 manifest 与 `damage-forecast.dll`，不得携带 Sentry 或
  `.deps.json`。

Stop: 不修改产品 Forecast/HUD 源码，不安装、不启动游戏、不改其他任务卡/Authority，
不执行 Git checkpoint、push、tag、Workshop 或 release；报告结果并等待后续授权。

### B110-2 Checkpoint — 2026-07-31

Prerequisite and target lock:
- 用户明确批准 B110-2 时，HUD 卡为 `I2-R9 Installed / Runtime Verification Pending`；
  源码/测试已停写且没有活跃构建。B110-2 未改 HUD/Forecast 产品源码或 HUD 卡。
- 执行期间本机游戏已自动更新到一个文件自报 `v0.110.1 / db5d3552` 的构建。B110-3C
  后确认它是未单独公告的 Steam `public-beta` BuildID `24489008`。本 Gate 没有扩大目标，
  而是从 B110-0 保留的精确输出冻结
  `v0.110.0 / eecc8c4d / 2026-07-30T19:54:36-07:00` snapshot；
  该未公告 build 被 identity gate 明确拒绝，未冒充 v0.110.0 证据。

Root cause and implementation:
- stock 复现中，Sentry DLL 已复制到 contract 输出，但 `beta` artifacts 目录仍保留
  frozen-beta 生成的旧 `.deps.json`，因此模块初始化找不到 `Sentry.Godot 1.0.0.0`。
- `Save-Sts2ReferenceSnapshot.ps1` 现在为精确 v0.110.0 要求并验证
  `Sentry 6.7.0.0` 与 `Sentry.Godot 1.0.0.0` 的 managed identity，写入 manifest
  dependency rows 和文件 SHA256；存在 `sts2.deps.json` 时也一并冻结。
- `Test-ForecastGuardrails.ps1` 保持原 `all/stable/beta` 默认治理契约，新增独立
  `-Current` 路径；它锁定 `v0.110.0 / eecc8c4d`、校验两项 Sentry identity/hash、
  以引用文件哈希隔离 contract artifacts，并复核生成的 contract `.deps.json`。
- `DamageForecast.ContractTests.csproj` 对两项条件 Sentry reference 显式设置
  `IncludeRuntimeDependency=true`。原有 Timeline Shadow 与 shadow-off artifact
  并行 hunk 均保留，未被本 Gate 重写。

Frozen current snapshot:
- Path: `C:\Users\ROG\Documents\Codex\STS2-reference-snapshots\v0.110.0-beta-eecc8c4d`。
- `sts2.dll` SHA256
  `7A2592364FDC6FF4C42BB5F1FF41F9FA12155F84DE772E203ACE1B088EBB607D`。
- `Sentry.dll` SHA256
  `4F1619B048D0B0F604265075BC5311F9F2E4A0ECDC09662009B7EE11D50C216C`；
  `Sentry.Godot.dll` SHA256
  `BEF522B322662F6DBF280891E23E2001483107F8BB981F1692C264928753112A`。

Verification:

| Target | Stock contracts | Release build | Artifact result |
|---|---|---|---|
| stable `v0.107.1 / 59260271` | `481/481 PASS` | 0 warning / 0 error | shadow owner `0` |
| frozen beta `v0.109.0 / c12f634d` | `481/481 PASS` | 0 warning / 0 error | shadow owner `0` |
| frozen current `v0.110.0 / eecc8c4d` | `481/481 PASS` via stock `-Current`; generated deps contains both Sentry entries | 0 warning / 0 error | shadow owner `0` |

- 三目标 Release DLL SHA256 均为当前 I2-R9 candidate
  `D71356F972A383B2E4F81E90C0E190A59B04EC0BC2A627B990A4E277BA051471`。
- Guardrail negative tests：缺失 `Sentry.Godot.dll`、将其错置为 `Sentry 6.7.0.0`、
  以及把未公告 BuildID `24489008`（文件自报 `v0.110.1 / db5d3552`）当作 current target，
  均在 contract/build 前
  fail closed；snapshot script 的缺失/错 identity 两组负例同样 PASS。
- 活动 Mod 目录只读复核仍严格只有 `damage-forecast.dll` 与 manifest；没有 Sentry、
  `.deps.json` 或第三个文件。DLL/manifest SHA256 分别为上述 `D71356...` 与
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- `git diff --check`、tracked/working forbidden-artifact review 均 PASS。
  本 Gate 未 publish、安装、启动游戏、执行 Git checkpoint/push/tag、Workshop 或 release；
  `RuntimeVerified=false` 仍保持。

## Gate B110-3 — Unannounced Public-Beta Build Compatibility Delta Audit

Status: Complete / Static and Build Verified / Runtime Verification Pending

Goal: 以新冻结的 v0.110.0 为直接基线，只读调查 live Steam `public-beta`
BuildID `24489008`（文件自报 `v0.110.1 / db5d3552`）的程序集、依赖、API 与预测相关
机制增量，并验证 B110-2 dependency closure 是否需要新 target identity。

Allowed: 只读检查 live public-beta build 与三份冻结 snapshot；运行现有合同/Release build；
只向本卡回填证据。不得修产品代码、脚本、测试或其他 Authority/任务卡。

Stop: 报告精确 delta 与兼容结论后停止；任何 snapshot 更新、兼容修复、安装、游戏启动、
Git/Workshop/release 均等待单独批准。

### B110-3 Checkpoint — 2026-07-31

Target and baseline lock:
- 用户明确批准 B110-3；直接基线为冻结 snapshot
  `v0.110.0 / eecc8c4d / 2026-07-30T19:54:36-07:00`，live 目标为
  Steam `public-beta` BuildID `24489008`；其 `release_info.json` 自报
  `v0.110.1 / db5d3552 / 2026-07-31T01:18:29-07:00`。这里是文件身份，不代表已有
  单独的官方 v0.110.1 patch announcement。
- live `sts2.dll` SHA256 为
  `7C446EFABF80614C429B5088E87101423AA5BB4C04FC3E73393261F6E6D404FD`；
  assembly identity 仍为 `sts2 / 0.1.0.0`，完整加载 `9737` types、`0` loader errors。
  基线为 `9736` types，净增 `1`。

Dependency and API delta:
- 直接引用集合与 `sts2.deps.json` 无变化；`GodotSharp.dll`、`0Harmony.dll`、
  `Sentry.dll`、`Sentry.Godot.dll` 的长度与 SHA256 均逐文件等同 v0.110.0。
  因此 B110-2 修复的 Sentry runtime closure 本身没有漂移。
- 公开 API 只有一项签名替换：
  `MegaCrit.Sts2.Core.AutoSlay.Handlers.Rooms.ShopRoomHandler` 从无参构造改为
  `Func<Task,CancellationToken,Task>` 注入构造。类型表中的 `17` 个新增与 `16` 个删除
  均为该 AutoSlay 异步改动引起的 compiler-generated closure/state-machine 类型。
- 可执行增量局限于 AutoSlay 商店购买时并行排空 overlay screen，以及
  `LoadRunLobby.IsPlayerReady(UInt64)` 对未连接/刚离开玩家返回未 ready；两者均不在
  Damage Forecast 读取、HUD 或预测机制路径上。

Forecast mechanics delta:
- 分类数量逐项相同：Card `1545`、Curse `1`、Encounter `118`、Intent `38`、
  Monster `557`、Power `631`、Relic `634`；无新增、删除或重分类。
- 分类内容及 forecast-relevant core type 的公开 API 与规范化 IL body hash 差异均为 `0`。
  本 delta 未发现预测相关数值、文本、执行顺序、damage/block/direct lane、Intent、HUD
  或 multiplayer snapshot 语义漂移；既有 `Unknown`/deferred 边界不变。

Contract, build, and artifact evidence:
- 针对 live BuildID `24489008` 直接运行当前共享 candidate：`482/482 PASS`，
  `0 failed / 0 skipped`；
  生成的 contract `.deps.json` 同时包含 `Sentry/6.7.0` 与 `Sentry.Godot/1.0.0`。
- live BuildID `24489008` Release build 为 `0 warning / 0 error`；普通产物只有
  `damage-forecast.dll` 与 `damage-forecast.json`，shadow runtime owner matches=`0`。
  DLL/JSON SHA256 分别为
  `3A7240BF22293B4F64EAADAA7BF720DCECC449A54AF515EE9029E64F41B19270` 与
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- stock `-Current` 按设计 fail-closed：它仍锁定 `v0.110.0 / eecc8c4d`，对 live 明确报告
  `Expected v0.110.0/eecc8c4d; found v0.110.1/db5d3552`。B110-3C 纠正后，不再把这条
  negative evidence 自动升级为 snapshot/current-target 更新需求；v0.110.0 authority 保持。
- `git diff --check` PASS。B110-3 未改产品代码、脚本、测试或其他 Authority/任务卡，
  未创建/更新 snapshot，未安装、启动游戏、执行 Git checkpoint/push/tag、Workshop 或 release。
  结论仅为 static + contract/build compatible，`RuntimeVerified=false`。

### B110-3C Checkpoint — 2026-07-31

Provenance correction:
- 用户指出当前没有单独公告的 v0.110.1，批准 B110-3C 只做来源确认与本卡纠正。
- 本机 `appmanifest_2868840.acf` 明确记录 `BetaKey=public-beta`、BuildID `24489008`、
  Windows depot manifest `3766109638843039681`。Steam `content_log.txt` 记录
  2026-07-31 17:39:36～17:39:41 从 BuildID `24485066` 更新到 `24489008`，
  `4 updated / 0 moved / 0 deleted files`。
- 实时 Steam branch 数据确认 `public-beta` 当前指向 BuildID `24489008`，built at
  `2026-07-31 08:40:18 UTC`、updated at `08:44:16 UTC`；该 build 页面同时明确
  `There are no official patch notes available for this build`。官方 Steam 新闻页当前最新
  版本公告仍为 `Beta Patch Notes - v0.110.0`，未列 v0.110.1 公告。

Corrected conclusion:
- BuildID `24489008` 是已通过 Steam `public-beta` 分发的真实小更新；其游戏文件内部
  自报 `v0.110.1 / db5d3552`。但“存在该自报身份的 public-beta build”不能写成
  “官方已经单独公告 v0.110.1”。本卡后续统一称其为
  `unannounced public-beta BuildID 24489008 (self-reported v0.110.1/db5d3552)`。
- B110-3 的程序集/合同/build delta 证据仍适用于该精确 build；它不构成官方版本公告证据。
  冻结 `v0.110.0 / eecc8c4d` 继续作为 current authority；不创建 v0.110.1 snapshot，
  不修改 `-Current` identity。等待官方 hotfix notes 或后续 public-beta build 后再单独定 Gate。
- B110-3C 未修改产品代码、脚本、测试、其他 Authority/任务卡或 snapshot，未构建、安装、
  启动游戏、执行 Git/Workshop/release；`RuntimeVerified=false` 保持。

## Gate B110-4 — Authority Sync And Local Git Checkpoint

Status: Complete / Checkpointed

Goal: 在 HUD I3 完成独立 checkpoint 后，复核共享 dirty tree，只同步 B110 当前事实并为
B110 自有脚本、测试、Authority 与本卡创建可追溯本地 Git checkpoint。

Allowed: 更新 `docs/build-environment.md`、`docs/mechanics-evidence.md`、
`docs/project-state.md`、本卡与必要 README 路由；只暂存 B110 自有文件/hunk；运行现有
guardrail、artifact review 与 staged diff 检查；创建本地 commit。

Stop: 不暂存 Timeline/Architecture、候选任务卡、产品源码或其他并行改动；不 push、tag、
安装、启动游戏、上传 Workshop 或发布。checkpoint 后记录 commit 与剩余 dirty tree 并停止。

### B110-4 Pre-Checkpoint — 2026-08-01

- Prerequisite: HUD I3 已独立完成 implementation `a4b2e23` 与 closure `d088aed`；
  B110 从该 HEAD 重新审计 dirty tree，不把 HUD 或其他已提交内容重复纳入。
- Authority: `docs/build-environment.md` 已改为 B110-2 后的 exact current snapshot/
  Sentry closure 事实；`docs/project-state.md` 区分 B110 static/contract/build 结论与
  HUD I2-R10 的有限用户运行时证据；`docs/mechanics-evidence.md` 保留 v0.110.0
  target-specific drift ledger。
- Verification: 当前共享 candidate 在 stable v0.107.1、frozen beta v0.109.0、
  frozen current v0.110.0 三目标均为 `482/482 PASS`；三目标 Release build 均
  `0 warning / 0 error`，current generated deps 含两项 Sentry runtime asset，
  shadow owner、tracked/working forbidden artifact 均为 `0`，`git diff --check` PASS。
- Boundary: Timeline/Architecture task cards、Timeline 产品/合同文件、其他候选任务卡、
  `task-closure-standard.md` 与个人 `.agents/.obsidian/canvas` 内容均不属于 B110，
  保持 unstaged。README 只在最终 closure commit 暂存 B110 自身路由 hunk。

### B110-4 Closure — 2026-08-01

Result: v0.110.0 程序集/API/依赖/机制审计、受影响 Authority 更新与 Sentry guardrail
closure 已完成；未公告 public-beta BuildID `24489008` 的精确 delta 也已审计并完成来源勘误。

Current state: frozen `v0.110.0 / eecc8c4d` 保持 current authority；当前共享 worktree
candidate 在 stable、frozen beta、frozen current 三目标均为 `482/482 PASS` 和
Release `0 warning / 0 error`。
B110 不声明 broad runtime verification；HUD I2-R10 只验证用户实际测试的 HUD 场景。

Authority: 本卡为唯一任务进度权威；当前产品/环境/机制事实已同步到
`docs/project-state.md`、`docs/build-environment.md`、`docs/mechanics-evidence.md`，
README 只登记关闭路由。

Repository: implementation checkpoint `bb99210` 只包含 B110 的 7 个文件/自有 hunk；
本次 docs-only closure commit 只包含本卡与 README 的 B110 路由 hunk。其余共享 dirty tree
保持 unstaged。Push、tag、Workshop、release、安装与游戏启动均为 Out of Scope / Unchanged。

## Completion and closure requirements

- 记录 beta v0.110.0、stable v0.107.1 和实际比较基线的精确身份，不能用“最新版本”替代。
- 合同/构建证据与真实游戏运行时证据分开；未安装、未启动即保持 `RuntimeVerified=false`。
- 新卡牌/遗物/敌人或机制只有在可执行差异与预测影响明确时才进入机制影响表。
- 兼容修复若有需要，必须在后续独立 Gate 中实施、验证，并在更新游戏文件后停下交给用户测试。
- 最终按当前收口标准形成可追溯 checkpoint；发布与 Workshop 仍需单独授权。

读取 `docs/task-notes/beta-v0-110-0-compatibility-and-mechanics-drift-master-task-card.md`，
核对 `Current Control`，只执行已批准的下一 Gate，完成后停止。
