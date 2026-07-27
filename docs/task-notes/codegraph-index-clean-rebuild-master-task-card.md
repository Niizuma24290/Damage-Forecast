# Damage Forecast — CodeGraph 索引干净重建与对接验证任务卡

日期：2026-07-27

Type: Tooling Maintenance / Local Index Rebuild
Area: Governance
Priority Tag: P2
Queue: Now
Depends on: 血条/HUD 源码编辑已停止；替换前必须确认本仓库 CodeGraph 写入者已停止

## Current Control

Classification: PROPOSED_TASK
State: Proposed / Authorized for bounded CC execution
Last completed: Codex read-only diagnosis and CC handoff preparation
Next: CG-0R — 检查、可恢复干净重建与验证
Approved: Yes — 用户已明确授权 Claude Code 检查并重建当前 CodeGraph；授权仅覆盖 CG-0R
Evidence: 本卡“当前已确认事实”
Repository: Registered / execution not started / no Git checkpoint

## Goal

让 GLM-5.2 在 Claude Code 中基于 Damage Forecast 当前磁盘状态重新建立可信的
CodeGraph 索引，清除全面改名后残留的 `src/STS2PartyWatchCode/` 等旧路径，使
后续 Codex、CC 和架构任务能够查询 `src/DamageForecast/` 的真实符号与调用关系。

CodeGraph 只负责导航、搜索和影响面分析，不是项目 Authority。当前源码、
`docs/task-notes/README.md`、`docs/project-state.md` 和对应任务卡始终优先。

## 当前已确认事实

- 仓库：`C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2`；
  `main...origin/main [ahead 10]`，当前存在多个会话遗留或正在整理的 dirty changes。
- 当前 CodeGraph 只报告 `21` 个 C# 文件、`356` nodes、`613` edges；
  当前 `src/` 与 `tests/` 下实际有 `112` 个 `.cs` 文件。
- CodeGraph 查询曾返回已废弃的 `src/STS2PartyWatchCode/...`；当前生产源码位于
  `src/DamageForecast/...`。
- `.codegraph/codegraph.db` 当前约 `0.88 MB`，主文件时间为
  `2026-07-02`；当前使用 SQLite WAL，并存在 `codegraph.db-wal` /
  `codegraph.db-shm`。
- `.codegraph` 已解析为仓库内部精确路径；仓库根 `.gitignore` 已忽略整个
  `.codegraph/`。内部 `.codegraph/.gitignore` 仍是合理的通用规则：
  `*.db`、`*.db-wal`、`*.db-shm`、`cache/`、`*.log`、`.dirty`。
- 当前 PowerShell execution policy 会阻止 `codegraph.ps1`；可使用
  `C:\Users\ROG\AppData\Roaming\npm\codegraph.cmd`。
- 当前机器存在多个 CodeGraph 自带的 `node.exe` 进程。不得在未确认归属时覆盖
  SQLite 文件，也不得为了方便终止所有 `node.exe`。
- 用户已停止血条任务继续编辑，并允许索引当前 dirty worktree；本任务不要求
  clean worktree，也不得修改、整理或提交这些产品差异。

## CC / GLM-5.2 执行约束

- 最多约 `12` 个 agentic turns；预计明显超出时先停止并报告。
- 不读取全部历史任务卡，不生成完整项目架构报告。
- 不编辑 `CLAUDE.md`、模型配置、MCP 全局配置或其他 Agent 配置。
- 不修改生产源码、测试、Authority、构建脚本或当前 dirty changes。
- 除本任务卡的执行记录外，不修改项目文档。
- 不构建、不测试、不安装、不启动游戏。
- 不执行 Git commit、reset、checkout、clean、push、tag 或全局 Git 配置修改。
- 不因 CodeGraph 报错而修改产品代码。
- 不把 CodeGraph 结果提升为项目 Authority 或运行时证据。

## CG-0R — 检查、可恢复干净重建与验证

### Entry checks

开始时重新取得：

- repository root、branch、HEAD、dirty status；
- `.codegraph` 的绝对路径、真实父目录和文件列表；
- CodeGraph CLI 版本与 `init/index/status` 帮助；
- 当前 CodeGraph/Node 进程及其命令行、工作目录或父进程证据。

必须明确确认：

