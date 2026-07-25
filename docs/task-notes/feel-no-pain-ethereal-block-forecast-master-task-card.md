# Damage Forecast — 无惧疼痛 × 虚无回合末护盾预测主任务卡

日期：2026-07-25

任务类型：新机制支持、回合末事件顺序、双输出管线路由、回归测试与 matching-artifact 运行验证

## Current Control

State: `Work Complete / Checkpoint Pending`
Last completed: `FP5 — authority synchronized and task-owned staged diff reviewed`
Next: `Separate approval for local Git checkpoint`
Approved: `FP5 closure review complete; commit is not approved`
Evidence: `§13–§14`
Repository: `Task-owned staged diff ready / No Checkpoint`

---

## 0. 下一 Session 执行指令

先完整阅读：

1. 本任务卡；
2. `docs/task-notes/task-closure-standard.md`；
3. `docs/mechanics-evidence.md` 当前 mechanism ledger 和回合末顺序证据；
4. `docs/task-notes/phase-13a-damage-display.md` 的 `N` / `-N` 定义；
5. `src/DamageForecast/Combat/LocalIncomingDamageReader.cs`；
6. `src/DamageForecast/Combat/VerifiedPreAttackBlockReader.cs`；
7. `src/DamageForecast/Combat/IncomingDamageDisplayOptions.cs`；
8. `src/DamageForecast/Combat/HpLossEventPolicy.cs`；
9. `src/DamageForecast/Forecast/LocalDamageForecast.cs`；
10. `tests/DamageForecast.ContractTests/BlockPolicyContractCases.cs` 及现有
    hand event、projection、lifecycle contracts。

首次进入只执行 FP0 只读诊断。不要因为本任务卡存在就自动修改代码、
运行构建、安装 Mod、启动游戏、stage、commit、push、tag 或更新 Workshop。

FP0 完成后必须报告：

- “无惧疼痛”的 stable/beta concrete Power type、数值来源与叠加方式；
- 虚无卡牌在回合结束时的真实 exhaust 顺序；
- 普通 Ethereal 与同时带 `HasTurnEndInHandEffect` 卡牌的顺序差异；
- 该护盾能保护哪些后续 blockable event，不能保护哪些先前/direct event；
- `-N`、可选正数 `N`、`🛡` 明细三者的精确出口；
- 最小安全实现是 aggregate future Block 还是 ordered Block event；
- FP1/FP2 精确候选文件和下一 Gate 批准语句。

---

## 1. 一句话目标

当本机玩家拥有“无惧疼痛”能力，且当前手牌中的虚无（Ethereal）卡会在
本回合结束时确定性消耗时，Damage Forecast 应按原生事件顺序预测由这些
exhaust 触发的未来护盾：默认 `-N` 始终纳入可信护盾；正数 `N` 只有在用户
明确开启“计入能力护盾”后才纳入，绝不能把该来源接到错误显示出口或遗物开关。

---

## 2. 用户需求的精确解释

### 2.1 机制范围

本任务只预测：

```text
当前手牌中的卡
  -> 在点击结束回合后的原生流程中因 Ethereal 确定性 exhaust
  -> 触发当前“无惧疼痛”Power
  -> 在后续敌方/回合末 blockable damage 前后按真实顺序获得 Block
```

不在本任务中预测：

- 用户之后可能主动打出或主动消耗的卡；
- 随机生成、随机消耗或目标选择；
- 任意 Mod Power / Mod card 的泛化规则；
- 不是由本轮 Ethereal exhaust 触发的未来 Block；
- 通过执行真实 `Exhaust`、Power hook 或 command queue 来探测结果。

玩家在当前回合中已经实际 exhaust 卡牌而获得的护盾，属于当前 Block，
应继续由现有 current-Block 路径读取，不能在本任务中再次预测并双计。

### 2.2 “类似王冠”的正确含义

可复用的是现有实现的设计原则：

- 只读取已验证的官方类型/能力表面；
- 预测未来敌方伤害前会存在的 Block；
- stable/beta 机制不同或证据不足时保守处理；
- 不执行真实 gameplay effect。

但“无惧疼痛”不能直接当作王冠：

