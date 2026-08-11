# Damage Forecast — 旧配置缺字段导致启动中止修复

日期：2026-08-11

Type: Config Migration Compatibility Fix
Area: Platform
Touches: Mod Initialization, BaseLib Config, Combat UI
Priority Tag: P1
Queue: Parked

## Current Control

Classification: CLOSED_TASK
State: Closed
Last completed: DFCM-3 — Affected-user RuntimeVerified / Repository Checkpoint
Next: None；后续新缺陷需另行登记和授权
Approved: DFCM-1 实现、contracts、三目标构建，DFCM-2 本地安装，以及 DFCM-3 用户运行反馈与任务自有本地 Git checkpoint 已批准；Codex 游戏启动、push、tag 和创意工坊上传未批准
Evidence: `Final Closure — 2026-08-11`
Repository: 本地 DFCM implementation / contract / runtime authority checkpoint `688ae41bc034ba5e6b34fa7a9ac85df4c5e456b9`；未 push 或 tag

## Goal

修复老玩家升级 Damage Forecast 后，旧 `STS2PartyWatch.cfg` 缺少后来新增的七个 incoming-damage 字段，导致配置迁移提前中止、BaseLib 配置未注册且 HUD 不生效的问题。更新后的 Mod 应自动完成安全迁移，不要求玩家手动查找或删除配置。

## Confirmed defect

- 玩家日志明确出现 `Config migration blocked startup: legacy config is not safe to migrate:missing-keys:...`，缺失项正好是 `DamageDisplayMode`、`IncomingDamagePlacement` 与五个 incoming-damage 开关。
- Loader 随后打印的 `Finished mod initialization` 只表示 initializer 返回；Damage Forecast 自有成功标志 `[Damage Forecast] Loaded` 缺失。
- 玩家已订阅并启用 BaseLib 与 Damage Forecast，但 Damage Forecast 未出现在 BaseLib 模组配置页；取消订阅、重新下载和重装游戏均不能删除创意工坊目录之外的旧配置。

## Scope and safety boundary

- 只有缺失字段全部属于上述七个历史新增字段时，才使用版本化默认值补齐。
- 已存在且可解析的玩家设置保持不变；迁移事务继续保留原始旧配置备份、源 SHA256 校验、目标复验和 marker。
- 任意其他缺失字段、未知字段、重复字段、非法值、损坏 JSON 或无效 UTF-8 继续 fail closed。
- 不修改伤害预测、HUD 布局、BaseLib、游戏文件格式或其他并行任务代码。

## DFCM-1 Implementation and verification

- `CompatibilityBootstrap` 在严格旧 schema 校验失败后，仅尝试受限的历史 partial-config recovery；恢复结果仍进入既有事务迁移与 HUD placement V2 迁移。
- 新增 `DFCM-001`：按玩家日志删除七个字段，验证默认补齐、其余值保持、原始 partial 文件逐字节备份、current V1 有效，并可继续生成严格 HUD placement V2。
- 新增 `DFCM-002`：验证无关字段缺失、已存在值非法或存在未知字段时仍阻止注册且不创建目标配置。
- stable `v0.107.1`、beta `v0.109.0`、frozen current `v0.110.0` 均通过 `554/554` contracts，Release 构建均为 0 warnings / 0 errors；三个 DLL SHA256 均为 `F3F289BDA017C04840356067D4A88F75E675DD0819BA3F9613FCD9368DCDA3AF`。

## DFCM-2 Installed checkpoint

- 用户明确批准本地安装；安装前游戏进程为 0，游戏为正式版 `v0.107.1 / 59260271`，本地 `mods` 为空，已知 Workshop item `3755598583` 目录不存在。
- 安装 Plan 为 `clean-install`；transaction `dfcm2-20260811` 只激活 `mods/damage-forecast` 下 DLL/manifest 两个文件。
- 安装后 DLL SHA256 为 `F3F289BDA017C04840356067D4A88F75E675DD0819BA3F9613FCD9368DCDA3AF`，manifest SHA256 为 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；Codex 未启动游戏。

## DFCM-3 Runtime feedback

- 用户随后自行上传新版本；此前交接的候选两文件哈希见 DFCM-2。Codex 未执行或控制创意工坊更新，本收口未独立复核线上 item manifest，因此不声称线上文件身份已由 Codex 复验。
- 原受影响用户更新后反馈“新的成功了”。该反馈确认旧配置导致的启动/激活故障已在真实玩家环境恢复，满足本任务 RuntimeVerified 目标。
- 运行证据只覆盖该受影响用户的升级激活结果；不扩张为所有游戏分支、所有配置损坏形态或完整 HUD 机制矩阵。

## Closure Correction — 2026-08-11

- Integration/upload candidate: 共享 dirty worktree 上的 stable、beta、current 验证为 `554/554`，三个候选 DLL SHA256 均为 `F3F289BDA017C04840356067D4A88F75E675DD0819BA3F9613FCD9368DCDA3AF`。该候选包含当时并行 Session 的最新工作，不能解释为仅由 DFCM checkpoint 重现的任务独立二进制。
- Task checkpoint: DFCM 源码、contracts 与收口 authority 独立 checkpoint 为 `688ae41bc034ba5e6b34fa7a9ac85df4c5e456b9`。从该 commit 经本地 bundle clone 导出的干净快照在 stable、beta、current 均通过 `439/439` contracts 和 0 warnings / 0 errors Release 构建；三个 checkpoint-only DLL SHA256 均为 `664AE204A612083737290E53D6D1751C5F9BF328978063E6C3A09C0A1502C4CD`。
- Runtime identity boundary: 原受影响用户的“新的成功了”反馈绑定其实际获取的新上传版本，但 Codex 未独立复核线上 Workshop manifest/hash，因此不把该反馈强行绑定为 `F3F289...` 或 `664AE2...` 的线上二进制身份证明。
- Local install supersession: DFCM-2 的本机 `F3F289...` 安装随后已被并行 MP-1R2 诊断安装替换；当前本地安装事实由 `docs/project-state.md` 与 MP 任务卡记录，不改变 DFCM 的历史安装和玩家运行结论。

## Final Closure — 2026-08-11

Result: 已从 Mod 本体修复历史 partial config 的安全向前迁移，玩家无需手动删除配置；原受影响用户确认更新后恢复生效。
Current state: Closed / RuntimeVerified for the reported legacy-config activation failure；非法或无法证明安全的配置继续 fail closed。
Authority: 本任务卡、`docs/project-state.md` 与 `docs/task-notes/README.md` 已同步。
Repository: 本地任务自有 checkpoint `688ae41bc034ba5e6b34fa7a9ac85df4c5e456b9`；其他并行 dirty work 未纳入，未 push 或 tag。
