# Damage Forecast — 屏幕计算过程 Debug Trace

日期：2026-07-30

Type: Diagnostic Feature Design / Conditional Implementation
Area: Forecast Core
Touches: Combat UI
Priority Tag: P2
Queue: Parked

## Current Control

Classification: WORK_COMPLETE
State: Work Complete / Checkpoint Pending
Last completed: 用户于 2026-08-11 确认 DT-2C 游戏内验证成功；个人 Debug 功能收口
Next: None for DT-2C；仅在用户另行批准后形成 Git checkpoint
Approved: DT-0 design + DT-1 implementation + DT-1I refresh + DT-2/DT-2A install + DT-3 safeguard/tests + DT-2B + DT-2C 当前基线合并构建/测试/安装；游戏启动、Git、发布均未批准
Evidence: DT-2C Current-Baseline Rebuild and Local Install Record + User Runtime Confirmation and Closure — 2026-08-11
Repository: DT-2C+MP-1R Personal Debug Installed / DT-2C RuntimeVerified / Recoverable MP-1R Backup / Checkpoint Pending (not authorized)

## Goal

提供默认关闭、仅本机显示的 Debug 模式。默认计算页只解释本次 `-N`、`N`、
`🛡`、`♥` 等实际数值如何得到；选择某个数值后，详细页再展示采用项、跳过项、
Unknown、来源等级与 snapshot 生命周期，便于定位机制和多人客户端错误。

## Current facts and unknowns

- 当前 HUD 提供预测结果和明细，但没有与本次 evaluation 绑定的结构化计算 trace。
- 过去的 HUD 对齐辅助线与日志已经删除；它们不是本任务要恢复的计算诊断功能。
- 原生 API 可能只提供修正后的最终 Intent；无法取得中间步骤时不得伪造“虚弱造成 X→Y”。
- 当前 Block 可能只有汇总值；若未记录获得来源，V0 不能声称 `5+5+5` 来自三张具体卡。
- Debug trace 必须与正式结果共用同一次输入、evaluation 和 snapshot，不能建立第二套算法。

## Confirmed V0 interaction direction

- 计算页按数值分组，只显示实际参与该结果的加减、倍率、顺序和最终等式；跳过项不得混入公式。
- 选择一个数值后打开详细页，列出采用/跳过/Unknown 项、`Native / Forecast / Recorded / Unknown` 来源以及对应 snapshot/generation。
- 跳过原因使用固定分类，例如 `Dead`、`NoAttackIntent`、`DisabledBySetting`、`AlreadyIncluded`、`UnsupportedShape`、`OwnerMismatch` 和 `StaleGeneration`。
- 默认页不显示敌人 ID、刷新原因和生命周期元数据；这些只在详细页出现。
- 详细页应能复制当前数值的完整文本；无法证明的 Strength/Weak、Block 来源或原生顺序保持 `Unknown`。

## Confirmed personal-build and minimum UI direction

以下内容是 2026-08-01 已确认的讨论结论；它们用于约束后续 DT-0 和实现 Gate，
不代表当前已获准创建文件、修改项目或构建 DLL。

### Source and build isolation

#### 当前最新基线重新构建安装规则（2026-08-11 冻结）

- 每次安装个人 Debug 版，都必须从“当时当前的正式源码基线”重新生成隔离候选；禁止直接复用
  `work/debug-trace/` 中上一次留下的 DLL。
- 构建前必须记录并锁定：源码基线 commit、当前游戏版本/commit、安装目录中 DLL/JSON 的
  SHA256，以及本次必须保留的功能开关。若无法联网确认远端，只能写“本机当前最新基线”，
  不得声称已确认线上最新。
- 如果安装目录当前包含另一个已批准的个人诊断（例如 MP-1R），新 Debug 候选必须在同一当前
  基线上合并并保留该诊断；不得用较旧 Debug DLL 把它覆盖掉。
- 安装前先构建默认关闭版与个人 Debug 合并版，并检查功能矩阵：默认关闭版不含 Debug UI；
  个人版含本次 Debug，同时保留基线中已启用的个人诊断；不带入未获批准的并行候选功能。
- 安装严格执行 `Plan -> 锁定候选/当前活动哈希 -> Execute -> 复核仅 DLL/JSON -> Post-Plan`。
  活动哈希在 Plan 与 Execute 之间变化时必须中止；备份必须放在 Mod loader 目录之外。
- 每次安装记录必须明确写出“当前最新基线重新构建”，并列出旧活动哈希、新候选哈希、备份
  位置和安装后复核结果。该句表示流程约束，不表示自动获得游戏启动、Git 或 Workshop 权限。

- 采用同一 `DamageForecast` 项目的条件编译，不另建第二个 Debug Mod 或程序集。
- 使用单一 MSBuild 开关 `DamageForecastDebugTrace`；默认值必须为 `false`，个人
  Debug 构建才显式设为 `true`。
- V0 源码保持为一个最小目录：

```text
src/DamageForecast/Diagnostics/DebugTrace/
├─ DebugTraceData.cs
├─ DebugTraceFormatter.cs
└─ DebugTracePanel.cs
```

- `DebugTraceData` 只承载与正式 evaluation/snapshot 同源的 trace 数据；
  `DebugTraceFormatter` 负责中文优先、英文辅助的显示和复制文本；
  `DebugTracePanel` 负责最小战斗 UI。目录或文件明显增长后才考虑再拆
  `Presentation` 或 `Text` 子目录。
- 正式构建除关闭常量外，还必须通过 `Compile Remove` 剔除整个 DebugTrace
  目录；正式 DLL 的验证应确认不存在 DebugTrace 类型、屏幕节点和双语诊断文案。
- 唯一 production 接入点可受编译常量保护，但不得让正式关闭路径产生持续字符串
  分配、节点 churn 或 trace 状态。

个人构建产物使用仓库已忽略的本地目录：

```text
work/debug-trace/damage-forecast/
├─ damage-forecast.dll
├─ damage-forecast.json
└─ SHA256.txt
```