- 它是 **Power 触发的 Block**，不是 Relic Block；
- 它由逐张卡牌 exhaust 事件触发，不一定在所有伤害事件之前一次性产生；
- legacy Diamond Diadem 的敌方伤害修正逻辑与本任务无关；
- 不能修改 `VerifiedEnemyDamageModifier` 或
  `LegacyDiamondDiademDamageForecast` 来接入本机制。

---

## 3. `-N`、正数 `N` 与高级明细的硬性出口合同

当前产品语义：

```text
-N = 默认的预计实际掉血
 N = 可选的正数来袭伤害，按用户选中的防御/减伤类别前向计算
```

“高级设置”必须拆成不同职责，不能混用：

- `DamageDisplayMode`：决定显示 `-N`、`N` 或两者；
- `IncludePowerBlockInIncomingDamage`：决定正数 `N` 是否计入 Power Block；
- `ShowAdvancedShieldHeartDetails`：只决定是否显示 `🛡` / `♥` 明细；
- `IncludeRelicBlockInIncomingDamage`：只控制 Relic Block；
- `IncludeCurrentBlockInIncomingDamage`：只控制已有 Current Block。

### 3.1 必须实现的真值表

| 显示/计算设置 | `-N` | 正数 `N` | 无惧疼痛护盾归属 |
| --- | --- | --- | --- |
| 默认：`ExpectedHpLossOnly`，Power Block 开关关闭 | 必须计入 | 不显示 | 只影响 `-N` |
| `Both`，Power Block 开关关闭 | 必须计入 | 必须忽略 | 不能因为显示了 `N` 就自动计入 |
| `IncomingDamageOnly`，Power Block 开关关闭 | `-N` 不显示但底层语义不变 | 必须忽略 | 不能接入 `N` |
| `Both`，Power Block 开关开启 | 必须计入 | 必须计入 | `-N` 与 `N` 各走自己的合法出口 |
| `IncomingDamageOnly`，Power Block 开关开启 | `-N` 不显示但底层语义不变 | 必须计入 | 只显示经过 Power Block 的 `N` |

### 3.2 Unknown / unsupported 路由

若玩家没有“无惧疼痛”，该来源贡献为 0。

若玩家拥有该 Power，但当前 stable/beta 类型、Block 数值或事件顺序无法可信读取：

- `-N`：不得静默漏算；应按既有保守策略进入 Unknown/隐藏边界；
- `N` 且 `IncludePowerBlockInIncomingDamage=true`：不得显示部分可信值，
  应进入 `N` 的 Unknown/隐藏边界；
- `N` 且 `IncludePowerBlockInIncomingDamage=false`：用户明确未选择 Power Block，
  不应读取或依赖此来源，也不应因为该来源 unsupported 而隐藏本来可计算的 `N`。

### 3.3 明细显示

`ShowAdvancedShieldHeartDetails=false` 只隐藏 `🛡` / `♥` 文本，不得改变
`-N` 或 `N` 的数值计算。

当明细开启时：

- 由无惧疼痛吸收的 blockable damage 应进入既有 `🛡` 语义；
- 不得创建第四种数字出口；
- 不得把能力护盾写成 direct HP-loss、Relic Block 或 Current Block；
- direct HP-loss 事件不受这份 Block 影响。

---

## 4. 当前静态基线与根因候选

### 4.1 当前 `-N` 路径

当前 expected-loss 路径会读取：

```text
current Block
+ VerifiedPreAttackBlockReader.Block
```

`VerifiedPreAttackBlockReader` 当前 Power Block 只包含已登记的 Frost 与
PlatingPower；没有“无惧疼痛 × 回合末 Ethereal exhaust”来源。

因此，缺少 verified reader 是高可信实现缺口，但尚未证明应把它简单加入
aggregate `PowerBlock`。

### 4.2 当前正数 `N` 路径

当前 `N` 只在选项需要时读取 Power/Relic Block，并将
`PreAttackBlockRead.PowerBlock` 与 `RelicBlock` 分开送入选择策略。

这正是本任务必须保持的出口：

