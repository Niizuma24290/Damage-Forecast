# Damage Forecast HUD 预设位置与结束回合冻结实施主任务卡

## Current Control

Classification: CLOSED_TASK
Area: Combat UI
Touches: Hub UI、Settings Compatibility
Priority Tag: P2
Queue: Parked
State: Closed
Last completed: I3 — Final Authority / Git Checkpoint
Next: None — task closed
Approved: 任务登记、I0、I1、I2、I2-R～I2-R10 与 I3；Codex 游戏启动、push、tag、Workshop 和发布未批准
Evidence: `I3 Final Closure Checkpoint — 2026-07-31`
Repository: Implementation checkpoint `a4b2e23`; this docs-only commit is the closure marker

## Goal

按已批准的 HP-2 唯一方案实施有限 HUD 预设位置系统，并修复结束回合后空重算覆盖最后有效预测的问题：

- `N`、`-N` 和明细各自使用同一个 `HudPlacementPreset`；
- V1 预设为血条左、右、上、下与结束回合按钮上方；
- 相同预设使用确定性 cluster，不保存任意屏幕坐标；
- 血条与按钮以子节点 Transform 继承为主，跨父节点精确换算为兜底；
- 结束回合冻结固定启用，锁定点击前最后有效 live snapshot；
- 主菜单预览与实战共用同一个纯布局核心。

本卡是实施任务的唯一进度权威。HP-0～HP-2 调查卡仍是前置证据，不与本卡竞争当前实施状态。

## Global boundaries

- 不增加 Boss、遭遇、房间或卡牌专用 HUD 位置分支。
- 不制作自由拖动编辑器，不保存任意屏幕坐标，不依赖固定分辨率。
- 只显示本地玩家 HUD；不扩展队友、共享或正式多人预测。
- 保留现有 Visibility Authority、覆盖层语义和 combat-state read-only 边界。
- 必须保留并行工作；共享 dirty 文件只允许加入本任务自有的最小 hunk。
- 安装、游戏启动、Git checkpoint、push、tag、Workshop 和发布均按 Gate 单独授权。
- 任何未来游戏启动前必须先停止并告知用户，由用户自行启动。

## Approved design contract

1. 血条侧 `DamageForecastHudRoot` 直接挂到本地 `NHealthBar`。结束回合侧分为两层：活动 Root 直属当前 `NEndTurnButton`，使用按钮局部 Rect 并继承其 Transform；点击前把最后可见文字与精确屏幕位置复制到本地 `NCombatUi` 冻结 Root，随后隐藏活动 Root，使按钮退场动画不会带走冻结 HUD。取消点击或下一玩家回合清理冻结层并恢复活动层；动态预设只允许逐帧重算血条/角色锚点布局，不逐帧重算伤害预测。
2. `HudLayoutEngine` 只接收同一局部空间中的锚点 Rect、可用边界、内容尺寸、预设和顺序，输出布局结果。
3. `ExpectedHpLossPlacementPreset`、`IncomingDamagePlacementPreset`、`DetailsPlacementPreset` 使用同一个五值枚举。
4. 同预设时，`IncomingDamagePlacement` 继续决定 `N` / `-N` 左右顺序；明细随后排列，必要时第二行；极端空间不足优先隐藏明细。
5. 只沿锚点切线方向约束到实际可用边界，不自动翻转用户预设。
6. 结束回合接受路径先捕获 pending snapshot，再以匹配 owner / generation 确认；拒绝或取消时不得冻结。
7. turn-end 兜底只允许提交最后有效 live snapshot；不可显示重算不得覆盖有效 committed snapshot。
8. 配置 V1 严格 18 键；V2 严格 20 键。V1 的单一锚点迁移到三个位置，冻结值规范化为 true。
9. V2 只有三个位置相同且能映射回旧四值血条枚举时才允许无损回滚；否则 fail closed。

### I2 Layout Clarification — 2026-07-29

- 设置仍保持三个 placement：`-N`、`N`、明细；不增加第四个下拉框。护盾与生命损失明细共享一个 placement，并作为一个不可拆分放置的 composite 明细组；组内可连续显示两个数值，但不能分别选择位置。
- 血条左、右侧采用“离血条最近的单元固定为第 1 个，新增单元向外增长”的规则。单个数值紧贴血条；两个、三个、四个数值依次向外扩展，不因数量变化重新居中。左侧视觉顺序为 `4 3 2 1 | 血条`，右侧为 `血条 | 1 2 3 4`。
- `IncomingDamagePlacement` 继续决定同位置时 `N` / `-N` 的先后；护盾、生命损失明细作为随后两个独立单元，沿相同增长方向排列。
- 角色上方预设必须避开本机角色模型。游戏原生事件或遗物造成角色模型增大时，HUD 随模型上边界向上移动；Mod 改变模型大小暂不纳入本轮保证。
- 角色充能球可能与角色上方 HUD 发生空间竞争，本轮保留为已知延后项，不作为 I2-R 失败条件。
- 角色下方预设必须考虑本机角色 Buff 行。Buff 数量导致现有位置遮挡时，HUD 下移一行；该位移高度与游戏实际一行 Buff 的高度一致。
- 结束回合按钮上方预设以按钮中线为整个 cluster 的中心：一个单元位于正中；两个单元分列中线两侧；四个单元仍以中线对称展开。其一般规则是整个可见 cluster 围绕按钮中线居中。
- 三个 HUD 组可在任何时候分别选择不同 placement 并同时显示：`-N`、`N`、`明细组`。明细组内部的护盾与生命损失始终一起出现、一起移动，但仍以两个连续数值单元参与该组布局。
- 结束回合按钮上方 HUD 当前还存在偶发不显示和闪烁，须与中心线布局一起修复；修正后同一有效 snapshot 和 placement 下不得因锚点重新解析、Root 可见性切换或刷新顺序产生间歇隐藏。
- 结束回合点击成功后，HUD 的数值与点击瞬间最后可见位置一起冻结；按钮随后退场时 HUD 不跟随下移或消失。取消结束回合时恢复动态跟随。
- 角色上方的后续锚点语义：除储君外，以角色头部最高点为垂直基准并忽略武器、法杖等装备高度；储君以椅子最高点为垂直基准。充能球空间竞争仍保持延后。本条仅登记后续人物缩放修正，不在 I2-R4 实施。
- 本记录只采用用户文字确认；不保存用户截图、截图副本或临时图片路径。

## Gate I0 — Contract / Schema

Status: Complete

Allowed:

- 新增纯布局模型、枚举与 contract。
- 新增纯 snapshot 生命周期 pending / confirm / cancel contract 和最小策略实现。
- 新增 V1/V2 严格 key set、升级与回滚拒绝 contract 和最小 schema policy。
- 运行不安装、不启动游戏的 contract / compile 验证。
- 回填本 Gate 增量证据。

Not allowed:

- 接线正式 Godot 节点、结束回合按钮 Patch、设置页面或主菜单预览。
- 安装 Mod、启动游戏、读取新的运行时证据。
- Git checkpoint、push、tag、Workshop 或发布。

Pass:

- 五个预设、同位置顺序、边界约束和明细降级有确定性纯 contract。
- 点击前捕获、匹配确认、取消、空重算保护、下一回合和覆盖层语义有纯 contract。
- V1 18 键、V2 20 键、V1→V2 三位置复制、冻结规范化和有损回滚拒绝有严格 contract。
- 当前获批 headless 验证通过，且未覆盖并行工作。

