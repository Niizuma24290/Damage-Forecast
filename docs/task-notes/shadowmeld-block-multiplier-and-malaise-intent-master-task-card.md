# Damage Forecast — 融入暗影格挡倍率与萎靡 Intent 修正调查主任务卡

日期：2026-07-30

Type: Mechanics Investigation / Conditional Multi-Gate Fix
Area: Mechanics
Touches: Forecast Core
Priority Tag: P1
Queue: Parked
Depends on: `damage-forecast-hud-placement-implementation-master-task-card.md` 完成当前 I2-R3 小阶段的 full guardrail checkpoint 并停止

## Current Control

Classification: CLOSED_TASK
State: Closed
Last completed: Shadowmeld 分支已完成实现、headless/build 验证、case-specific 用户 runtime 验证与本地 Git checkpoint
Next: 无活动 Gate；Malaise/post-card final-Intent 分支明确 Deferred / 未解决，若未来继续需单独授权
Approved: Shadowmeld repository closure and local Git checkpoint only; completed in this commit; no build, install, game-file mutation, push, tag, release, or Workshop
Evidence: `Final Closure — 2026-08-02`
Repository: Closed / Local Git checkpoint (this commit)

## Goal

确认并修正 `Shadowmeld / 融入暗影` 对 Damage Forecast 格挡预测的影响，同时调查 `Malaise / 萎靡` 及“凌虐”这类卡牌效果结算后，Damage Forecast 是否及时读取原生最终 Intent，而不是继续显示结算前的旧值。

## Current facts and unknowns

- v0.109.1 存在 `MegaCrit.Sts2.Core.Models.Cards.Shadowmeld` 与 `ShadowmeldPower`；该卡不直接给予 Block，而是修改本回合后续 Block gain。
- 当前已落入 `localCreature.Block` 的数值应视为原生最终当前 Block，不能因 Shadowmeld 再次翻倍。
- 当前未来 Power/Relic Block readers 直接读取各来源基础值；Shadowmeld 的倍率顺序、叠层、适用来源与未来事件接入点尚未验证。
- `Malaise` 的原生效果是敌人失去 X Strength 并获得 X Weak；当前预测依赖 `AttackIntent.GetTotalDamage(...)` / 必要时 `GetSingleDamage(...)`。
- 用户在文件自报 `v0.110.1` 的多人战斗中观察到：敌人原生攻击 Intent 在“凌虐”结算后已由 `8` 变为 `0`，但本机 Forecast 仍显示结算前的 `8`。这是用户运行时复现，尚未证明仅限多人，也尚未确认“凌虐”的准确原生类型与 hook。
- 朋友客户端曾报告相关数值异常，但多人敌人死亡与 HUD 快照问题属于现有 HUD 任务或后续独立调查，不在本卡预设为同一根因。

## Scope and boundaries

Included:

- 核对 Shadowmeld 原生 Power、Block 修改 hook、倍率/叠层和事件顺序。
- 区分已经实现的当前 Block 与尚未发生的 Power、Relic、Ethereal 等未来 Block grant。
- 核对 Malaise 与“凌虐”结算前后单击、多段 Intent 的原生显示值、最终 Power/状态与当前 Mod 读取值，并确认“凌虐”的准确原生类型和结算 hook。
- 仅在证据证明现有预测错误后，设计最小修正和对应 contract/runtime matrix。

Excluded:

- 暗影步、卡牌悬停假设预测、完整出牌模拟。
- 多人敌人死亡后的刷新、结束回合冻结和队友/共享 HUD；本次多人画面只作为卡牌结算后旧 Intent 的复现输入，不预设为网络根因。
- 借本任务重写 Forecast 架构、扩大到所有未知 Block 或伤害修正机制。
- 未经单独批准的构建、安装、游戏启动、Git、发布或 Workshop 动作。

Preserved:

- 当前 Block 不重复乘算；不通过最终 `-N` 反推原始伤害或 Block。
- Strength、Weak 和多段取整优先信任并验证原生 Intent API，不先加入手写减法。
- 同一可见状态下原生 Intent 已为 `0` 时，不允许 Forecast 无说明地保留结算前的 `8`；优先修复最终状态刷新，不添加针对“凌虐”的手写伤害补偿。
- 无法证明原生顺序或来源时保持 `Unknown` / 隐藏，不显示可信度不足的部分结果。

## Gate SM-0 — Read-only Native Mechanism Audit

Goal: 建立 Shadowmeld 与 Malaise 的原生机制、当前调用链和最小复现矩阵。
Allowed: 只读检查当前游戏 artifact、权威文档、生产源码与现有 contracts；不得修改、构建、安装、启动游戏或写入 Git。
Deliverable: 简洁证据报告，列出准确类型/方法、计算顺序、当前缺口，并建议继续同卡实施或拆分。
Verification: 至少覆盖 Shadowmeld 无/单层/可验证叠层、当前与未来 Block；Malaise 前后单击与多段 Intent、Strength 与 Weak 组合。
Pass: 能解释每个案例应由原生最终值、Block modifier 还是 HUD snapshot 负责，且不存在未说明的重复修正风险。
Stop: 更新本卡 SM-0 增量证据后停止，等待后续 Gate 单独批准。