V0 不预先建立按游戏版本、commit 或 build-id 分层的目录；确实需要同时保留多个
版本时再扩展。正式 Workshop staging 不得从 `work/debug-trace/` 取件。正式版与
个人 Debug 版即使文件名相同，也必须通过来源目录、SHA256 和用途明确区分。

### Minimum battle HUD and interaction

- V0 以战斗页面可点击入口为主，不实现快捷键；避免与游戏、Steam 或其他 Mod
  的输入绑定冲突。若实际使用证明点击不便，未来可另行讨论 `F8`，不得预先加入。
- 入口是战斗画面右上角、版本/HASH 信息下方的一个小按钮：`调试 Debug`。该按钮和全部相关 UI 只存在于
  个人 Debug 构建，正式构建中完全不存在。
- 点击按钮后开启本机会话内 trace，并通过同一条正式预测刷新路径取得当前
  evaluation/snapshot；不得启动第二套 Debug 计算或从最终值反推过程。
- 再次点击按钮、点击关闭按钮或在面板打开时按 `Q`，关闭面板并停止 trace；不再捕获 `Esc`。
- 不增加永久配置项；每次启动游戏和进入新会话时默认关闭。
- V0 只有一个弹出面板：顶部选择 `-N`、`N`、`🛡`、`♥` 等本次存在的数值；
  默认区域显示该数值的简洁公式，进入详情后显示采用、跳过、Unknown、来源和
  snapshot/generation。无需第二个独立窗口。
- 中文作为主标签，英文作为同处辅助标签，例如 `调试 Debug`、`查看详情 Details`、
  `复制 Copy`；V0 不增加语言切换按钮。
- 最小操作固定为：选择数值、查看/返回详情、复制、关闭；标题栏可拖动，且窗口必须
  限制在当前屏幕范围内。V0 不加入缩放、搜索、过滤、跨战斗/重启持久化窗口位置或
  额外 Debug 控制台。

## Scope and boundaries

Included:
- 定义带来源标签的 trace：`Native`、`Forecast`、`Recorded` 与显式 `Unknown`。
- 为每个数值生成只含实际参与项的简洁计算式，并提供按数值进入的可滚动详细页。
- 详细页展示攻击次数、原生有效伤害、Block、未来事件、跳过原因、snapshot/generation、刷新原因和本机多人身份。
- 让结束回合冻结数字与对应 trace 保持同一 snapshot，并支持复制所选数值的详情文本。

Excluded:
- V0 不记录完整出牌历史，也不保证把汇总 Block 拆成每张牌、Power 或遗物的贡献。
- 默认计算页不显示跳过列表、敌人 ID、refresh reason 或 owner/generation 等技术元数据。
- 不发送网络消息，不制作队友/共享 Debug HUD，不上传遥测。
- 不复制 Forecast 算法、不从最终数值倒推过程，也不借本任务采用尚未进入 production 的候选架构。

Preserved:
- Debug 默认关闭，关闭时不得产生持续字符串分配、屏幕节点 churn 或玩家可见变化。
- 无法验证的中间值保持 `Unknown`，最终显示值和 trace 必须可追溯到同一 snapshot。
- 安装、游戏启动、Git、发布和 Workshop 仍按后续 Gate 单独授权。

## Gate DT-0 — Read-only Trace Contract Design

Goal: 确认当前 evaluation 能可靠提供哪些步骤，并定义 V0 trace 数据、来源等级和屏幕出口。
Allowed: 只读检查 authority、源码、contracts 与相关任务卡；不修改、构建、安装、启动游戏或写入 Git。
Deliverable: 计算页/详细页示例、逐数值字段、固定跳过分类、Unknown 边界、同 snapshot 数据流、最小文件影响面、性能预算与后续 Gate 建议。
Verification: 至少覆盖多段攻击、当前 Block、未来 Block、Strength/Weak、结束回合冻结和多人客户端 snapshot。
Pass: 每个数值的公式只由正式结果实际使用项组成；详细页能解释采用/跳过状态且不需要第二套计算，无法提供的来源明确标为 Unknown。
Stop: 回填 DT-0 增量证据后停止，等待用户继续讨论并单独批准后续 Gate。

## DT-0 Read-only Trace Contract Design Record — 2026-08-01

Result: `Complete / StaticReviewed / Continue to DT-1 only after separate approval`。

本 Gate 只读审查当前磁盘上的 production source、contracts、Architecture authority、
MP lifecycle diagnostics 和收口标准，然后只更新本任务卡。未修改 production/test
source，未运行 contracts、build、游戏或 Sim，未 stage、commit、push、tag、安装、
发布或操作 Workshop。

### Current production truth

当前玩家结果链仍是：

```text
ForecastRefreshPatch.BuildForecastHudSnapshot(creature)
  ├─ LocalIncomingDamageReader.ReadForLocalCreature(creature)
  │    -> IncomingDamageRead
  │    -> LocalDamageForecast.Calculate(...)
  │    -> ForecastResult                  # -N / shield / heart
  └─ LocalIncomingDamageReader.ReadIncomingDamageForLocalCreature(...)
       -> IncomingDamageDisplayRead       # N
  -> ForecastHudSnapshot
  -> DamageForecastHudSnapshotStore / HudSnapshotLifecyclePolicy
  -> ForecastHudProjectionPolicy
  -> DamageForecastHudRoot text
```

静态审查确认：

- `-N` 与 `N` 在同一次 HUD refresh 内分别执行两次 Reader 路径；它们不是当前意义上的
  一个统一 evaluation，但可以属于同一个 Debug capture session。Debug 不得把两次读取
  伪装成一次，也不得为了补 trace 再执行第三次读取。
- `IncomingDamageRead` 只保留 `State / RawDamage / EffectiveBlock / DirectHpLoss`；
  ordered-event 路径可能把已经过 Block/修正的 blockable HP loss 放入 `RawDamage` 且把
  `EffectiveBlock` 置零。因此不能仅凭最终 DTO 把字段统一解释为“原始伤害 - Block”。
- `IncomingDamageDisplayRead` 只保留最终 `State / Damage`；敌人/手牌/Power 事件、
  native order、Block timeline、setting 选择和 HP-loss modifier 均在返回前被压平。
