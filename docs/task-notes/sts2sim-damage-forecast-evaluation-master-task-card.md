# StS2Sim × Damage Forecast 利用评估主任务卡

## Current Control

State: `DF-S2 Complete / DF-S3 Not Authorized`
Last completed: `DF-S2C Evidence Closure`
Next: `DF-S3A read-only Differential Scenario Matrix Design only after separate approval`
Approved: `No — DF-S3 is not authorized; DF-S2A / DF-S2B are complete`
Evidence: `本卡 §4 DF-S0 / DF-S1 / DF-S2 增量证据`
Repository: `Implementation and documentation checkpoints complete — DF-S2A 286fd56 / DF-S2B 573baa6 / DF-S2C this task-card commit`

任务性质：Damage Forecast 独立的测试与验证能力评估。

本卡只回答：

> Damage Forecast 是否能安全利用一个经过版本核验的 StS2Sim，把它作为隔离的外部 test oracle、场景生成器或事件顺序参考，而不让生产 Mod、HUD、Forecast Engine 或发布物依赖该工具。

---

## 1. 前置依赖

本任务不负责修复 StS2Sim 本身。

进入任何实现 Gate 前，必须先获得 StS2Sim 独立任务输出：

- exact upstream / maintained fork commit；
- LICENSE；
- stable/beta 支持矩阵；
- capability manifest；
- API / Hook / reflection 风险；
- deterministic test 结果；
- unsupported mechanism registry。

如果上述信息缺失，本任务只能停在设计参考层，不能引入代码或依赖。

---

## 2. 当前产品与证据边界

- Damage Forecast 当前生产身份、设置、安装和运行时边界以现有 current authority 为准；
- DF-S0 时测试源码静态识别到 267 个唯一 case ID；DF-S2B 当前最终普通
  stable/beta guardrail 均为 `298/298`，Release 0 warning / 0 error；
- Forecast Engine 架构任务已有纯事件、顺序、modifier、projection 和 lifecycle 方向；
- StS2Sim SS7-A/B/C 已收口为 exact clean maintained-fork checkpoint
  `42396191e4bd66ca8ab27cd9b9b9f4f537966978`；stable/beta 均为
  `HeadlessVerified / L2`，`RuntimeVerified=false`；
- StS2Sim 的 synthetic combat 不能自动等同于真实游戏 runtime；
- damaging Status/Curse card HUD defect 已由独立任务关闭，本任务不得重新夹带行为修改；
- 本任务不改变玩家可见行为，不安装游戏，不修改 Workshop。

---

## 3. 候选利用方式

### 3.1 事件顺序参考

参考 StS2Sim 对以下阶段的显式建模，但必须由 frozen stable/beta 和当前源码重新确认：

```text
BeforeTurnEnd*
→ Ethereal
→ TurnEndInHand
→ BeforeFlush*
→ discard / retain
→ EndOfTurnCleanup
→ AfterTurnEnd
```

用途：

- 帮助设计 game-neutral 事件 phase；
- 明确同阶段 deterministic order；
- 发现当前 Reader/patch 隐含顺序；
- 不直接复制其类型名或结论。

### 3.2 外部 test oracle

候选流程：

```text
Damage Forecast pure scenario
→ test-only adapter
→ validated headless runner
→ observed native result
→ differential report
```

可能验证：

- Block 消耗；
- HP-loss modifiers；
- Poison / pre-action survival；
- turn-end Status/Curse；
- `DamageVar`；
- projection 输入；
- fixed seed 顺序。

以上是候选问题，不代表 StS2Sim 当前已经具备对应能力。DF-S0 已确认：

- turn-end phase、Burn/Doubt、retain/discard 和 fixed-seed 行为可作为有限 L2 参考；
- 真实敌人回合、AttackIntent、Poison pre-action survival 和多敌人目标当前明确不受支持；
- Tungsten Rod / Beating Remnant 没有对应的可消费验证；
- `N` / `-N` projection 与 HUD lifecycle 是 Damage Forecast 自身语义，不应交给外部工具决定。

限制：

- oracle 不能决定生产逻辑；
- oracle 缺失时普通 contracts/build 必须完全可运行；
- 不把 oracle 结果自动写成 RuntimeVerified；
- synthetic environment footgun 必须进入结果元数据。

