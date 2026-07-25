# Damage Forecast 时间链与 API 统一审查主任务卡

## Current Control

State: `Proposed / FTU-S0 Not Authorized`
Last completed: `Task card creation and parent linkage only`
Next: `FTU-S0 read-only Current Trigger Chain Audit only after separate approval`
Approved: `No — 创建本任务卡不构成 FTU-S0 或任何实现 Gate 授权`
Parent:
[`sts2sim-damage-forecast-evaluation-master-task-card.md`](sts2sim-damage-forecast-evaluation-master-task-card.md)
Parent status: `DF-S4 Complete / Closed — DF-S4C local checkpointed in this commit`
Related:
[`forecast-engine-architecture-stabilization-master-task-card.md`](forecast-engine-architecture-stabilization-master-task-card.md)
Repository baseline:
`DF-S3 173560d6e6c630c05a6879b5600dd4d449b2d364 / DF-S3F dfd0a8ce6352257ca663d956a6164db4882d9568`
Parent closure checkpoint: `DF-S4C this commit`

任务性质：先审查、后决策、再按独立 Gate 渐进实施的 Forecast Engine 内部架构候选。

本卡只回答：

> Damage Forecast 当前分散在 hand、power、relic、enemy intent、HP-loss modifier、
> block、poison 和 lifecycle 等板块中的触发链，是否应统一为一个只读、game-neutral、
> 有序、可失败关闭的 Forecast Timeline；StS2Sim 已核对的时间阶段、触发顺序和接口
> seam 中，哪些值得借鉴，哪些绝不能进入生产预测路径，以及应如何以最小风险接入。

---

## 1. 为什么建立本任务

当前 Damage Forecast 已经有事件化基础，但时间与触发语义分散在多个位置：

- hand turn-end damage、fixed HP loss、power turn-end damage 和 enemy intent 分别读取；
- block、HP-loss modifier、poison 与 survival preview 使用不同输入和顺序逻辑；
- 部分路径用 `NativeExecutionOrder` 或 reader-local 数字区间表达先后；
- lifecycle、刷新时机、牌堆状态和玩家阶段由另外的 guard/read path 决定；
- 新增跨板块机制时，容易同时修改 reader、policy、projection 和 HUD 邻接代码；
- 相同“何时触发、先发生什么”的事实可能在多个板块重复表达或产生漂移。

StS2Sim 的价值不是把模拟器搬进生产 Mod，而是提供一个已经经过 stable/beta
headless 维护验证的参考：

- 显式回合阶段；
- 确定性 listener / card 顺序；
- turn-start、turn-end、Ethereal、TurnEndInHand、flush、discard/retain 的链；
- runtime adapter 与纯 orchestration 的分层；
- chronological event output。

如果能把其中适合预测的语义转成 Damage Forecast 自己的只读 timeline contract，
未来新增机制可以优先回答“产生什么事件、在哪个 phase、顺序是什么、证据是否充分”，
而不是继续给每个板块增加独立特例。

---

## 2. 与现有任务的关系

### 2.1 Parent：StS2Sim × Damage Forecast 利用评估

本任务承接 DF-S4A 的只读设计与 DF-S4B 的最终闭环：

- StS2Sim 对 Damage Forecast 为 `条件有用`；
- 保留离线 contracts / fixtures 与 explicit optional differential tool；
- 不把 StS2Sim 建成生产依赖或长期 external oracle；
- 更高价值的候选是借鉴其 phase、order 和 seam，统一 Damage Forecast 内部时间链；
- `HeadlessVerified / L2` 不等于真实游戏 `RuntimeVerified / L3`。

父任务已关闭。本任务不重新打开 DF-S2/DF-S3 process-adapter 范围，也不自动获得
StS2Sim 执行、实现或 Git 权限；FTU-S0 仍需用户单独批准。

### 2.2 Related：Forecast Engine Architecture Stabilization

已有的 Forecast Engine Architecture Stabilization 任务卡仍是更广的排队候选。本卡：

- 不替代其 current/target architecture、ownership 或 migration authority；
- 只聚焦 timeline、trigger、event API 和接入顺序；
- FTU-S0/FTU-S1 的审查结果必须与该卡仲裁，不能形成第二套目标架构；
- 如果两卡提出重叠实现，以用户后续批准的单一 authority 和较窄 diff 为准。

### 2.3 并行 Feel No Pain 工作

建立本卡时，共享工作区存在 Feel No Pain / Ethereal block 相关并行改动。本卡创建：