```text
Repository root:
Resolved live index:
Live index is inside repository: true
Live target name is exactly .codegraph: true
Source/test/docs directories are not targets: true
Blood-bar/HUD editor still active: false
Exact CodeGraph writers stopped or safely coordinated: true
```

如果不能精确识别 CodeGraph 写入者，停止并让用户关闭相关 CodeGraph MCP/会话；
不得按进程名批量杀死所有 `node.exe`。

### Rebuild strategy

本轮优先采用可恢复的干净重建，不手工只替换 `codegraph.db`：

1. 记录重建前 `status --json`、数据库文件集合和代表性旧路径查询。
2. 停止或安全协调本仓库的 CodeGraph writer / watcher。
3. 将现有 `.codegraph` 作为一个整体移动到已解析、可恢复的临时备份位置；
   备份必须在允许的 workspace/temp 范围内，且不能覆盖已有目录。
4. 在原仓库根执行当前 CLI 的正式 `init`，让它重新创建 `.codegraph` 并完整索引。
5. 比较新旧 `.codegraph/.gitignore`：
   - 新文件覆盖现有通用规则时直接保留；
   - 新文件缺失或漏规则时，从备份恢复/合并上述通用规则；
   - 不修改仓库根 `.gitignore`，除非发现当前事实与本卡冲突并先报告。
6. 验证新索引通过后保留临时备份到本任务结束；不要在同一步骤中永久删除备份。
7. 若初始化或验证失败，停止新 writer，移走失败的新索引并恢复旧 `.codegraph`；
   报告 `Checkpoint`，不得留下半套数据库。

不得在临时复制的仓库中构建索引后只搬运数据库，因为索引可能绑定错误根路径。
必须在真实仓库根生成最终索引。

### CLI boundary

PowerShell 中使用已确认存在的：

```powershell
& 'C:\Users\ROG\AppData\Roaming\npm\codegraph.cmd' status -j 'C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2'
& 'C:\Users\ROG\AppData\Roaming\npm\codegraph.cmd' init 'C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2' --verbose
```

执行前必须以当前 `--help` 为准；不要因 `codegraph.ps1` 被 execution policy
拦截而修改系统 execution policy。

`index --force` 是可用的官方全量重索引命令，但本仓库已经出现旧路径残留和显著
覆盖缺口，因此本轮默认采用上面的可恢复干净重建。只有 clean rebuild 被当前
CLI 明确阻塞时，才能把 `index --force` 作为降级方案，并在结果中说明。

### Validation

重建后必须检查：

- `status --json` 为 healthy/ready，不存在未处理的 lock/dirty 状态；
- 索引文件数量与当前真实源码规模大致一致；差异必须能够由索引规则解释；
- 以下代表性符号返回当前真实路径：
  - `ForecastRefreshPatch`
  - `DamageForecastHudDisplay`
  - `DamageForecastHudSurfacePolicy`
  - `DamageForecastBaseLibConfig`
  - `LocalIncomingDamageReader`
  - `ForecastTimeline`
  - `ForecastTimelineShadowComparer`
  - `HudNodeOwnershipContractCases`
- 查询 `src/STS2PartyWatchCode` 和
  `tests/STS2PartyWatchCode.ContractTests` 不再返回当前源码节点；
- 至少一次 `callers`、`callees` 或 `impact` 查询能返回与当前磁盘一致的合理关系；
- task notes 中出现的旧路径只能作为 Markdown 历史文本，不能被当成当前 C# file；
- 重建前后 `git status` 的产品文件差异一致；
- `.codegraph`、数据库、WAL/SHM、cache、logs 和 marker 都未进入 Git 跟踪候选。

CodeGraph watcher 在重建后重新产生 `db-wal` / `db-shm` 属于正常 SQLite 行为，
不能据此判定失败。

### Pass

- 旧源码路径和陈旧符号从当前索引消失；
- 当前 `src/DamageForecast/`、Timeline、HUD、Settings、Reader 和 tests 均可查询；
- 代表性调用关系与当前磁盘源码一致；
- 未修改任何产品文件，也未干扰当前 dirty worktree；
- 新索引可被 Codex/CC 正常读取。

### Stop

完成验证后：