- `ForecastHudSnapshot` 目前只含 expected/incoming 最终值，不含 capture identity、owner、
  refresh reason、lifecycle phase 或 generation。
- lifecycle state 单独持有 owner、latest live、committed、pending generation 与
  `Live / LocalReadyWaiting / Frozen`；冻结 snapshot 会保留值，但当前没有可随值流转的
  trace token。
- 当前 MP diagnostics 暴露的 `DiagnosticState`、refresh trigger 和相关 patch hunks 仍是
  未形成 repository checkpoint 的并行工作。Debug 不得把这些易变诊断接口当作稳定 API；
  DT-1 必须重新核对并只加独立的 compile-gated 小 hunk。
- Timeline P1–P5 仍不是 production player authority。DT-1 只记录当前 legacy authority
  实际走过的步骤，不借 Debug 接线或切换 Timeline candidate。

### Frozen V0 trace contract

全部 V0 类型可先收在 `DebugTraceData.cs`。逻辑结构冻结为：

| Contract | Minimum fields / invariant |
|---|---|
| `DebugTraceCapture` | `CaptureId`、owner、refresh reason、expected evaluation、incoming evaluation、sealed values；创建后不可变 |
| `DebugTraceDisplayBinding` | displayed capture id、`Live/Committed` origin、lifecycle phase、pending generation、stale/unavailable reason |
| `DebugTraceValue` | value kind、state、actual displayed amount、formula terms、detail steps；必须与同 capture 的正式结果一致 |
| `DebugTraceStep` | source id、source level、status、reason、native order、lane、granularity、input/output amount；不得持有 MegaCrit/Godot object |

固定枚举：

```text
ValueKind:
  ExpectedTotalHpLoss | IncomingDamage | BlockableHpLoss | DirectHpLoss

SourceLevel:
  Native | Forecast | Recorded | Unknown

StepStatus:
  Applied | Skipped | Unknown

Lane:
  BlockableDamage | DirectHpLoss | BlockGain | Modifier | Lifecycle

Granularity:
  SingleEvent | PerHit | Aggregate | Unknown

Reason:
  None | NotLiveCombat | Dead | PredictedDeadBeforeAction | NoAttackIntent
  DisabledBySetting | AlreadyIncluded | UnsupportedShape | UnsupportedOrder
  UnsupportedGranularity | UnsupportedModifier | OwnerMismatch | ReadFailure
  StaleGeneration | TraceNotCapturedForSnapshot
```

规则：

- 默认公式只包含 `Applied` 且确实影响该屏幕数值的项；`Skipped` 和 `Unknown` 只能进入
  详细页，不得混入公式或被当成零贡献。
- `Native` 表示数值直接来自当前游戏 API，并不表示 Debug 知道该数值内部的 Strength、
  Weak 或其他原生 modifier 分解。
- `Recorded` 只用于已经观测并进入正式计算的状态，例如本回合已花费 HP-loss budget；
  不记录完整出牌历史。
- source label 使用稳定内部 key；中英文名称在 formatter 阶段生成，core trace 不保存
  Godot 节点、Card/Creature/Power/Relic 实例或本地化后的长字符串。
- 所有 amount 与 formula 必须在正式代码确定该步骤时同步记录；formatter 不得从最终
  `-N`/`N` 反推来源或顺序。

### Value-by-value truth boundary

| 屏幕值 | V0 可以可靠解释 | 必须保持 Unknown / 不得声称 |
|---|---|---|
| `-N` | 最终 blockable HP loss、direct HP loss 及其合计；走 ordered path 时可列实际事件顺序 | 只凭 `IncomingDamageRead.RawDamage` 判断它一定是 pre-Block raw |
| `N` | 当前选项实际采用的事件、Block、future Block、modifier 后合计 | 被设置关闭的来源不得加入默认公式 |
| `🛡` | expected result 中实际 surviving blockable HP loss | 当前 Block 的逐卡/逐 Power/逐 Relic 历史来源 |
| `♥` | expected result 中正式采用的 direct HP loss | 未被 reader 记录的 HP-loss 原生中间修正 |
| 敌人多段攻击 | `GetSingleDamage × Repeats == GetTotalDamage` 时按 hit 展示；否则只展示 Native aggregate | aggregate 情况下伪造每 hit 数值或 Tungsten Rod 逐 hit 结果 |
| Strength / Weak | 展示 `AttackIntent.GetTotalDamage(...)` 的 Native final effective value | 将未知的 base、Strength、Weak 步骤拆成 `X + Y -> Z`；详情标记 `AlreadyIncluded / Unknown` |
| 当前 Block | 展示 `localCreature.Block` 为 Native final current Block | 把汇总 Block 声称为具体三张牌或再次应用 Shadowmeld |
| future Block | 已有 source/order 的 Feel No Pain event；已明确取得 base/multiplier 时可展示 Shadowmeld 乘法 | 缺少 grant provenance、eligibility 或 order 时猜测来源/倍数 |
| Poison 生存预览 | 记录敌人因正式 survival preview 被保留或跳过 | 把预测死亡等同于原生已死亡；两者必须使用不同 reason |

示例仅表示格式，不替代运行时记录：

```text
-18 = 15 blockable HP loss + 3 direct HP loss
15 blockable = chronological applied events after current/future Block

12 = Native effective attack 4 × 3
Strength / Weak breakdown = Unknown (already included in Native effective value)
```

### Same-snapshot and lifecycle flow

DT-1 必须实现以下单向数据流：

```text
Debug OFF in personal build
  -> stable Debug button only; no recorder/list/string work per refresh

click Debug while Live
  -> enable recorder
  -> request one ordinary registered-bar refresh
  -> BeginCapture once
  -> record the exact expected and incoming paths already used by that refresh
  -> seal trace
  -> attach CaptureId to that debug-build ForecastHudSnapshot
  -> lifecycle moves the same snapshot/CaptureId through live or committed state
  -> projection selects actual visible values
  -> panel binds only to the displayed CaptureId
```