Stop:

- I0 完成后更新 `Current Control`，停止并等待 I1 单独批准。

### I0 Checkpoint — 2026-07-28

- Result: Complete / HeadlessVerified for the approved current-target contract scope。
- Layout: 新增单一五值 `HudPlacementPreset`、无 Godot / Viewport / 固定分辨率依赖的 `HudLayoutEngine`，覆盖同位置 `N` / `-N` 顺序、明细换行/隐藏、切线边界约束、逻辑单位 offset 和独立预设不自动换位。
- Freeze: `HudSnapshotLifecyclePolicy` 新增 pending snapshot / generation 的 prepare、confirm、cancel；不可显示 refresh 保留最后有效 live snapshot，空 turn-end 重算回退到最后有效值，已有 committed snapshot 不再被后续重算覆盖。
- Config: 新增严格 V1 18 键 / V2 20 键 schema policy；V1 单锚点复制到三个 V2 placement，冻结值强制规范化为 true；不同 placement 或 `EndTurnButtonAbove` 均拒绝有损 V2→V1 回滚。
- Contracts: 新增 `HL-001..010`、`HF-001..009`、`HPC-001..008`，共 27 个 I0 contract。
- Verified: `C:\sts2\dotnet\dotnet.exe run --project .\tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release`；`456/456` passed，0 failed，编译成功。
- Preserved: 未接 Godot 节点、结束回合按钮 Patch、设置页面或预览；未安装、未启动游戏、未执行 Git checkpoint。共享 dirty `Program.cs` 与 `README.md` 只增加本任务最小 hunk，其余 Forecast Timeline / CodeGraph / HUD surface 并行改动保持原状。
- Boundary: 本结果不是 stable / beta 双目标 build，也不是运行时验证。I1、I2、I3 仍须分别批准。
- Stop: I0 到此停止，等待用户单独批准 I1。

## Gate I1 — Implementation / Headless

Status: Complete

- 接线 Root、Resolver、正式 HUD 布局、固定冻结、设置页面和主菜单预览。
- 运行 contract、stable / beta build 与发布树检查。
- 不安装、不启动游戏；完成后停止等待 I2。

### I1 Checkpoint — 2026-07-28

- Result: Complete / HeadlessVerified for the approved I1 scope.
- HUD: `DamageForecastHudRoot` 分别作为本地 `NHealthBar` 与 `NEndTurnButton` 的直接子节点；`HudAnchorResolver` 使用完整 canvas transform 把血条或按钮矩形换算到对应 Root 的局部空间，并只在刷新或缓存失效时解析结束回合按钮。
- Layout: 正式 HUD 与设置页预览均调用同一个 `HudLayoutEngine`；`-N`、`N` 和明细各自使用统一五值 `HudPlacementPreset`，相同预设继续服从原 `IncomingDamagePlacement` 顺序。
- Freeze: `CallReleaseLogic` Prefix 捕获点击前 pending snapshot，匹配的 `OnDisable` 确认，未接受的 release 在 Postfix 取消；turn-end 兜底只提交 pending / last-valid live snapshot，不再重算预测。冻结固定启用且设置页不再显示开关。
- Config: BaseLib 使用三个独立 placement 属性；V1 18 键以事务备份迁移为严格 V2 20 键并把 freeze 规范化为 `True`。严格 V2 可幂等重启；只有三个位置相同且能映射到旧四值血条预设时允许无损回滚，其余回滚 fail closed。
- Contracts: `463/463` passed，0 failed；新增实际文件迁移、V2 重启、无损回滚与有损回滚拒绝证据。
- Dual target: `Test-ForecastGuardrails.ps1` 对 stable `v0.107.1` 与 beta `v0.109.0` 均通过 contract、Release build、shadow-off artifact、`git diff --check` 与 artifact review；两目标均 0 warning / 0 error。
- Publish tree: `Build-DualTargets.ps1 -SkipRestore` 生成并验证 stable / beta 树；每树严格只有 `damage-forecast.dll` 与 `damage-forecast.json`，两树 SHA256 完全一致，无需 hash-difference approval。
- Preserved: 未安装 Mod、未启动游戏、未读取新的运行时证据、未执行 Git checkpoint / push / tag / Workshop / release。共享 dirty 文件只加入本任务最小 hunk，其余 Forecast Timeline 等并行改动保持原状。
- Boundary: 本结果不是 I2 真实游戏运行时验证。I2 与 I3 仍须分别批准。
- Stop: I1 到此停止，等待用户单独批准 I2。

## Gate I2 — Runtime

Status: Approved / In Progress

### I2 Pre-install Record — 2026-07-28

- Runtime target: 本机 beta `v0.109.1`，commit `c8c577f6`；游戏在安装计划与执行前均须保持未运行。
- Staging: `work/publish/beta-v0.109.1/damage-forecast`，严格两文件 `damage-forecast.dll` / `damage-forecast.json`。
- Staging identity: manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；DLL SHA256 `23247659B143E491C914935631171901ED39AA586F05DD04A3A110F40BCCC49F`。
- Active before install: `damage-forecast` `v0.3.0`；manifest SHA256 与 staging 相同；DLL SHA256 `D1F6626E710CA9F6962E2130C9DA9D90FD8BE2C94CAE0E87DBED9D66ABAC1CFB`。
- Install target: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`；transaction `20260728T005500000Z-i2-beta-v01091`。
- Recovery: 安装前活动版本整体移动到 Loader 扫描根外的 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\20260728T005500000Z-i2-beta-v01091-damage-forecast-v0.3.0`；安装失败由脚本自动恢复旧活动树。当前 legacy / current config 均不存在，因此本次安装不修改配置。
- Stop boundary: 安装与身份审计完成后必须在游戏启动前停止，由用户自行启动。

### I2 Install Record — 2026-07-28