### SM-0 Checkpoint — 2026-07-31

- Result: `Complete / Static Evidence Only`；只读核对当前 beta `v0.110.0 / eecc8c4d` 的 `sts2.dll`（SHA256 `7A2592364FDC6FF4C42BB5F1FF41F9FA12155F84DE772E203ACE1B088EBB607D`）、XML、当前生产源码与 contracts；未构建、安装、启动游戏或执行 Git。
- Shadowmeld native: `Shadowmeld.OnPlay` 以基础 `Power=1` 施加 `ShadowmeldPower`；该 Power 为 `Counter` stacking，`ModifyBlockMultiplicative(...)` 对 owner 返回 `2^Amount`、对其他 creature 返回 `1`，并在 owner 所在 side 的 `AfterSideTurnEnd(...)` 移除。
- Block order/source: `CreatureCmd.GainBlock(...)` 先走 enchantment additive/multiplicative，再走 model additive、model multiplicative（含 Shadowmeld），最后非负化并由 `Creature.GainBlockInternal(...)` 写入 `Creature.Block`。Frost、Plating、Feel No Pain、Orichalcum/FakeOrichalcum、Ripple Basin 与 Cloak Clasp 的已支持未来 grant 均通过该命令，且相关 turn-end grant 发生在 Shadowmeld 移除前。
- Confirmed gap: 当前 `localCreature.Block` 已是原生最终当前 Block，必须保持不再乘算；`VerifiedPreAttackBlockReader` 与 `VerifiedEtherealExhaustBlockReader` 读取的未来 grant 仍是基础值且没有 Shadowmeld reader，因此 active 单层/两层分别会漏掉未来 grant 的 `×2/×4`，ordered future Block event 也必须逐事件保留原生顺序。
- Malaise native: `Malaise.OnPlay` 令 `powerAmount = ResolveEnergyXValue()`，升级后 `+1`；先 `PowerCmd.Apply<StrengthPower>(-powerAmount)`，await 完成后再 `PowerCmd.Apply<WeakPower>(powerAmount)`。Strength/Weak 均为 `Counter`；Strength 允许负数，Weak 的普通 multiplier 为 `0.75` 且层数只表示持续时间。
- Intent arithmetic: `AttackIntent.GetSingleDamage(...)` 从 `DamageCalc` 出发，调用原生 `Hook.ModifyDamage` 的 additive → multiplicative → cap 链，再逐击转整数并以 `0` 为下限；`SingleAttackIntent.GetTotalDamage(...)` 返回该逐击值，`MultiAttackIntent.GetTotalDamage(...)` 返回逐击值 × `Repeats`。原生 Intent label 与 Damage Forecast 使用同一 `GetSingleDamage/GetTotalDamage` 入口。
- No duplicate correction: Damage Forecast 先读 `GetTotalDamage(...)`；仅在需要逐击粒度时再读 `GetSingleDamage(...)` 并验证 `single × repeats == total`，不会把两者重复相加。因此没有证据支持手写 Strength/Weak 减法或再次乘 Weak。
- Refresh gap: 原生 `CardModel.OnPlayWrapper` 在 `Malaise.OnPlay` 前先把卡移出手牌；当前 Mod 会在 hand contents change 时刷新，但没有在 `Hook.AfterCardPlayed` 或最终 Power application 后确定性刷新本机 HUD。静态证据证明存在 final-state refresh 缺口；原生 UI 是否偶然触发额外本机血条刷新仍为 runtime `Unknown`。

| 最小案例 | 责任边界 | SM-0 结论 |
| --- | --- | --- |
| Shadowmeld 无 / 已有当前 Block | 原生最终 `Creature.Block` | 当前读取保持不变，不乘算 |
| Shadowmeld 1 层 / 未来 Block grant | `Hook.ModifyBlock` | 每个 eligible grant `×2`；当前 reader 漏算 |
| Shadowmeld 2 层 / 未来 Block grant | `Hook.ModifyBlock` | 每个 eligible grant `×4`；当前 reader 漏算 |
| Malaise 后单击 Intent | 原生 `GetTotalDamage` | 同状态下与原生 label 同源；不加手写补偿 |
| Malaise 后多段 Intent | 原生逐击取整 × repeats | 必须保留逐击粒度，不能总量后再取整 |
| Strength + Weak 已同时存在 | 原生 additive/multiplicative hooks | 更新后的单一 Power 实例各生效一次，不重复减法或重复乘算 |

- Disposition: 保留同一 master task card，但后续拆成两个独立授权 Gate：Shadowmeld 分支先做 fail-closed multiplier/ordered-event contracts 与最小实现；Malaise 分支只做 final-state refresh/snapshot contract 与运行复现，不做伤害算术补偿。两个分支不得互相推断批准。

### User Runtime Reproduction Input — 2026-08-01