- `-N` 使用可信的全部 expected future Block；
- `N` 只在 `IncludePowerBlockInIncomingDamage=true` 时选择该 Power Block；
- 不能让 `IncludeRelicBlockInIncomingDamage` 控制无惧疼痛。

### 4.3 为什么不能立即做“卡数 × 护盾”

回合末不是单一时点。必须先确认：

1. ordinary Ethereal card 何时从手牌 exhaust；
2. 带 `HasTurnEndInHandEffect` 的 Ethereal card 是先执行效果还是先 exhaust；
3. 每次 exhaust 触发 Block 的准确时点；
4. Block 是否能保护同一张卡的 turn-end blockable damage；
5. 多张 turn-end card 与多次 exhaust 的 native order；
6. auto-play、Retain、Eternal、Unplayable 等关键字是否改变实际离手路径；
7. stable/beta 是否使用同一 Power type、BlockVar 和 hook。

如果 Block 在部分已支持伤害事件之后才产生，把总护盾提前一次性加入
`EffectiveBlock` 会过度减伤。FP0 必须先决定是否需要 ordered Block event。

---

## 5. 必须保持的行为不变量

1. 不执行真实卡牌、Power hook、exhaust、command queue、RNG、存档或网络操作。
2. 只读取本机玩家当前状态，不扩展队友/共享预测。
3. 没有“无惧疼痛”时，所有现有预测结果逐位不变。
4. 没有 Ethereal 手牌时，该 Power 不产生虚构 future Block。
5. 已离开手牌或将在该阶段前确定性离手的卡不得计数。
6. 每张实际 Ethereal exhaust 只触发一次，不因多条 reader 重复计数。
7. 多层/升级 Power 必须按已验证原生数值计算，不写死中文卡名或猜测常数。
8. Block 只作用于产生之后的 blockable event；不得倒流保护更早伤害。
9. direct HP-loss lane 永远不消耗、不受益于 Block。
10. Current Block、Power Block、Relic Block 三类在正数 `N` 中保持独立开关。
11. 默认设置下只有 `-N` 受到影响；正数 `N` 不得被暗中改变。
12. `ShowAdvancedShieldHeartDetails` 只控制文本明细，不控制机制计算。
13. unsupported selected path 保守 Unknown；未选择的 Power Block 不污染 `N`。
14. existing Frost、Plating、Orichalcum、FakeOrichalcum、RippleBasin、
    CloakClasp 和 Diamond Diadem 路径不得回归。
15. existing Burn/Toxic/Decay/Infection/Wither、Beckon/Bad Luck/Regret、
    Constrict/Disintegration 的顺序、分类与数值不得回归。
16. stable/beta、18 项配置 schema、技术身份、安装目录和 Workshop 状态不变。
17. 不新增通用 Power 扫描器，不宣称自动支持未来官方或 Mod Power。

---

## 6. 任务范围

### 6.1 包含

- stable/beta 原生“无惧疼痛”类型、数值、叠加和 exhaust hook 的只读确认；
- 当前手牌中确定性 Ethereal exhaust 的识别；
- 回合末 Block 产生与伤害事件的 native order 建模；
- `-N` expected-loss 路径的无条件可信接入；
- 正数 `N` 的 `IncludePowerBlockInIncomingDamage` 条件接入；
- `🛡` 明细与 committed snapshot 一致性；
- game-neutral policy contracts 和必要的最小 game-coupled contracts；
- stable/beta contracts、guardrail、Release build、发布白名单/hash 检查；
- 用户另行批准后的 matching-artifact 安装与 L3 运行矩阵；
- 用户另行批准后的 authority 更新和 Git checkpoint。

### 6.2 不包含

- 所有 exhaust 触发型 Power 的通用框架；
- Mod card / Mod Power 自动适配；
- 主动出牌、主动消耗或未来玩家行动模拟；
- 随机目标、随机弃牌、随机消耗或搜索；
- Forecast Engine AR1–AR8 架构稳定化；
- 把 `N` 从独立前向投影改为由 `-N` 反推；
- 新增或修改 BaseLib 设置、默认值、enum、配置 key；
- HUD 布局、颜色、位置或文案重设计；
- Mod identity、Harmony owner、安装目录或 Workshop 修改；
- push、tag、发布或 Workshop 动作，除非后续单独明确批准。