- Result: Installed / Awaiting user launch.
- Active audit: Loader 扫描根内只有一个 `damage-forecast` 身份，legacy `0`、target `1`、orphan artifact `0`；活动树严格只有 manifest 与 DLL。
- Installed hashes: manifest `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；DLL `23247659B143E491C914935631171901ED39AA586F05DD04A3A110F40BCCC49F`，与登记 staging 完全一致。
- Recovery audit: 旧活动 DLL 已验证为 `D1F6626E710CA9F6962E2130C9DA9D90FD8BE2C94CAE0E87DBED9D66ABAC1CFB`，备份位于登记的 Loader 扫描根外路径；安装 ledger 为 `20260728T005500000Z-i2-beta-v01091-install-ledger.json`。
- Launch boundary: 审计时游戏未运行；Codex 到此停止，不代替用户启动游戏。

### I2 Pause Record — 2026-07-28

- State: 用户休息，I2 暂停至下一次继续。
- Completed: beta `v0.109.1` 精确构建、可恢复安装、活动身份与安装/备份哈希审计。
- Pending first evidence: 设置页可打开、三个位置下拉框、固定冻结开关不显示、预览单项切换。
- Pending later evidence: beta 五预设/跟随/碰撞、冻结/覆盖层/战斗重入/UI Scale/本地多人，以及 stable 关键路径复测。
- Truth boundary: 尚未收到本次安装后的任何用户运行时通过结论；I2 不是 RuntimeVerified，也未形成 I2 checkpoint。
- Resume: 下次从“设置页第一轮检查”继续，不重新构建或安装，除非安装状态发生变化。

### I2 Resume Record — 2026-07-28

- State: 用户恢复 I2；当前安装保持不变。
- Next evidence: 先完成设置页第一轮检查，再进入 beta 实战位置与冻结验证。

### I2 Runtime Finding RF-I2-001 — Preview End-turn Placement

- Passed: 设置页正常打开；三个 placement 选项存在；固定冻结开关未显示。
- Failed: 用户将一项切换到“结束回合按钮上方”后，预览发生移动但对应数字不可见。
- Clarified mismatch: 当前实现的固定宽度明细文本块、血条侧 cluster 增长方式、结束回合按钮直系子节点挂载和按钮上方排列，不能完整表达 `I2 Layout Clarification — 2026-07-29` 的三组独立 placement、明细 composite 与中心线规则。
- State: Corrected headless / runtime confirmation pending；不得登记设置页预览或实战通过，也不得继续提升 I2 结论。

- 安装前记录目标与恢复方案。
- 启动游戏前必须停止并告知用户；由用户自行启动。
- 覆盖 stable / beta、常见宽高比、至少两档 UI Scale、连续移动、同预设碰撞、冻结、覆盖层、战斗重入和本地多人本地玩家绑定。

## Gate I2-R — Layout Repair

Status: Headless Complete / Repair Install Pending

### I2-R Approval — 2026-07-29

- User authorization: 将结束回合按钮上方 HUD 偶发不显示/闪烁纳入同一修正，并确认三个 HUD 组可随时分别放置；护盾与生命损失属于同一明细组、一起出现。
- Authority boundary: 允许修正实现、contract、设置页预览并执行不启动游戏的 stable / beta 验证；不包含安装、游戏启动、Git checkpoint、push、tag、Workshop 或发布。

Allowed:

- 保持一个 `DetailsPlacementPreset` 和现有三下拉框 schema；明细继续作为一个 composite 组一起移动，并按实际内容宽度参与 cluster。
- 修正血条左右侧由近及远增长、角色上方模型避让、角色下方 Buff 行避让，以及结束回合按钮中线对称 cluster。
- 同步正式 HUD、设置页预览和纯布局 contract；运行不安装、不启动游戏的 contract、stable / beta build 与发布树检查。
- 回填 RF-I2-001 的修正证据并停止，等待修正版安装与用户运行时复测授权。

Not allowed:

- 自动处理 Mod 改变角色模型大小或充能球冲突。
- 安装修正版、启动游戏、Git checkpoint、push、tag、Workshop 或发布。

### I2-R Headless Checkpoint — 2026-07-29

- Layout: 血条左侧把第一个 HUD 组固定为最靠近血条，后续组向外增长；右侧保持同一由近及远语义。结束回合 placement 把整个可见 cluster 围绕按钮中线居中。
- Independent groups: `-N`、`N`、composite 明细组继续使用三个独立 placement；明细组内护盾与生命损失一起移动，并按实际 RichText 内容宽度参与布局。
- Stable end-turn surface: 结束回合 Root 从按钮直系子节点迁到稳定的本地 `NCombatUi`；按钮仅提供动态 Rect。临时锚点解析失败时保留上一帧有效布局，不再因按钮隐藏、裁剪、动画或刷新顺序主动闪烁。
- Dynamic avoidance: 角色上方通过匹配本地 `NCreature.Visuals.Bounds` 跟随原生模型边界；角色下方读取匹配本地角色的可见 `NPower` Rect，只有发生重叠时才按实际 Buff 行高逐行下移。充能球与 Mod 模型改变仍为延后项。
- Refresh boundary: 动态 placement 逐帧只重算锚点与布局，不重算伤害预测；普通血条左右 placement 继续依赖既有刷新与 Transform 跟随。
- Preview: 预览与正式 HUD 继续复用同一 Root / layout engine，并用护盾加生命损失的 composite 示例验证明细组。
- Contracts: 新增 `HL-011..014`，覆盖血条左侧向外增长、结束按钮中线居中、Buff 行高避让和恰好三个独立 placement 组；完整合同 `467/467` passed，0 failed。
- Dual target: `Test-ForecastGuardrails.ps1 -Target all` 对 stable `v0.107.1` 与 beta `v0.109.0` 均通过 contract、Release build、shadow-off artifact、diff check 与 artifact review；两目标均 0 warning / 0 error。
- Publish trees: stable / beta 两树均严格只有 `damage-forecast.dll` 与 `damage-forecast.json`，两树一致；DLL SHA256 `564F36B7F7E533BA90B33C3EB40515735F10DFA17E65BDB37D743F8905BC09C2`。
- Current beta: 另以本机 beta `v0.109.1` 引用完成精确 publish，仍为两文件；DLL SHA256 与双树相同。
- Boundary: 未安装修正版、未启动游戏、未执行 Git checkpoint / push / tag / Workshop / release。RF-I2-001 仍须用户运行时复测后才能关闭。

### I2 Runtime Finding RF-I2-002 — Placement Localization / Character Shrink Offset

- Passed: 用户确认血条左侧、右侧和下方 placement 的基本位置正常。
- Failed — settings: 三个新 placement 字段及下拉选项仍显示内部英文标识；`HealthBarAbove` 的用户可见含义应为“人物上方”，不是“血条上方”。
- Failed — runtime: 原生敌人效果缩小本机角色模型后，选择“人物上方”的 HUD 发生严重偏移。
- Diagnosis: 设置页本地化刷新仍遍历 V1 字段顺序，因而遗漏三个 V2 placement 字段；人物上方锚点直接采用缩放后的 `NCreature.Visuals.Bounds` 中心和顶部，模型缩放会同时拖动 HUD 的横向中线与垂直位置。
- Repair contract: 人物上方以本机血条中线作为稳定横向中线；以角色碰撞框/模型上边界得出高度，并在同一角色生命周期内只增加避让高度。角色增大时允许继续上移，缩小时不得随模型向下或横向漂移。
- State: Corrected headless / runtime confirmation pending。
- Boundary: 本记录只采用用户文字反馈，不保存截图、副本或临时图片路径；本轮不安装、不启动游戏、不执行 Git checkpoint / push / tag / Workshop / release。

### I2-R2 Headless Checkpoint — 2026-07-29

- Localization: 保留 V1 `PropertyOrder` 作为旧配置身份，新增独立 V2 本地化顺序；三个 placement 字段和下拉项现均进入本地化刷新。`HealthBarAbove` 用户可见名称改为“人物上方” / `Above Character`。
- Character-above anchor: 横向中心固定取本机血条中线；碰撞框与模型上边界只用于计算上方避让高度。同一角色生命周期保留最大避让高度，因此原生缩小不会把 HUD 向下或横向拖走，模型增大仍会继续向上扩展。
- Contracts: 新增 `HL-015..016` 与 `HPT-001..002`，覆盖缩小稳定中线/高度、增大上移、V2 本地化字段覆盖和“人物上方”文案；完整合同 `471/471` passed，0 failed。
- Dual target: `Test-ForecastGuardrails.ps1 -Target all` 对 stable `v0.107.1` 与 beta `v0.109.0` 均通过 contract、Release build、shadow-off artifact、diff check 与 artifact review；两目标均 0 warning / 0 error。
- Publish trees: stable / beta 两树均严格只有 `damage-forecast.dll` 与 `damage-forecast.json`，两树一致；DLL SHA256 `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`。
- Current beta: 另以本机 beta `v0.109.1` 引用完成精确 publish，仍为两文件；DLL SHA256 与双树相同。
- Installed audit: 当前活动 DLL 仍是 I2 初始安装版本 `23247659B143E491C914935631171901ED39AA586F05DD04A3A110F40BCCC49F`，不是 I2-R 或 I2-R2 修正版；审计时游戏未运行。
- Boundary: 未安装修正版、未启动游戏、未执行 Git checkpoint / push / tag / Workshop / release。RF-I2-001 与 RF-I2-002 均须安装后由用户运行时复测。

### I2-R2 Pre-install Record — 2026-07-29

- User authorization: 用户明确要求安装 I2-R2 修正版；游戏启动仍由用户自行执行。
- Runtime target: 本机 beta `v0.109.1`，commit `c8c577f6`；安装计划确认游戏未运行。
- Transaction: `20260729T152342017Z-i2-r2-beta-v01091`；action `target-upgrade`。
- Staging: `work/publish/beta-v0.109.1/damage-forecast`，严格两文件；manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256 `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`。
- Active before install: 唯一活动身份 `damage-forecast` `v0.3.0`；legacy `0`、target `1`、orphan artifact `0`。manifest SHA256 与 staging 相同，DLL SHA256 `23247659B143E491C914935631171901ED39AA586F05DD04A3A110F40BCCC49F`。
- Recovery: 旧活动目录将整体移动到 Loader 扫描根外的 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\20260729T152342017Z-i2-r2-beta-v01091-damage-forecast-v0.3.0`；事务失败时由安装脚本恢复旧活动目录。
- Config boundary: 本次安装事务只替换 Mod 活动目录，不写入或迁移用户配置；计划中的配置哈希为空不作为配置不存在的证据。
- Stop boundary: 安装与哈希/身份审计完成后停止，不启动游戏，不执行 Git checkpoint / push / tag / Workshop / release。