- 不审查、修改、暂存或提交这些改动；
- 不把其 working-tree 形态冻结为 timeline 基线；
- 未来进入任何实现 Gate 前，必须重新核对其最终 commit、contracts 和 ownership；
- 不允许两个任务同时修改相同 reader、policy 或 `Program.cs` hunk。

---

## 3. 当前审查对象

FTU-S0 必须以执行时源码为准，至少覆盖：

| 板块 | 当前问题 |
|---|---|
| enemy attack intent | 多敌人、多 hit、稳定身份与敌方执行顺序如何进入统一 timeline |
| hand turn-end damage | hand index、TurnEndInHand、普通 Ethereal 与带 effect 的 Ethereal 顺序 |
| direct HP loss | Beckon、Bad Luck、Regret 等是否是单事件、何时应用 modifier |
| power turn-end damage | power listener 顺序与 hand/enemy event 如何比较 |
| future block | current/power/relic/Ethereal block 在哪些 damage 之前生效 |
| HP-loss modifiers | Intangible、Tungsten Rod、Beating Remnant 的顺序、粒度与预算 |
| poison / survival | 敌方行动前后、致死判断与已有 preview seam |
| lifecycle / refresh | snapshot 在何时有效、何时过期、何时必须返回 Unknown |
| projection / HUD | 只消费最终结果，不拥有机制或时间链 |

审查必须列出：

- producer；
- consumer；
- 当前 DTO；
- 当前 order 表达；
- game-specific dependency；
- Known / Unknown / Unsupported 行为；
- 覆盖它的 contract ID；
- 与其他板块重复或冲突的语义；
- 是否适合迁入统一 timeline。

---

## 4. StS2Sim 参考边界

固定参考 checkpoint：
`42396191e4bd66ca8ab27cd9b9b9f4f537966978`，`sourceTree=clean`。

### 4.1 可以借鉴

- `TurnHooks` 中显式 phase 与子阶段顺序；
- listener snapshot 后按确定性顺序处理的原则；
- turn-start 与 draw/play window 分离；
- turn-end 中 BeforeTurnEnd、Ethereal、TurnEndInHand、BeforeFlush、
  discard/retain、cleanup、AfterTurnEnd 的相对顺序；
- runtime-version adapter 与 game-neutral orchestration 分离；
- chronological event output、seed、target、version 和 evidence metadata；
- timeout、unsupported、mismatch 与 unknown 的 fail-closed 原则。

### 4.2 不得直接迁移

- 不复制或调用会改变真实战斗状态的 `TurnHooks`；
- 不在预测路径调用 `CardCmd`、`CardPileCmd`、`OnTurnEndInHandWrapper`、
  `EndOfTurnCleanup` 或真实 listener hook；
- 不迁移 headless `SaveManager`、`LocalContext.NetId`、`RunManager`、
  network proxy 或 Godot shim；
- 不把 `Harness.BeginCombat`、play policy、Best-of-K、server/job API 放入生产 DLL；
- 不复制 provider-specific marker mapping 作为生产事实；
- 不让生产 DLL 引用、探测或启动 StS2Sim；
- 不把 L2 synthetic 顺序自动提升为 L3 真实游戏证据。

原因：StS2Sim 可以在可销毁 synthetic combat 中真正执行 hook；Damage Forecast 位于玩家
真实战斗，只能读取 snapshot 和做纯投影，不能为了预测而触发实际副作用。

---

## 5. 候选目标边界

以下名称只是设计占位，FTU-S1 可以修改；未批准前不得创建生产类型。

```text
Game-specific read-only snapshot adapters
  → ForecastSnapshot
  → IReadOnlyList<ForecastTimelineEvent>
  → ForecastTimelineValidator
  → pure ForecastTimelineReducer
  → ForecastResult
  → existing projection / presenter / HUD
```

候选 game-neutral 类型：

- `ForecastPhase`
  - combat start；
  - player turn start；
  - before/after hand draw；
  - card play；
  - before turn end；
  - ordinary Ethereal exhaust；
  - TurnEndInHand effect；
  - before flush；
  - discard / retain；
  - cleanup / after turn end；
  - enemy action；
- `ForecastTimelineEvent`
  - stable event ID；
  - source ID；
  - phase；
  - order within phase；
  - kind；
  - lane；
  - amount；
  - event granularity；
  - evidence state；
- `ForecastEvidenceState`
  - verified；
  - conditional；
  - unknown；
  - unsupported；
- `IForecastEventSource`
  - 从只读 snapshot 产生事件；
  - 不直接写 HUD；
  - 不执行游戏 command 或 hook；
