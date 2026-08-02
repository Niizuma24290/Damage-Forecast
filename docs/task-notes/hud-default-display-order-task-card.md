# Damage Forecast — HUD 默认数值顺序调整

日期：2026-08-02

Type: Small Default Behavior Change
Area: Combat UI
Touches: Hub UI
Priority Tag: P2
Queue: Now

## Current Control

Classification: CHECKPOINT_TASK
State: Work Complete / HDO-1 Checkpoint Pending
Last completed: HDO-1 — User Runtime Verification
Next: 建立已授权的 HDO-1 本地 Git checkpoint，并登记最终收口
Approved: HDO-1 源码、contracts、仓库内构建验证、本机事务安装、用户运行验证与本地 Git checkpoint；游戏启动、push、tag、发布和 Workshop 未批准
Evidence: `HDO-1 Runtime Verified Checkpoint — 2026-08-02`
Repository: Shared dirty worktree / HDO-1 checkpoint pending on HEAD `e23ea0c`

## Goal

当三个 HUD 组保持当前默认 `HealthBarRight` 位置且同时显示时，默认视觉顺序改为：

```text
血条 | N | -N | 明细
```

其中明细仍是护盾与生命损失组成的一个 composite 组。

## Current facts and unknowns

- `HudLayoutEngine.OrderNumeric(...)` 已支持 `LeftOfExpectedHpLoss` 产生 `N → -N`；明细已固定在数值组之后。
- HDO-1 后 `DamageForecastBaseLibConfig`、`DamageForecastUiSettings` 与 `ResetDefaults()` 默认均为 `LeftOfExpectedHpLoss`。
- 三个 placement 当前默认均为 `HealthBarRight`。
- 既有用户配置可能已显式保存顺序；本任务不得静默覆盖。

## Scope and boundaries

Included:
- 将新配置、未加载配置及“恢复默认”后的顺序改为 `N → -N → 明细`。
- 更新最小 HUD/config contracts，证明默认值、恢复默认和同位置布局顺序。
- 保留设置页现有顺序选项及中英文含义。

Excluded:
- 不改预测算法、数值含义、间距、字体、颜色、锚点或分辨率换算。
- 不改变三个 placement，也不强制迁移或覆盖既有 `DamageForecast.cfg`。
- 不处理 Debug Trace、多人诊断、架构或其他 HUD 问题。

Preserved: 血条左侧镜像与由近及远增长、明细换行/空间不足隐藏、三个组独立 placement、用户手动选择相反顺序的能力。

## First Gate — HDO-1

Goal: 以最小改动更新默认顺序，不重写 `HudLayoutEngine`。
Allowed: 修改相关默认值和既有 HUD/config contracts；运行当前仓库要求的相关 contracts 与构建验证。
Deliverable: 新配置及恢复默认得到 `LeftOfExpectedHpLoss`，默认右侧 cluster 为 `血条 | N | -N | 明细`。
Verification: 覆盖 fresh/static default、`ResetDefaults()`、三组同为 `HealthBarRight`、显式 `RightOfExpectedHpLoss` 仍可用，以及既有配置不被迁移。
Pass: 相关 contracts 和要求的构建检查通过，任务外 diff 未被修改。
Stop: 回填 HDO-1 证据后停止；不安装、不启动游戏、不执行 Git 或发布操作。

### HDO-1 Headless Verification — 2026-08-02

- Result: 新配置静态默认、业务 UI 初始默认和 `ResetDefaults()` 均改为 `LeftOfExpectedHpLoss`；三个 placement 继续默认为 `HealthBarRight`，因此三组同时显示时默认右侧视觉顺序为 `血条 | N | -N | 明细`。
- Changed: 只修改 `DamageForecastBaseLibConfig` 与 `DamageForecastUiSettings` 的三处默认值，并在既有 `HudLayoutContractCases` / `IdentityMigrationContractCases` 增加或调整最小证据；`HudLayoutEngine`、placement、迁移算法和配置 fixture 均未修改。
- Contracts: 隔离快照新增 `HDO-001` / `HDO-002` 均 PASS，既有 `HL-003` / `HL-004` 继续证明两种显式顺序均可用，`HPC-010` 继续证明现有 V2 配置重启不重写。叠加 HDO 后全套为 `426/430`；纯 HEAD 基线为 `424/428`，两者相同的四项失败均为任务外既有 `PT-001..004` publish-tree fixture/root 不一致，HDO 新增两项全部通过且未增加失败。
- Builds: 从当前 HEAD `e23ea0c` 导出隔离快照，仅叠加四个 HDO 文件；current、本仓库 frozen stable `v0.107.1 / 59260271` 与 frozen beta `v0.109.0 / c12f634d` Release build 均为 0 warnings / 0 errors。未执行 publish。
- Existing config boundary: 历史/既有配置 fixture 保持显式 `RightOfExpectedHpLoss`，`DamageForecast.cfg` schema、读取与迁移代码均未修改；已保存值和用户显式选择不会被迁移或覆盖。只有没有已保存值的新配置和用户主动“恢复默认”采用新顺序。
- Preserved: 血条左侧镜像与由近及远增长、明细换行/隐藏、三个独立 placement、显式 `RightOfExpectedHpLoss` 相反顺序选项均保留；Debug Trace、多人诊断、Timeline/架构、BaseLib、Shadowmeld、Workshop 及其他 dirty worktree 内容未改写或清理。
- Boundary: 未安装、未修改游戏目录、未启动游戏、未暂存或提交 Git，未 push/tag、发布或更新 Workshop。等待安装授权后再进行用户运行验证。