### I2-R2 Install Record — 2026-07-29

- Result: Installed / Awaiting user launch and runtime verification。
- Active audit: Loader 扫描根内唯一活动身份为 `damage-forecast` `v0.3.0`；legacy `0`、target `1`、orphan artifact `0`，活动目录严格只有 DLL 与 manifest。
- Installed hashes: manifest `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；DLL `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`，与登记 staging 完全一致。
- Recovery audit: 旧活动 DLL `23247659B143E491C914935631171901ED39AA586F05DD04A3A110F40BCCC49F` 已在登记的 Loader 根外备份中验证；安装 ledger 为 `20260729T152342017Z-i2-r2-beta-v01091-install-ledger.json`。
- Post-install plan: action `target-already-current`，确认活动版本与 staging 一致且无需再次替换。
- Launch boundary: 安装和审计期间游戏始终未运行；Codex 不启动游戏，由用户自行启动。
- Runtime checks: 仅复测三个新 placement 字段/下拉项中文化与“人物上方”文案、原生缩小前后人物上方 HUD 中线/高度，以及结束回合按钮上方 HUD 的可见性和闪烁。
- Truth boundary: 安装成功不等于 RuntimeVerified；RF-I2-001 与 RF-I2-002 仍等待用户运行时结果。

### I2 Runtime Finding RF-I2-003 — Shrink Follow / Shared End-turn Visibility

- Passed: 设置页三个 placement 字段及下拉项已完成中文化，“人物上方”文案正确；原生模型增大时人物上方 HUD 能正确上移。
- Failed — shrink: 原生模型缩小时，人物上方 HUD 仍保留正常体型的最大避让高度，离缩小后的角色过远。
- Failed — end-turn: 结束回合按钮上方 HUD 仍会闪烁，且大多数时间不显示。
- Diagnosis — shrink: I2-R2 的人物上方策略保留同一角色生命周期内的最大 clearance；这与用户确认的双向尺寸跟随不符。横向中线应继续取血条，但垂直位置必须取当前角色模型上边界，缩小后允许下移。
- Diagnosis — end-turn: 结束回合 Root 由本机 `NCombatUi` 共享，但所有非本机/摘要血条进入 `HideExisting` 时也会隐藏该共享 Root；敌方血条刷新因此可以在本机血条显示之后再次把它隐藏，表现为按刷新顺序闪烁或长期不可见。
- Repair contract: 删除跨帧最大 clearance；人物上方每帧跟随当前碰撞框/模型顶部且保持血条中线。只有已登记的本机血条失效时才允许隐藏共享结束回合 Root，未登记的敌方/摘要血条只能清理自己的血条侧 HUD。
- State: Repair complete / I2-R3 install approved。
- Boundary: 本记录只采用用户文字反馈，不保存截图、副本或临时图片路径；本轮不安装、不启动游戏、不执行 Git checkpoint / push / tag / Workshop / release。

### I2-R3 Intermediate Headless Record — 2026-07-30

- Character-above: 删除跨帧最大 clearance；横向中线继续取本机血条，垂直位置每帧重新取当前碰撞框/模型上边界，因此缩小允许向下靠近当前角色，增大继续向上避让。
- Shared end-turn surface: `HideExisting` 现在先检查血条是否已登记为本机血条；未登记的敌方/摘要血条只清理自己的血条侧 HUD，不再隐藏本机 `NCombatUi` 上共享的结束回合 Root。
- New contracts: `HL-015` 更新为缩小跟随当前顶部，`HL-016` 保留增大上移；新增 `HN-008` 固定“只有已登记本机血条可隐藏共享结束回合 Root”。这些目标 contract 均通过。
- Full contract boundary: 游戏运行期间发现 `472` 项，`468` 项通过；其余 `IU-007`、`IU-009`、`IU-010`、`C2-018` 是安装/回滚夹具被 `Slay the Spire 2 is running; refusing identity mutation` 保护拒绝，不能登记为完整合同通过，也不作为本次 HUD 代码失败。
- Dual build: stable `v0.107.1` 与 beta `v0.109.0` Release build 均 0 warning / 0 error；两发布树严格两文件且一致，DLL SHA256 `DAFB9D01E1A85600FA7969C8BE493C4F95568C61EF3F9B63831013F3816E0AA9`。
- Current beta: 另以本机 beta `v0.109.1` 引用完成精确 publish；安装活动 DLL 仍为 I2-R2 `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`，I2-R3 未安装。
- Pause: 审计时游戏进程仍在运行。需用户退出游戏后才能执行完整 guardrail；本轮不由 Codex 关闭或启动游戏，也不安装 I2-R3。

### I2-R3 Full Headless Checkpoint — 2026-07-30

- Process gate: 用户已退出游戏；完整门禁执行前确认无 `SlayTheSpire2` 进程。
- Contracts: `SUMMARY discovered=472 passed=472 failed=0 skipped=0`；包含 `HL-015`、`HL-016`、`HN-008`。
- Quality gate: stable 与 beta 两目标均 `PASS`；Release build 均 0 warning / 0 error；shadow-off artifact 与 artifact review 通过，`git diff --check` 仅报告既有换行提示。
- Exact beta artifact: `work/publish/beta-v0.109.1/damage-forecast` 严格两文件；DLL SHA256 `DAFB9D01E1A85600FA7969C8BE493C4F95568C61EF3F9B63831013F3816E0AA9`。
- Approval: 用户于本 checkpoint 后明确批准安装 I2-R3；Codex 游戏启动、I3、Git checkpoint / push / tag / Workshop / release 仍未批准。

### I2-R3 Pre-install Review — 2026-07-30

- Transaction: `20260729T173059260Z-i2-r3-beta-v01091`；plan action `target-upgrade`；plan 时游戏未运行。
- Target: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`；loader scan 中 legacy `0`、target `1`、orphan artifact `0`。
- Staging: manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；DLL SHA256 `DAFB9D01E1A85600FA7969C8BE493C4F95568C61EF3F9B63831013F3816E0AA9`。
- Active before install: manifest SHA256 与 staging 相同；I2-R2 DLL SHA256 `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`。
- Recoverable backup: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\20260729T173059260Z-i2-r3-beta-v01091-damage-forecast-v0.3.0`，位于 loader scan root 外。

### I2-R3 Installed Checkpoint — 2026-07-30

- Install: transaction `20260729T173059260Z-i2-r3-beta-v01091` 以 `target-upgrade` 成功激活 `damage-forecast` `v0.3.0`；安装前后游戏均未运行，Codex 未启动游戏。
- Active audit: 活动目录严格只有 `damage-forecast.dll` 与 `damage-forecast.json`；manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256 `DAFB9D01E1A85600FA7969C8BE493C4F95568C61EF3F9B63831013F3816E0AA9`。
- Identity audit: loader scan root 内唯一相关身份为 `damage-forecast`；identity `1`、legacy `0`、orphan staging `0`。
- Rollback evidence: I2-R2 备份严格两文件，manifest SHA256 与当前相同，DLL SHA256 `BA1677DAAA41D764C41DB7E09EC50935DA1FA0A1DA830D23BDC23A70288B02D5`；安装 ledger 已写入备份根。
- Runtime boundary: 等待用户自行启动游戏，只验证人物缩小后的上方 HUD 跟随，以及结束回合按钮上方 HUD 的持续可见性；尚未登记 RuntimeVerified。

### I2 Runtime Finding RF-I2-004 — Stale End-turn Button Binding / Exit Animation

- Failed — runtime: 结束回合按钮上方 HUD 在新战斗中仍可能完全不显示；出现时可落到按钮退场后的右下角隐藏位置。
- Corrected diagnosis: I2-R3 已阻止敌方/摘要血条隐藏共享 Root，但 `HudAnchorResolver` 仍可能缓存上一场战斗中尚未销毁的 `NEndTurnButton`。战斗结束清空缓存后，旧登记血条的最后一次刷新又能把旧按钮写回；按钮退场的 `+250` 垂直位移随后被 HUD 持续跟随。
- Repair contract: 参考 Minty Spire 2 的生命周期绑定方式，在当前 `NEndTurnButton._Ready` 登记按钮实例；缓存必须同时绑定并校验所属 `NCombatUi`。点击结束回合时先固定最后可见布局，确认后继续冻结，取消时恢复；战斗结束显式隐藏并清空登记血条，不允许旧血条重新填充按钮缓存。
- Boundary: 本记录只采用用户文字反馈与本地只读程序集检查；不保存截图、副本或临时图片路径。

### I2-R4 Headless Checkpoint — 2026-07-31

- Implementation: 新增当前按钮与所属 `NCombatUi` 的成对弱引用绑定；不同战斗 owner、已分离按钮和失效节点全部拒绝复用。删除战斗结束后刷新旧登记血条的路径，并在清空 resolver 前显式隐藏/清除现有 Root。
- Freeze placement: `CallReleaseLogic` 接受路径在按钮开始退场前冻结动态锚点；确认后的 snapshot 刷新只更新冻结内容，不重排最后可见位置；取消路径恢复动态跟随，下一玩家回合同样显式恢复。
- Contracts: 新增 `HN-009..010` 与 `HF-011..012`，固定当前 owner 接受、旧 owner 拒绝、点击后停止动态追踪及冻结刷新保持最后可见位置。
- Current target drift: 本机游戏已更新为 `v0.110.0`、commit `eecc8c4d`。合同测试为新增的 `Sentry.dll` / `Sentry.Godot.dll` 采用存在时复制的条件引用，不改变旧 stable/beta 目标。
- Verification: 当前 `v0.110.0` 与 stable `v0.107.1` / beta `v0.109.0` 均 `SUMMARY discovered=477 passed=477 failed=0 skipped=0`；stable/beta 双目标总门禁 `PASS`，Release build 均 0 warning / 0 error。
- Exact current artifact: `work/publish/beta-v0.110.0/damage-forecast` 严格两文件；DLL SHA256 `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Boundary: 未安装 I2-R4、未启动游戏、未执行 Git checkpoint / push / tag / Workshop / release。角色头部/储君椅子顶部锚点仅登记，未在本轮实施。