---

## 7. FP0 根因与机制判定树

按顺序检查，不跳步：

1. **Artifact / version**
   - 当前 supported stable/beta 版本和引用程序集；
   - Power 与卡牌 type 是否双目标存在；
   - 不能用一个目标的 IL 代替另一个目标。

2. **Power identity**
   - concrete FullName；
   - 当前 amount、stacking、升级结果；
   - BlockVar 或其他公开动态数值来源；
   - 多份 Power 是合并 amount 还是多个实例。

3. **Ethereal selection**
   - 当前 `PileType.Hand`；
   - `CardKeyword.Ethereal` 的真实判定；
   - ordinary Ethereal 与 `HasTurnEndInHandEffect` 分支；
   - Retain/Eternal/auto-play/特殊移动对 exhaust 的影响。

4. **Native order**
   - BeforeTurnEnd / Ethereal / TurnEndInHand / flush / enemy action 顺序；
   - 每次 exhaust 与 Power Block command 的相对时点；
   - 现有 hand damage/direct damage/power damage 的相对时点。

5. **Outlet**
   - `ReadForLocalCreature` 的 `-N` EffectiveBlock；
   - `ReadIncomingDamageForLocalCreature` 的正数 `N` options；
   - advanced `🛡` detail；
   - freeze/committed snapshot。

6. **Implementation shape**
   - 若全部 Block 在所有受支持伤害前产生，可使用 verified aggregate；
   - 若 Block 与伤害交错，必须使用 ordered Block event 或等价窄模型；
   - 若某一子场景顺序无法确认，保持该子场景 Unknown，不扩大猜测。

---

## 8. Gate 计划

### FP0 — Read-only Native Mechanics and Outlet Baseline

类型：只读诊断

动作：

- 核对 branch、HEAD、remote、status 和用户未提交文件；
- 读取 stable/beta Power/card/CombatManager 静态元数据或反编译证据；
- 建立 Ethereal selection、Power amount 和 native order 矩阵；
- 画出当前 `-N` 与正数 `N` 的真实调用路径；
- 判断 aggregate reader 是否安全，或必须引入 ordered Block event；
- 输出 FP1 contracts 与 FP2 最小候选 diff。

完成门槛：

- 不修改文件；
- 不 build、不安装、不启动游戏、不执行 Git/Workshop 动作；
- stable/beta 结论分开标记；
- 所有未知明确登记，不用卡牌中文名猜 native type；
- 下一 Gate 需要重新批准。

### FP1 — Contract-first Outlet and Ordering Reproduction

类型：合同/测试边界

优先建立 game-neutral 输入：

```text
Power present / readable
Block per exhaust
Current hand cards
WillEtherealExhaust
HasTurnEndInHandEffect
Native order
Damage events
IncomingDamageDisplayOptions
```

候选合同：

```text
FP-001 NoPower_ContributesZero
FP-002 PowerWithNoEthereal_ContributesZero
FP-003 OneEthereal_OneVerifiedBlockGrant
FP-004 MultipleEthereal_GrantsOncePerCard
FP-005 StackedOrUpgradedPower_UsesVerifiedNativeValue
FP-006 NonEtherealAndAlreadyLeftHand_AreIgnored
FP-007 TurnEndEffectEthereal_UsesEffectThenExhaustOrder
FP-008 BlockProtectsOnlyLaterBlockableEvents
FP-009 DirectHpLoss_DoesNotConsumeOrBenefitFromBlock
FP-010 ExpectedMinusN_AlwaysIncludesVerifiedPowerBlock
FP-011 IncomingN_PowerOptionOff_IgnoresPowerBlock
FP-012 IncomingN_PowerOptionOn_IncludesPowerBlock
FP-013 RelicAndCurrentOptions_DoNotControlFeelNoPain
FP-014 DetailsOff_DoesNotChangeEitherCalculation
FP-015 UnsupportedPower_HidesMinusNAndSelectedNOnly
FP-016 UnselectedUnsupportedPower_DoesNotPoisonN
FP-017 DuplicateRefresh_DoesNotDoubleCount
FP-018 FreezeSnapshot_PreservesRoutedValues
```