- `ForecastHudSnapshot` 可在 `DAMAGE_FORECAST_DEBUG_TRACE` 下增加一个 debug-only
  `CaptureId`；默认正式构建中该字段必须不存在。这样 committed/frozen snapshot 自然携带
  同一 token，不需要 Debug 复制一套 lifecycle state。
- 如果用户在 `Frozen` snapshot 形成后才开启 Debug，而该 snapshot 没有有效 CaptureId，
  面板必须显示 `TraceNotCapturedForSnapshot / 此快照未记录`。不得重算当前游戏状态并把
  新 trace 绑定到旧冻结数字；从下一次 live snapshot 才开始提供过程。
- owner 不匹配、generation 过期或永久失效时，面板清空并显示相应 reason；临时覆盖界面
  可保留同一 committed trace，但不得生成新 capture。
- 战斗结束、owner 切换和永久 invalidation 必须同时清理 trace store；不发送网络消息，
  不把 Debug 状态同步给队友。

### Minimum panel behavior

- `调试 Debug` 按钮只存在于个人 Debug build；不开快捷键、不增加 BaseLib 永久设置。
- 一个 panel 完成计算页与详细页：顶部 selector 只列当前 projection 实际显示的
  `-N / N / 🛡 / ♥`；未显示的值不出现在默认 selector。
- 计算页只显示 applied formula；详情页显示 applied/skipped/unknown、source level、
  order/lane/granularity、owner/capture/generation/refresh reason。
- 中文主标签、英文辅助标签；固定操作只有选择数值、详情/返回、复制、关闭。
- 再次点击按钮或 `×` 关闭并停止后续 capture；`Esc` 只关闭 panel。无 trace 的冻结值
  仍可打开 panel，但只显示不可追溯原因。
- panel 作为独立 debug-only control 管理，不修改 `DamageForecastHudRoot` 的正式文本节点，
  不参与 frozen visual-root copy，也不改变原 HUD layout contract。

### Performance and packaging budget

- ordinary/default build：`Compile Remove="Diagnostics\DebugTrace\**\*.cs"`，并验证 DLL
  中 DebugTrace 类型、按钮节点名与中英文诊断文案均为零；不允许仅靠运行时 `if`。
- personal Debug build、trace off：只保留一个稳定按钮节点和一次廉价 enabled 判断；每次
  refresh 不创建 capture、step list、formula string 或 panel subtree。
- trace on：每次实际 HUD calculation 最多创建一个 capture；不重复 Reader/evaluation；
  最多保留 latest live 与 committed/frozen 两个 capture。
- 单 capture 最多 256 steps、复制文本最多 64 KiB；达到上限时停止追加并记录
  `UnsupportedGranularity`/truncated 状态，不能静默丢失后仍声称完整。
- formatter 仅在 panel 打开且 CaptureId/选择值变化时生成文本；不按 frame 重建字符串。
- V0 不写持续日志、不上传遥测、不保存 trace 到磁盘。个人 DLL/JSON/SHA256 仍只输出到
  `work/debug-trace/damage-forecast/`，不得进入正式 Workshop staging。

### Required contract matrix for implementation

DT-1 至少需要纯 contracts 证明：

1. multi-hit 可验证时逐 hit；aggregate 时 granularity 明确且不伪造；
2. current Block 是 native final，只影响 blockable lane；
3. future Block 只保护 native order 之后的 blockable event，direct HP loss 绕过 Block；
4. Strength/Weak 只显示 Native final，未知分解不进入公式；
5. displayed amount 与 sealed trace formula 相等，Unknown 不被当作零；
6. live token 随 committed/frozen snapshot 保留；旧 frozen snapshot 无 token 时拒绝重算绑定；
7. owner/generation mismatch 返回 `OwnerMismatch`/`StaleGeneration` 并清理陈旧 panel；
8. formatter 中英文标签、复制文本和 256-step/64-KiB 上限稳定；
9. default build compile-excludes全部 Debug source/type/string，Debug build off 不产生 per-refresh trace allocations。

### Minimum implementation impact and volatile ownership

DT-1 预计只允许以下最小影响面；执行前仍须重新核对当前 writer 与 exact diff：

```text
New:
  src/DamageForecast/Diagnostics/DebugTrace/DebugTraceData.cs
  src/DamageForecast/Diagnostics/DebugTrace/DebugTraceFormatter.cs
  src/DamageForecast/Diagnostics/DebugTrace/DebugTracePanel.cs
  tests/DamageForecast.ContractTests/DebugTraceContractCases.cs

Conditional, exact hunks only:
  src/DamageForecast/DamageForecast.csproj
  src/DamageForecast/Forecast/ForecastHudSnapshot.cs
  src/DamageForecast/Forecast/LocalDamageForecast.cs
  src/DamageForecast/Combat/LocalIncomingDamageReader.cs
  src/DamageForecast/Patches/ForecastRefreshPatch.cs
  src/DamageForecast/UI/DamageForecastHudSnapshotStore.cs
  tests/DamageForecast.ContractTests/DamageForecast.ContractTests.csproj
  tests/DamageForecast.ContractTests/Program.cs
  this task card
```

- `DamageForecast.csproj` 与 test `Program.cs` 含 Timeline/其他并行 hunks；Reader 含
  Timeline/SM 变化；RefreshPatch/SnapshotStore 含 MP diagnostics 变化。DT-1 不得整文件
  stage、重写或清理这些内容。
- 设计避免修改 `HudSnapshotLifecyclePolicy.cs` 和 `DamageForecastHudRoot.cs`：debug-only
  CaptureId 随现有 snapshot 流转，panel 独立存在。
- 如果执行时无法把 Debug hunks 与现有并行变化精确分离，DT-1 必须停止，不得用 reset、
  checkout、整文件覆盖或复制旧 HEAD 消除冲突。

## Gate DT-1 — Compile-isolated Personal Debug Trace V0

状态：`Complete / StaticVerified / Built / RuntimeNotVerified`。

Goal：实现上述最小三文件 Debug Trace、同 capture 记录、单 panel/button 与纯 contracts，
生成 default-off ordinary build 和 explicit-on personal Debug build；不安装游戏文件。