### 3.3 Fixture / scenario 生成

可以评估把 headless 结果转换为：

- game-neutral fixture；
- ordered event trace；
- expected native lane；
- unsupported reason；
- target/build/hash metadata。

生成后的 fixture 必须可离线审查，不能在普通测试中隐式重新调用外部工具。

---

## 4. Gate 计划

### DF-S0 — Read-only Dependency Review

类型：只读。

动作：

- 读取当前 task index、project state、Forecast Engine task card 和 guardrails；
- 核对 branch/HEAD/status 与用户未提交改动；
- 读取 StS2Sim 独立任务最终报告与 capability manifest；
- 判断它是否达到进入本任务的最低证据门槛；
- 以当前测试源码为准列出既有 tests 与候选利用的重叠。

输出：

- 当前基线；
- 可用 / 不可用 / 条件可用结论；
- 最小第一批预计 diff；
- DF-S1 授权请求。

不得修改源码、测试、任务索引或运行外部工具。

#### DF-S0 增量证据

- Result: `Complete — conditionally useful / reference-only for implementation`；
- Damage Forecast baseline: `main` / `f627f9e85d2d2af8b65f624a3ba93ccdf0621562`；工作树原有一份架构任务卡修改和本卡未跟踪文件；
- Contract inventory: 当前测试源码有 267 个唯一 case ID；`267/267` 是 current authority 已记录的 stable/beta 证据，不是 DF-S0 新运行结果；
- StS2Sim upstream: exact upstream `25e8bf62d1587d8089aea78835194d1283734135`，MIT License；
- StS2Sim target evidence: stable `v0.107.1 / 59260271` 与 beta `v0.109.0 / c12f634d` 均为 `HeadlessVerified / L2`，跨进程 deterministic match，`RuntimeVerified=false`；
- Blocking gap: 当前 SS7-A/B/C 由 10 个已跟踪修改和 4 个未跟踪新文件组成；现有 HEAD `b38fa043333885549c5a1e988f11ade536bd04ae` 不能唯一代表已验证的双目标能力；
- Capability boundary: real enemy turns、multi-enemy random targeting、multiplayer targets、部分 Godot/CombatRoom 路径和 live runtime parity 仍 unsupported/unverified；
- Adoption boundary: 当前只能用于设计、阶段顺序参考和有限离线 fixture 候选，不得进入 DF-S2 adapter/oracle 实现；
- Preserved: 未修改源码、测试、任务索引、安装、游戏、Workshop 或 Git。

### DF-S1 — Game-neutral Boundary Design

目标：只设计最小边界，不接入工具。

#### DF-S1.1 边界形式

采用版本化、可离线审查的 JSON 数据契约：

```text
ForecastScenario JSON
→ explicit optional test-only adapter process
→ NativeObservation JSON
→ offline validation / differential report
```

本阶段不定义生产 runtime provider，不建立 plugin discovery、DI 容器或长期服务。普通
contract harness 只允许读取手写或已冻结的离线 fixture；任何外部进程调用必须由未来
单独命令显式启动，不能成为普通 guardrails、build 或玩家运行的隐式前置条件。

#### DF-S1.2 数据契约草案

```text
ForecastScenario
OrderedObservedEvent
NativeObservation
ObservationMetadata
UnsupportedObservation
```

`ForecastScenario`

- `schemaVersion`：边界 schema 版本；
- `scenarioId`：稳定、与工具无关的场景 ID；
- `targetChannel`：`stable` 或 `beta`；
- `seed`：显式固定种子；
- `requestedCapabilities`：本场景要求观察的能力集合；
- `initialState`：只包含场景所需的 game-neutral 数值和枚举；
- `orderedInputs`：显式 phase/order 的输入事件；
- `expectedEvidenceLevel`：允许的最高证据等级，不得写成 runtime 承诺。

`OrderedObservedEvent`