完成门槛：

- 先证明现有代码缺少该贡献；
- 真值表 §3.1 每一行都有合同；
- 至少一个合同能捕获“误接到 N”；
- 至少一个合同能捕获“Block 提前保护更早伤害”；
- production 代码尚未修改；
- FP2 需要重新批准。

### FP2 — Narrow Production Implementation

类型：最小生产实现

候选结构（以 FP0/FP1 为准）：

- 新建窄范围 `VerifiedEtherealExhaustBlockReader` 或等价 reader；
- 只读取 verified official Feel No Pain Power 和当前 hand Ethereal cards；
- 若需要顺序，新增最小 `UpcomingBlockEvent` / ordered mitigation policy；
- `-N` 路径无条件使用 verified result；
- `N` 路径只在 `IncludePowerBlockInIncomingDamage` 为 true 时读取/选择；
- Power 贡献进入 `PowerBlock`，绝不进入 `RelicBlock`；
- 不改设置 schema，不改显示模式。

候选文件：

```text
src/DamageForecast/Combat/LocalIncomingDamageReader.cs
src/DamageForecast/Combat/VerifiedPreAttackBlockReader.cs
src/DamageForecast/Combat/HpLossEventPolicy.cs
src/DamageForecast/Combat/<new verified Ethereal Block reader/policy>.cs
tests/DamageForecast.ContractTests/BlockPolicyContractCases.cs
tests/DamageForecast.ContractTests/<new Feel No Pain contract cases>.cs
tests/DamageForecast.ContractTests/Program.cs
```

限制：

- 若 ordered event 可在现有 policy 中窄扩展，不启动 Forecast Engine 重构；
- 只有证据证明 refresh 缺失时才允许触碰 `ForecastRefreshPatch.cs`；
- 不修改 `VerifiedEnemyDamageModifier`、
  `LegacyDiamondDiademDamageForecast` 或任何配置文件。

完成门槛：

- FP1 新 contracts 通过；
- 现有全部 contracts 无回归；
- 默认 `-N` 正确、默认正数 `N` 语义不变；
- Power option off/on 的 `N` 分支严格分离；
- direct lane 和事件顺序正确；
- 没有身份、配置、安装或 Workshop 变化。

### FP3 — Dual-target Automated Verification

类型：自动化验证；不安装、不启动游戏

动作：

- 复用现有 `scripts/Test-ForecastGuardrails.ps1`；
- 运行当前 supported stable/beta contracts；
- 运行 stable/beta Release build；
- 检查 publish 白名单、DLL hash 和 forbidden artifacts；
- 审查 diff 只包含本任务 production、contract 和 authority 文件；
- 确认配置仍为 18 keys、默认值未变化。

完成门槛：

- stable/beta contracts 与 Release build 全通过；
- guardrail、白名单、hash、forbidden-artifact 检查通过；
- 自动化证据只写 L0/L1/L2，不冒充 L3；
- 安装和运行必须另行批准。

### FP4 — Matching-artifact Install and L3 Runtime Matrix

类型：本地安装与用户手动运行；必须拆分批准

顺序：

1. `FP4 Plan` 报告目标、publish 目录、manifest、DLL SHA256、回滚来源；
2. 用户明确批准后才安装；
3. 安装后确认只有一个 Damage Forecast manifest；
4. Codex 不自动启动游戏；
5. 用户手动完成矩阵并退出；
6. Codex 只读核对 fresh log、异常、active hash 和唯一 manifest。

最低运行矩阵：

| 场景 | 设置 | `-N` 预期 | 正数 `N` 预期 |
| --- | --- | --- | --- |
| 无无惧疼痛 + Ethereal | 默认 | 与当前版本相同 | 不显示 |
| 无惧疼痛 + 1 张 Ethereal | 默认 | 计入未来 Block | 不显示 |
| 无惧疼痛 + 多张 Ethereal | 默认 | 每张仅计一次 | 不显示 |
| `Both` + Power Block 关闭 | 非默认显示 | 计入 | 明确不计入 |
| `Both` + Power Block 开启 | 非默认显示 | 计入 | 计入 |
| `IncomingDamageOnly` + Power Block 关闭 | 非默认显示 | 不显示 | 明确不计入 |
| `IncomingDamageOnly` + Power Block 开启 | 非默认显示 | 不显示 | 计入 |
| advanced details 关闭/开启 | 两次对照 | 数值相同 | 数值相同 |
| Ethereal + blockable turn-end damage | 依实际顺序 | 只保护后续事件 | 与开关一致 |
| Ethereal + direct HP loss | 依实际顺序 | direct lane 不减 | direct lane 不减 |