### HDO-1 Installed Checkpoint — 2026-08-02

- Authority: 用户明确授权“安装吧”；安装前与安装后 `SlayTheSpire2` 进程数均为 `0`。本 Gate 只扩张到本机事务安装，不包含 Codex 启动游戏、Git、发布或 Workshop。
- Build candidate: 从当前 HEAD `e23ea0c` 导出隔离源码，只覆盖 HDO-1 两个生产源码文件；以本机 current `v0.110.1 / db5d3552` 引用执行 Release build，0 warnings / 0 errors。staging 严格只有 `damage-forecast.dll` 与 `damage-forecast.json`。
- Install: transaction `20260801T183253028Z` 执行 `target-upgrade`；活动 DLL SHA256 `B7148519F6A334F6131844B296089AEB3E7DD695FF72837891ADC84D2A4E1EFB`，manifest SHA256 `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`，安装后只读 Plan 返回 `target-already-current`。
- Recovery: 旧活动版本严格两文件备份位于 Loader 扫描根外的 `20260801T183253028Z-damage-forecast-v0.3.0`；旧 DLL SHA256 `141FD05C9F78AB4C99E618B3C20CD90B3AE9FC9F198B78BFB14A0BE65E017A20`，旧 manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。ledger 为 `20260801T183253028Z-install-ledger.json`。
- Existing config preserved: 当前 `DamageForecast.cfg` 为 20 键，SHA256 `B2C4196496A94F4FF6A534FA3D94AF9E7E55DF47A521A1AB1BEBC165C965B029`，最后写入时间早于本次安装；现有显式值仍为 `RightOfExpectedHpLoss`，三个 placement 仍为 `EndTurnButtonAbove`。安装没有删除、迁移或覆盖该配置，因此启动后不会自动改成新默认。
- Runtime handoff: 若不希望重置其他设置，人工在设置页把三个 placement 选为 `HealthBarRight`、把顺序选为 `N` 位于 `-N` 左侧，并开启同时显示与明细；预期为 `血条 | N | -N | 明细`。显式切回相反顺序时仍应得到 `血条 | -N | N | 明细`。若使用“恢复默认”，需接受其他设置一并重置后再开启同时显示与明细。
- Boundary: 未启动游戏、未暂存或提交 Git，未 push/tag、发布或更新 Workshop。等待用户运行验证结果。

### HDO-1 Runtime Verified Checkpoint — 2026-08-02

- Result: 用户在已安装 HDO-1 上明确反馈“测试通过，是会默认到左边”。本次 `RuntimeVerified` 只绑定恢复默认后的 `IncomingDamagePlacement` 使用 `LeftOfExpectedHpLoss`，即同位置时 `N` 默认位于 `-N` 左侧。
- Preserved boundary: 既有 `DamageForecast.cfg` 与用户显式选择仍不被迁移或覆盖；本次用户反馈未单独报告显式相反顺序场景，因此不扩张为该项运行验证。
- Authority: 用户同时明确授权 HDO-1 “收口和 checkpoint”；仅允许任务自有本地 Git checkpoint，不包含 push、tag、发布或 Workshop。
- Image boundary: 用户图片只用于本次视觉判断，不保存到任务卡、仓库或记录中。

## Completion and closure requirements

- 安装必须另行批准；更新游戏文件后立即停止，并说明现有配置不会被自动改写。
- 人工验证可由用户手动选择“`N` 位于 `-N` 左侧”，或在明确接受其他设置重置后使用“恢复默认”；不得删除配置冒充新用户。
- 用户确认默认右侧顺序正确后，才能登记 `RuntimeVerified`。
- 按当前收口标准同步必要产品事实，并在另行获批的任务自有 Git checkpoint 后标记 `Closed`。

读取 `docs/task-notes/hud-default-display-order-task-card.md`，核对 `Current Control`，只执行 HDO-1，完成后停止。