Allowed：上述 exact files/hunks；targeted + full contract harness；stable/beta/current
guardrails；默认与 Debug 两种构建；default DLL exclusion scan；个人产物写入
`work/debug-trace/damage-forecast/` 并生成 SHA256。

Stop：完成静态/自动验证和两种本地产物区分后立即停止；不得安装、启动游戏、
stage/commit/push/tag、发布或 Workshop 上传。

## DT-1 Compile-isolated Personal Debug Trace V0 Record — 2026-08-01

Result: `Complete / StaticVerified / Built / RuntimeNotVerified / Stop Before Install`。

### Implemented boundary

- `DamageForecastDebugTrace` 默认 `false`；默认构建使用
  `Compile Remove="Diagnostics\DebugTrace\**\*.cs"`，个人构建显式设为 `true`。
- 新增 `DebugTraceData.cs`、`DebugTraceFormatter.cs`、`DebugTracePanel.cs`，没有建立第二个
  Mod、第二套 forecast algorithm、持续日志、遥测或磁盘 trace。
- Debug capture 只包围同一次正式 refresh 已有的 expected 与 incoming Reader 调用；
  sealed `CaptureId` 仅在 Debug build 中附着到 `ForecastHudSnapshot`，并随现有
  live/committed/frozen lifecycle 流转。
- UI 只有 `调试 Debug` 按钮和一个惰性创建 panel；面板提供数值选择、计算/详情、复制、
  关闭与 Esc。trace off 不创建 capture、step list、formula string 或 panel subtree。
- frozen snapshot 无 token 时显示 `TraceNotCapturedForSnapshot / 此快照未记录`，不重算并
  冒充旧数字的过程；永久失效和战斗结束清理本地 trace，临时覆盖保留 committed trace。
- 单 capture 最多 256 steps，只保留 latest/committed 两个 capture；复制文本最多 64 KiB。

### Automated evidence

- full contract harness：`SUMMARY discovered=534 passed=534 failed=0 skipped=0`；
  `DT-001` 至 `DT-011` 全部通过。
- stable + beta guardrails：`QUALITY_GATE targets=2 status=PASS exit_code=0`。
- current `v0.110.0 / eecc8c4d` guardrail：
  `QUALITY_GATE targets=1 status=PASS exit_code=0`，dependency closure 与 generated deps 通过。
- default Release build：成功，`0 warnings / 0 errors`；personal Debug Release build：成功，
  `0 warnings / 0 errors`。
- default DLL 扫描中 `DebugTrace`、`DamageForecastDebugTracePanel`、`Calculation Debug`、
  `调试 Debug`、`计算调试` 均为 `found=False`；personal Debug DLL 中均为 `found=True`。
- guardrails 同时通过 `git diff --check`、forbidden artifact review，并确认 ordinary artifact
  的 Timeline shadow runtime owner matches 为 0。

### Superseded DT-1 artifact identity

```text
Pre-BLP-4I Personal Debug DLL, no longer present at the current output path
  SHA256 B73DAAAE02071450C7DBCD5F5D004101B61894EC752D16795BB1525488FAEFB5

Personal JSON
  work/debug-trace/damage-forecast/damage-forecast.json
  SHA256 FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB

Pre-BLP-4I default verification DLL, superseded
  SHA256 779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50
```

上述两个 DLL 在并行 BLP-4I 完成后被判定为过时；旧 personal Debug DLL 含 SM-1F 与
Debug Trace，但缺少 BLP-4I 三个动态箭头安全区关键 symbols，不得用于 DT-2 安装。

未安装到游戏目录，未启动游戏，未执行人工 HUD 验证；因此 UI 可见性、点击、滚动、复制、
Esc、冻结/覆盖层实际表现均保持 `RuntimeNotVerified`。安装与用户手动验证属于下一道独立
Gate；未 stage、commit、push、tag、发布或操作 Workshop。

## DT-1I BLP-4I + SM-1F + Debug Trace Merged Artifact Refresh — 2026-08-01

Result: `Complete / IntegratedSourceVerified / Built / RuntimeNotVerified / Stop Before Install`。

- Parallel-work audit：旧 personal Debug DLL `B73DAAAE...` 的生成时间早于 BLP-4I
  production source，且缺少 `ClosedDropdownArrowSafeInset`、
  `ShouldFitEnglishClosedDropdownFont`、`ResolveSafeClosedDropdownFontSize`；因此没有沿用。
- Current source matrix：当前共享源码与 test registration 同时包含 BLP-4I、SM-1F/Shadowmeld
  与 `DT-001–DT-011`；没有 reset、checkout、整文件回退或删除其他任务 hunk。
- Full contracts：`SUMMARY discovered=535 passed=535 failed=0 skipped=0`，其中
  `BLP4I-001–004`、`SM-019–025` 与 `DT-001–011` 在同一轮通过。
- stable + beta：`QUALITY_GATE targets=2 status=PASS exit_code=0`；current
  `v0.110.0 / eecc8c4d`：`QUALITY_GATE targets=1 status=PASS exit_code=0`。
- 两种 Release rebuild 均为 `0 warnings / 0 errors`。刷新后的 default DLL SHA256 与当前
  已安装并由用户验证过的 BLP-4I + SM-1F DLL `42FAFBE...` 完全一致。
- default DLL 同时含 BLP-4I 与 SM-1F symbols，且 Debug 类型、节点与双语文案均不存在；
  personal Debug DLL 同时含 BLP-4I、SM-1F 与 Debug Trace 三组 symbols。
- `git diff --check`、forbidden artifact review、ordinary Timeline shadow runtime-owner scan
  全部通过。

### Current installable personal Debug artifact identity

```text
Personal Debug DLL
  work/debug-trace/damage-forecast/damage-forecast.dll
  SHA256 5D521AE77C260F9E363B57B5A71E061427BDCF3792EB31C1B8C7188D01698592

Personal JSON
  work/debug-trace/damage-forecast/damage-forecast.json
  SHA256 FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB

Default verification DLL, not for personal Debug install
  work/debug-trace/verification/default/damage-forecast.dll
  SHA256 42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF
```