尽可能补充：

- 升级或叠层无惧疼痛；
- 当前已有 Block；
- 同时存在 Power Block 与 Relic Block；
- 带 `HasTurnEndInHandEffect` 的 Ethereal 卡；
- 一张卡在结束回合前确定性 auto-play/离手的边界；
- end-turn freeze snapshot。

若某场景当前卡池/种子无法构造，必须记录 `Not Exercised`，不能写 Pass。
stable/beta 未实际运行的一方保留 `L3 Pending`。

### FP5 — Authority and Repository Closure

类型：文档与 Git；需要单独批准

动作：

- 更新本任务卡唯一 `Current Control`；
- 更新 `docs/mechanics-evidence.md` 的机制 ledger；
- 仅在当前产品事实变化时更新 `docs/project-state.md`；
- 更新 `docs/task-notes/README.md` 路由状态；
- 仅 stage 本任务文件并审查 staged diff；
- 获批后 commit；push/tag/Workshop 仍需独立批准。

最终收口只写：

```text
Result: <无惧疼痛支持的准确范围>
Current state: <N/-N 路由、双目标和 L3 边界>
Authority: <已同步文件>
Repository: <checkpoint 或明确 pending>
```

---

## 9. 验证分层

| 层级 | 能证明 | 不能证明 |
| --- | --- | --- |
| L0 静态 | Power type、BlockVar、Ethereal/order 代码证据 | 游戏实际触发 |
| L1 contracts | 路由、顺序、计数和 Unknown 边界 | 原生 callback 必然一致 |
| L2 dual-target build | stable/beta 编译兼容、配置与发布卫生 | 玩家实际 HUD 数值 |
| L3 matching runtime | 指定 artifact 下的真实 `N` / `-N` 行为 | 未运行目标和未来版本 |

任何 Gate 不得越级表述。

---

## 10. 最小修改预算与停止条件

### 10.1 默认修改预算

FP2 默认允许：

- 1 个新 verified reader/policy 文件；
- `LocalIncomingDamageReader.cs` 的两条窄接线；
- 0–1 个现有 block policy 文件；
- 1 个新 contract case 文件和 registry 接线；
- 本任务卡与必要 authority 更新。

超出预算必须由 FP0/FP1 证据解释，并重新获得批准。

### 10.2 立即停止条件

出现以下情况时停止并报告：

- stable/beta Power type、数值或事件顺序不一致；
- 无法证明哪些 Ethereal 卡会在本轮实际 exhaust；
- 简单 aggregate 会把 Block 错误提前到伤害之前；
- 正确实现必须重构整个 Forecast Engine；
- 实现会改变默认正数 `N` 或配置 schema；
- Power option 关闭时仍需读取 unsupported Power 才能计算 `N`；
- normal/no-Power contracts 发生变化；
- 发现与另一活跃 Session 重叠的未提交改动；
- 需要新增依赖、修改游戏文件或执行 gameplay mutation；
- 安装、Git、push、tag 或 Workshop 尚未获批。

---

## 11. 与其他任务的关系

- 本任务是独立的小型机制支持任务，可与地图/牌堆 HUD 覆盖任务分别批准和收口。
- 本任务可以先于 queued Forecast Engine 架构稳定化执行，但不得顺手启动 AR1–AR8。
- 若 FP0 证明必须引入通用 ordered block/damage event architecture，应停止本小任务，
  把证据交给架构任务决策，而不是在本卡中隐式扩张。
- Mod 卡牌兼容只作为未来方向，不在本任务中实现。
- “王冠”只作为验证式未来防御读取的类比，不共享错误的 Relic/output 路由。

---

## 12. 下一批准边界