### B110-1 v0.110.0 Contract Qualification — 2026-07-31

- 上述 v0.110.0 `477/477` 结果是在调查用 host 显式解析当前精确
  `Sentry.dll` / `Sentry.Godot.dll` 后取得；合同断言本身全部通过。
- stock `Test-ForecastGuardrails.ps1` 当前目标入口会在 case discovery 前因生成的
  contract `.deps.json` 未登记 `Sentry.Godot, Version=1.0.0.0` 而失败。
  “条件引用并复制 DLL”不足以形成可独立复现的 stock current-target PASS。
- stable v0.107.1 与 frozen beta v0.109.0 的 stock 双目标 `477/477`、Release build
  和 I2-R4 精确产物哈希不受此限定影响。I2-R4 仍等待用户运行时验证，
  本说明不改变其 `Priority Tag`、`Queue`、`Approved` 或下一 Gate。
- 本说明只限定已安装/B110-0 checkpoint 的 `9021C7E5...` 产物；
  后续共享工作树候选必须独立重跑三目标验证，不能继承本段结论。

### I2-R4 Pre-install Review — 2026-07-31

- Transaction: `20260731T083000000Z-i2-r4-beta-v01100`；计划动作 `target-upgrade`；本机游戏目标 `v0.110.0`、commit `eecc8c4d`，计划时游戏未运行。
- Staging: 严格两文件；manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256 `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`。
- Active before install: 唯一活动身份为 `damage-forecast`，legacy `0`、target `1`、orphan `0`；I2-R3 DLL SHA256 `DAFB9D01E1A85600FA7969C8BE493C4F95568C61EF3F9B63831013F3816E0AA9`。
- Recovery: 旧活动目录整体备份到 Loader 扫描根外的 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\20260731T083000000Z-i2-r4-beta-v01100-damage-forecast-v0.3.0`；安装不修改配置。

### I2-R4 Installed Checkpoint — 2026-07-31

- Install: `target-upgrade` 成功激活 `damage-forecast` `v0.3.0`；活动目录严格只有 manifest 与 DLL。
- Active audit: manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256 `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`，与已复核 staging 完全一致。
- Identity audit: 安装后计划返回 `target-already-current`；loader scan 中 legacy `0`、target `1`、orphan `0`。I2-R3 备份严格两文件且哈希复核通过；安装 ledger 为 `20260731T083000000Z-i2-r4-beta-v01100-install-ledger.json`。
- Launch boundary: 安装前后游戏均未运行；Codex 未启动游戏。等待用户自行启动，只验证结束回合按钮 HUD 的持续可见、正确锚点、点击后位置/数值冻结，以及下一回合或下一场战斗恢复到当前按钮。
- Truth boundary: 安装成功不等于 RuntimeVerified；角色头部/储君椅子顶部锚点仍只登记，尚未实施。

### I2 Runtime Finding RF-I2-005 — End-turn Instance Ownership / Native Shrink Transform

- Failed — end-turn: I2-R4 仍可能把 HUD 放到结束回合按钮的隐藏位置；按钮上方内容不能稳定保持在显示位置。
- Corrected diagnosis — end-turn: `NEndTurnButton._Ready` 发生在父级 `NCombatUi` 完成公开 `EndTurnButton` 赋值之前，I2-R4 的 owner 校验会拒绝首次绑定；无 owner 上下文的旧缓存随后仍可能被复用。按钮自身退场会从 `ShowPos` 向下移动 `250px`，未在点击前固定的 HUD 会跟到隐藏位置。
- Failed — shrink: 原生 `ShrinkPower` 通过 `NCreature.ScaleTo` 只缩放 `NCreatureVisuals.Scale`，不缩放 `Hitbox`；I2-R4 取 `Hitbox.Top` 与 `Visuals.Bounds.Top` 的最小值，因此未缩放 Hitbox 持续遮蔽缩小后的模型位置。增大时 Visuals 能超过 Hitbox，所以仅增大表现正确。
- Repair contract: 结束按钮使用 `_Ready` 时创建的直属命名标记确认实例身份，并以同一 `CombatState` 下唯一 `NCombatUi`、其公开按钮、按钮私有 owner 与直属标记四项共同校验；不再复用跨战斗按钮缓存。人物上方不再读取 Hitbox：除储君外使用随 `Visuals.Scale` 变化的 `TalkPosition` 语义点，储君使用缩放后 `Visuals.Bounds.Top` 作为椅子顶部。
- Boundary: 只记录文字事实与本地程序集证据，不保存截图、副本或临时图片路径。

### I2-R5 Headless Checkpoint — 2026-07-31

- End-turn implementation: 参考 Minty Spire 2 的按钮 `_Ready` 直属子节点生命周期模式，在真实 `NEndTurnButton` 下幂等创建 `DamageForecastEndTurnAnchorMarker`；父级 `NCombatUi._Ready` 完成公开按钮赋值后主动刷新已登记血条，闭合子节点早于父节点就绪的首帧时序。HUD Root 仍保留在稳定的 `NCombatUi` 层，点击时先按按钮显示位置完成布局并冻结，再允许按钮执行退场动画。
- Ownership: 删除按钮/owner 全局弱缓存和“无上下文可复用”规则。由本机血条对应 `CombatState` 精确寻找唯一 `NCombatUi`，并同时核验 `NCombatUi.EndTurnButton`、按钮私有 `_combatUi`、祖先关系及直属标记；旧战斗、分离按钮、缺失标记或多重匹配全部 fail closed。
- Character-above implementation: 普通角色横纵锚点改为当前 `Visuals.TalkPosition`，完整 Canvas Transform 每帧换算到 HUD Root；储君改用当前缩放后的 `Visuals.Bounds` 中线与顶部。Hitbox 不再参与人物上方计算；充能球和 Mod 自定义模型空间竞争保持延后。
- Contracts: 新增/更新 `HL-012`、`HL-015..017`、`HN-009..010`，覆盖按钮上方垂直方向、真实“Hitbox 不缩放”条件、普通角色双向缩放、储君椅子顶部、当前战斗实例接受及旧/分离/无标记实例拒绝。
- Verification: 当前 v0.110.0 合同 `478/478`；stock stable `v0.107.1` 与 beta `v0.109.0` 双目标质量门均 `478/478`、Release build 0 warning / 0 error、shadow-off artifact / diff check / artifact review 全部 PASS。
- Exact current artifact: `work/publish/beta-v0.110.0/damage-forecast` 严格只有 DLL 与 manifest；最终 DLL SHA256 `4691E40E2B39CDFF12A304650BFFE9ABF2418332AC5CE920CB793E810CEB764D`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。

### I2-R5 Installed Checkpoint — 2026-07-31

- Install: 初始候选 transaction `20260731T091217109Z-i2-r5-beta-v01100` 安装后，在安装后审查中补齐 `NCombatUi._Ready` 首帧刷新；最终 transaction `20260731T091649717Z-i2-r5b-beta-v01100` 再次以 `target-upgrade` 激活 `damage-forecast` `v0.3.0`。两次安装前后 `SlayTheSpire2` 进程数均为 `0`，Codex 未启动游戏。
- Active audit: 活动目录严格只有 `damage-forecast.dll` 与 `damage-forecast.json`；最终 DLL SHA256 `4691E40E2B39CDFF12A304650BFFE9ABF2418332AC5CE920CB793E810CEB764D`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，与最终 staging 完全一致。
- Recovery: 最终事务的直接回退候选位于 Loader 根外的 `20260731T091649717Z-i2-r5b-beta-v01100-damage-forecast-v0.3.0`，DLL SHA256 `C633887245B7E94B91F606F3D659D47DFC9B37C44B6B5512935A7370C74B8CE3`；I2-R4 原活动版本仍保存在前一事务备份中，DLL SHA256 `9021C7E5D72A08161834A5B55F95F2CAABB0A28316E98B9C8C3E6EC894330A8D`。最终 ledger 为 `20260731T091649717Z-i2-r5b-beta-v01100-install-ledger.json`。
- Runtime boundary: 安装成功不等于 RuntimeVerified。等待用户自行启动游戏验证：结束按钮上方 HUD 持续显示且不落到按钮下方；点击结束回合后位置与数值冻结；原生缩小后普通角色 HUD 下移到头顶、增大后上移；储君位于椅子顶部。
- Authority boundary: 未执行游戏启动、Git checkpoint / push / tag、Workshop 或发布；I3 仍未批准。

### I2-R6 Headless Checkpoint — 2026-07-31

- Runtime diagnosis: 用户确认普通角色缩小跟随与储君椅子顶部锚点已经通过，剩余失败仅为结束按钮上方 HUD。当前错误值稳定落在右下方，根因不是伤害算法，而是 I2-R 将实际 HUD Root 从按钮直属子节点迁到 `NCombatUi` 后，依赖跨父节点采样按钮 Rect；按钮从 `ShowPos` 向 `HidePos` 退场及刷新保留策略会把旧/退场坐标留给 Root。Minty Spire 2 与最初成功的 I2 实现都把实际显示节点直属添加到 `NEndTurnButton`。
- Two-layer repair: 活动 `DamageForecastEndTurnHudRoot` 恢复为当前按钮直属子节点，锚点固定为按钮局部 `(0, 0, width, height)`，不再逐帧跨父节点采样。点击前将当前可见三个内容组的文字、尺寸和 Canvas 精确位置复制到 `NCombatUi` 下独立 `DamageForecastFrozenEndTurnHudRoot`，复制成功后隐藏活动 Root；确认后冻结层保持，取消、下一玩家回合或战斗结束时清理。
- Scope preservation: 未修改已经通过运行时验证的普通角色头顶/储君椅子顶部缩放锚点，也未保存用户截图、截图副本或临时图片路径。
- Contracts: `HF-011..012` 锁定活动层/冻结层的不同父级及冻结期间抑制活动层；新增 `HN-011` 锁定结束按钮活动锚点只能是与屏幕位置无关的按钮局部 Rect。
- Verification: 当前 v0.110.0 合同 `479/479`；stock stable `v0.107.1` 与 beta `v0.109.0` 双目标质量门均 `479/479`、Release build 0 warning / 0 error、shadow-off artifact / diff check / artifact review 全部 PASS。
- Exact current artifact: `work/publish/beta-v0.110.0/damage-forecast` 严格只有 DLL 与 manifest；DLL SHA256 `E539B2CD34A62815B0B8BDE92CE1BB1B2C26DA09440BAAA06144A5286A84C088`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`。
- Authority boundary: 本检查点未安装、未启动游戏、未执行 Git checkpoint / push / tag、Workshop 或发布；当前活动安装仍是 I2-R5，DLL SHA256 `4691E40E2B39CDFF12A304650BFFE9ABF2418332AC5CE920CB793E810CEB764D`。I2-R6 安装与 I3 均待单独批准。