当前游戏活动 DLL 仍为 `42FAFBE...`，DT-1I 没有修改游戏目录、没有启动游戏，也没有
stage、commit、push、tag、发布或操作 Workshop。Debug UI 仍为 `RuntimeNotVerified`。

## Gate DT-2 — Local Personal Debug Install and Manual Validation

状态：`Installed / User Runtime Verification Pending`。

Goal：仅把 DT-1I 当前 hash 绑定的 personal Debug DLL/JSON 安装到本地 Mod 目录，然后停止并由
用户亲自启动游戏验证；Codex 不代为启动游戏，也不发布 Workshop。

## DT-2 Local Personal Debug Install Record — 2026-08-01

Result: `Installed / HashVerified / RuntimeNotVerified / Waiting for User`。

- Install staging：`work/debug-trace/install-staging/dt1i-5d521ae7`，严格只有
  `damage-forecast.dll` 与 `damage-forecast.json`。
- Reviewed Plan：action=`target-upgrade`、gameRunning=`false`、target=`1`、legacy=`0`、
  orphan=`0`；staging DLL `5D521AE...`，active DLL `42FAFBE...`。
- Transaction：`DT2-20260801-debugtrace-5d521ae7`。安装脚本以 staging/active 四个完整
  SHA256 锁定，只执行一次事务替换。
- Active：`C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`
  严格两文件；DLL SHA256
  `5D521AE77C260F9E363B57B5A71E061427BDCF3792EB31C1B8C7188D01698592`，JSON SHA256
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Active symbol audit：BLP-4I `ClosedDropdownArrowSafeInset` / dynamic font fit、SM-1F
  `ForecastActionRefreshPolicy` / `VerifiedShadowmeldFutureBlockModifier`、Debug Trace panel/runtime
  与 `调试 Debug` 文案全部存在。
- Recovery：上一活动版完整备份到 Loader root 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\DT2-20260801-debugtrace-5d521ae7-damage-forecast-v0.3.0`；
  备份 DLL SHA256 `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`。
  ledger 为同目录根下 `DT2-20260801-debugtrace-5d521ae7-install-ledger.json`。
- Post-install Plan：action=`target-already-current`、gameRunning=`false`、target=`1`、
  legacy=`0`、orphan=`0`。

Codex 未启动游戏、未修改用户配置，未执行 stage/commit/push/tag、发布或 Workshop。
Debug UI 与合并功能在此新 DLL 上仍为 `RuntimeNotVerified`，等待用户亲自测试。

## DT-3 Production Publish Debug-Leakage Guard — 2026-08-01

Result: `Complete / AutomatedGuardVerified / No Upload Performed`。

- `Build-DualTargets.ps1` 在 restore/build/publish 三步显式传入
  `DamageForecastDebugTrace=false`，不再只依赖项目默认值。
- `Test-IdentityPublishTrees.ps1` 只接受位于仓库 `work/publish/` 下的 stable/beta tree；
  `work/debug-trace/`、本地游戏目录或其他任意目录不能伪装成正式发布来源。
- 正式 DLL 扫描固定拒绝 `DebugTrace`、`DamageForecastDebugTracePanel`、
  `Calculation Debug`、`调试 Debug`、`计算调试`；命中任一标记即失败。
- identity contract 已冻结 approved publish root 与 forbidden marker list；验证器仍为只读，
  仍要求每树严格两文件、合法 identity/manifest、SHA256 对比以及发布单独授权。
- 新增 `PT-006` 证明 Debug-enabled DLL 被拒绝，`PT-007` 证明 publish root 外来源被拒绝；
  完整 harness 为 `SUMMARY discovered=537 passed=537 failed=0 skipped=0`。
- 实际运行 `Build-DualTargets.ps1`：stable/beta build 均为 `0 warnings / 0 errors`，两树
  DLL SHA256 均为 `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`，
  `debugTraceMarkerMatches=0`，两树哈希一致。
- 负向实测：把 `work/debug-trace/install-staging/dt1i-5d521ae7` 作为 publish tree 时，
  验证器以 `must remain under approved publish root` 拒绝，exit code=`1`。

本 Gate 没有修改当前游戏安装，活动个人 Debug DLL 仍为 `5D521AE...`；没有启动游戏、
stage/commit/push/tag、上传 Workshop 或执行其他发布操作。

## DT-2A Viewport Entry and Draggable Panel Build and Test Record — 2026-08-01

Result: `SourceComplete / ContractsPassed / Built / NotInstalled / RuntimeNotVerified`。

- `DebugTracePanel` 入口位置改为按 viewport 可用边界计算，固定在战斗画面右上角、
  版本/HASH 信息下方，不再按本地角色血条宽度定位。
- 弹窗标题栏接收左键拖动；拖动位置换算回 viewport 坐标，并在每次重新绑定时保持当前
  战斗内位置。窗口四边被限制在 viewport 内，不能拖出屏幕。
- 面板打开时的关闭键由 `Esc` 改为 `Q`；同时兼容逻辑键位与物理键位，且源码不再捕获
  `Key.Escape`。关闭按钮与再次点击入口仍然保留。
- 新增 `DT-012`、`DT-013` contract，分别冻结 viewport 定位/标题拖动/边界限制和
  `Q` 关闭/不捕获 Esc 的约束。完整 harness 为
  `SUMMARY discovered=539 passed=539 failed=0 skipped=0`；同轮 `BLP4I-001–004`、
  `SM-019–025` 与 `DT-001–013` 全部通过。
- personal Debug Release build 为 `0 warnings / 0 errors`；刷新后的
  `work/debug-trace/damage-forecast/damage-forecast.dll` SHA256 为
  `9561BB713BDB2C6D971EC58C8F0D25E330057750AB543B2850D5047AA93528E2`，JSON SHA256 为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。DLL 同时命中
  Debug Trace、BLP-4I dynamic font fit、SM-1F Shadowmeld 与 action refresh markers。
- explicit Debug-off Release build 为 `0 warnings / 0 errors`，DLL SHA256 仍为
  `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`；所有 Debug Trace
  类型、节点与中英文 UI markers 均不存在，而 BLP-4I 与 SM-1F markers 仍存在。
- 当前游戏目录仍是此前安装的 DT-1I DLL `5D521AE...`，不含 DT-2A；本 Gate 没有安装、
  启动游戏、stage/commit/push/tag 或发布 Workshop。

## DT-2A Local Personal Debug Install Record — 2026-08-01

Result: `Installed / HashVerified / RuntimeNotVerified / Waiting for User`。

- Install staging：`work/debug-trace/install-staging/dt2a-9561bb71`，严格只有
  `damage-forecast.dll` 与 `damage-forecast.json`。首次直接调用脚本被本机 PowerShell
  execution policy 拒绝，未发生变更；随后按仓库惯例以 `-ExecutionPolicy Bypass` 执行。
- Reviewed Plan：transaction=`DT2A-20260801-debugtrace-9561bb71`、action=`target-upgrade`、
  gameRunning=`false`、target=`1`、legacy=`0`、orphan=`0`；staging DLL `9561BB71...`，
  active DLL `5D521AE...`，两侧 JSON 均为 `FF8D4E07...`。
- Active：`C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`
  严格两文件；DLL SHA256
  `9561BB713BDB2C6D971EC58C8F0D25E330057750AB543B2850D5047AA93528E2`，JSON SHA256
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Active marker audit：DT-2A Debug UI/双语文案、BLP-4I dynamic font fit、SM-1F
  Shadowmeld 与 action refresh markers 全部存在。
- Recovery：上一活动 personal Debug 版完整备份到 Loader root 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\DT2A-20260801-debugtrace-9561bb71-damage-forecast-v0.3.0`；
  备份 DLL SHA256 `5D521AE77C260F9E363B57B5A71E061427BDCF3792EB31C1B8C7188D01698592`。
  ledger 为同目录根下 `DT2A-20260801-debugtrace-9561bb71-install-ledger.json`。