FP0–FP5 的实现、验证与 authority 审查已经完成。下一步只接受单独的本地
checkpoint 批准；push、tag、publish、安装、启动游戏和 Workshop 仍不包含：

```text
批准 Feel No Pain 本地 Git checkpoint。
只允许提交已经审查的 task-owned staged diff；提交前后核对 staged paths、
staged diff 和剩余未暂存并行改动。不得 push、tag、publish、安装、启动游戏
或执行 Workshop 动作。
```

若未来希望在一部分确定性 Stampede 场景中恢复 `-N`，必须另开独立精化
Gate，先证明原生选牌和副作用是确定且完整可建模的；不得在本次 checkpoint
中扩大当前安全的 Unknown 边界。

---

## 13. FP4 L3 手动运行结果记录

记录日期：2026-07-25

本节只记录人工运行的数值结论和退出游戏后的只读核对结果；不保存、复制、
嵌入或引用运行截图。

### 13.1 L3-10 — Ethereal + direct HP loss

Target：beta，host `v0.109.0`

人工构造：

- 当前 Feel No Pain 数值为 4；
- 手牌包含 1 张会在回合结束时 exhaust 的普通 Ethereal 卡；
- 手牌包含回合结束固定直接失去 6 生命的 `Beckon`；
- 敌方后续攻击为 13；
- 正数 `N` 使用 Power Block 关闭路线；
- advanced details 开启。

观察值：

```text
正数 N = 19
-N = -15
🛡 blockable-loss lane = 9
♥ direct-HP-loss lane = 6
```

分析：

```text
Power Block 关闭的 N = 13 + 6 = 19
Feel No Pain future Block = 4
后续可格挡掉血 = 13 - 4 = 9
预计实际总掉血 = 9 + 6 = 15
```

用户确认实际结算与预测一致。直接失去生命的 6 点没有被 Block 减少，也没有
消耗 Feel No Pain 产生的 Block；该 Block 只抵消后续敌方攻击。因此 L3-10
判定为 `Pass`。

### 13.2 L3-12 — Stampede auto-play Unknown 边界

此前 stable `v0.107.1` 的人工观察重新分类为 `Pass`：

- 无减号正数 `N` 保持为已知的 9；
- 带减号 `-N` 在打出 Stampede 后隐藏；
- 回合结束时 Stampede auto-play 先于 Ethereal exhaust；
- 实际掉血为 9。

这证明 selected expected-loss 路线在 auto-play 结果尚不确定时按合同
fail-closed，而未选择 Power Block 的正数 `N` 没有被该 Unknown 污染。
因此该场景不是数值预测失败，不应登记为 Fail。

### 13.3 退出后的 matching-artifact 核对

只读核对时间：2026-07-25 18:22:10 +08:00

- 游戏进程数：0；
- fresh log：
  `C:\Users\ROG\AppData\Roaming\SlayTheSpire2\logs\godot.log`，
  最后写入 2026-07-25 18:18:30 +08:00；
- host：`v0.109.0`，日志标记为 public beta；
- `[Damage Forecast] Loaded` 出现一次；
- 与 Damage Forecast 关联的 error/exception/fail：0；
- 活动 manifest：唯一 1 份，
  `damage-forecast` `v0.3.0`；
- Workshop 中 `damage-forecast` / `sts2-party-watch-v2` manifest：0；
- 活动 manifest SHA256：
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；
- 活动 DLL SHA256：
  `8BC96C07DB047F963940D1378A0257F101B886C949368F23F4FAD1C41B0CDF49`；
- 活动 DLL 与 FP3 `work/forecast-guardrails/bin/beta` 候选逐位一致；
- FP3 stable/beta 候选 DLL 本身也逐位一致。

fresh log 中另有第三方 `NndxVideoOverlay.BuildSpriteFrames` 异常和 Godot
退出时资源泄漏警告；调用栈及文本不指向 Damage Forecast，本任务不把它们
归因为 Feel No Pain 实现。

### 13.4 L3 覆盖边界

| Target | Pass | Not Exercised / Pending |
| --- | --- | --- |
| stable `v0.107.1` | L3-01–09、L3-11、L3-12 | L3-10 |
| beta `v0.109.0` | L3-10 | L3-01–09、L3-11、L3-12 |

