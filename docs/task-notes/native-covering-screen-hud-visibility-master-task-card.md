# Damage Forecast — 地图与牌堆覆盖层 HUD 可见性修复主任务卡

日期：2026-07-25

任务类型：玩家可见 HUD 覆盖缺陷、原生界面生命周期兼容、回归测试与双目标运行验证

## Current Control

State: `Closed`
Last completed: `CV5 — Git Checkpoint`
Next: `None within this task — stable L3 remains a documented target limitation`
Approved: `Yes — local Git checkpoint only; push, tag, and Workshop remain unapproved`
Evidence: `§12`
Repository: `Local checkpoint created by this commit; push, tag, and Workshop not executed`

---

## 0. 下一 Session 执行指令

先完整阅读：

1. 本任务卡；
2. `docs/task-notes/task-closure-standard.md`；
3. `docs/project-state.md` 中 Current HUD Behavior、安装产物和 stable/beta 状态；
4. `src/DamageForecast/UI/DamageForecastNativeCoveringScreenTracker.cs`；
5. `src/DamageForecast/Patches/NativeCoveringScreenLifecyclePatch.cs`；
6. `src/DamageForecast/UI/DamageForecastHudVisibilityPolicy.cs`；
7. `tests/DamageForecast.ContractTests/LifecycleContractCases.cs`；
8. 仅按需读取 `phase-12c-aud-0007-visibility-performance-measurement.md` 中
   `NCardPileScreen` 的历史证据。

首次进入只执行 CV0 只读诊断。不要因为本任务卡存在就自动修改代码、
运行构建、安装 Mod、启动游戏、stage、commit、push、tag 或更新 Workshop。

CV0 完成后必须报告：

- 当前运行 artifact、分支、游戏版本与仓库 HEAD 的绑定关系；
- 地图、抽牌堆、弃牌堆和消耗牌堆各自的实际原生节点类型；
- 地图失败和牌堆失败是否为同一根因；
- 当前 open / hide / close 生命周期中哪个事件漏失；
- 防止“修完后正常战斗 HUD 永久不显示”的合同矩阵；
- CV1/CV2 的精确候选文件和最小 diff；
- 下一 Gate 所需的明确批准。

---

## 1. 一句话目标

当战斗中的地图、抽牌堆、弃牌堆或经确认的消耗牌堆界面真正覆盖战斗场景时，
Damage Forecast HUD 应只在覆盖期间隐藏；关闭最后一个覆盖界面后，HUD 必须立即
恢复到本来应该显示的状态，绝不能通过“默认一直隐藏”或残留 stale open 状态来
掩盖覆盖问题。

---

## 2. 当前用户观察与证据边界

### 2.1 当前 L3 用户观察

用户当前报告：

- 打开战斗地图后，Damage Forecast HUD 仍显示在覆盖界面上；
- 打开抽牌堆后，HUD 仍显示；
- 打开弃牌堆后，HUD 仍显示；
- 消耗牌堆（Exhaust pile；用户本次称“烧牌堆”）尚未测试；
- 用户明确要求：修复后，正常战斗情况下 HUD 不能变成始终不显示。

这组观察证明存在玩家可见问题，但尚未完成：

- exact installed DLL / manifest / game branch 绑定；
- stable 与 beta 的逐项复现；
- 地图和各牌堆的 concrete native type 记录；
- 覆盖界面关闭后的 restore 行为矩阵；
- 消耗牌堆是否实际复现。

因此，消耗牌堆当前状态必须写作 `Not Yet Tested`，不能提前写成
`Confirmed Broken` 或 `Working`。

### 2.2 已有静态与历史证据

当前生产代码的显式覆盖名单已经包含：

```text
MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen
```

但不包含历史上已静态确认用于抽牌堆/弃牌堆查看器的：

```text
MegaCrit.Sts2.Core.Nodes.Screens.NCardPileScreen
```

历史 AUD-0007 运行记录也已把 `NCardPileScreen` 缺失登记为独立的
covering-screen compatibility gap。

所以当前根因必须拆分：