- Evidence level: 用户运行时观察；未由 Codex 启动游戏或独立复现，不记录截图副本或临时图片路径。
- Observed: “凌虐”结算前敌人原生攻击 Intent 为 `8`；结算后原生 Intent 已显示 `0`，Damage Forecast 的 `-N`、正数 `N` 与可格挡明细仍保留旧值 `8`。
- Version / scene: 游戏文件自报 `v0.110.1`，画面为多人战斗；当前证据不足以断言该问题仅发生于此版本、仅发生于多人或与房主身份有关。
- Working hypothesis: 与 SM-0 已确认的 hand-contents-change 早于卡牌最终效果、且缺少确定性 post-card final-state refresh 的缺口一致；这只是待验证假设，不把刷新缺口提前写成已确认根因。
- Routing: 将原 `Malaise final-refresh` 候选分支泛化为窄范围 `post-card final-Intent refresh`，至少覆盖 Malaise 与“凌虐”；多人敌人死亡/结束回合快照仍由独立 MP 任务负责。
- Approval boundary: 只登记复现并规范化后续范围；未批准新的只读 Gate、代码修改、构建、安装、游戏启动、Git、发布或 Workshop 动作。

## Gate SM-1S — Shadowmeld Future-Block Contract Specification

Goal: 在不修改生产逻辑的前提下，把 Shadowmeld 对“已经落入当前 Block”与“尚未发生的 future Block grant”的时间边界写成可执行的 test-only contract matrix。
Allowed: 更新本任务卡；新增独立 contract fixture/cases；在 contract runner 做一处最小注册；运行限定的 headless contract tests。不得修改 `src/`、安装、启动游戏或执行 Git 写操作。
Contracts:

- `localCreature.Block` 代表已经结算的原生最终当前 Block；Shadowmeld 生效、叠层或移除均不得追溯乘算。
- 只有发生在 Shadowmeld 生效之后、`AfterSideTurnEnd` 移除之前的每笔 future grant 才按 grant 发生时的层数应用 `2^Amount`；一层 `×2`、两层 `×4`，且保持原生事件顺序。
- 覆甲（`PlatingPower`）与奥利哈刚（`Orichalcum`）均作为独立 future grant 覆盖；奥利哈刚仍先遵守触发时零 Block 的原生前置条件，Shadowmeld 不改变触发资格。
- 覆甲与奥利哈刚同时存在但尚未证明原生 listener 先后顺序时，组合结果保持 `Unknown`，不得把基础值先聚合后猜测。
- 不支持的 Power 形状、owner 不匹配、未知时间窗或未知 grant 资格保持 `Unknown`；不得显示部分可信结果。

Pass: test-only matrix 覆盖 absent、单层、两层、生效前当前 Block、生效后 future grant、叠层不追溯、移除后、覆甲、奥利哈刚、组合顺序 Unknown、逐事件顺序与 unsupported/owner/timing Unknown；限定 headless contracts 全部通过。
Stop: 记录 SM-1S 增量证据并停止；生产 policy/reader 接入、构建、安装、游戏 runtime、Malaise/post-card refresh 与 Git checkpoint 均需后续单独批准。

### SM-1S Checkpoint — 2026-08-01

- Result: `Complete / Test-only Contract Verified`；新增 `SM-001–SM-018`，没有修改 `src/` 或接入生产 reader。
- Changed: 新增 `tests/DamageForecast.ContractTests/ShadowmeldFutureBlockContractCases.cs`（SHA256 `D223BE41BC54E19E046E7DB811662FE2FA3B292ACC84ACD7AC4DFD76DC67575E`）；在 `Program.cs` 现有 contract registration 区新增一行 `ShadowmeldFutureBlockContractCases.Create()`；本卡登记 Gate 与证据。
- Contract matrix: absent future grant 保持基础值；当前 Block 不重乘；一层/两层 future grant 分别 `×2/×4`；第二层不追溯；移除后不乘；覆甲与奥利哈刚独立覆盖；奥利哈刚资格不满足时不产生 grant；组合 listener 顺序未证实时整条 future-Block 结果 `Unknown`；Feel No Pain 倍率后仍只保护后续伤害；unsupported、owner mismatch、未知时间窗/资格与非法 active 层数均 fail closed。
- Verified: `C:\sts2\dotnet\dotnet.exe run --project .\tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release`；`SUMMARY discovered=504 passed=504 failed=0 skipped=0`，其中 `SM-001–SM-018` 为 `18/18 PASS`。
- Reference boundary: 本次编译引用当前已安装 `v0.110.1 / db5d3552` 的 `sts2.dll`（SHA256 `7C446EFABF80614C429B5088E87101423AA5BB4C04FC3E73393261F6E6D404FD`）；这些是 game-neutral test-only contracts，不构成 v0.110.1 原生机制复审或游戏内 `RuntimeVerified`。
- Preserved: `localCreature.Block` 继续视为原生最终当前 Block；未修改生产逻辑、配置、安装目录或游戏状态；未启动游戏，未执行 Git 写操作，也未触碰 Malaise/post-card refresh 分支。
- Risks / Pending: 当前 evaluator 是测试侧规格 oracle，尚未验证任何 production adapter；后续生产 Gate 必须让同一 matrix 直接约束 production policy/reader。覆甲＋奥利哈刚的原生 listener 先后顺序仍未证明，组合保持 `Unknown`；Git checkpoint 仍待单独授权。

## Gate SM-1S-R1 — Orichalcum End-Turn Eligibility Contract Correction