### I2-R6 Installed Checkpoint — 2026-07-31

- Install: transaction `20260731T122104766Z-i2-r6-beta-v01100` 以 `target-upgrade` 激活 `damage-forecast` `v0.3.0`；安装前后 `SlayTheSpire2` 进程数均为 `0`，Codex 未启动游戏。
- Active audit: 活动目录严格只有 `damage-forecast.dll` 与 `damage-forecast.json`；DLL SHA256 `E539B2CD34A62815B0B8BDE92CE1BB1B2C26DA09440BAAA06144A5286A84C088`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，与 I2-R6 staging 完全一致；安装后计划返回 `target-already-current`。
- Recovery: I2-R5 直接回退候选位于 Loader 扫描根外的 `20260731T122104766Z-i2-r6-beta-v01100-damage-forecast-v0.3.0`，严格两文件，DLL SHA256 `4691E40E2B39CDFF12A304650BFFE9ABF2418332AC5CE920CB793E810CEB764D`；ledger 为 `20260731T122104766Z-i2-r6-beta-v01100-install-ledger.json`。
- Runtime boundary: 安装成功不等于 RuntimeVerified。等待用户自行启动游戏，只验证结束按钮上方 HUD：回合中稳定显示在按钮上方；点击结束回合后数值与位置保持冻结，不随按钮退场到右下方。已经通过的普通角色缩小与储君锚点无需重复专项验证。
- Authority boundary: 未执行游戏启动、Git checkpoint / push / tag、Workshop 或发布；I3 仍未批准。