1. **抽牌堆/弃牌堆**：`NCardPileScreen` 缺少覆盖注册是高可信根因候选；
2. **地图**：类型已在当前名单中，不能以同一解释直接收口，必须继续检查
   artifact、类型漂移、实际可见节点、生命周期 hook 和 refresh 交付；
3. **消耗牌堆**：先确认是否复用 `NCardPileScreen`、另一类型或另一 UI 路径，
   再决定是否纳入同一最小修复。

### 2.3 已有 lifecycle 合同不能替代本任务

现有 contracts 已覆盖：

- `LC-006`：临时覆盖隐藏时保留 committed snapshot；
- `LC-007`：覆盖关闭后恢复同一个 committed snapshot。

它们证明“已被正确识别为临时覆盖”后的 snapshot 策略，但没有证明：

- 地图/牌堆 native screen 会被正确分类；
- open / `Show` / `set_Visible` 会被 tracker 收到；
- close / `Hide` / exit 会清除 tracker；
- 普通战斗不会因为 stale tracker 状态而被永久隐藏。

本任务新增合同应补分类和状态转换缺口，不复制 `LC-006/LC-007`。

---

## 3. 非法修法与硬性失败条件

出现下列任一结果，本任务即失败，不得进入“Verification Complete”：

1. 正常战斗、没有覆盖界面时 HUD 不再显示；
2. 关闭地图或牌堆后 HUD 仍隐藏，必须结束回合、切场景或重启才能恢复；
3. 把“任何 Screen / CanvasItem 可见”都当作覆盖层，导致普通战斗 UI 误杀；
4. 仅把 HUD 默认关闭、透明或移出屏幕来通过覆盖场景检查；
5. 打开一个覆盖界面后在 tracker 中留下永久 active 状态；
6. 关闭嵌套覆盖层中的一个时，另一个仍开着却提前显示 HUD；
7. 为了修复牌堆而改变伤害预测、`N`/`-N`、advanced details 或 snapshot 数值；
8. 只在一个 game target 工作，却把双目标 build 当成双目标运行证明；
9. 把“消耗牌堆尚未测试”升级成已验证结论；
10. 用当前代码静态确认替代 matching-artifact 游戏运行验证。

---

## 4. 必须保持的行为不变量

1. 正常本机单人战斗且 HUD 已启用、玩家有效、生命条可见、没有覆盖层时，
   `ShouldRenderHud` 仍应允许显示。
2. 覆盖行为只改变临时可见性，不清空 live 或 committed forecast snapshot。
3. 最后一个覆盖层关闭后，HUD 依据当前有效状态立即恢复；若本来就因设置关闭、
   玩家无效或离开战斗而应隐藏，则不得错误强制显示。
4. 多个覆盖层同时存在时使用“至少一个仍打开”语义；必须等最后一个关闭才恢复。
5. repeated `Show`、`Hide`、`set_Visible`、`_Ready`、`_EnterTree`、`_ExitTree`
   必须幂等，不重复积累同一个实例。
6. 已失效、已离树或被释放的 weak reference 必须可清理，不能造成永久隐藏。
7. 未验证的普通节点、战斗 HUD 子节点和非覆盖 UI 不得进入覆盖集合。
8. 现有地图、设置、奖励、选牌、商店、暂停和 game-over 覆盖行为不得回归。
9. HUD mode、placement、advanced details、freeze、多人本机 HUD 边界保持不变。
10. stable/beta compatibility、配置 schema、技术身份、安装目录与 Workshop 状态不变。
11. 不新增 per-frame 全树扫描；默认 build 不启用 AUD-0007 profiling。
12. 不执行真实卡牌动作、RNG、存档、网络或 gameplay mutation 来探测界面。

---

## 5. 任务范围

### 5.1 包含

- 绑定当前用户复现与 exact installed artifact；
- 识别地图、抽牌堆、弃牌堆和消耗牌堆的实际节点/可见性路径；
- 为 native covering-screen 分类和 open/close 状态建立最小回归合同；
- 仅在证据支持的显式类型或窄生命周期路径中实施修复；
- stable/beta contracts、guardrail、Release build 和发布白名单/hash 检查；
- 用户另行批准后的 matching-artifact 安装与 L3 手动运行矩阵；
- 用户另行批准后的 authority 更新和 Git checkpoint。