Reason: 用户澄清奥利哈刚的资格在点击结束回合时由当时的 Block 快照决定；若该时刻为 `0`，后续覆甲先获得 Block 不会撤销奥利哈刚。该输入尚未由 Codex 独立启动游戏复现，不升级为广义 `RuntimeVerified`。
Allowed: 只更新本卡与现有 Shadowmeld test-only contracts，并运行 headless contract runner；不得修改 `src/`、安装、启动游戏、执行 Git 写操作或进入生产 Gate。
Correction:

- `SM-010` 不再因覆甲与奥利哈刚 listener 顺序返回 `Unknown`。
- 结束回合快照 Block 为 `0` 时，覆甲先产生自己的 future grant，奥利哈刚仍产生已在边界确定资格的 future grant；两笔 grant 分别应用 Shadowmeld 倍率并保留各自原生顺序。
- 结束回合快照 Block 非 `0` 时，奥利哈刚保持不触发；Shadowmeld 不改变该资格判定。
- 原 SM-1S 中“组合 listener 顺序未证实即 `Unknown`”的合同结论由本 Gate 明确勘误；其他 `Unknown`、当前 Block 不重乘和逐事件顺序边界不变。

Pass: `SM-010` 在一层 Shadowmeld、结束回合快照 Block 为 `0`、覆甲基础 `3`、奥利哈刚基础 `6` 时，依次保留 `PlatingPower=6` 与 `Orichalcum=12`；`SM-001–SM-018` 与整套 headless contracts 全部通过。
Stop: 记录 SM-1S-R1 增量证据并停止；生产实现仍需单独批准。

### SM-1S-R1 Checkpoint — 2026-08-01

- Result: `Complete / Contract Correction Verified`；原 SM-1S 的覆甲＋奥利哈刚组合 `Unknown` 结论已由本 Gate 明确勘误。
- Changed: `SM-009` 名称明确资格来自结束回合 Block 快照；`SM-010` 改为结束回合快照 Block 为 `0` 时，按原生顺序保留 `PlatingPower 3 × 2 = 6` 与 `Orichalcum 6 × 2 = 12`。修订后 `ShadowmeldFutureBlockContractCases.cs` SHA256 为 `92F237D92A116310688523E84E1984FF78744AE045E0C4406417A067DF4623A1`。
- Verified: `C:\sts2\dotnet\dotnet.exe run --project .\tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release`；`SUMMARY discovered=504 passed=504 failed=0 skipped=0`，`SM-001–SM-018` 全部 PASS。
- Evidence boundary: 资格语义来自用户运行行为澄清；本 Gate 未由 Codex 启动游戏独立复现，因此只修正合同，不声明广义 `RuntimeVerified`。
- Preserved: 当前 Block 不重乘、逐 grant 倍率、事件顺序、unsupported/owner/timing/eligibility fail-closed 及奥利哈刚在结束回合快照非 `0` 时不触发均保持不变。
- Production / external state: 未修改 `src/`、`Program.cs` registration、配置或安装目录；未启动游戏、执行 Git 写操作、处理 Malaise/post-card refresh 或进入生产实现。
- Risks / Pending: 当前仍是测试侧规格 oracle；后续 production Gate 必须直接受同一 matrix 约束，并显式保留“结束回合边界确定奥利哈刚资格，覆甲后续 grant 不撤销资格”的数据流。Git checkpoint 仍待单独授权。

## Gate SM-1P — Shadowmeld Future-Block Production Integration

Goal: 将 `SM-001–SM-018` 的时间窗、资格、倍率与 fail-closed 规则落入统一生产 policy/native adapter，并接入当前已支持的 future Power/Orb/Relic Block readers；不得修改已经写入 `localCreature.Block` 的当前 Block。
Allowed: 新增一个窄范围 `Combat` production policy/adapter 文件；修改 `VerifiedPreAttackBlockReader.cs`、`VerifiedEtherealExhaustBlockReader.cs` 与现有 Shadowmeld contracts；更新本卡；运行 headless contracts/build。不得安装、启动游戏、修改配置、处理 Malaise/post-card refresh、执行 Git 写操作、发布或更新 Workshop。
Implementation contract:

- exact native `ShadowmeldPower`/owner/Amount 校验失败、原生 multiplier 与已审计 `2^Amount` 不一致、算术溢出或调用异常时 fail closed。
- 生产 adapter 调用 `ShadowmeldPower.ModifyBlockMultiplicative(...)`，不把通用 `Hook.ModifyBlock(...)` 套到伪造卡牌上下文，也不在各 reader 重复手写倍率。
- `VerifiedPreAttackBlockReader` 先基于结束回合边界的 `localCreature.Block` 分别决定覆甲、奥利哈刚等基础 grant，再逐 grant 应用同一 modifier；覆甲产生的预测 Block 不得反向取消已由边界快照确定的奥利哈刚资格。
- `VerifiedEtherealExhaustBlockReader` 在保持 `UpcomingBlockEvent` source/order 的前提下逐事件应用 modifier；未知时整条已选择的 future-Block 读取返回 `Unknown`。
- 当前 Block、Malaise/Post-card refresh、HUD snapshot、安装与 runtime 均保持不变。

Pass: `SM-001–SM-018` 直接调用生产 policy；当前安装目标上的 headless contracts/build 全部通过；差异仅限本 Gate 文件与任务卡，且不产生 `RuntimeVerified` 声明。
Stop: 记录 SM-1P 增量证据后停止，等待安装/runtime、Malaise/post-card refresh 与 Git checkpoint 的后续独立授权。