- `ForecastTimelineReducer`
  - 只做排序、block consumption、modifier application 和 totals；
  - 不读取 Godot/UI 状态；
- `ForecastSnapshotIdentity`
  - 标记 target、turn、combat identity 和刷新窗口；
  - 防止把旧 snapshot 结果用于新状态。

核心约束：

1. `phase + orderWithinPhase` 替代跨文件约定的 magic order 区间；
2. 同 phase tie-break 必须稳定、可测试且来源明确；
3. 不能证明顺序时返回 Unknown，不用猜测继续求和；
4. game-specific 类型只存在于 snapshot adapter 边缘；
5. reducer、validator 和 DTO 可在无游戏进程时离线测试；
6. HUD 继续只消费最终 projection；
7. stable/beta 差异位于 adapter/capability 层，不复制两套 reducer。

---

## 6. 需要回答的设计问题

FTU-S0 至 FTU-S2 必须给出明确答案：

1. 统一范围是全部 Forecast event，还是先限制为 HP loss + future block？
2. phase 应使用粗粒度 enum，还是允许 versioned subphase？
3. 当前 `NativeExecutionOrder` 哪些是游戏事实，哪些只是本地实现约定？
4. hand、power、relic、enemy listener 的同阶段 tie-break 如何证明？
5. modifier 是 timeline event，还是 reducer 内独立的 ordered policy？
6. current block、future block 与 damage consumption 如何避免重复扣减？
7. direct HP loss 和 blockable damage 是否共享同一事件基类？
8. Unknown 应使单事件、单 lane 还是整个 forecast fail-closed？
9. snapshot 缓存键和失效条件是什么？
10. 如何在不改玩家结果的情况下做 old/new shadow comparison？
11. stable/beta API 漂移由哪个 adapter seam 吸收？
12. 新机制以后最少需要实现哪些接口和 contracts？

---

## 7. Gate 计划

每个 Gate 必须由用户单独批准，不继承前一 Gate 权限。

### FTU-S0 — Read-only Current Trigger Chain Audit

类型：只读。

动作：

- 重读 current authority、相关 architecture task card 和执行时 Git 状态；
- inventory 当前 readers、policies、DTO、order constants、consumers 和 tests；
- 输出 current call graph 与触发顺序矩阵；
- 标记重复语义、magic order、跨板块耦合、Unknown 泄漏和改动热点；
- 记录并行任务 ownership，不修改任何文件。

输出：

- current trigger-chain map；
- source-of-truth / duplicate / gap 表；
- 可统一、应保留、不可迁移清单；
- FTU-S1 readiness。

禁止：

- 修改文档、源码、测试；
- 调用 StS2Sim；
- 启动游戏；
- Git 写操作。

### FTU-S1 — Game-neutral Timeline Boundary Design

类型：仅文档设计。

动作：

- 定义 phase、event、evidence、snapshot、source、validator 和 reducer seam；
- 映射 StS2Sim 参考语义，但不得复制 provider implementation；
- 给出 stable/beta adapter 边界；
- 给出 Unknown、unsupported、mismatch 和 stale snapshot 规则；
- 给出复杂度与性能预算的 benchmark 计划。

输出：

- target data flow；
- API 草案；
- phase/order table；
- dependency rule；
- rejected alternatives；
- 不修改源码。

### FTU-S2 — Adoption and Migration Plan

类型：仅文档决策。

动作：

- 比较 keep-as-is、局部统一、HP-loss/block 统一、全 Forecast timeline 四种范围；
- 按价值、风险、迁移成本、回滚成本和测试增量排序；
- 选择第一个窄切面，但不实施；
- 明确与 Forecast Engine Architecture Stabilization 的 authority 归属；
- 给出后续最小候选 diff。

完成后必须停下，等待用户决定是否进入实现。

### FTU-S3 — Contract-first Timeline Kernel

类型：未来候选；未授权。

允许范围：

- 先增加 game-neutral DTO、validator、reducer 与离线 contracts；
- 不接入生产 reader；
- 不改变现有 Forecast 输出；
- 不调用 StS2Sim。

### FTU-S4 — One-lane Shadow Integration

类型：未来候选；未授权。

原则：

- 只选择一个由 FTU-S2 批准的窄切面；
- old path 仍是唯一玩家结果；
- new timeline 只做 shadow comparison；
- mismatch 记录证据，不自动改玩家行为；
- stable/beta 分别验证。

### FTU-S5 — Incremental Production Migration

类型：未来候选；未授权。

前置：

- shadow matrix 无未解释 mismatch；
- L1/L2 通过；
- 涉及真实生命周期时取得目标对应 L3；
- 有独立 rollback commit。