### 5.2 不包含

- Forecast Engine AR1–AR8 架构稳定化；
- Damage Forecast 计算、卡牌机制或 Mod 卡牌支持扩展；
- HUD 视觉重设计、位置调整或默认开关变化；
- 通用 Godot UI 框架或所有未知 Screen 的自动识别；
- 设置 schema、BaseLib 持久化或 migration 修改；
- Mod ID、assembly、namespace、Harmony owner 或安装 identity 修改；
- 游戏文件修改、自动启动游戏、自动操作战斗；
- Workshop 上传、visibility、说明或封面修改；
- push、tag 或发布，除非后续 Gate 单独明确批准。

---

## 6. CV0 根因判定树

按顺序检查，不跳步：

1. **Artifact 层**
   - 游戏是否已关闭；
   - 当前 stable/beta 与版本；
   - active manifest、DLL SHA256、日志加载路径；
   - installed artifact 是否对应当前 HEAD 或历史发布。

2. **Concrete type 层**
   - 地图实际节点是否仍为 `NMapScreen`；
   - 抽牌堆、弃牌堆是否都为 `NCardPileScreen`；
   - 消耗牌堆是否复用同一类型；
   - stable/beta 是否存在 namespace/type drift。

3. **Patch resolution 层**
   - `AccessTools.TypeByName` 是否解析；
   - `_Ready` / `_EnterTree` / `_ExitTree` 是 declared 还是 inherited；
   - `Show` / `Hide` / `set_Visible` 是否发生在名单中的节点本身。

4. **Tracker 层**
   - open 时是否 `MarkOpened`；
   - visible-in-tree 判断是否与玩家实际看到的覆盖层一致；
   - close 时是否 `MarkClosed`；
   - cleanup 是否清除 invalid / exited 实例；
   - 多实例或嵌套情况下 active count 是否正确。

5. **Refresh/render 层**
   - 覆盖状态转换后是否调用 `RefreshRegisteredBars()`；
   - `ShouldRenderHud(..., out temporarilyCovered)` 是否得到预期值；
   - close 后是恢复当前 live snapshot 还是已提交 snapshot；
   - 若 tracker 已正确而 HUD 仍错误，才扩大到注册 bar/lifecycle 路径。

不得在地图根因未确认时，以广泛隐藏规则覆盖症状。

---

## 7. Gate 计划

### CV0 — Read-only Baseline and Root-Cause Split

类型：只读诊断

动作：

- 核对 branch、HEAD、remote、status 和用户未提交改动；
- 核对游戏进程、目标版本、active manifest/DLL hash 与日志；
- 读取当前 tracker、lifecycle patch、visibility policy 和既有 contracts；
- 对四类界面建立 `Observed / Static / Unknown` 三层矩阵；
- 分别判断地图、牌堆和消耗牌堆的根因；
- 输出 CV1 测试 seam 与 CV2 最小候选 diff。

完成门槛：

- 不修改文件；
- 不 build、不安装、不启动游戏、不执行 Git/Workshop 动作；
- 每条结论明确标记 `Confirmed`、`Candidate` 或 `Unknown`；
- 用户另行批准后才进入 CV1。

### CV1 — Contract-first Visibility Regression

类型：合同/测试边界

动作：

- 增加显式 native type 分类合同；
- 增加 open → hide → close → restore 状态合同；
- 增加“普通战斗可见”的负向对照；
- 覆盖 duplicate event、stale cleanup 和 nested covering screen；
- 复用 `LC-006/LC-007` 的 snapshot 语义，不复制一套 lifecycle；
- 先证明当前错误可被测试捕获，再批准生产修改。

候选测试 ID：

```text
CV-001 KnownMapType_IsCovering
CV-002 KnownCardPileType_IsCovering
CV-003 UnknownCombatUi_IsNotCovering
CV-004 NoCover_OrdinaryCombatRemainsVisible
CV-005 OpenCover_HidesWithoutClearingSnapshot
CV-006 CloseLastCover_RestoresVisibility
CV-007 DuplicateOpenClose_IsIdempotent
CV-008 NestedCover_RemainsHiddenUntilLastClose
CV-009 InvalidOrExitedCover_DoesNotLeavePermanentHide
```