- 将增量结果回填本卡；
- 报告临时备份的精确位置和是否仍保留；
- 不自动启动 Forecast Engine 架构重构、血条任务或其他产品任务；
- 不执行 Git checkpoint；等待用户决定任务卡文档和临时备份的最终处置。

## Failure / safety stops

出现以下任一情况立即停止：

- 目标路径不能证明是仓库内精确 `.codegraph`；
- 无法停止或协调正在写入该数据库的进程；
- 重建命令要求修改系统或全局配置；
- 新索引仍以旧目录作为当前生产源码；
- 新索引明显漏掉当前 `src/DamageForecast/`；
- Git status 出现无法解释的产品文件变化；
- 回滚需要删除或覆盖未解析的目录。

## Completion and closure requirements

执行 Session 最终只需报告：

```text
Result: Rebuilt
Indexed repository: C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2
CodeGraph status: initialized=true, version=1.5.0, state=complete, reindexRecommended=false, journalMode=wal, no pendingChanges/lock/dirty
Indexed files/nodes/edges: 105 files / 2,068 nodes / 8,292 edges (was 21 / 356 / 613)
Old-path queries: src/STS2PartyWatchCode and tests/STS2PartyWatchCode no longer return any indexed nodes (files output grep clean)
Current-path queries: ForecastRefreshPatch, DamageForecastHudDisplay, DamageForecastHudSurfacePolicy, DamageForecastBaseLibConfig, LocalIncomingDamageReader, ForecastTimelineShadowComparer, HudNodeOwnershipContractCases all resolve to current src/DamageForecast or tests/DamageForecast.ContractTests paths; ForecastTimeline resolves to current Forecast/Timeline symbols (no exact class named ForecastTimeline exists on disk — expected, matched Reducer/Validator)
Representative relationship checks: callers(LocalIncomingDamageReader) -> ForecastRefreshPatch.cs:31; callees(ForecastRefreshPatch) -> DamageForecastUiSettings + RefreshRegisteredBars — consistent with current source
Repository files changed: none; product/test/script dirty set identical to pre-rebuild (only .codegraph.bak-20260727_2335/ added as untracked, the recoverable backup)
Temporary backup: .codegraph.bak-20260727_2335/ (inside repo root, contains .gitignore + codegraph.db; kept until user decides)
Remaining limitations: ForecastTimeline has no exact-named node on disk (matches ForecastTimelineReducer/Validator instead) — not a defect, reflects current source naming; new .codegraph/.gitignore uses the "*" + "!.gitignore" rule which strictly subsumes the old *.db/*.db-wal/*.db-shm/cache/*.log/.dirty rules
Recommended next action: user decides fate of .codegraph.bak-20260727_2335/ (keep until verified by Codex/CC reads, then delete) and whether to Git checkpoint this task card; no product/Git action taken
```

本任务最多收口为 `Work Complete / Checkpoint Pending`，直到任务卡记录获得后续
Git checkpoint。CodeGraph 本身是 ignored local cache，不进入 Git checkpoint。

---

## CG-0R 执行回填（Claude Code，2026-07-27）

- 重建方式：可恢复干净重建（整目录改名备份 + 仓库根 `init --verbose`）。
- 备份：`.codegraph.bak-20260727_2335/`，未删除，可一键回滚（`mv` 还原）。
- 写入者确认：重建前 CodeGraph writer 已全部退出（关 Codex 后无 `codegraph serve` 进程，db mtime 静止 20+ 分钟）。
- 未修改任何产品/测试/脚本 dirty changes；未执行 Git 操作；未构建/测试/启动游戏。
- 新旧 `.codegraph/.gitignore` 已对比：新规则 `*` + `!.gitignore` 覆盖并强于旧规则，直接保留，无需补回。
- 仓库根 `.gitignore:20` 仍忽略 `.codegraph/`；新 `.codegraph` 未进入 Git 跟踪候选。

---

给 Claude Code 的启动语：

> 读取 `docs/task-notes/codegraph-index-clean-rebuild-master-task-card.md`。用户已停止血条/HUD 编辑，并已授权本卡 CG-0R。先完成 Entry checks；只有在精确确认本仓库 CodeGraph 写入者已停止、目标路径安全且可回滚后，才执行可恢复的干净重建。验证完成后回填本卡并停止，不得修改产品代码、构建、安装、启动游戏或执行 Git 操作。