每次只迁移一个 ownership 明确的 producer/reducer；不得一次替换全部 Forecast Engine。

### FTU-S6 — Closure and Local Checkpoint

类型：未来候选；未授权。

- 更新本卡唯一 `Current Control`；
- 更新 current architecture 只写已经成为产品事实的内容；
- 保存 target-specific verification；
- 仅在单独批准时创建 local Git checkpoint；
- push、tag、发布、安装、Workshop 和游戏动作仍需另外授权。

---

## 8. 验证等级

| 等级 | 本任务含义 |
|---|---|
| L0 | 静态依赖、call graph、ownership 与 forbidden dependency 检查 |
| L1 | game-neutral timeline contracts、validator、reducer 与 shadow fixtures |
| L2 | stable/beta target-coupled build 与 headless/explicit differential evidence |
| L3 | matching target 的真实游戏生命周期与玩家可见结果 |

规则：

- 一个 target 的 L3 不能代表另一个 target；
- StS2Sim `HeadlessVerified` 仍是 L2；
- shadow match 不能自动证明真实游戏顺序；
- 生产切换若涉及生命周期、hook 时序或玩家可见结果，必须单独定义 L3 matrix。

---

## 9. 完成标准

审查/设计阶段完成必须满足：

- 当前触发链有逐 producer/consumer inventory；
- 每个顺序事实有 owner、证据和 Unknown 行为；
- StS2Sim 的可借鉴与禁止迁移项明确分离；
- 目标 API 不依赖 StS2Sim、进程、HUD 或工具路径；
- target model 能减少跨 reader 的重复顺序编码；
- 新增机制的最小接入面可以被清楚描述；
- 有渐进 shadow migration 与 rollback 方案；
- 与已有架构任务不存在竞争 authority；
- 未修改玩家行为。

整个任务最终完成还必须满足：

- 所有已迁移板块 old/new 结果差异均有解释；
- stable/beta 分别验证；
- legacy path 只在独立 Gate 和 checkpoint 后移除；
- current authority 更新为真实状态；
- 未引入外部运行时依赖、安装或发布变化。

---

## 10. 风险与退出条件

| 风险 | 处置 |
|---|---|
| 把手工 Sim 顺序当官方事实 | 只作候选参考，必须由目标源码/contracts/L3 复核 |
| 建立过度通用事件框架 | FTU-S2 先选窄范围，未证明价值不扩张 |
| 所有 reader 一次迁移 | 禁止；每 Gate 单 ownership、小 diff、可回滚 |
| phase enum 仍隐藏 magic order | 必须记录同 phase tie-break 与来源 |
| Unknown 被 reducer 吞掉 | validator 强制 fail-closed，禁止静默 totals |
| snapshot 过期 | 绑定 combat/turn/state identity，过期即不可发布 |
| 与 Feel No Pain/AR 并行冲突 | implementation 前 authority 仲裁和 clean/checkpoint revalidation |
| 性能退化 | shadow 阶段记录 p50/p95/最坏耗时与分配，不达预算不切换 |

应暂停或撤回统一方案的条件：

- 只能通过复制 CombatManager/TurnHooks 才能表达目标行为；
- 需要在预测时执行真实 hook 或 command；
- timeline abstraction 增加的复杂度高于减少的重复；
- 无法为关键 order 提供 fail-closed 行为；
- shadow comparison 持续出现无法解释的 target-specific mismatch；
- 统一后新机制仍需要跨多个旧板块写同一顺序事实。

---

## 11. 可复制给下一 Session 的启动提示

```text
请完整阅读：
C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2\docs\task-notes\damage-forecast-timeline-api-unification-review-master-task-card.md

并读取其 Parent：
C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2\docs\task-notes\sts2sim-damage-forecast-evaluation-master-task-card.md

本次只执行 FTU-S0 — Read-only Current Trigger Chain Audit。

只读 inventory Damage Forecast 当前 hand、power、relic、enemy intent、HP-loss
modifier、block、poison、lifecycle、projection/HUD 的 producer、consumer、DTO、
order 表达、Unknown/Unsupported 行为与 contracts；输出 current trigger-chain map、
重复/缺口/耦合表、StS2Sim 可借鉴与禁止迁移项，以及 FTU-S1 readiness。

不得修改文档、源码、测试、adapter、tools、普通 guardrail、HUD、设置、安装、游戏或
Workshop；不得调用 StS2Sim；不得进行 Git 写操作。共享工作区中的 Feel No Pain 和
其他并行改动只记录 ownership，不修改、不暂存、不提交。
```