测试名称可按实际 seam 调整，但上述语义不可删除。

完成门槛：

- 测试能同时捕获“覆盖时没隐藏”和“关闭后一直隐藏”；
- 未知/普通 UI 不被误分类；
- production 代码尚未改动；
- CV2 需要重新批准。

### CV2 — Narrow Production Fix

类型：最小生产修复

候选方向（以 CV0/CV1 证据为准）：

- 将已确认的 `NCardPileScreen` 加入显式覆盖类型；
- 若地图存在类型漂移，只增加已在 stable/beta 证明的精确类型；
- 若生命周期发生在继承方法或实际可见宿主上，只修正对应窄 hook；
- 若 tracker 状态转换存在 stale 状态，只调整 open/close/cleanup 的幂等语义；
- 不引入“任意 screen 都隐藏”或 per-frame UI tree scan。

预期候选文件：

```text
src/DamageForecast/UI/DamageForecastNativeCoveringScreenTracker.cs
src/DamageForecast/Patches/NativeCoveringScreenLifecyclePatch.cs
tests/DamageForecast.ContractTests/<CV0 确认的最小 contract 文件>
```

只有 CV0 证明 render policy 本身有问题时，才允许把
`DamageForecastHudVisibilityPolicy.cs` 加入候选 diff。

完成门槛：

- CV1 新 contracts 全部通过；
- 现有 contracts 无回归；
- normal/no-cover 路径保持可见；
- 最后一个 cover 关闭后可恢复；
- 没有配置、身份、安装、Workshop 或预测语义变化。

### CV3 — Dual-target Automated Verification

类型：自动化验证；不安装、不启动游戏

动作：

- 运行完整 guardrail；
- 运行当前支持的 stable/beta contracts；
- 运行 stable/beta Release build；
- 检查匹配发布目录白名单、DLL hash 和 forbidden artifacts；
- 审查 diff 仅包含本任务的 production、contract 和任务 authority 文件。

完成门槛：

- stable/beta contracts 与 Release build 均通过；
- 白名单/hash/forbidden-artifact 检查通过；
- 产物只证明 L0/L1/L2，不写成 L3；
- 安装和运行仍需 CV4 单独批准。

### CV4 — Matching-artifact Install and L3 Runtime Matrix

类型：本地安装与用户手动运行；必须拆分批准

顺序：

1. `CV4 Plan`：报告待安装目录、manifest、DLL SHA256、回滚来源和排除项；
2. 用户明确批准后才安装；
3. Codex 不自动启动游戏；
4. 用户手动运行并报告矩阵；
5. 退出游戏后核对日志、异常、active hash 和唯一 manifest。

最低运行矩阵：

| 场景 | 打开前 | 覆盖期间 | 关闭后 |
| --- | --- | --- | --- |
| 普通战斗负向对照 | HUD 正常显示 | 无覆盖 | HUD 仍正常显示 |
| 地图 | HUD 正常显示 | HUD 隐藏 | HUD 立即恢复 |
| 抽牌堆 | HUD 正常显示 | HUD 隐藏 | HUD 立即恢复 |
| 弃牌堆 | HUD 正常显示 | HUD 隐藏 | HUD 立即恢复 |
| 消耗牌堆 | 先确认可进入 | HUD 隐藏 | HUD 立即恢复 |

补充运行要求：

- 地图、抽牌堆、弃牌堆至少各重复打开/关闭两次；
- 至少一次在 end-turn committed snapshot 存在时打开并关闭覆盖层；
- 至少一次从一个覆盖层切换到另一个，确认不会提前恢复或永久隐藏；
- 关闭所有覆盖层后继续正常战斗，确认 HUD 不是只在下一回合才恢复；
- 若消耗牌堆在测试局无法进入，记录 `Not Exercised`，不伪造通过；
- stable/beta 哪个目标未运行，就保留该目标 `L3 Pending`。

### CV5 — Authority and Repository Closure

类型：文档与 Git；需要单独批准

动作：