- `eventId`、`sourceId`：稳定身份，不使用显示名称作为 key；
- `phase`、`order`：显式阶段和同阶段顺序；
- `lane`：`Blockable`、`DirectHpLoss` 或明确的非伤害 lane；
- `granularity`：`SingleEvent`、`PerHit`、`Aggregate`；
- `amount`：已观察值，不允许负值或 overflow 静默归一；
- `status`：`Observed`、`Unsupported`、`Unavailable`；
- `reasonCode`：稳定、可机器判断的原因码；
- `providerDetail`：仅供诊断的可选文本，不能参与业务判断。

`NativeObservation`

- `scenarioId` 与输入严格对应；
- `status`：完整观察、部分观察、unsupported 或 provider failure；
- `events`：有序事件列表；
- `blockableTotal`、`directHpLossTotal`：只能从完整可信事件导出；
- `unsupported`：零到多个结构化 `UnsupportedObservation`；
- `metadata`：必需的 `ObservationMetadata`；
- `rawProviderOutputHash`：冻结原始输出的 SHA256，便于离线复核。

`ObservationMetadata`

- `schemaVersion`；
- `providerId` 与 `providerVersion`；
- `sourceRevision`：必须是代表当前 provider 源码的 exact commit；
- `sourceDirty`：进入 DF-S2 时必须为 `false`；
- `gameChannel`、`gameVersion`、`gameCommit`、`gameAssemblySha256`；
- `adapterVersion`、`providerArtifactSha256`；
- `evidenceLevel`、`runtimeVerified`；
- `seed`、`runId`、`generatedAtUtc`；
- `capabilityManifestSha256` 与 `unsupportedRegistrySha256`。

`UnsupportedObservation`

- `scope`：场景、阶段、事件或机制；
- `reasonCode`：稳定原因码；
- `providerMechanismId`：可选外部 registry ID；
- `detail`：审查信息；
- `failClosed`：必须为 `true`，否则整份 observation 无效。

这些名称是边界语义草案，不授权创建同名生产类型。

#### DF-S1.3 验证与 fail-closed 规则

以下任一情况都必须把观察结果标为不可比较，不得生成 `Match`：

- schema version 未知；
- `scenarioId`、seed 或 requested capability 不一致；
- `sourceRevision` 缺失，或 `sourceDirty=true`；
- game channel/version/commit/DLL hash 与请求不一致；
- adapter version、artifact hash 或 capability/unsupported registry hash 缺失；
- provider 报告 unsupported、partial、timeout、crash 或非零退出；
- event ID 重复、phase/order 缺失、lane/granularity 非法；
- aggregate 结果被伪装成 single/per-hit；
- partial events 却提供无说明的完整 totals；
- 相同 target/seed 的重复运行输出不一致；
- provider 将 L2 headless 结果声明为 L3/runtime evidence。

外部观察只能产生 differential evidence，不能决定 Damage Forecast 的生产规则。Mismatch
只能分类为 `Expected version difference`、`Tool unsupported`、`Tool suspect`、
`Forecast suspect` 或 `Needs runtime evidence`；不得自动修改生产代码或更新
`RuntimeVerified`。

#### DF-S1.4 当前能力处置

| 候选能力 | 当前 StS2Sim 证据 | DF-S1 处置 |
|---|---|---|
| fixed seed / cross-process repeat | stable/beta L2 exact match | 可进入未来 adapter metadata/health check |
| Burn turn-end HP loss | stable/beta SS4 代表例 | 可作为有限 fixture 候选 |
| Doubt turn-end Power | stable/beta SS4 代表例 | 可作为非伤害 turn-end fixture 候选 |
| retain/discard 与 turn-end chain | 有 headless 行为和显式手工阶段 | 仅作阶段参考；观察必须标记 synthetic |
| Ethereal | 有手工实现声明，缺少本任务所需独立矩阵 | reference-only |
| 基础 Block 获取/下一回合清除 | 有 L2 代表例 | 不等于 Forecast 的 Block 分类/消耗 oracle |
| current/Power/Relic Block order | 无对应完整观察 | unsupported |
| AttackIntent / real enemy turn | registry 明确 unsupported | unsupported |
| Poison pre-action survival / Intent removal | real enemy turn 不受支持 | unsupported |
| Intangible HP-loss modifier | 只有有限 Power/card 证据 | unsupported for differential oracle |
| Tungsten Rod / Beating Remnant | 无对应验证 | unsupported |
| Toxic/Decay/Infection/Wither `DamageVar` | 未形成目标专属验证矩阵 | unsupported；不替代现有 contracts |
| `N` / `-N` projection、HUD lifecycle | Damage Forecast 自身产品语义 | provider out of scope |
| multi-enemy / multiplayer target | registry unsupported/fail-closed | unsupported |

