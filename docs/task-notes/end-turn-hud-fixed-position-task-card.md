# Damage Forecast — 结束回合按钮 HUD 固定位置修复

日期：2026-08-02

Type: Small Combat UI Lifecycle Repair
Area: Combat UI
Priority Tag: P2
Queue: Now

## Current Control

Classification: CLOSED_TASK
State: Closed
Last completed: ETF-1R — RuntimeVerified / Repository Checkpoint
Next: None；后续新缺陷需另行登记和授权
Approved: ETF-1 源码、contracts、三目标构建、受保护安装、用户运行验证与任务自有本地 Git checkpoint 已批准；Codex 游戏启动、push、tag、发布和 Workshop 未批准
Evidence: `Final Closure — 2026-08-02`
Repository: 本地 ETF-1 implementation / contract / runtime authority checkpoint 由本收口提交形成；未 push 或 tag

## Goal

点击结束回合前，HUD 继续跟随原生结束回合按钮；点击被接受、原生按钮开始下降后，HUD 固定在点击前的屏幕位置，不随按钮下降，同时继续遵守现有隐藏、恢复和清理规则。

## ETF-0 findings and ETF-1 result

- 活跃 HUD 根节点属于 `NEndTurnButton`，因此会继承按钮动画。
- 冻结 HUD 根节点属于 `NCombatUi`；现有设计本就试图在点击时切换到稳定层。
- ETF-0 确认旧 `FreezeEndTurnAnchor(...)` 用冻结根调用仅接受按钮直属根的 `TryResolveEndTurnButton(...)`；ETF-1 已改为从活跃根捕获锚点并转换到固定根坐标。
- ETF-1 已补充跨父层坐标转换、按钮下降后位置不变、等待态刷新和冻结层完整隐藏/恢复合同；证据见下文。
- HDO-1 已安装、等待用户运行验证；其默认顺序改动必须保留，本任务不重复实现或代替其收口。

## Scope and boundaries

Included:
- 在原生下降动画前，把按钮视觉锚点转换到 `NCombatUi` 固定层坐标，并在成功后隐藏活跃层。
- 等待/冻结期间允许数值刷新，但位置只能使用已捕获锚点；恢复行动、新回合或战斗结束时清理固定层。
- 补充最小合同，覆盖坐标交接、按钮移动、数值刷新不改位置以及覆盖页隐藏/恢复。

Excluded:
- 不把 HUD 从战斗开始就永久移到 `NCombatUi`，不修改原生按钮动画。
- 不改预测算法、数值顺序、placement、间距、字体、颜色或分辨率常量。
- 不处理 Debug Trace、多人敌人死亡快照、Timeline/架构或其他 dirty worktree 内容。

Preserved: 点击前继续跟随按钮；地图、牌堆、设置等覆盖页隐藏两层；临时遮挡恢复固定位置；永久失效、取消结束、新回合、禁用 HUD 或战斗结束按现有生命周期清理。

## First Gate — ETF-1

Goal: 以最小改动完成活跃层到固定层的事务性交接。
Allowed: 读取和修改精确相关的 HUD 锚点/冻结源码与合同；运行相关 contracts 和 current/stable/beta 仓库内构建。
Deliverable: 仅在锚点转换和快照复制均成功后显示固定层；交接失败时 fail closed，不让 HUD 跟随按钮下降。
Verification: 证明交接前后画布位置相等；按钮模拟下移后固定层不移动；等待态更新文字不改位置；取消/新回合恢复；覆盖页隐藏两层并正确恢复；缩放、平移和画布变换下不依赖固定分辨率。
Pass: 新增合同及相关既有合同通过，要求的构建通过，任务外 dirty changes 未被改写。
Stop: 回填 ETF-1 证据后停止；不安装、不修改游戏文件、不启动游戏、不执行 Git、发布或 Workshop 操作。

### ETF-1 Evidence — 2026-08-02
- Result: Complete for approved non-runtime scope.
- Changed: 从按钮直属活跃根捕获原生按钮视觉锚点，经实际 canvas transform 转换到 `NCombatUi` 固定根；仅在根、坐标转换和可见快照复制全部成功时提交固定层，失败时持续抑制活跃层直到恢复生命周期。
- Changed: 覆盖页或不可显示状态同时隐藏活跃层和固定层；临时覆盖不清除已捕获锚点，关闭后仍选择固定层恢复。
- Verified: `HF-001..HF-022` 22/22、`LC-001..LC-019` 19/19、`CV-001..CV-010` 10/10 通过；新增 `HF-018..HF-022` 覆盖事务门、缩放/平移转换、按钮下移、等待态文字刷新中心线及双层隐藏/恢复。
- Verified: current Release、stable `v0.107.1`、beta `v0.109.0` 均为 0 warnings / 0 errors；stable/beta publish tree 各仅含 DLL/JSON、产物一致，DLL SHA256 `E25A64C18977B7F8A2B2F19A0FEB6CED9386DF961C4ED426FECCBBF930CF56BF`。
- Full-suite note: `548 discovered / 544 passed / 4 failed`；失败仅为任务外 `IU-007`、`IU-009`、`IU-010`、`C2-018` 安装/回滚夹具，当前游戏进程触发安全拒绝；未关闭游戏或绕过保护。
- Preserved: HDO-1 默认顺序、预测算法、placement/间距/字体/颜色/分辨率常量及其他 dirty worktree 内容未由 ETF-1 改写。
- Risks / Pending at ETF-1 stop: 当时为 `RuntimeNotVerified / NotInstalled`；后续安装增量见 `ETF-1I`，游戏启动、Git、发布和 Workshop 仍未执行。