### SM-1P Checkpoint — 2026-08-01

- Result: `Complete / Production Integrated / Headless and Build Verified / Runtime Pending`。
- Production policy/native adapter: 新增 `VerifiedShadowmeldFutureBlockModifier.cs`（SHA256 `1EA335E8116799D5F7E5E1F7A3F921F45AB0607DC7ABBDFE797182E1AB2727B6`）。adapter 只接受 exact native `ShadowmeldPower`、owner 匹配与可验证正层数，调用原生 `ModifyBlockMultiplicative(...)`，并要求返回值与已审计 `2^Amount` 一致；异常、漂移、非整数或溢出均返回 `Unknown`。
- Pre-attack integration: `VerifiedPreAttackBlockReader.cs`（SHA256 `FC404241FA6D1CFC828A2E21E5B4C0DD846BD5BA2EA32577E68BC1F91EBC5896`）先从同一 `localCreature.Block` 结束回合边界快照决定奥利哈刚/Fake Orichalcum 资格，再将 Frost 各 orb、Plating 与每笔 relic grant 分别交给统一 modifier；预测出的覆甲 Block 不会反向取消已确定的奥利哈刚资格。
- Ordered-event integration: `VerifiedEtherealExhaustBlockReader.cs`（SHA256 `6818887C4B2018A597330D2FAFB522E6C31F0E517AACBA38E18B42E7F6FD61BC`）逐个变换 Feel No Pain `UpcomingBlockEvent.Amount`，保留 source 与 `NativeExecutionOrder`，任何 modifier Unknown 都使该 future-Block read 整体 Unknown。
- Contracts: `ShadowmeldFutureBlockContractCases.cs`（SHA256 `570D203C52E302F9869BD4D3D2639F0AF1799FC2FA40857EDF461F6624B67A3F`）已删除测试侧 evaluator/types，`SM-001–SM-018` 通过薄 wrapper 直接调用生产 `ShadowmeldFutureBlockPolicy.Evaluate(...)`。
- Verified headless: `C:\sts2\dotnet\dotnet.exe run --project .\tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release`；`SUMMARY discovered=509 passed=509 failed=0 skipped=0`，Shadowmeld `18/18 PASS`。相较上一 Gate 新增的 5 个 `MPD` contracts 来自并行多人诊断工作，本 Gate 未修改其文件或 registration，最终整套状态已共同复核。
- Verified build: `C:\sts2\dotnet\dotnet.exe build .\src\DamageForecast\DamageForecast.csproj -c Release --no-restore`；成功，`0 warning / 0 error`。生成物仅位于仓库 `bin/Release`，未安装到游戏目录。
- Target boundary: 编译引用当前已安装 `v0.110.1 / db5d3552` 的 `sts2.dll`（SHA256 `7C446EFABF80614C429B5088E87101423AA5BB4C04FC3E73393261F6E6D404FD`）；headless 没有构造真实 Creature/Power，因此只证明接口兼容与生产 pure policy，不证明游戏内 native adapter 已执行。
- Preserved / out of scope: 没有重乘 `localCreature.Block`；未修改 `LocalIncomingDamageReader.cs`、HUD/snapshot、Malaise/post-card refresh、设置、安装目录或 Workshop；未启动游戏、执行 Git 写操作、push/tag 或发布。
- Risks / Pending: 需要后续单独批准安装/runtime Gate，人工覆盖 Shadowmeld 无/一层/两层、生效前当前 Block、生效后 Frost/覆甲/奥利哈刚/Feel No Pain、覆甲＋奥利哈刚结束回合快照以及移除后不乘。新 adapter、合同与任务卡仍未跟踪，两个 reader 为工作树修改；Git checkpoint 未授权。

## Gate SM-1R — Local Install and User Runtime Handoff

Goal: 从当前共享工作树构建可运行的两文件本地 Mod，使用带 Plan/hash/identity/backup 门禁的安装脚本替换现有 `damage-forecast` 本地安装，然后停止并由用户亲自启动游戏执行 Shadowmeld runtime matrix。
Allowed: 重新运行 headless/build/publish；在新的 `work/publish` 子目录生成 staging；运行 `Install-LocalMod.ps1` Plan 与经审阅哈希绑定的 `-Execute` 本地安装；读取安装身份、哈希、备份与 ledger；更新本卡。不得由 Codex 启动游戏、修改配置、执行 Git 写操作、发布、更新 Workshop 或处理 Malaise/post-card refresh。
Artifact boundary: staging 与安装目录必须严格只有 `damage-forecast.dll` 和 `damage-forecast.json`；当前工作树包含并行 HUD/多人改动，安装 artifact 会绑定安装时的完整当前源状态，不伪装成仅含 Shadowmeld 的隔离产物。
Safety: 游戏进程必须关闭；Plan 后 staging 与 active DLL/manifest 哈希必须在 Execute 前保持不变；现有 active 安装移入 loader root 外的可恢复备份；安装后只允许一个 target identity，legacy identity 为 `0`。
Pass: publish、Plan、Execute 与安装后 identity/hash 验证全部通过；记录 staging/installed SHA256、备份/ledger 与当前目标版本；状态写为 `Installed / Runtime Verification Pending`，不得提前写 `RuntimeVerified`。
Stop: 安装核验后立即停止，给出人工测试步骤、预期结果和反馈项；等待用户测试结果后再继续。