#### DF-S1.5 未来候选文件清单

DF-S1 不创建下列文件。若 DF-S2 获批，最小候选范围是：

```text
tests/DamageForecast.ContractTests/ExternalObservation/
  ExternalObservationContract.cs
  ExternalObservationValidationCases.cs
  ExternalObservationFixtureCases.cs

tests/DamageForecast.ContractTests/fixtures/external-observation/
  <reviewed scenario and observation JSON only>

tools/Invoke-DamageForecastExternalObservation.ps1
```

约束：

- 不引用 StS2Sim namespace；
- 不依赖其进程、源码或内部 DTO；
- 输入/输出能由 mock 和手写 fixture 提供；
- 复用现有 `DamageForecast.ContractTests` runner，不创建第二套普通 contract harness；
- provider-specific 调用只允许位于显式的 `tools/` adapter 边界；
- 普通 `scripts/Test-ForecastGuardrails.ps1` 不要求外部工具存在；
- 不修改任何 `src/DamageForecast/` 生产文件；
- 不改 HUD；
- 不改 BaseLib；
- 不改发布物；
- 不提前建立复杂 plugin system。

#### DF-S1.6 未来自动化测试矩阵

无需外部工具、可由手写 fixture 完成：

- valid schema / unknown schema；
- exact scenario ID / mismatched scenario ID；
- clean exact revision / missing revision / dirty revision；
- stable exact metadata / beta exact metadata / cross-target mismatch；
- DLL、adapter、artifact、capability 和 registry hash 缺失或不符；
- ordered events / duplicate ID / missing order / invalid lane / invalid granularity；
- single/per-hit/aggregate 保真；
- complete totals / partial events 禁止伪造 totals；
- explicit unsupported / timeout / crash / malformed JSON；
- same-seed deterministic match / deterministic mismatch；
- L2 evidence 保持 L2，不能升级为 RuntimeVerified；
- reviewed Burn、Doubt、retain/discard fixture；
- AttackIntent、Poison pre-action、modifier、multi-enemy fixture 必须 fail-closed。

未来只有 DF-S2 获批后才能执行的 adapter tests：

- exact clean provider revision 握手；
- stable/beta 目标自动/显式选择；
- process timeout、cancel、non-zero exit 和残缺输出；
- raw output hash 与转换后 observation 对应；
- capability/unsupported registry snapshot 一致；
- 同 target/seed 两次独立进程结果一致；
- 外部工具不存在时，普通 contracts/build 仍完全通过。

#### DF-S1.7 DF-S2 进入条件

DF-S1 关闭时，DF-S2 为 `Blocked`，不是普通 approval pending。当时要求 StS2Sim
独立任务先提供：

1. 代表 SS7-A/B/C 当前能力的 exact maintained-fork commit；
2. clean worktree 证据；
3. capability manifest 中可核对的 provider source revision；
4. exact stable/beta game hash、adapter version 和 provider artifact hash；
5. capability 与 unsupported registry 的冻结 hash；
6. 当前 L2 deterministic evidence 与上述 revision/hash 一致；
7. 用户对 DF-S2 test-only adapter 的单独明确批准。

Damage Forecast Session 不得自行修复、提交或收口 StS2Sim 来满足这些条件。

该历史 Blocked 状态后续已由 SS7 本地 checkpoint
`42396191e4bd66ca8ab27cd9b9b9f4f537966978`、readiness revalidation 和用户对
DF-S2A / DF-S2B 的分别明确批准解除；不代表 DF-S3 已获授权。

#### DF-S1 增量证据