### ETF-1I Installed Checkpoint — 2026-08-02
- Authority: 用户明确授权“安装吧”；安装前后 `SlayTheSpire2` 进程均为 0。本 Gate 仅扩张到本机事务安装，不包含 Codex 启动游戏、Git、发布或 Workshop。
- Isolation: 从当前活动版本对应 checkpoint `f89d863` 隔离导出源码，仅叠加 ETF-1 的 `ForecastRefreshPatch.cs`、`HudAnchorResolver.cs`、`HudLayoutEngine.cs` 与 `DamageForecastHudSurfacePolicy.cs`；共享 Timeline、多人诊断、Debug Trace 等 dirty work 未进入安装候选。
- Candidate: 绑定本机 `v0.110.1 / db5d3552` 的 Release publish，严格只有 DLL/manifest；DLL SHA256 `381B8CAD3FE9FC96203E31247AAF193BB9E414DD159F1F352C6E337A043D67E1`，manifest 保留活动文件精确字节，SHA256 `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`。
- Contracts: 隔离候选 `HF-001..HF-022` 全部通过；总计 `437 discovered / 433 passed / 4 failed`，仅有与 HDO 隔离基线一致的任务外 `PT-001..PT-004` publish-tree 夹具失败，没有新增失败。
- Install: transaction `20260802T132608101Z` 执行 `target-upgrade`；安装后只读 Plan 返回 `target-already-current`，活动目标为唯一 `damage-forecast v0.3.0`，活动两文件 SHA256 与 staging 完全一致，legacy/orphan 均为 0。
- Recovery: 前一活动版本备份位于 Loader 扫描目录外的 `20260802T132608101Z-damage-forecast-v0.3.0`；旧 DLL SHA256 `3DFD5EE03D553018B473EA3CCB9F1D23A2A6888A4E8C4F6A6E1AD4A2BB294AB7`，ledger 为 `20260802T132608101Z-install-ledger.json`。
- Config boundary: `configRollbackAction=not-applicable`，没有配置迁移、重置或覆盖操作。
- Runtime handoff: 用户自行启动游戏，验证按钮下降后 HUD 固定、等待态数值刷新不改变锚点、覆盖页隐藏/恢复、取消结束和新回合恢复。
- Boundary: `RuntimeNotVerified`；未启动游戏、未执行 Git、未 push/tag、未发布或更新 Workshop。

### ETF-1R Runtime Verified — 2026-08-02
- Authority: 用户在已安装 DLL SHA256 `381B8CAD3FE9FC96203E31247AAF193BB9E414DD159F1F352C6E337A043D67E1` 上按交接步骤测试后明确反馈“正常，可以收口了”。
- RuntimeVerified: 点击前 HUD 跟随结束回合按钮；点击被接受、按钮下降后 HUD 保持点击前位置；等待态数值刷新不改变固定锚点；覆盖页隐藏并在关闭后恢复；取消结束与新回合恢复正常。
- Scope: 该证据绑定本机 current `v0.110.1 / db5d3552`、本地玩家结束回合按钮 HUD 与上述场景；不扩张为多人敌人死亡快照、Debug Trace、Timeline、其他 placement 或未报告矩阵的运行证明。
- Boundary: Codex 未启动游戏；未 push、tag、发布或更新 Workshop。

## Final Closure — 2026-08-02

Result: 结束回合 HUD 已在原生按钮退场前事务性交接到 `NCombatUi` 固定层，并由用户确认运行正常。
Current state: current `v0.110.1` 安装产物已 `RuntimeVerified`；可恢复备份保留在 Loader 扫描目录外。
Authority: 本任务卡、`docs/project-state.md` 与 `docs/task-notes/README.md` 已同步。
Repository: 本地 ETF-1 implementation / contract / runtime authority checkpoint 由包含本记录的提交形成；其他并行 dirty work 未纳入，未 push 或 tag。

## Completion and closure requirements

- 安装必须另行批准；游戏文件更新后立即停止，交给用户按测试步骤验证按钮下降、等待态刷新、覆盖页、取消结束和新回合恢复。
- 用户反馈前不得写 `RuntimeVerified`；HDO-1 与本任务分别记录运行结果，不混写任务 authority。
- 用户确认运行表现后，按当前收口标准同步必要事实；Git checkpoint 需另行批准后才能 `Closed`。

读取 `docs/task-notes/end-turn-hud-fixed-position-task-card.md`，核对 `Current Control`；ETF-1 已关闭，无后续授权中的 Gate。