- 更新本任务卡唯一 `Current Control`；
- 只在当前产品事实变化时更新 `docs/project-state.md`；
- 更新 `docs/task-notes/README.md` 路由状态；
- 仅 stage 本任务文件并审查 staged diff；
- 获批后 commit；push/tag/Workshop 仍是独立外部动作。

最终收口必须只写：

```text
Result: <修复了哪些已验证覆盖界面>
Current state: <stable/beta 的自动化与 L3 边界、剩余未测项>
Authority: <已同步文件>
Repository: <checkpoint 或明确 pending>
```

---

## 8. 验证分层

| 层级 | 能证明 | 不能证明 |
| --- | --- | --- |
| L0 静态 | 类型、hook、调用关系和最小 diff | 游戏中真实可见性 |
| L1 contracts | 分类和状态机回归边界 | Godot/native callback 必然触发 |
| L2 dual-target build | stable/beta 编译兼容和发布卫生 | 玩家实际打开/关闭行为 |
| L3 matching runtime | 指定 artifact 下的真实 HUD 行为 | 未运行的另一目标或未来版本 |

任何 Gate 不得越级表述。

---

## 9. 最小允许修改与停止条件

### 9.1 最小允许修改

CV2 默认预算：

- 1 个 native covering-screen tracker 文件；
- 0–1 个 lifecycle patch 文件；
- 1 个现有或新建 contract case 文件；
- contract registry 的最小接线；
- 本任务卡和必要的 current authority 更新。

超出该预算必须在 CV0/CV1 以证据说明，并重新获得批准。

### 9.2 立即停止条件

出现以下情况时停止并报告，不自行扩大：

- active artifact 无法绑定；
- 地图 concrete type 或回调路径在 stable/beta 间不一致；
- 修复需要 patch 所有 `CanvasItem` 之外的新全局 hook；
- 普通战斗负向对照失败；
- close 后恢复依赖未授权的 snapshot/lifecycle 重构；
- 必须修改 Forecast Engine 计算语义；
- 发现与另一活跃 Session 重叠的未提交改动；
- 构建需要新增依赖或修改游戏文件；
- 安装、Git、push、tag 或 Workshop 尚未获批。

---

## 10. 与其他任务的关系

- 本任务是独立的小型 HUD compatibility fix，可以在用户批准后先于 queued
  Forecast Engine 架构稳定化执行。
- 本任务不得顺手启动 AR1–AR8，也不得修改架构任务卡的 active planning 内容。
- Mod 卡牌兼容、未来未知伤害牌自动识别与本任务无关。
- 历史 AUD-0007 只提供 `NCardPileScreen` 缺口证据；其性能结论保持关闭，
  不因本任务重新打开。

---

## 11. 下一批准边界

本任务由 CV5 本地 Git checkpoint 收口，任务内没有下一 Gate。stable covering-screen
L3 保持为已记录的目标限制，不因 beta L3 完成而被推定通过。

push、tag 或 Workshop 更新若未来需要，仍须独立批准；它们不属于本任务收口。

---

## 12. Gate 执行证据与收口

### 12.1 CV0 — Read-only Baseline and Root-Cause Split

- Result: `Complete`
- Confirmed: stable v0.107.1 与 beta v0.109.0 均解析 `NMapScreen` 和
  `NCardPileScreen`；牌堆声明 `_Ready` / `_EnterTree` / `_ExitTree`，地图声明
  `Open` / `Close`。
- Root cause: 牌堆缺少显式 covering 分类；地图已分类但缺少直接
  `Open` / `Close` 状态转换目标。两者不是同一根因。
- Preserved: 未修改文件、构建、安装、启动游戏或执行 Git / Workshop 动作。

### 12.2 CV1 — Contract-first Visibility Regression

- Result: `Complete`
- Changed: 新增 `NativeCoveringScreenContractCases.cs` 的 CV-001–CV-010，并在
  contract registry 最小接线。
- Verified: 冻结 stable/beta 均为 `277 discovered / 275 passed / 2 failed`；
  CV-002 捕获牌堆未分类，CV-010 捕获地图 `Open` / `Close` 未接线。
- Preserved: CV-001、CV-003–CV-009 和全部既有 contracts 通过；production
  尚未修改。

### 12.3 CV2 — Narrow Production Fix

