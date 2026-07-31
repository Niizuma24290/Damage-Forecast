# Documentation Start Here

`docs/task-notes/` 是 Damage Forecast 的唯一任务登记和历史证据目录。
不要创建第二套平行进度目录。

## Task routing

- 收口规则：[`task-closure-standard.md`](task-closure-standard.md)。
- 最近关闭的状态/诅咒 HUD 修复：[`damaging-status-curse-hud-hidden-master-task-card.md`](damaging-status-curse-hud-hidden-master-task-card.md)。
- 最近关闭任务：[`post-g6-full-name-unification-master-task-card.md`](post-g6-full-name-unification-master-task-card.md)。
- 已关闭测试护栏：[`forecast-engine-test-guardrails-master-task-card.md`](forecast-engine-test-guardrails-master-task-card.md)。
- 已关闭 G6：[`full-technical-identity-migration-master-task-card.md`](full-technical-identity-migration-master-task-card.md)。
- 已关闭 HUD 覆盖层修复：[`native-covering-screen-hud-visibility-master-task-card.md`](native-covering-screen-hud-visibility-master-task-card.md)。
- 已关闭 StS2Sim × Damage Forecast 利用评估：[`sts2sim-damage-forecast-evaluation-master-task-card.md`](sts2sim-damage-forecast-evaluation-master-task-card.md)。
- 已关闭的无惧疼痛机制支持：[`feel-no-pain-ethereal-block-forecast-master-task-card.md`](feel-no-pain-ethereal-block-forecast-master-task-card.md)。
- 已关闭 HUD 预设位置与冻结实施：[`damage-forecast-hud-placement-implementation-master-task-card.md`](damage-forecast-hud-placement-implementation-master-task-card.md)。
- 新候选 Forecast 时间链/API 统一审查：[`damage-forecast-timeline-api-unification-review-master-task-card.md`](damage-forecast-timeline-api-unification-review-master-task-card.md)。
- 排队中的架构任务：[`forecast-engine-architecture-stabilization-master-task-card.md`](forecast-engine-architecture-stabilization-master-task-card.md)。
- 历史 authority consolidation：[`documentation-authority-consolidation-master-task-card.md`](documentation-authority-consolidation-master-task-card.md)。

## 默认阅读顺序

普通新 Session 默认只读：

1. 本文件；
2. [`docs/project-state.md`](../project-state.md)。

仅当未来存在已批准任务时，再读取该任务卡。

然后按任务主题读取对应 Tier 1 或精确历史证据。不要默认加载全部 Phase、audit 或 handoff note。

## Authority map

| 主题 | 当前唯一 authority |
| --- | --- |
| 玩家产品、安装、功能边界 | [`README.md`](../../README.md) |
| 当前实现、验证等级、限制、release/repository 状态 | [`docs/project-state.md`](../project-state.md) |
| 当前生产架构 | [`docs/architecture.md`](../architecture.md) |
| 当前机制支持、条件支持、未知与证据矩阵 | [`docs/mechanics-evidence.md`](../mechanics-evidence.md) |
| 当前构建、依赖、stable/beta 和验证命令 | [`docs/build-environment.md`](../build-environment.md) |
| 已完成里程碑短摘要与未来方向 | [`docs/v2-roadmap.md`](../v2-roadmap.md) |
| 当前任务和历史证据路由 | 本文件 |
| 任务状态词、Gate 与 checkpoint 收口规则 | [`task-closure-standard.md`](task-closure-standard.md) |

其他文档可以提供历史证据，但不得竞争这些 current-state 职责。

## 状态词

统一使用 [`task-closure-standard.md`](task-closure-standard.md) 第 7 节；本索引不复制状态定义。

## 按主题读取