- Result: `Complete — documentation design only`；
- Boundary: versioned JSON scenario/observation contract；无生产 provider 或 plugin system；
- Fail-closed: revision、dirty state、target、game DLL、adapter、artifact、capability、unsupported、event order 和 determinism 均为强制校验；
- Files: 仅列出未来 tests/fixtures/tools 候选；本 Gate 未创建；
- Matrix: 离线 schema/metadata/event/unsupported fixtures 与未来显式 adapter tests 已分离；
- Adoption: 当前仅允许事件阶段参考和有限 Burn/Doubt/retain/discard fixture 候选；
- Preserved: 未修改源码、测试、HUD、BaseLib、普通 guardrails、发布物、安装、游戏、Workshop 或 StS2Sim；
- Next at DF-S1 closure: DF-S2 在 exact clean maintained-fork checkpoint 出现前保持
  Blocked；该状态已由后续 DF-S2 readiness 和实现证据 supersede。

### DF-S2 — Optional Test-only Adapter

前置：

- StS2Sim capability 已验证；
- DF-S1 已批准；
- 用户明确批准实现。

原则：

- adapter 只存在于 tests/tools；
- 进程调用显式、可关闭；
- 普通 guardrails 不要求安装外部工具；
- engine version、game build、DLL hash、adapter version 必须随结果返回；
- mismatch 必须 fail-closed；
- 不分发游戏 DLL；
- 不把外部代码加入生产项目。

#### DF-S2 readiness revalidation

- Result: `Complete — Ready`；
- StS2Sim branch:
  `local/sts2sim-ss3-ss5-checkpoint-20260724`；
- Exact provider source revision:
  `42396191e4bd66ca8ab27cd9b9b9f4f537966978`；
- Provider source tree: `clean`；
- stable: `v0.107.1 / 59260271 / stable-v1 / HeadlessVerified`；
- beta: `v0.109.0 / c12f634d / beta-v1 / HeadlessVerified`；
- capability manifest、unsupported registry、game DLL 和 provider artifact 均有冻结
  SHA256；
- 两个目标的 deterministic evidence 与相同 source revision/hash 绑定；
- `RuntimeVerified=false`；没有启动游戏或产生 L3 证据。

#### DF-S2A — Offline Boundary Contract

- Result: `Complete — local checkpoint`；
- Commit:
  `286fd56df62afa789260eccb4d07810e08f36a1a`；
- Scope: 只在现有 `DamageForecast.ContractTests` 中增加 game-neutral DTO、严格离线
  JSON loader、fail-closed validator、手写 stable/beta/异常/unsupported/determinism
  fixtures；
- Cases: `EO-001` 至 `EO-014`；
- Validator: unknown schema、dirty/missing revision、cross-target、重复事件、partial totals、
  unsupported、determinism 和 totals mismatch 均不可比较或 fail-closed；
- Preserved: 无 StS2Sim namespace、无进程 adapter、无生产源码、HUD、普通 guardrail、
  安装或 Workshop 修改；
- Evidence at Gate closure: 当时工作树普通 contracts `291/291`，stable/beta Release
  均 0 warning / 0 error；
- Evidence level: offline contracts/build only；`RuntimeVerified=false`。

#### DF-S2B — Explicit Optional Process Adapter

- Result: `Complete — local checkpoint`；
- Commit:
  `573baa66adb2d845de9d324679f9b9c2f8f94b2c`；
- Adapter:
  `tools/Invoke-DamageForecastExternalObservation.ps1`，只能由显式命令启动；
- Provider boundary: 固定 source revision、checkpoint/capability/unsupported snapshot
  SHA256、stable/beta game identity 和 provider artifact SHA256 全部交叉核对；Git clean
  核验只使用 per-command `safe.directory`，不修改全局配置；
- Complete mappings: 仅 reviewed stable/Burn 与 beta/Doubt；其他 scenario 返回结构化
  `Unsupported`，不从输入猜测观察值；
- Fail-closed: cross-target、timeout、cancel、non-zero exit、missing tool、零退出但残缺
  output 均不得生成 `Complete`；
- Explicit adapter tests: `10/10`；
- Determinism: stable 同 target/seed 两次独立进程 raw output SHA256 完全一致
  (`873a151286cd04c622f07721d7c3d76dee23aafb460dbc10b4e9ca64fcc9a585`)；
- Ordinary guardrail independence: `scripts/Test-ForecastGuardrails.ps1` 未接入 adapter 或
  StS2Sim；最终 stable/beta 均 `298/298`，Release 0 warning / 0 error，
  `QUALITY_GATE targets=2 status=PASS`；