### SM-1R Install Checkpoint — 2026-08-01

- Result: `Installed / User Runtime Verification Pending`；Codex 未启动游戏，未声明 `RuntimeVerified`。
- Artifact: 从当前共享工作树 publish 到 `work/publish/sm1r-v0.110.1-db5d3552-20260801/damage-forecast`，目录严格只有 `damage-forecast.dll`（429568 bytes，SHA256 `C9F057BBA719807EC34263A1FEB353FE161B07F1B01D34BA01E308C2167CFED3`）和 `damage-forecast.json`（371 bytes，SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`）。该产物绑定安装时完整当前源状态，包含并行 HUD/多人改动，不伪装成仅含 Shadowmeld 的隔离产物。
- Target: 当前游戏为 `v0.110.1 / db5d3552`，`sts2.dll` SHA256 `7C446EFABF80614C429B5088E87101423AA5BB4C04FC3E73393261F6E6D404FD`；manifest 为 `damage-forecast` v0.3.0。
- Guarded Plan: transaction `SM1R-20260801T032600Z`；游戏关闭，action=`target-upgrade`，target identity=`1`、legacy=`0`、orphan=`0`；旧 active DLL SHA256 `D1FC7619CDE78BF65FDD446DC10F68D28A224E8F4EDA9A34688B699F4B5A1FFC`，manifest SHA256 与 staging 一致。
- Execute/install: 成功激活到 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`；安装目录仍严格两文件，大小与 SHA256 均与 staging 一致。
- Recovery: 旧两文件安装已移至 loader root 外的 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\SM1R-20260801T032600Z-damage-forecast-v0.3.0`；ledger 为同级 `SM1R-20260801T032600Z-install-ledger.json`，记录 action、前后哈希与备份路径。
- Post-verify: 再次 Plan 返回 action=`target-already-current`，target=`1`、legacy=`0`、orphan=`0`；游戏进程仍为 `0`。未修改配置，未执行 Git 写操作、发布或 Workshop 操作，Malaise/post-card refresh 未触碰。
- Manual runtime priority: 先验证“已有 Block 后打出一层 Shadowmeld 不追溯翻倍”；再以回合结束边界 Block=`0` 验证覆甲先加、奥利哈刚仍按边界资格生效，二者每笔均为 `×2`；最后验证两层为每笔未来 grant `×4`。Forecast 与实际获得 Block 应一致，已有当前 Block 始终不被再次乘算。
- Additional runtime coverage: Frost orb 与 Feel No Pain 的每笔未来 grant 应分别乘 `×2/×4`；Feel No Pain 只保护其后可格挡伤害，不保护更早伤害或直接失血；Shadowmeld 在下一回合移除后不再影响 Block。

### SM-1R Runtime Failure Report — 2026-08-01

- User-observed case: 当前 Block=`8`，随后打出一层 Shadowmeld，再获得覆甲基础值 `6`；敌方原生攻击 Intent=`26`。HUD 显示 Forecast=`-12`，用户确认正确预期为只失去 `6` HP。
- Arithmetic diagnosis: 错误值严格等于 `26 - 8 - 6 = 12`，说明最终有效 Block 链使用了当前 Block `8` 与未乘算的覆甲基础值 `6`；正确链应为 `26 - 8 - (6 × 2) = 6`。这不是当前 Block 被追溯翻倍，而是 Shadowmeld 倍率未进入最终覆甲抵扣结果。
- Contract gap: `SM-001–SM-018` 证明 production pure policy 的单笔 grant 倍率，但没有覆盖真实 reader/snapshot 到 `LocalDamageForecast` 的组合回归：已有当前 Block、Shadowmeld 已生效、之后新增 PlatingPower、再以两者共同抵扣攻击。因此 headless `509/509 PASS` 不能覆盖本次 runtime failure。
- Root-cause boundary: 当前证据尚不能区分 `VerifiedShadowmeldFutureBlockModifier.Read(...)` 在 runtime 把已存在的 Shadowmeld 误判为 absent，还是 HUD refresh/snapshot 在卡牌最终 Power 状态稳定前捕获了旧值；两者都可产生基础覆甲 `6` 被使用的结果。不得在缺少观测证据时把其中之一写成已确认根因。
- Verdict: `SM-1R Runtime Failed`；撤销任何潜在的 runtime 正确性推断，已安装产物保持原位供后续受控复现。Codex 未启动游戏、未修改生产代码、未重新构建/安装，未执行 Git、发布或 Workshop 操作。

## Gate SM-1F — Runtime-shaped Regression and Minimal Fix

Goal: 以本次 `26 / current Block 8 / Shadowmeld 1 / Plating 6` 案例建立失败回归，观测并区分 Power 读取与 post-card final-state refresh 边界，然后只修正被证据确认的最小路径。
Allowed after explicit approval: 修改本卡、相关 contracts、Shadowmeld future-Block adapter/reader 与必要的最终卡牌结算刷新点；运行 headless/build。安装与用户 runtime 复测仍需在实现通过后另行确认，不自动授权。
Pass: regression 证明最终 HP loss 为 `6`，同时保持无 Shadowmeld 时为 `12`、已有当前 Block 不追溯乘算、两层 future grant `×4`、覆甲与奥利哈刚结束回合边界顺序，以及无关预测不变。
Stop: 记录实现与 headless/build 证据后停止；不得自行启动游戏、安装、Git、发布或 Workshop。

### SM-1F Checkpoint — 2026-08-01

- Result: `Complete / Root Cause Confirmed / Headless and Build Verified / Runtime Pending`。
- Native boundary: current `v0.110.1 / db5d3552` IL 证明 `Creature.GetPower<T>()` 在 `_powers` 上以 `isinst T` 查找；`ShadowmeldPower.ModifyBlockMultiplicative(...)` 对 owner 返回 `2^Amount`。没有发现类型查找会把已存在 exact native ShadowmeldPower 静默降为基础倍率的证据。
- Refresh order: `CardModel.OnPlayWrapper(...)` 在卡牌离开手牌后才执行并 await `OnPlay(...)`、`Hook.AfterCardPlayed(...)` 与 cleanup；`PlayCardAction.ExecuteAction(...)` await 整个 wrapper 后才完成。现有 `ActionExecutor.AfterActionExecuted` 订阅在 `LocalReadyWaiting` 才刷新，Live 阶段只采集诊断，因此 hand-change 早期快照没有确定性的 post-card final-state 替换。这与用户 runtime 错值严格使用基础覆甲 `6` 的证据一致，确认为本次最小修复边界。
- Failing regression: 新增 `SM-019–SM-025` 后，修复前完整 headless 为 `SUMMARY discovered=523 passed=522 failed=1 skipped=0`；唯一失败 `SM-022 Live.CompletedPlayCard_RefreshesFinalState`，actual=`false`。组合算术 `current Block 8 + Shadowmeld 1 × Plating 6` 已单独证明应以 effective Block `20` 抵扣 attack `26`，结果 HP loss=`6`；无 Shadowmeld 保持 `12`，两层 future grant 保持 `×4`。
- Minimal production fix: 新增 `ForecastActionRefreshPolicy.cs`（SHA256 `43129DA5B012619799002114F40B26FBBA05D6F3FF8F19A670F9C8AA3E96FA85`）；仅允许 `Live + completed PlayCardAction` 或既有 `LocalReadyWaiting + any action` 刷新。`Frozen + card` 与 `Live + non-card` 均不刷新，避免覆盖 committed snapshot 或扩大到所有动作。
- Runtime wiring: `ForecastRefreshPatch.cs`（共享文件最终 SHA256 `6F20DEDF79155F5C2DC95F72064275C243DFB1E1390D91328541CBDC8F84E8B1`）在 `AfterActionExecuted` 以 `action is PlayCardAction` 调用该 policy；Live 卡牌使用 `action-complete-card-live` trigger。文件同时含已存在及本 Gate 执行期间继续进入的 HUD/多人并行修改，本 Gate 只拥有该 action-completion routing hunk，不声明拥有整文件；检测到并行哈希变化后已针对最终共享文件重新运行完整 headless 与普通 build。
- Contracts: `ShadowmeldRuntimeRegressionContractCases.cs`（SHA256 `F0DF1F4CDD3D7AF4184B2D11673F361A8A564CC1FC05822A217E993F7CF190E2`）覆盖 SM-019–SM-025；`MultiplayerForecastLifecycleDiagnosticsContractCases.cs`（SHA256 `0C5C6052D9E706C716729CFDB1923A0F44E3CD100DD11486F75A7A4E36B2DAA4`）更新 MPD-003，明确 Waiting、Live-card、Live-non-card 与 Frozen 分流；`Program.cs` 仅追加新 suite registration 到当前共享 registration 集。
- Verified headless: `C:\sts2\dotnet\dotnet.exe run --project .\tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release`；最终 `SUMMARY discovered=523 passed=523 failed=0 skipped=0`。
- Verified build: `C:\sts2\dotnet\dotnet.exe build .\src\DamageForecast\DamageForecast.csproj -c Release --no-restore`；成功，`0 warning / 0 error`。
- Preserved boundaries: 当前 Block 仍不追溯乘算，覆甲/奥利哈刚资格与逐 grant Shadowmeld 倍率未改；非卡牌 Live action、Frozen snapshot、Malaise 算术、配置与 Workshop 未改。未安装新 DLL、未启动游戏、未执行 Git 写操作、push/tag 或发布；仍需单独批准安装和用户 runtime 复测。

### SM-1F + BLP-4H Merged Install — 2026-08-01

- Coordination override: 用户要求并行 BLP-4H 只完成代码/测试、不得安装其隔离 DLL；SM-1F 完成后必须从当前共享工作树
  统一 rebuild，确认同一 DLL 同时包含两项改动，并只安装一次。BLP-4H 隔离 DLL 从未安装。
- Shared verification before build: 当前完整 harness 为 `532/532`；stable/beta guardrail 各为 `532/532` contracts，
  Release build 0 warning / 0 error，最终 `QUALITY_GATE PASS`。这些结果已同时包含 BLP-4H 与 SM-1F 当前源码/合同。
- Shared-tree candidate: 针对本机 `v0.110.1 / db5d3552` 从当前共享工作树生成
  `work/publish/stable/damage-forecast-blp4h-sm1f-merged`。候选仅含 manifest/DLL；manifest SHA256
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256
  `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50`。
- Cross-feature proof: DLL 同时含 SM-1F 的 `ForecastActionRefreshPolicy`、`PlayCardAction`、
  `VerifiedShadowmeldFutureBlockModifier` / policy symbols，以及 BLP-4H 的两个定项百分比和 BaseLib baseline symbols；
  构建前后两边六个关键源码哈希一致。
- Single install / recovery: 安装前 Plan 为 `target-upgrade`、`gameRunning=false`、单一 target identity、无 legacy/orphan；
  以 staging/active 四个精确 SHA256 只执行一次安装。活动目录现恰好两文件并与 staging 哈希完全一致，活动 DLL 内复核两组
  symbols 均存在；上一版 BLP-4G 已完整备份到 Loader 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4h-sm1f-merged-20260801-damage-forecast-v0.3.0`，
  ledger 为同级 `blp4h-sm1f-merged-20260801-install-ledger.json`。post-install Plan 为 `target-already-current`，游戏未运行。