| 任务 | 追加读取 |
| --- | --- |
| 预测机制 | [`docs/mechanics-evidence.md`](../mechanics-evidence.md) + 精确 Phase note |
| 架构 | [`docs/architecture.md`](../architecture.md) + [Forecast Engine task card](forecast-engine-architecture-stabilization-master-task-card.md)（仅在批准后） |
| 构建/兼容 | [`docs/build-environment.md`](../build-environment.md) + 对应 stable/beta note |
| 测试护栏 | [已关闭的 Forecast Engine 测试护栏任务卡](forecast-engine-test-guardrails-master-task-card.md) + [`docs/build-environment.md`](../build-environment.md) |
| 产品/玩家文案 | [`README.md`](../../README.md) + current rename record |
| 路线和排序 | [`docs/v2-roadmap.md`](../v2-roadmap.md) |
| 审计复核 | [`phase-12c-audit/`](phase-12c-audit/) 下精确 ledger |
| Workshop | [`workshop-private-rc-2026-07-01.md`](workshop-private-rc-2026-07-01.md) + [`docs/project-state.md`](../project-state.md) |
| G6（已关闭） | [`full-technical-identity-migration-master-task-card.md`](full-technical-identity-migration-master-task-card.md) + [`name-migration-inventory.md`](phase-12c-audit/name-migration-inventory.md) |
| Post-G6 C1-C4 | [`post-g6-full-name-unification-master-task-card.md`](post-g6-full-name-unification-master-task-card.md) |
| Damage-dealing Status / Curse HUD 修复（已关闭） | [`damaging-status-curse-hud-hidden-master-task-card.md`](damaging-status-curse-hud-hidden-master-task-card.md) |
| 地图与牌堆覆盖层 HUD 修复（已关闭） | [`native-covering-screen-hud-visibility-master-task-card.md`](native-covering-screen-hud-visibility-master-task-card.md) |
| StS2Sim × Damage Forecast 利用评估（已关闭） | [`sts2sim-damage-forecast-evaluation-master-task-card.md`](sts2sim-damage-forecast-evaluation-master-task-card.md) |
| 无惧疼痛 × 虚无回合末护盾预测（已关闭） | [`feel-no-pain-ethereal-block-forecast-master-task-card.md`](feel-no-pain-ethereal-block-forecast-master-task-card.md) |
| HUD 预设位置与结束回合冻结实施（已关闭） | [`damage-forecast-hud-placement-implementation-master-task-card.md`](damage-forecast-hud-placement-implementation-master-task-card.md) |
| Forecast 时间链/API 统一审查（候选） | [`damage-forecast-timeline-api-unification-review-master-task-card.md`](damage-forecast-timeline-api-unification-review-master-task-card.md) |

## Task-note classification

- `CURRENT_AUTHORITY`：本文件。
- `CHECKPOINT_TASK`：无。
- `CLOSED_TASK`：[`damage-forecast-hud-placement-implementation-master-task-card.md`](damage-forecast-hud-placement-implementation-master-task-card.md)，I0–I3 已关闭，implementation checkpoint `a4b2e23`；[`feel-no-pain-ethereal-block-forecast-master-task-card.md`](feel-no-pain-ethereal-block-forecast-master-task-card.md)，FP0–FP5 已关闭，implementation checkpoint `a72fff5`；[`sts2sim-damage-forecast-evaluation-master-task-card.md`](sts2sim-damage-forecast-evaluation-master-task-card.md)，DF-S0–DF-S4C 已关闭；[`native-covering-screen-hud-visibility-master-task-card.md`](native-covering-screen-hud-visibility-master-task-card.md)，CV0–CV5 已关闭；[`damaging-status-curse-hud-hidden-master-task-card.md`](damaging-status-curse-hud-hidden-master-task-card.md)，HC0-HC5 已关闭；[`post-g6-full-name-unification-master-task-card.md`](post-g6-full-name-unification-master-task-card.md)，C1-C4 已关闭；[`forecast-engine-test-guardrails-master-task-card.md`](forecast-engine-test-guardrails-master-task-card.md)，TG0-TG7 已关闭；[`full-technical-identity-migration-master-task-card.md`](full-technical-identity-migration-master-task-card.md)，G6-0..G6-7 已关闭。
- `PROPOSED_TASK`：[`damage-forecast-timeline-api-unification-review-master-task-card.md`](damage-forecast-timeline-api-unification-review-master-task-card.md)，FTU-S0 尚未批准；[`forecast-engine-architecture-stabilization-master-task-card.md`](forecast-engine-architecture-stabilization-master-task-card.md)，保持 queued，尚未获得 AR1。
- `SUPERSEDED_HANDOFF`：所有 `*handoff*` 文件；保留 hash、工作区和当时 next-step。
- `RAW_EVIDENCE`：`phase-12c-aud-0007-*` 和 `phase-12c-audit/*`。
- `HISTORICAL_RECORD`：已关闭的 Documentation Authority Consolidation task card、`project-total-note.md` 及其余 completed Phase、runtime、Workshop 和 compatibility note。

完整 41 文件逐项 disposition 见[已关闭的 consolidation task card](documentation-authority-consolidation-master-task-card.md)。
所有历史 note 默认不读、不移动、不删除，正文中的旧 next-step 只代表其原日期。

## 禁止事项

- 不提交 DLL、PDB、PCK、logs、publish 输出、NuGet 缓存、Workshop uploader 文件、封面、`mod_id.txt`、`work/` 或游戏目录文件。
- 不修改游戏文件。
- 不把代码确认写成运行时验证事实。
- 不创建第二套平行进度目录。
- 不把本机多人 HUD 写成正式多人/队友/共享 HUD。