### I2-R7 Installed Checkpoint — 2026-07-31

- Runtime input: 用户确认 I2-R6 结束按钮 HUD 已稳定显示且不再闪烁，但视觉间距偏大。原因是结束按钮与血条上方共用 `14px` 垂直间距，而按钮控件自身顶部透明区域进一步放大了视觉距离；本记录只采用用户文字结论，不保存截图、副本或临时图片路径。
- Change: 仅为 `EndTurnButtonAbove` 增加独立 `2px` 垂直间距，将 HUD 相对 I2-R6 下移约 `12px`；`HealthBarAbove` / `HealthBarBelow` 仍保持 `14px`，按钮直属活动层、`NCombatUi` 冻结层和用户偏移语义均未改变。
- Verification: 当前 v0.110.0 合同 `479/479`；stock stable `v0.107.1` 与 beta `v0.109.0` 双目标质量门均 `479/479`、Release build 0 warning / 0 error，其他门禁全部 PASS。
- Install: transaction `20260731T124601160Z-i2-r7-spacing-v01100` 以 `target-upgrade` 激活严格两文件版本；DLL SHA256 `4207FA086B2593AB0B33C5605BFA2AA8A49B85E95188C38609ADEC2C74A3DCBF`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，安装后计划返回 `target-already-current`。
- Recovery: I2-R6 回退候选位于 Loader 扫描根外的 `20260731T124601160Z-i2-r7-spacing-v01100-damage-forecast-v0.3.0`，严格两文件，DLL SHA256 `E539B2CD34A62815B0B8BDE92CE1BB1B2C26DA09440BAAA06144A5286A84C088`；ledger 为 `20260731T124601160Z-i2-r7-spacing-v01100-install-ledger.json`。
- Runtime boundary: 等待用户自行启动游戏，只判断按钮上方间距是否合适，并顺带确认点击后仍不闪烁、不移动；未执行 Codex 游戏启动、Git checkpoint / push / tag、Workshop 或发布。

### I2-R8 Installed Checkpoint — 2026-07-31

- Runtime input: I2-R7 截图显示单个数值与按钮可见图框中线不一致，稳定向左偏。只采用用户文字结论与本地原生程序集元数据，不保存截图、副本或临时图片路径。
- Native evidence: `NEndTurnButton` 外层 Control 内含 `_image : TextureRect` 与 `_visuals : Control`；此前算法以外层 `Size` 中线布局，而实际可见按钮图框由内部 `_image` 表示，因此外层中线并不等于视觉中线。
- Change: 结束按钮锚点横向优先使用 `_image` 经同一 Canvas Transform 得到的 `X/Width`，不可用时 fail-safe 回退外层按钮；纵向继续使用按钮外层基准，避免内部透明边距改变已经测试的高度，并在 I2-R7 基础上额外下移 `8px`。按钮直属活动层和 `NCombatUi` 冻结层未改变。
- Contracts: 新增 `HN-012`，锁定横向采用 `_image`、纵向保留外层按钮；`HL-012` 锁定相对 I2-R7 基线下移 `8px`。当前 v0.110.0、stable `v0.107.1` 与 beta `v0.109.0` 均 `480/480`；双目标 Release build 0 warning / 0 error，全部质量门 PASS。
- Install: transaction `20260731T130110047Z-i2-r8-center-v01100` 以 `target-upgrade` 激活严格两文件版本；DLL SHA256 `E6575162DDB598EE7FA63D968ADD308FA053F1401333247F12D49BEF9356C54B`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，安装后计划返回 `target-already-current`。
- Recovery: I2-R7 回退候选位于 Loader 扫描根外的 `20260731T130110047Z-i2-r8-center-v01100-damage-forecast-v0.3.0`，严格两文件，DLL SHA256 `4207FA086B2593AB0B33C5605BFA2AA8A49B85E95188C38609ADEC2C74A3DCBF`；ledger 为 `20260731T130110047Z-i2-r8-center-v01100-install-ledger.json`。
- Runtime boundary: 等待用户自行启动游戏，验证单值中线、额外下移后的高度，以及点击后仍不闪烁、不移动；未执行 Codex 游戏启动、Git checkpoint / push / tag、Workshop 或发布。

### I2-R9 Installed Checkpoint — 2026-07-31