- Post-install Plan：action=`target-already-current`、gameRunning=`false`、target=`1`、
  legacy=`0`、orphan=`0`。Codex 未启动游戏、未执行 stage/commit/push/tag 或 Workshop。

## DT-2B Plus-12 Entry Offset Source Record — 2026-08-01

Result: `SourceComplete / NotBuilt / NotInstalled`。

- 用户确认 DT-2A 整体效果满意，只要求右上角 `调试 Debug` 入口再向下移动 12 个界面单位。
- `ButtonTopInset` 从 `160f` 调整为 `172f`；弹窗未被手动拖动时，其初始位置仍跟随入口
  一同向下移动，标题栏拖动、viewport 边界限制、`Q` 关闭与 Esc 不捕获逻辑均不变。
- `DT-012` contract 现在固定检查 `ButtonTopInset = 172f`。本 Gate 没有运行 contract、构建、
  安装、启动游戏、stage/commit/push/tag 或 Workshop；当前活动 DLL 仍是 DT-2A `9561BB71...`。

## DT-2B Build, Test and Local Install Record — 2026-08-01

Result: `ContractsPassed / Built / Installed / HashVerified / RuntimeNotVerified / Waiting for User`。

- Full contracts：`SUMMARY discovered=539 passed=539 failed=0 skipped=0`；`DT-001–013`、
  `BLP4I-001–004` 与 `SM-019–025` 同轮通过。
- personal Debug 与 explicit Debug-off Release build 均为 `0 warnings / 0 errors`。个人 Debug
  DLL SHA256 为 `141FD05C9F78AB4C99E618B3C20CD90B3AE9FC9F198B78BFB14A0BE65E017A20`；
  默认关闭 DLL 仍为 `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`，
  不含 Debug 类型、节点或双语 UI markers，同时保留 BLP-4I 与 SM-1F markers。
- personal artifact 已刷新到 `work/debug-trace/damage-forecast/`；checksum 与实际 DLL/JSON
  一致。Install staging 为 `work/debug-trace/install-staging/dt2b-141fd05c`，严格两文件。
- Reviewed Plan：transaction=`DT2B-20260801-debugtrace-141fd05c`、action=`target-upgrade`、
  gameRunning=`false`、target=`1`、legacy=`0`、orphan=`0`；staging DLL `141FD05C...`，
  active DLL `9561BB71...`，两侧 JSON 均为 `FF8D4E07...`。
- Active：`C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`
  严格两文件；DLL SHA256
  `141FD05C9F78AB4C99E618B3C20CD90B3AE9FC9F198B78BFB14A0BE65E017A20`，JSON SHA256
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。Debug Trace、
  BLP-4I 与 SM-1F/action refresh markers 全部存在。