- Runtime boundary: 静态、合同、构建与安装均不能替代游戏内复测；当前保持 RuntimeNotVerified。Codex 未启动游戏、未执行
  Git 写操作、push/tag、发布或 Workshop。

### SM-1F User Runtime Verification — 2026-08-01

- Result: `RuntimeVerified / Case-specific`。用户在当前合并 DLL 上复测 `enemy attack 26 / current Block 8 / Shadowmeld 1 / Plating 6`，Forecast 正确显示只失去 `6` HP；该结果验证 SM-1F 的 post-card final-state refresh 与“当前 Block 不追溯乘算、之后覆甲 grant 乘 `×2`”组合链。
- Artifact binding: 保持已安装合并 DLL 原位，SHA256 `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50`；未重新 build、install 或修改任何游戏文件。
- Scope boundary: 本次 `RuntimeVerified` 只覆盖上述 SM-1F 核心回归，不自动扩大为两层、Frost、Feel No Pain、覆甲＋奥利哈刚或所有版本/多人环境的广义证明。
- Independent BLP-4H result: 用户同时反馈两个英文关闭态仍碰箭头；该现象由 BaseLib 本地化任务先做只读原因检查，不属于 SM-1F 回归，也不撤销本次 Shadowmeld runtime 通过。
- Preserved: 未启动游戏、未执行 Git 写操作、push/tag、release 或 Workshop；Shadowmeld 分支在此停止。