- Provider after invocation: StS2Sim 仍为 exact clean
  `42396191e4bd66ca8ab27cd9b9b9f4f537966978`；
- Preserved: 未修改生产源码、普通 guardrail、HUD、安装、游戏或 Workshop；未 push、
  tag 或发布；
- Evidence level: explicit headless differential evidence / L2 only；
  `RuntimeVerified=false`。

#### DF-S2C — Evidence Closure

- Result: `Complete — documentation checkpoint`；
- Authority: 本卡已从 `DF-S1 Complete / DF-S2 Blocked` 更新为
  `DF-S2 Complete / DF-S3 Not Authorized`；
- Checkpoints: DF-S2A `286fd56`；DF-S2B `573baa6`；
- No new execution: 本 Gate 不重新调用 StS2Sim，不重跑 tests/build；
- Preserved: 不修改源码、测试、tools、普通 guardrail、README、HUD、安装、游戏或
  Workshop；
- Next: 只有用户单独批准后才能进入 DF-S3A read-only matrix design；DF-S3 不能因
  DF-S2 完成而自动开始。

### DF-S3 — Differential Scenario Matrix

最低矩阵：

- current Block only；
- Power/Relic Block order；
- direct HP loss；
- Intangible / Tungsten Rod / Beating Remnant；
- Poison；
- Status/Curse turn-end；
- discard/retain/Ethereal；
- unknown/unsupported；
- same seed repeat；
- stable/beta difference。

每项结果分为：

```text
Match
Expected version difference
Tool unsupported
Tool suspect
Forecast suspect
Needs runtime evidence
```

不允许看到 mismatch 就自动修改生产代码。

### DF-S4 — Adoption Decision

比较：

1. 只保留文档参考；
2. 保留离线 fixture generator；
3. 保留可选 differential test tool；
4. 建立长期 external oracle；
5. 完全不采用。

评估：

- 维护成本；
- 对当前 267-case contract inventory 的增量价值；
- silent error 风险；
- stable/beta 更新成本；
- CI/本机可重复性；
- 是否引入不必要架构。

最终由用户决定；不自动进入生产使用。

---

## 5. 硬性边界

- 生产 DLL 不引用、启动或探测 StS2Sim；
- HUD 不读取外部工具结果；
- Forecast Engine 规则不以外部工具存在为前提；
- 发布包不携带 StS2Sim、游戏 DLL 或其缓存；
- 普通玩家不需要安装外部工具；
- Workshop 不修改；
- 已知 HUD defect 不在本任务修复；
- 不重复建设已有纯 tests；
- 不因外部结果覆盖现有 runtime evidence。

---

## 6. 完成标准

- 明确 StS2Sim 对本项目是无用、参考、条件有用还是长期有用；
- 对每种候选利用有证据与成本；
- game-neutral boundary 不绑定具体工具；
- 现有 contracts 与新增价值无明显重复；
- stable/beta mismatch 能被标记而非静默；
- 没有生产依赖、安装或发布变化；
- 最终结论写回本任务卡，其他 current authority 只在产品事实确有变化时更新。

---

## 7. 给专门 Session 的启动提示

```text
请完整阅读：
C:\Users\ROG\Documents\Codex\STS2-Party-Watch-v2\docs\task-notes\sts2sim-damage-forecast-evaluation-master-task-card.md

只执行 DF-S3A read-only Differential Scenario Matrix Design；这不是 DF-S3 实现授权。

以 DF-S2A commit 286fd56、DF-S2B commit 573baa6、固定 StS2Sim source revision
42396191e4bd66ca8ab27cd9b9b9f4f537966978 和本卡当前 capability boundary 为依据，
逐项把最低矩阵分类为：

Comparable
Expected version difference candidate
Tool unsupported
Needs runtime evidence
Provider out of scope

不得新增或修改 scenario/fixture/adapter，不得调用 StS2Sim，不得修改生产源码、测试、
tools、普通 guardrail、HUD、设置、安装、游戏或 Workshop，不得进行 Git 写操作。
只输出 matrix、证据来源、blocking gaps 和未来最小候选 diff。
```