- Runtime diagnosis: I2-R8 仍稳定左偏。对用户提供画面进行只读像素量化后，预测数字包围框中线为 `x=214`，按钮原生文字包围框中线为 `x=252`，可见按钮外框中线为 `x=253`；因此实际误差为 `38–39px`。本卡仅记录量化数值，不保存截图、副本或临时图片路径。
- Reference decision: `_image : TextureRect` 的 Control 矩形没有代表纹理内部可见图形中线，不能作为主参考；原生 `_label` 与可见外框中线只差 `1px`，采用游戏自身已经校准的 `_label` 作为主中线参考。顺序固定为 `_label` → `_image` → 外层按钮，后两者仅作缺失时的兼容回退。
- Parameters: 横向只从选定参考物提取 `X/Width`，纵向始终保留按钮外层基准；I2-R8 已批准的额外下移 `8px` 参数保持不变，避免再次混合中线修复与高度调参。
- Contracts: `HN-012` 改为锁定原生 `_label` 优先且保留外层纵向基准；新增 `HN-013` 锁定 `_image` 仅作 label 缺失回退。当前 v0.110.0、stable `v0.107.1` 与 beta `v0.109.0` 均 `481/481`；双目标 Release build 0 warning / 0 error，全部质量门 PASS。
- Install: transaction `20260731T130946183Z-i2-r9-label-center-v01100` 以 `target-upgrade` 激活严格两文件版本；DLL SHA256 `D71356F972A383B2E4F81E90C0E190A59B04EC0BC2A627B990A4E277BA051471`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，安装后计划返回 `target-already-current`。
- Recovery: I2-R8 回退候选位于 Loader 扫描根外的 `20260731T130946183Z-i2-r9-label-center-v01100-damage-forecast-v0.3.0`，严格两文件，DLL SHA256 `E6575162DDB598EE7FA63D968ADD308FA053F1401333247F12D49BEF9356C54B`；ledger 为 `20260731T130946183Z-i2-r9-label-center-v01100-install-ledger.json`。
- Runtime boundary: 等待用户自行启动游戏，验证单值是否与按钮原生文字/可见外框共中线，并确认高度、防闪烁和冻结仍正确；未执行 Codex 游戏启动、Git checkpoint / push / tag、Workshop 或发布。

### I2-R10 Installed Checkpoint — 2026-07-31

- Install verification before diagnosis: 游戏活动目录已确认加载候选为 I2-R9 DLL `D71356F972A383B2E4F81E90C0E190A59B04EC0BC2A627B990A4E277BA051471`，与 staging 完全一致；因此 I2-R9 画面不变不是漏装或旧 DLL 残留。
- Root cause: 最新画面再次量得预测单字中线 `x=170`、按钮原生文字中线 `x=208.5`、可见外框中线 `x=209`，仍稳定左偏约 `39px`。布局实际已把固定宽度 `72px` 的数字 Label 槽位放在正确中线，但 `GetMainTextSize` 始终保留 `72px` 宽度，同时 `ApplyMainHudStyle` 强制 `HorizontalAlignment.Left`，导致单字贴在居中槽位左侧。更换按钮参考物无法改变槽内文字对齐，故 I2-R8/R9 视觉不变。
- Change: 保留 `72px` 槽位及其确定性 cluster 尺寸；仅当数值自身 placement 为 `EndTurnButtonAbove` 时把该 Label 改为槽内 `Center`，四个血条 placement 继续 `Left`，不改变已经通过的血条左右增长与人物上下布局。I2-R9 的 `_label` → `_image` → 外层按钮参考顺序以及额外下移 `8px` 均保留。
- Contracts: 新增 `HL-018`，锁定结束按钮数值槽居中且全部血条预设仍左对齐。当前 v0.110.0、stable `v0.107.1` 与 beta `v0.109.0` 均 `482/482`；双目标 Release build 0 warning / 0 error，全部质量门 PASS。
- Install: transaction `20260731T145227421Z-i2-r10-slot-center-v01100` 以 `target-upgrade` 激活严格两文件版本；DLL SHA256 `3A7240BF22293B4F64EAADAA7BF720DCECC449A54AF515EE9029E64F41B19270`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，安装后计划返回 `target-already-current`。
- Recovery: I2-R9 回退候选位于 Loader 扫描根外的 `20260731T145227421Z-i2-r10-slot-center-v01100-damage-forecast-v0.3.0`，严格两文件，DLL SHA256 `D71356F972A383B2E4F81E90C0E190A59B04EC0BC2A627B990A4E277BA051471`；ledger 为 `20260731T145227421Z-i2-r10-slot-center-v01100-install-ledger.json`。
- Runtime boundary: 等待用户自行启动游戏，验证单字现在是否与原生文字/外框共中线，并确认高度、防闪烁和冻结保持正确；未执行 Codex 游戏启动、Git checkpoint / push / tag、Workshop 或发布。画面只用于本次只读量化，没有保存截图副本或图片路径。

### I2-R10 Runtime Verified Checkpoint — 2026-07-31

- Result: 用户在本机 v0.110.0 实战确认 I2-R10 “现在很完美”。结束按钮上方单值与按钮视觉中线一致，高度合适；结合 I2-R6 已确认的不闪烁与冻结稳定结果，本轮结束按钮位置、显示稳定性和点击后冻结均通过。
- Preserved runtime evidence: 血条左右/下方位置此前已确认正常；普通角色原生缩小/增大跟随与储君椅子顶部锚点已分别确认通过。本轮后续仅修改结束按钮侧布局，没有改动这些已通过路径。
- Validation scope: `RuntimeVerified` 仅绑定用户实际运行的本机 v0.110.0 与已测试场景；stable v0.107.1 / beta v0.109.0 仍为 headless contract/build 证据，不扩张为对应版本运行时兼容证明。
- Artifact: 当前活动目录严格两文件，I2-R10 DLL SHA256 `3A7240BF22293B4F64EAADAA7BF720DCECC449A54AF515EE9029E64F41B19270`，manifest SHA256 `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`；可恢复安装与备份证据见上一节。
- Boundary: 用户图片未保存到任务卡、仓库或记录。I2 工作与运行时验证完成；Git checkpoint 仍属于未批准的 I3，未执行 commit、push、tag、Workshop 或发布。

## Gate I3 — Authority / Checkpoint

Status: Complete

- 只同步已验证的当前产品事实。
- Git checkpoint 必须单独批准；只纳入任务自有路径和 hunk。
- push、tag、Workshop 与发布不随 checkpoint 自动授权。

### I3 Final Closure Checkpoint — 2026-07-31

- Result: 已把 HUD 预设位置、角色跟随、结束按钮稳定显示与固定冻结的任务自有实现和合同建立为本地 implementation checkpoint `a4b2e23`（`Implement Damage Forecast HUD placement and freeze`）。
- Isolated verification: 从暂存区导出的干净快照分别以 stable `v0.107.1` 与 beta `v0.109.0` 引用运行合同，均为 `391/391` passed；I2-R10 的完整共享工作区质量门证据仍为 `482/482`，不把并行 Timeline 合同混入本 checkpoint。
- Authority: 本卡、`docs/project-state.md` 当前产品事实与 `docs/task-notes/README.md` 路由已同步；本任务从活动队列移至关闭任务。
- Preserved: 仅提交本任务自有文件与共享文件 hunk；Forecast Timeline、v0.110.0 兼容性调查及其他任务的 dirty worktree 内容保持未暂存。
- External boundary: 未执行 push、tag、Workshop、发布或 Codex 游戏启动；当前安装仍为已由用户运行时验证的 I2-R10 两文件版本。

## Final closure

最终只记录：

Result: HUD 预设位置、角色跟随、结束按钮稳定显示与点击后冻结已交付，并由用户在本机 v0.110.0 运行时验证通过
Current state: Closed；本机 v0.110.0 RuntimeVerified，stable/beta 仅完成 headless 验证
Authority: 本卡
Repository: Implementation checkpoint `a4b2e23`; closure marker is the docs-only commit containing this record