## Final Closure — 2026-08-02

Result: Shadowmeld future-Block 修正已完成；`26 / current Block 8 / Shadowmeld 1 / Plating 6 => -6` 已在 SM-1F + BLP-4H 合并 DLL `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50` 上由用户验证通过。Malaise/post-card final-Intent 分支未执行并明确 `Deferred`，不伪装成已完成或已验证。

Current state: 当前活动产物已由独立 BaseLib 任务更新为 BLP-4I + SM-1F 合并 DLL，SHA256 `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`；该任务卡记录最终 DLL 继续包含 SM-1F `ForecastActionRefreshPolicy`、`PlayCardAction` 与 `VerifiedShadowmeldFutureBlockModifier` symbols，并通过共享 guardrail。SM-1F 的 case-specific runtime 证据绑定上一合并 DLL，不扩大为当前产物重新执行过该场景，也不扩大为两层、Frost、Feel No Pain、覆甲＋奥利哈刚、多人或跨版本全矩阵证明。BLP-4I 的运行验证与限制由其独立任务卡负责。

Authority: 本任务卡与 `docs/task-notes/README.md` 已同步；没有新增第二份机制 authority，也没有改写历史 checkpoint。

Repository: `Closed / Local Git checkpoint`。Shadowmeld 自有源码、contracts、精确共享 hunk、本卡与 README 路由已在包含本收口记录的本地 commit 中形成可追溯 checkpoint；其他共享 dirty changes 未纳入。本轮未执行 build、install、游戏启动、游戏文件修改、push/tag、release 或 Workshop。

## Completion and closure requirements

- Shadowmeld 修正若需要实施，必须证明当前 Block 不重复翻倍，未来 Block 按原生顺序和适用范围处理。
- Post-card final-Intent 分支只有在原生 Intent API 与实际游戏 Intent 不一致时才增加补偿；否则保留原生读取并补足最终结算刷新/contract 证据，不增加卡牌名专用算术。
- Headless 验证必须覆盖无相关机制时的不变性，以及 Malaise、“凌虐”、单击/多段、`8 → 0` 零伤害下限和已支持未来 Block 的回归。
- 如更新游戏文件，执行 Session 必须停止，给出人工测试步骤、预期结果和反馈项，等待用户测试后再继续。
- 最终按当前收口标准更新必要 authority、形成可追溯 checkpoint；push、tag、发布和 Workshop 仍需单独授权。

读取本卡，核对 Current Control，只执行已批准的下一 Gate，完成后停止。