- Recovery：DT-2A 完整备份位于 Loader root 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\DT2B-20260801-debugtrace-141fd05c-damage-forecast-v0.3.0`；
  备份 DLL SHA256 为 `9561BB713BDB2C6D971EC58C8F0D25E330057750AB543B2850D5047AA93528E2`，
  ledger 为同根下 `DT2B-20260801-debugtrace-141fd05c-install-ledger.json`。
- Post-install Plan：action=`target-already-current`、gameRunning=`false`、target=`1`、
  legacy=`0`、orphan=`0`。Codex 未启动游戏、未执行 stage/commit/push/tag 或 Workshop。

## DT-2C Current-Baseline Rebuild and Local Install Record — 2026-08-11

Result: `SourceComplete / ContractsPassed / IsolatedMergedBuild / Installed / HashVerified /
RuntimeNotVerified / Waiting for User`。

### Player-facing change

- 右上角入口的 `ButtonTopInset` 从 `172f` 调整为 `220f`，用于避开当前游戏版本/HASH
  信息区；仍按 viewport 可用边界定位，弹窗仍可拖动并限制在屏幕内。
- 默认页改为中文简易说明，固定展示“数值、计算、说明、结论”，不显示
  `Capture / Owner / Refresh / generation` 等开发字段。
- 技术信息集中在 `开发者详情 Details`；`复制完整诊断 Copy` 同时复制中文简易说明、
  完整步骤和当前 snapshot binding，便于直接发送给 Codex。
- `Q` 关闭、Esc 不捕获、每次会话默认关闭和正式构建剔除 Debug UI 的边界保持不变。

### 当前最新基线重新构建

- 本次没有复用旧 DT-2B DLL。以本机当前正式源码 `HEAD 0cdf830`、当前活动 MP-1R DLL
  `1FAF67CA74F935CB1714781EB044FAF221038618AF459B224733E871226A0831`、manifest
  `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D` 为锁定输入。
- 本机游戏引用仍为 `v0.110.1 / db5d3552` 对应的 `sts2.dll` SHA256
  `7C446EFABF80614C429B5088E87101423AA5BB4C04FC3E73393261F6E6D404FD`。本次未能联网
  确认远端更新，因此准确表述为“本机当前最新基线”，不声称线上最新。
- 隔离候选位于
  `work/debug-trace/current-baseline/0cdf830-1faf67ca/dt2c-source`：从 MP-1R 隔离源重新
  生成，只叠加 Debug Trace 接入和 DT-2C 文案/UI；未带入共享工作树中的 Timeline 候选。
- 主工作树完整 harness 为 `SUMMARY discovered=552 passed=552 failed=0 skipped=0`。隔离
  harness 中 `DT-001–015` 与 `MPD-001–007` 全部通过；全隔离 harness 为 `455/459`，
  仅 `PT-001–004` 因嵌套候选沿父仓库解析 publish root 而失配，不涉及 Mod 编译或
  Debug/MP 行为。
- `DamageForecastDebugTrace=true + DamageForecastMultiplayerLifecycleAuto=true` 合并 Release
  build 为 `0 warnings / 0 errors`，DLL SHA256
  `76EAD663D0E416947B3706543E87F3A78B94A0E198A12782472149C3EA5465FD`。
- `DamageForecastDebugTrace=false + DamageForecastMultiplayerLifecycleAuto=true` 对照 Release
  build 为 `0 warnings / 0 errors`，DLL SHA256
  `429A6685D8B9608AF5DD8D4F93E553CB25CE582DA96953A4A2A558926E96D4DF`。对照版不含
  Debug 类型/中文面板文案，但保留 `auto-multiplayer`、`MP-LIFECYCLE`；两版均不含
  `IncomingDamageTimelineShadow` / `ForecastTimelineShadow`。

### Hash-locked local install

- 严格两文件 staging：
  `work/debug-trace/install-staging/dt2c-current-0cdf830-76ead663`；DLL 为 `76EAD663...`，
  JSON 为 `B1BEA532...`。
- Reviewed Plan：transaction=`DT2C-20260811-current-baseline-76ead663`、
  action=`target-upgrade`、gameRunning=`false`、target=`1`、legacy=`0`、
  orphan=`0`；活动旧 DLL=`1FAF67CA...`，候选 DLL=`76EAD663...`。
- Execute 同时锁定 staging/active 的四个完整 SHA256；安装后活动目录严格只有
  `damage-forecast.dll` 与 `damage-forecast.json`，哈希分别为 `76EAD663...` 和
  `B1BEA532...`。
- 活动二进制复核检出 `DebugTracePanel`、`预测计算说明`、`复制完整诊断`、
  `开发者详情 Details`、`auto-multiplayer`、`MP-LIFECYCLE` 和诊断环境变量 marker；
  未检出 Timeline 候选 marker。
- 旧 MP-1R 严格两文件备份位于 Loader root 外：
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\DT2C-20260811-current-baseline-76ead663-damage-forecast-v0.3.0`；
  ledger 为同根下
  `DT2C-20260811-current-baseline-76ead663-install-ledger.json`。
- Post-install Plan：action=`target-already-current`、gameRunning=`false`、
  target=`1`、legacy=`0`、orphan=`0`。

Codex 未启动或结束游戏进程，未修改配置，未执行 stage/commit/push/tag、发布或 Workshop。
DT-2C 和保留的 MP-1R 在新合并 DLL 上均为 `RuntimeNotVerified`，等待用户亲自测试。

## DT-2C User Runtime Confirmation and Closure — 2026-08-11

- 用户在安装活动 DLL `76EAD663...` 后亲自进入游戏完成 DT-2C 人工验证，并反馈
  “成功了 收口吧”。
- 本次确认将 DT-2C 的游戏内入口位置、面板打开与拖动、中文简易说明、开发者详情、
  完整诊断复制及 `Q` 关闭视为 `RuntimeVerified`；未报告 HASH 遮挡或可读性问题。
- 该反馈只关闭 DT-2C Debug Trace UI；不替代 MP-1R 所需的普通多人非房主验证，
  MP-1R 继续按其独立任务卡保持 evidence pending。
- 未重新构建或安装文件，未启动/结束游戏进程，未修改配置，也未执行 Git、发布或
  Workshop 操作。

Final closure:

- Result: DT-2C 当前基线个人 Debug 已实现、验证并完成运行时收口。
- Current state: 活动 DLL `76EAD663...`；DT-2C `RuntimeVerified`；MP-1R 仍为独立验证待办。
- Authority: 本任务卡；当前最新基线重新构建规则与 DT-2C 构建、安装、运行证据均已同步。
- Repository: `Work Complete / Checkpoint Pending`；Git checkpoint 未获单独授权。

## Completion and closure requirements

- 纯 contract 验证 trace 与正式结果使用同一输入、顺序、舍入和 snapshot identity。
- 结束回合冻结、覆盖层和 owner/generation 切换时，数值与详情不得跨 snapshot 混用。
- Debug 关闭路径具有明确的低开销或零持续分配证据。
- 更新游戏文件后立即停止，提供人工测试步骤、预期结果和反馈项，等待用户亲自测试。
- 最终按当前收口标准同步必要 authority，并形成获批的可追溯 repository checkpoint。

读取 `docs/task-notes/forecast-debug-calculation-trace-task-card.md`，核对
`Current Control`，只执行已批准的下一 Gate，完成后停止。