按场景合并已有 L3-01–12 的人工覆盖，但不能把跨 target 的合并覆盖表述为
stable 或 beta 任一单目标的完整 `12/12`；FP5 authority 同步也不把这组
跨 target 证据升级成单目标完整认证。

---

## 14. FP0–FP5 增量证据与收口

### FP0 — Read-only Native Mechanics and Outlet Baseline

- Result: `Complete`
- Verified: stable/beta 均使用原生 `FeelNoPainPower`，每次 exhaust 的 Block
  读取当前 `Amount`；ordinary Ethereal 先 exhaust，带 turn-end hand effect
  的卡先执行自身效果、再因 Ethereal exhaust。
- Order boundary: Stampede 的 `AutoPostPlay` 先于 Ethereal exhaust；存在
  pending playable Attack 时，点击结束回合前不模拟随机 auto-play 结果。
- Outlet: `-N` 默认选择可信 future Power Block；正数 `N` 仅在
  `IncludePowerBlockInIncomingDamage=true` 时选择它。

### FP1 — Contract-first Outlet and Ordering Reproduction

- Result: `Complete`
- Added: `FP-001`–`FP-019`，覆盖计数、原生顺序、direct lane、`N/-N`
  路由、明细开关、freeze 和 Stampede Unknown；`BK-015`/`BK-016`
  锁定 future Block 原生顺序及 hand order 映射。
- Preserved: production 在本 Gate 未修改。

### FP2 — Narrow Production Implementation

- Result: `Complete`
- Added: `VerifiedEtherealExhaustBlockReader.cs`。
- Changed: `HpLossEventPolicy.cs`、`LocalIncomingDamageReader.cs` 以及 FP1
  contracts/registration；未修改 `VerifiedPreAttackBlockReader.cs`、
  `ForecastRefreshPatch.cs`、配置或身份。
- Boundary: 非原生/owner 不可信、Power 读取异常和 pending Stampede
  playable Attack 都 fail closed；未选择 Power Block 的正数 `N` 不受污染。

### FP3 — Dual-target Automated Verification

- Result: `Complete / L1-L2`
- Verified: stable/beta 的 19 项 FP contracts、2 项 BK contracts 和既有
  contracts 均通过；双目标 Release build、现有 guardrail、白名单、
  forbidden-artifact、配置和 diff 检查通过。
- Artifact: stable/beta candidate DLL 均为
  `8BC96C07DB047F963940D1378A0257F101B886C949368F23F4FAD1C41B0CDF49`；
  manifest 为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Boundary: 自动化只证明 L1/L2，不替代 L3。

### FP4 — Matching Artifact and Manual Runtime

- Result: `Complete within §13 target boundary`
- Verified: beta matching artifact、唯一活动 manifest、Workshop 重复身份 0、
  fresh log 一次加载且无 Damage Forecast attributable error。
- Runtime: L3-01–12 已获得跨 stable/beta 的场景覆盖；单 target 完整
  `12/12` 未宣称。
- Preserved: 未修改配置、Workshop 或 Git；Codex 未自动启动游戏。

### FP5 — Authority and Repository Closure Review

- Result: `Complete / Checkpoint Pending`
- Authority: 本任务卡、`docs/mechanics-evidence.md`、
  `docs/project-state.md`、`docs/task-notes/README.md` 已同步。
- Staging: 仅纳入 Feel No Pain 自有文件和共享文件中的独立 hunks；
  Forecast Timeline/Architecture 并行改动保持未暂存。
- Repository: 尚未 commit；下一步需要单独批准本地 checkpoint。

最终收口：

```text
Result: 原生 Feel No Pain 已按真实 Ethereal/turn-end 顺序支持 future Block，
        并正确分流到默认 -N、可选 N 和既有明细。
Current state: stable/beta L1/L2 通过；L3-01–12 跨 target 覆盖；
               pending Stampede playable Attack 保持安全 Unknown。
Authority: 本任务卡 + mechanics-evidence.md + project-state.md + task-notes/README.md。
Repository: task-owned staged diff ready；checkpoint pending。
```