- Result: `Complete`
- Changed: 仅修改 `DamageForecastNativeCoveringScreenTracker.cs` 和
  `NativeCoveringScreenLifecyclePatch.cs`；加入 `NCardPileScreen` 显式分类，
  并仅为 `NMapScreen` 接入 `Open` / `Close`。
- Verified: 冻结 stable/beta 均为 `277/277 passed`，CV-002、CV-010 转绿，
  其余可见性安全 contracts 无回归。
- Preserved: 未改变 render policy、snapshot、预测、配置、身份或 HUD 默认状态。

### 12.4 CV3 — Dual-target Automated Verification

- Result: `Complete`
- Verified: 完整 guardrail 在 stable/beta 上各 `291/291 passed`；双目标
  Release build 均为 `0 warnings / 0 errors`；diff hygiene 与 forbidden-artifact
  检查通过。
- Publish: stable/beta 各严格只有 `damage-forecast.dll` 与
  `damage-forecast.json`，两套产物逐文件相同。
- SHA256: DLL
  `7D3DEEFB5A17584B67C6F28B8C28C7D5E48FF4AB53D440B2FE287BB1D8916FCF`；
  manifest
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Scope: 当时工作树中的并行 ExternalObservation contracts 不属于本任务；
  范围协调确认本任务只拥有两处 production、CV contract 文件和 registry 单行。

### 12.5 CV4 — Matching-artifact Install and L3 Runtime Matrix

- Result: `beta L3 Complete / stable L3 Pending`
- Plan: 当前游戏为 beta v0.109.0 (`c12f634d`)；安装前 active DLL SHA256 为
  `9600B23C85DB1AF7CFEDD75536CCA1FC2ECCC6455AD6C18C1AD6FF54AB25E44B`。
- Install: beta matching artifact 已安装到 `mods/damage-forecast`；active DLL
  SHA256 为 `7D3DEEFB5A17584B67C6F28B8C28C7D5E48FF4AB53D440B2FE287BB1D8916FCF`，
  manifest SHA256 为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Recovery: 前一 active artifact 已移至 Loader 扫描范围外的
  `.damage-forecast-backups/cv4-beta-install-20260725-damage-forecast-v0.3.0`；
  安装 ledger 存在，staging / failed-target 残留为 0。
- Preserved: `DamageForecast.cfg` 安装前后 SHA256 均为
  `FDED35CCC1B1EC2CFFFEBF0B5EDC2FF0E228F8ADF5B1B647C9BF0B82F752830B`；
  Workshop、Git 和配置内容未修改。
- Runtime: 用户手动确认普通战斗、地图、抽牌堆、弃牌堆和消耗牌堆的
  hide / close / restore 矩阵全部成功。
- Log: 最新 beta 日志中 manifest、DLL、initializer、`[Damage Forecast] Loaded`
  和模组列表记录均各 1 次；Damage Forecast 可归因 error / exception 为 0。

### 12.6 CV5 — Authority Sync

- Result: `Complete`
- Changed: 同步本任务卡唯一 `Current Control`、`docs/project-state.md` 当前事实
  和 `docs/task-notes/README.md` 路由状态，并形成仅包含本任务文件与共享文件
  精确 hunks 的本地 Git checkpoint。
- Preserved: 未修改 production / contract、未重新安装或启动游戏、未执行
  push、tag 或 Workshop 动作；并行 ExternalObservation、Feel No Pain 和其他任务
  文件未纳入 checkpoint。
- Pending: stable covering-screen L3 仍为 Pending；这是已记录的目标限制，
  不阻止当前 beta 修复任务收口。

### 12.7 最终收口

Result: 地图、抽牌堆、弃牌堆与消耗牌堆在 beta matching artifact 下覆盖期间
隐藏 HUD，关闭最后一个覆盖界面后立即恢复；普通战斗 HUD 保持显示。

Current state: stable/beta L1/L2 自动化通过；beta v0.109.0 L3 Complete；
stable v0.107.1 L3 Pending。Workshop unchanged。

Authority: 本任务卡、`docs/project-state.md`、`docs/task-notes/README.md` 已同步。

Repository: 本 commit 是本地 checkpoint；未执行 push、tag 或 Workshop 更新。
