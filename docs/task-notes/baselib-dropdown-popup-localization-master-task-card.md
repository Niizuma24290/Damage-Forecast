# Damage Forecast — BaseLib 设置下拉弹层本地化调查与修复主任务卡

日期：2026-08-01

Type: Completed Investigation / Implementation / Runtime Verification
Area: Hub UI
Touches: Platform
Priority Tag: P2
Queue: Parked

## Current Control

Classification: CLOSED_TASK
State: Closed
Last completed: BLP-5 — local repository checkpoint and final closure
Next: None — future language additions or BaseLib layout changes require a separate task and approval
Approved: Yes — BLP implementation, contracts, local install, runtime verification, authority closure and local Git checkpoint; no push/tag/release/Workshop
Evidence: 本卡 BLP-0、BLP-3、BLP-4、BLP-4F、BLP-4G、BLP-4H 与 BLP-4I 增量证据
Repository: This local closure commit is the BLP implementation / contract / authority checkpoint; no push or tag

## Goal

让 Damage Forecast 设置页的展开下拉选项与当前选择的模组语言一致。选择简体中文后，
收起状态和展开候选项都显示中文；切回英文后两者都显示英文，同时保留 BaseLib
标准设置页布局、配置持久化和现有控制器/焦点行为。

## 当前事实与未知

- 用户于 2026-08-01 再次确认：选择中文后，主页面的当前选中值是中文，但展开选择栏仍显示英文或原始枚举名。
- `AUD-0008` 已将该问题记录为 `Confirmed / Severity Low / Confidence High / E4`，不是新的假设。
- 两次历史修复均已运行时失败：逐帧扫描弹层文字，以及反射改写 `NConfigDropdown._items`。
- 当前源码仍保留未成功的 `_items` 改写路径；不得在其上继续叠加未经证明的反射猜测。
- 当前设置页使用 BaseLib 自动行和控件；项目没有 BaseLib `settings_ui` 本地化 PCK。编译基线固定为 BaseLib 3.3.4，当前实际运行版本和弹层创建路径需在 BLP-0 核实。
- `docs/project-state.md` 仍将展开项写成“需要运行时验证”，与已确认失败的审计证据不一致；只在最终收口 Gate 更正当前权威，不改写历史记录。

## Scope and boundaries

Included:
- 还原 BaseLib 配置枚举从属性值到收起标签、弹层数据和最终可见节点的真实调用链与时序。
- 调查 BaseLib 官方 `settings_ui` 本地化能力，以及使用相近 BaseLib/API 的公开模组实现。
- 在证据不足时设计仅作用于 Damage Forecast 的临时定点诊断，再据此选择正式修复路径。
- 修复后覆盖语言、显示模式、数值位置、HUD 锚点/预设等实际枚举下拉项。

Excluded:
- 不处理独立的游戏内 BaseLib 配置入口问题 `AUD-0009`。
- 不重做完整设置页，不建立 Damage Forecast 自有的第二套设置框架。
- 不全局修改 BaseLib 行为，不影响其他模组的设置弹层。
- 不顺带修改战斗 HUD、预测算法、其他翻译或发布/Workshop 状态。

Preserved:
- BaseLib 自动布局、滚动、恢复默认、左侧模组列表和稳定英文查找身份保持不变。
- 配置键、枚举持久化值、默认值和迁移兼容性保持不变；只改变玩家可见文本。
- 临时诊断不得作为永久轮询或全场景逐帧扫描留在生产代码中。
- 构建、安装、游戏启动、Git checkpoint、发布和 Workshop 均需后续 Gate 单独授权。

## Gate BLP-0 — Read-only Popup Lifecycle Investigation

Goal: 证明展开项最终从哪里生成、何时覆盖文本，以及 BaseLib 支持的本地化入口。
Allowed: 只读检查 authority、当前源码、已验证 BaseLib 依赖和本机实际版本；反射/反编译相关类型；查阅 BaseLib 官方资料、源码与相近版本公开模组；不修改、构建、安装、启动游戏或写入 Git。
Deliverable: 收起值与展开项的数据流/实例/时序图，两次失败方案未命中最终显示路径的原因，官方资源方案与窄范围钩子方案的证据比较，最小文件影响面、风险和下一 Gate 建议。
Verification: 至少追踪 `NConfigDropdown`、`ItemData`、`NConfigDropdownItem` 与原生 `NDropdown` 的初始化、属性刷新、打开、克隆/重建和文本赋值路径；公开实现必须标明版本与适用边界。
Pass: 能以代码/API/运行资源证据解释当前现象并选出受支持的修复路径；若仍无法证明，则给出最小定点日志及明确待观测字段，不猜测实现。
Stop: 只回填 BLP-0 增量证据并停止，等待用户讨论和批准下一 Gate。

### BLP-0 增量证据（2026-08-01）

- Result: Complete / ReadOnlyStaticVerified；除本节任务卡证据回填外，未修改源码、构建、安装、
  启动游戏或执行 Git 写操作。
- Version boundary: 编译 DLL 为 BaseLib `3.3.4.0`，SHA256
  `C593F14EAAB504FC1D31C89DA7C029116D269F65706D9612D6F71A048E504235`；当前 Workshop
  磁盘文件为 `v3.4.3` / assembly `3.4.3.0`，DLL SHA256
  `9F9581F4F6B6A2AB0C744BDF73729E10DF8FF2C07BEFBC184B6C65F010932E83`。本 Gate 未启动游戏，
  因此只确认 `InstalledOnDisk`，不声称本次 `RuntimeLoaded`。
- Data flow: `GenerateOptionFromProperty` -> `CreateRawDropdownControl` ->
  `NConfigDropdown.Initialize`。`Initialize` 对每个枚举值读取
  `settings_ui` 键 `<ModPrefix><SlugProperty>.<EnumValue>`，缺失时回退原始枚举名，并把结果保存为
  `_items: List<ItemData>`；`SetFromProperty` 只选择当前 index/刷新收起标签。
- Instance / timing: `NConfigDropdown._Ready` 清空原生容器，然后对 `_items` 逐项执行
  `NConfigDropdownItem.Create(item)`、加入 `_dropdownItems`、连接 `Selected`、`Init(index)`；展开项因此是
  各自持有 `Data`/`Text` 的已创建节点。BaseLib 在 open 阶段没有重建项，既有容器由原生
  `NDropdown.OpenDropdown` 管理显示；选择后
  `OnDropdownItemSelected` 从所选节点的 `Data.Text` 回写收起标签并调用 `Data.OnSet`。
- Failure cause: 当前 `ApplyLocalizedText` 在行已加入 live `optionContainer` 后才反射替换
  `NConfigDropdown._items`。此时 `_Ready` 已把旧 `ItemData` 复制到展开节点；替换 list entry 不会更新
  `NConfigDropdownItem.Data` 或 `NDropdownItem.Text`，所以收起值可被直接改写，而展开项继续显示原始名。
  逐帧扫描方案也没有命中此确定的创建点，且当前 `DropdownTextUpdater` 仅残留类型定义、没有实例化入口。
- Official route: BaseLib 官方文档要求使用 PCK 中的 `localization/<lang>/settings_ui.json`；源码只在
  `Initialize` 时从当前游戏语言表读取并固化字符串。`ConfigDropdownOverrideLocalizationAttribute` 只改
  key 的 property 部分，不能提供运行时文本或选择 Damage Forecast 自有语言。因此资源方案适合“跟随游戏语言”，
  但不能单独满足本任务“跟随模组内 ConfigLanguage 并立即切换”的合同；当前 manifest 亦为
  `has_pck: false`，引入 PCK 会扩大项目、发布与安装影响面。
- Public boundary: 可确认公开 Sadida `v0.3.264` 使用 BaseLib `SimpleModConfig`、PCK 和
  `settings_ui.json` 本地化其设置页，但公开证据只覆盖普通设置项，不证明枚举展开项随模组自有语言动态切换；
  未发现可复用的 `ConfigDropdownOverrideLocalizationAttribute` 动态语言实例。
- Selected path: BLP-1/BLP-2 诊断不是当前必需步骤。建议 BLP-3 继续使用 BaseLib 自动行，只在
  `DamageForecastBaseLibConfig` 内按 `Value` 同步 `_items` 与已创建 `NConfigDropdownItem.Data/Text`，
  并删除死的全树扫描路径；不得全局 Harmony 修改 BaseLib，不改变配置键、枚举值、布局或焦点合同。
- Minimal impact / verification: production 预计仅触及
  `src/DamageForecast/Settings/DamageForecastBaseLibConfig.cs`，并在现有 contract harness 增加定点合同；
  stable/beta 构建与安装仍属于 BLP-3/BLP-4。风险是 BaseLib 私有 `_items` 漂移，因此必须 fail closed、
  以 `Value` 而非 index 配对，并在 3.3.4 编译基线和当前 3.4.3 运行版本分别验证。
- Sources: BaseLib 官方
  [`NConfigDropdown`](https://github.com/Alchyr/BaseLib-StS2/blob/master/Config/UI/NConfigDropdown.cs)、
  [`ModConfig`](https://github.com/Alchyr/BaseLib-StS2/blob/master/Config/ModConfig.cs)、
  [`ConfigAttributes`](https://github.com/Alchyr/BaseLib-StS2/blob/master/Config/ConfigAttributes.cs)、
  [`Mod Configuration`](https://alchyr.github.io/BaseLib-Wiki/docs/utilities/config.html) 与
  [`PCK/localization 发布说明`](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup)；公开相近实现边界见
  [Sadida changelog](https://www.nexusmods.com/slaythespire2/mods/631?tab=logs)。

### BLP-3 实施证据（2026-08-01）

- Result: Complete / ContractVerified / StableBetaBuildVerified / RuntimeNotVerified。用户明确批准 BLP-3；本 Gate
  未安装、未启动游戏、未执行 Git checkpoint、未发布且未更新 Workshop。
- Production change: `DamageForecastBaseLibConfig` 先完整构造本次语言对应的
  `NConfigDropdownItem.ItemData` replacements，确认 `_items` 的每一项均为预期类型后才整体替换；BaseLib 私有成员
  缺失或结构漂移时直接返回，不留下半更新状态。
- Live popup synchronization: 在同一 Damage Forecast 设置控件子树中查找已创建的
  `NConfigDropdownItem`，按 `Data.Value` 匹配 replacement，同时更新 `Data` 与原生 `Text`；不依赖 display index，
  保留原 `Value` 和 `OnSet`，不改变配置键、枚举持久化值、布局或焦点路径。
- Dead-path cleanup: 已删除未实例化的 `DropdownTextUpdater`、全场景逐帧扫描和相关通用反射文本改写分支；未增加
  Harmony patch，也未修改 BaseLib 或其他模组的全局行为。
- Contracts: 新增 `BLP3-001` 至 `BLP3-004`，覆盖 BaseLib `3.3.4` 私有源列表/公开 live-item 成员缝、
  乱序 replacement 按 `Value` 配对、当前全部实际配置枚举的友好中英文，以及死扫描器不再进入产物。
- Verification: `C:\sts2\dotnet\dotnet.exe run --project
  tests\DamageForecast.ContractTests\DamageForecast.ContractTests.csproj -c Release --no-restore` 通过，
  `SUMMARY discovered=513 passed=513 failed=0 skipped=0`。随后
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ForecastGuardrails.ps1` 通过；stable 与 beta
  各为 `513/513` contracts、Release build `0` warning / `0` error，最终
  `QUALITY_GATE targets=2 status=PASS exit_code=0`，`git diff --check` 亦通过。
- Ownership / shared tree: production 仅修改
  `src/DamageForecast/Settings/DamageForecastBaseLibConfig.cs`；新增
  `tests/DamageForecast.ContractTests/BaseLibDropdownLocalizationContractCases.cs` 并在既有 `Program.cs` 末尾追加一条
  注册。`Program.cs`、guardrail 与仓库内其他并行任务的既有改动均保留，未整理、暂存或归入 BLP-3。
- Remaining boundary: 静态/契约/构建结果不能证明 Workshop 磁盘 BaseLib `3.4.3` 的实际加载行为，也不能证明展开态、
  页面重开、重启持久化或鼠标/控制器焦点。只有单独批准 BLP-4、更新游戏文件并由用户完成 shared matrix 后，
  才能标记 `RuntimeVerified`。

### BLP-4 安装证据（2026-08-01）

- Result: InstallComplete / AwaitingUserRuntime / RuntimeNotVerified。用户明确批准 BLP-4；Codex 未启动游戏、未读取或
  修改配置内容、未执行 Git checkpoint、未发布且未更新 Workshop。
- Parallel isolation: 当前共享工作区仍有其他任务的未提交生产改动，因此没有安装整仓 guardrail 产物。安装源由
  `HEAD 156545f76f42cac0212c1c409fcdaa1477d23204` 的归档加 BLP-3 唯一 production 文件
  `DamageForecastBaseLibConfig.cs` 组成；产物包含 `RewriteCreatedDropdownItems` / `FindDropdownReplacement`，不含
  `DropdownTextUpdater`、`ForecastTimelineShadow` 或 `VerifiedShadowmeldFutureBlockModifier`。
- Current target build: 针对本机当前游戏 `v0.110.1 / db5d3552` 成功 restore/publish；这只证明编译与产物生成成功，
  不等同该版本的运行时验证。
- Reviewed staging: `work/publish/stable/damage-forecast-blp4` 恰好包含两个文件：
  `damage-forecast.json` SHA256 `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`，
  `damage-forecast.dll` SHA256 `B3D64C4751F65EAFA753D2DD9C2BD082C3F64C568081E29E711F41A915822743`。
- Install plan: read-only Plan 报告 `action=target-upgrade`、`gameRunning=false`、`legacyActiveCount=0`、
  `targetActiveCount=1`、`orphanArtifactCount=0`；执行时同时绑定 staging 与旧 active 的四个 SHA256。
- Activation: 已更新
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\damage-forecast`。旧 active 完整移动到
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4-20260801-popup-localization-damage-forecast-v0.3.0`；
  ledger 为同级 `blp4-20260801-popup-localization-install-ledger.json`。
- Post-install audit: active 目录仍恰好是 manifest/DLL 两文件且哈希与 staging 完全一致；备份 manifest/DLL 哈希分别为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB` 与
  `C9F057BBA719807EC34263A1FEB353FE161B07F1B01D34BA01E308C2167CFED3`，与安装前一致。复跑 Plan 得到
  `action=target-already-current`、单一 target identity、无 legacy/orphan，且游戏仍未运行。
- Manual stop: 必须由用户启动游戏并完成下方 Shared verification matrix；在收到反馈前保持
  `RuntimeNotVerified`，不进入 BLP-5，不更正 `docs/project-state.md`，不创建 repository checkpoint。

### BLP-4 用户反馈与 BLP-4F 字号小修证据（2026-08-01）

- BLP-4 runtime result: 用户报告 Shared verification matrix “都是成功的”；因此原本地化修复可标记为
  `RuntimeVerified`。用户截图另行显示三个英文 `Above End Turn Button` 收起值的尾部被右侧箭头遮挡；这是独立的
  英文长文本字号问题，不推翻 BLP-4 本地化结果。
- Authorized refinement: 用户在确认“只缩小、不左移”方案后要求“试试吧”。本次只授权 BLP-4F 代码、契约与构建；
  未授权再次安装、游戏启动、Git checkpoint、发布或 Workshop。
- Production change: 三个 `HudPlacementPreset` 设置仅在语言为 English 且值为 `EndTurnButtonAbove` 时，将首个实际
  `Label` / `RichTextLabel` 的主题字号设为默认值的 `90%`；不改变左边界、对齐、控件宽度或中文。收起值和已创建
  popup item 均应用同一策略；每次重算先移除 override，因此中文、较短值和无关下拉框恢复主题默认字号。
- Contracts: 新增 `BLP4F-001` / `BLP4F-002`，验证严格限定三个 placement properties、English、长选项和 `90%`
  font-only policy。完整 harness 为 `SUMMARY discovered=515 passed=515 failed=0 skipped=0`。
- Guardrail: `Test-ForecastGuardrails.ps1` 的 stable/beta 均为 `515/515` contracts，Release build 0 warning / 0 error；
  最终 `QUALITY_GATE targets=2 status=PASS exit_code=0`，`git diff --check` 通过。
- Parallel-safe staging: 再次使用 `HEAD 156545f76f42cac0212c1c409fcdaa1477d23204` 归档加本任务唯一 production
  文件生成本机 `v0.110.1 / db5d3552` 隔离产物，未包含 timeline-shadow 或 Shadowmeld 并行改动。
  `work/publish/stable/damage-forecast-blp4f` 恰好包含 manifest/DLL；manifest SHA256 为
  `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`，DLL SHA256 为
  `80E8AB4C8B5FAB0AE99FDF9B2FF3A2F22267B514F900863AC2B613DA16F89312`。
- Install boundary: read-only Plan 为 `action=target-upgrade`、`gameRunning=false`、单一 target identity、无
  legacy/orphan；当前活动 DLL 仍是已通过 BLP-4 的
  `B3D64C4751F65EAFA753D2DD9C2BD082C3F64C568081E29E711F41A915822743`。必须取得 BLP-4F 安装明确批准后才能替换。
- Install result: 用户随后明确要求“安装我看看”。复跑 Plan 后以 staging/active 四个 SHA256 锁定执行；活动 DLL 已更新为
  `80E8AB4C8B5FAB0AE99FDF9B2FF3A2F22267B514F900863AC2B613DA16F89312`，manifest 保持
  `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`。上一版完整备份到
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4f-20260801-font-90-damage-forecast-v0.3.0`，
  ledger 为同级 `blp4f-20260801-font-90-install-ledger.json`。
- Post-install audit: active 与 staging 两文件哈希完全一致；复跑 Plan 为 `action=target-already-current`、
  `gameRunning=false`、单一 target identity、无 legacy/orphan。Codex 未启动游戏；字号效果仍等待用户运行时检查。

### BLP-4F 失败原因与回滚证据（2026-08-01）

- Runtime result: 用户截图确认 English 与 Simplified Chinese 的全部下拉框文字都异常缩小，要求调查并退回上一版本；
  BLP-4F 判定失败，不保留其运行时产物或源码实现。
- Root cause: `ApplyFirstTextFontScale(..., compact: false)` 仍会执行
  `RemoveThemeFontSizeOverride(...)`。该调用删除的是 BaseLib 场景本来用于正常大字号的本地 override，而不是只删除
  Damage Forecast 新增的 override；控件随后继承较小的全局主题字号。因此所有非目标下拉框及中文也一起缩小。目标长英文
  路径同样先删除 BaseLib override，再以较小的继承字号计算 `90%`，所以进一步变小。
- Game rollback: 以活动 manifest/DLL 和备份 manifest/DLL 四个 SHA256 锁定回滚。当前 active 已恢复为
  manifest `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`、DLL
  `B3D64C4751F65EAFA753D2DD9C2BD082C3F64C568081E29E711F41A915822743`。失败 DLL
  `80E8AB4C8B5FAB0AE99FDF9B2FF3A2F22267B514F900863AC2B613DA16F89312` 保留在 Loader 外的
  `blp4f-20260801-font-rollback-target-before-rollback` 恢复目录；ledger 为
  `blp4f-20260801-font-rollback-rollback-ledger.json`。
- Installer boundary: 原 rollback Plan 因安装器仍强制当前配置符合旧的 exact-18-key schema 而失败；这与字号故障无关。
  两个产物同为 target identity `v0.3.0` 且不需要配置迁移，因此执行时使用仓库内空的 inspection-only `ConfigRoot`，
  得到 `configRollbackAction=target-direct`；实际用户配置未被读取、迁移或修改。该 strict-schema 漂移作为独立工具问题保留，
  本 Gate 不顺带修复安装器。
- Source rollback: 已仅撤销 `CompactEnglishDropdownFontPercent`、所有 font override helper/调用以及
  `BLP4F-001` / `BLP4F-002`；BLP-3 的 source/live item 本地化同步保持不变。完整 harness 恢复为
  `SUMMARY discovered=513 passed=513 failed=0 skipped=0`，`git diff --check` 通过。
- Post-rollback audit: active 与 BLP-4 staging 哈希完全一致；Plan 为 `action=target-already-current`、
  `gameRunning=false`、单一 target identity、无 legacy/orphan。Codex 未启动游戏，也未执行 Git、发布或 Workshop 操作。

### BLP-4G 安全字号修复证据（2026-08-01）

- Result: ImplementationComplete / ContractVerified / IsolatedBuildVerified / InstallNotApproved / RuntimeNotVerified。
  用户在确认“捕获 BaseLib 原始字号、只缩小目标英文关闭态、其他状态精确恢复”的方案后要求“开始吧”；本 Gate
  未安装、未启动游戏、未执行 Git checkpoint、未发布且未更新 Workshop。
- Corrected ownership: 只处理三个 placement 下拉框的关闭态当前值 `Label` / `RichTextLabel`，不对
  `NConfigDropdownItem` 弹出候选项应用字号；继续保留 BLP-3 对候选项 `Data/Text` 的纯本地化同步。
- Baseline preservation: 每个关闭态文字控件第一次出现时按 instance id 捕获 BaseLib 原有的 theme key、
  `HasThemeFontSizeOverride` 和最终字号。目标值仅在 English + `EndTurnButtonAbove` 时应用该原始字号的 `94%`；切到中文、
  短英文或其他值时，原来有本地 override 就精确重设原值，原来没有才移除本模组 override。没有左移、改对齐、改宽度或改中文。
- Contracts: 新增 `BLP4G-001` 至 `BLP4G-003`，覆盖三个属性边界、语言/枚举值边界、`94%` 整数舍入，
  以及 baseline 的 key/原 override/字号元数据。执行时完整 harness 为
  `SUMMARY discovered=516 passed=516 failed=0 skipped=0`，本任务文件 `git diff --check` 通过。
- Shared-tree guardrail boundary: 随后运行共享工作树 stable/beta guardrail 时，其他并行任务已加入 Shadowmeld 回归合同；
  stable 在 `SM-022 Live.CompletedPlayCard_RefreshesFinalState` 得到 `false`，总计 `522/523`，因此整树 guardrail 如实记为
  blocked，未声称 PASS。BLP-4G 三条合同在该次执行中仍全部 PASS；失败合同和对应 production 路径均不属于本 Gate，
  本 Gate 未修改或绕过它们。
- Parallel-safe build: 由 `HEAD 156545f76f42cac0212c1c409fcdaa1477d23204` 归档仅覆盖本任务 production 文件，
  分别针对 frozen stable `v0.107.1 / 59260271` 与 beta `v0.109.0 / c12f634d` 完成 Release build，均为
  0 warning / 0 error；另针对本机当前 `v0.110.1 / db5d3552` 完成 restore/publish。候选不含
  `DropdownTextUpdater`、`ForecastTimelineShadow` 或 `VerifiedShadowmeldFutureBlockModifier`。
- Reviewed staging: `work/publish/stable/damage-forecast-blp4g` 恰好包含两个文件：manifest SHA256
  `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`，DLL SHA256
  `4AADA9C3C1F1AEF601E61237BE5E1433209BE67D08CD382752D6E35178B3B2B5`；DLL 包含
  `ShouldUseCompactEnglishClosedDropdownFont` / `DropdownFontBaseline`，且程序集名仍为 `damage-forecast`。
- Install boundary: read-only Plan 为 `action=target-upgrade`、`gameRunning=false`、单一 target identity、无 legacy/orphan；
  当前 active 仍是 BLP-4 DLL `B3D64C4751F65EAFA753D2DD9C2BD082C3F64C568081E29E711F41A915822743`。
  只有用户明确批准 BLP-4G 安装后，才可用上述 staging/active 精确哈希执行替换。
- Install result: 用户随后明确要求“安装吧”。复跑 Plan 仍为 `action=target-upgrade`、`gameRunning=false`，随后以 staging
  manifest/DLL 与旧 active manifest/DLL 四个 SHA256 锁定执行。活动目录现恰好包含 manifest/DLL 两文件，哈希分别为
  `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D` 与
  `4AADA9C3C1F1AEF601E61237BE5E1433209BE67D08CD382752D6E35178B3B2B5`。
- Recovery / post-install audit: 上一版 BLP-4 完整备份到 Loader 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4g-20260801-baseline-94-install-damage-forecast-v0.3.0`；
  备份 DLL SHA256 为 `B3D64C4751F65EAFA753D2DD9C2BD082C3F64C568081E29E711F41A915822743`，ledger 为同级
  `blp4g-20260801-baseline-94-install-install-ledger.json`。复跑只读 Plan 得到 `action=target-already-current`、
  `gameRunning=false`、单一 target identity、无 legacy/orphan。Codex 未启动游戏，字号效果保持 `RuntimeNotVerified`。

### BLP-4H 两个长英文关闭态值定向字号证据（2026-08-01）

- Runtime feedback: 用户截图确认 BLP-4G 的 `Above End Turn Button` 在 `94%` 下仍与箭头相碰；另一个未进入
  BLP-4G 策略的 `Right of Expected Loss` 也明显被箭头遮挡。截图中的展开候选项和 `Show Both` 等短值不需要缩小。
- Authorized scope: 用户同意采用定项方案并要求“试试吧”。本 Gate 只授权 BLP-4H 代码、契约、构建与只读安装 Plan；
  未授权安装、游戏启动、Git checkpoint、发布或 Workshop。
- Production change: 继续只处理关闭态当前值并复用 BLP-4G 的 BaseLib 原始字号捕获/精确恢复。
  三个 placement preset 属性的 English + `EndTurnButtonAbove` 从 `94%` 调为 `90%`；
  `IncomingDamagePlacement` 的 English + `RightOfExpectedHpLoss` 新增 `88%`。其他英文、全部中文与所有展开候选项保持
  BaseLib 原始字号，不移动文字、不改对齐或宽度。
- Contracts: 当前三条 typography 合同更新为 `BLP4H-001` 至 `BLP4H-003`，覆盖两个值的属性/语言边界、
  `90%`/`88%` 整数舍入，以及 baseline metadata。完整 harness 为
  `SUMMARY discovered=532 passed=532 failed=0 skipped=0`。
- Guardrail: stable 与 beta 各为 `532/532` contracts，Release build 均为 0 warning / 0 error；最终
  `QUALITY_GATE targets=2 status=PASS exit_code=0`，`git diff --check` 与 artifact review 通过。
- Parallel-safe staging: 从 `HEAD 156545f76f42cac0212c1c409fcdaa1477d23204` 干净归档只覆盖本任务 production 文件，
  针对本机 `v0.110.1 / db5d3552` restore/publish。`work/publish/stable/damage-forecast-blp4h` 恰好包含两文件：
  manifest SHA256 `B1BEA532527122635AEAC344AE9DCE15FC7BAE39FF321B51F10EA383AD703A8D`，DLL SHA256
  `11699ADCB0FB047580773FA94A367D8F043E4904E32D20B48345D04A5D5C0824`。候选包含两个定项百分比和 baseline
  symbols，不含 dead scanner、timeline-shadow 或 Shadowmeld 并行实现。
- Install boundary: read-only Plan 为 `action=target-upgrade`、`gameRunning=false`、单一 target identity、无 legacy/orphan；
  当前 active 仍是已安装 BLP-4G DLL `4AADA9C3C1F1AEF601E61237BE5E1433209BE67D08CD382752D6E35178B3B2B5`。
  只有用户明确批准 BLP-4H 安装后才可替换。
- Merged-build override: 用户随后明确要求 BLP-4H 只保留代码/测试，不安装上述隔离 DLL；等待并行 SM-1F 完成后，
  必须从当前共享工作树统一 rebuild，确认同一产物同时含 BLP-4H 与 SM-1F，并只安装一次合并 DLL。隔离候选
  `11699ADCB0FB047580773FA94A367D8F043E4904E32D20B48345D04A5D5C0824` 未被安装。
- Shared-tree build: SM-1F 任务卡已为 `Complete` 后，从当前共享工作树针对本机 `v0.110.1 / db5d3552`
  restore/publish 到 `work/publish/stable/damage-forecast-blp4h-sm1f-merged`；构建前后 BLP-4H 与 SM-1F 六个关键源码
  SHA256 完全一致。合并 staging 恰好两文件：manifest
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`、DLL
  `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50`。
- Cross-feature artifact proof: 合并 DLL 同时包含 BLP-4H 的
  `EndTurnButtonAboveClosedDropdownFontPercent` / `RightOfExpectedLossClosedDropdownFontPercent` / baseline symbols，
  以及 SM-1F 的 `ForecastActionRefreshPolicy` / `PlayCardAction` / `VerifiedShadowmeldFutureBlockModifier` / policy symbols。
- Single install: 安装前 Plan 为 `action=target-upgrade`、`gameRunning=false`、单一 target identity、无 legacy/orphan；
  以合并 staging 与当前 BLP-4G active 四个 SHA256 锁定，只执行一次安装。活动目录现恰好两文件且哈希与合并 staging
  完全一致；活动 DLL 内再次确认两组 feature symbols 均存在。上一版 BLP-4G 完整备份到 Loader 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4h-sm1f-merged-20260801-damage-forecast-v0.3.0`，
  ledger 为同级 `blp4h-sm1f-merged-20260801-install-ledger.json`。复跑 Plan 为 `target-already-current`，游戏仍未运行。

### BLP-4H 运行失败与只读原因检查（2026-08-01）

- Runtime result: 用户两张截图确认 `Right of Expected Loss` 与 `Above End Turn Button` 的关闭态文字仍进入右侧箭头区域；
  BLP-4H typography 判定 Runtime Failed。用户同时确认合并 DLL 中的 SM-1F 测试成功，该结果已转交融入暗影任务，不把
  Shadowmeld 成功外推为本字号方案成功。
- Loaded artifact proof: 当前活动目录仍为合并 manifest
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB` 与 DLL
  `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50`；活动 DLL 内存在 BLP-4H 的
  `90%`/`88%` constants 与 resolver symbols。不是旧 DLL、隔离 DLL或 feature 遗失。
- Applied-policy evidence: 截图中两个目标关闭态均已小于其展开候选项，且 `Right of Expected Loss` 相对未定项缩小的
  `Show Both` 呈现预期字号差异；说明属性/值/语言匹配和主题字号 override 已执行。失败不是策略未命中。
- BaseLib geometry: 当前 BaseLib `NConfigDropdown` 直接转入原生 `screens/settings_dropdown` scene，并把 dropdown 与
  `NDropdownPositioner` 的 logical minimum size 都固定为 `324 × 64`、horizontal `ShrinkEnd`。截图中的按钮约为
  `432` px 宽，与 `4/3` UI 缩放相符；当前值文字以整个按钮中心对齐，而箭头叠放在同一矩形右侧，没有独立的文字安全区。
- Native auto-size boundary: current `v0.110.1 / db5d3552` 的 `MegaLabel.SetTextAutoSize` IL 在改文后调用
  `AdjustFontSize`；该算法把 label 的完整 `GetRect().Size` 传给 `MegaLabelHelper.IsTooBig`，只验证文字是否放进 label rect，
  不读取箭头 rect，也不从可用宽度扣除箭头占位。因此原生 auto-size 本身也不能检测这类视觉相交。
- Verdict: BLP-4H 的固定 `90%`/`88%` 确实围绕原中心缩小文字，但在当前字体/UI scale 下仍不足以让右边界退到箭头左侧。
  两张图的共同根因是“标签以完整 dropdown rect 居中，箭头无预留安全区”，而固定比例只缓解宽度、没有改变碰撞模型。
- Boundary: 本轮仅查看截图、活动哈希/符号、当前源码、BaseLib 官方源码与 native IL；未修改生产代码或 contracts，未构建、
  未安装、未启动游戏、未执行 Git、发布或 Workshop。下一修复需单独批准 BLP-4I。

### BLP-4I 箭头安全区动态字号证据（2026-08-01）

- Authorized scope: 用户明确回复“也可以 开始吧”，批准 BLP-4I 代码、contracts 与共享树构建；安装继续作为单独 Gate，
  未授权游戏启动、Git checkpoint、发布或 Workshop。
- Production change: 删除 BLP-4H 的 `90%` / `88%` 固定比例。仍只匹配三个 placement preset 属性中的 English
  `EndTurnButtonAbove`，以及 `IncomingDamagePlacement` 的 English `RightOfExpectedHpLoss`；中文、短英文、展开候选项、
  控件宽度、文字中心和对齐均不改变。
- Collision model: 使用 dropdown 当前 logical width；布局尚未提供宽度时回退 BaseLib 的 `324`。由于文字仍以整个按钮中心
  对齐，从左右各扣除 `42` logical px，`324` 宽时得到 `240` 的居中文字安全宽度。随后用 Godot 当前主题字体的
  `Font.GetStringSize(...)` 从 BaseLib 原始字号向下逐级实测，选择不大于安全宽度的最大字号，不再依赖 UI scale 下易失真的百分比。
- Baseline lifecycle: `DropdownFontBaseline` 同时记录 BaseLib 原始 local override 与 Damage Forecast 上次预期应用状态；若 BaseLib
  因选择值变化重新执行 auto-size，检测实际状态变化并刷新 baseline。离开两个目标值时精确恢复 BaseLib 状态，避免重现
  BLP-4F 的全局缩小。
- Contracts: typography 合同替换为 `BLP4I-001` 至 `BLP4I-004`，覆盖精确属性/值/语言边界、对称箭头安全区、
  基于实测宽度且不放大的字号选择，以及可刷新的 baseline metadata。完整 harness 为
  `SUMMARY discovered=535 passed=535 failed=0 skipped=0`。
- Guardrail: stable 与 beta 各为 `535/535` contracts，Release build 均为 0 warning / 0 error；最终
  `QUALITY_GATE targets=2 status=PASS exit_code=0`，`git diff --check` 与 artifact review 通过。
- Shared-tree candidate: 针对本机 `v0.110.1 / db5d3552` 从当前共享工作树 restore/build/publish 到
  `work/publish/stable/damage-forecast-blp4i-sm1f-merged`。目录恰好包含 manifest/DLL；manifest SHA256 为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB`，DLL SHA256 为
  `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`。
- Cross-feature artifact proof: 合并 DLL 同时包含 BLP-4I 的 `ClosedDropdownArrowSafeInset`、
  `ShouldFitEnglishClosedDropdownFont`、`ResolveSafeClosedDropdownFontSize`、`DropdownFontBaseline`，以及已通过用户运行验证的
  SM-1F `ForecastActionRefreshPolicy`、`PlayCardAction`、`VerifiedShadowmeldFutureBlockModifier` symbols。
- Install boundary: 只读 Plan 为 `action=target-upgrade`、`gameRunning=false`、单一 target identity、无 legacy/orphan；当前 active
  仍是 BLP-4H + SM-1F 合并 DLL `779D1761DA9A1E6FD18A696FC8FA1CEA73F098CC1000E5879B0E410B75BF0E50`。
  当时 BLP-4I 候选尚未安装，必须等待用户单独批准安装。
- Install result: 用户随后明确批准“安装 BLP-4I 合并 DLL”。复跑 Plan 保持 `target-upgrade`、`gameRunning=false` 与上述四个
  staging/active 精确 SHA256 后，只执行一次安装。活动目录现在恰好包含 manifest/DLL 两文件，哈希分别为
  `FF8D4E07E574F9FC89EDEDF0D569EE8A7CADFE2A6A2907CAA9E3097F476C32DB` 与
  `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`；活动 DLL 再次确认 BLP-4I 与 SM-1F
  关键 symbols 均存在。上一版完整备份到 Loader 外的
  `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\.damage-forecast-backups\blp4i-sm1f-merged-20260801-arrow-safe-fit-damage-forecast-v0.3.0`，
  ledger 为同级 `blp4i-sm1f-merged-20260801-arrow-safe-fit-install-ledger.json`。复跑 Plan 为 `target-already-current`、
  `gameRunning=false`、单一 target identity、无 legacy/orphan。Codex 未启动游戏，字号结果保持 `RuntimeNotVerified`。
- Runtime result: 用户按安装后的验证交接检查并回复“完美 我很满意”，因此登记 BLP-4I 为 `RuntimeVerified`：两个目标长英文
  关闭态值已避开箭头，且未反馈中文、短英文或展开候选项字号回归。该结论只覆盖当前安装的
  `v0.3.0` DLL `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF` 与当前 BaseLib/game UI。
- Final limitation: 箭头安全区策略有意只覆盖两个已证实遮挡的 English 关闭态值；未来若新增更长文案，或 BaseLib 改变
  dropdown/arrow 几何，需要新的运行证据，不把本次成功外推到未知布局。

## Final closure

Result: BaseLib 下拉框中英文展开态本地化已完成；两个已确认遮挡的英文关闭态值改为实际字体宽度与箭头安全区动态适配，并由用户运行验证成功。
Current state: 活动 `v0.3.0` 合并 DLL 为 `42FAFBE970D378F18E76BA95AE4FD05903D61D3111B883BCDAA53CE6EB44F4CF`；BLP-3 与 BLP-4I `RuntimeVerified`，旧版可恢复备份仍在 Loader 外。
Authority: `docs/task-notes/baselib-dropdown-popup-localization-master-task-card.md` 已同步为唯一当前权威；历史 BLP-4F/4G/4H 失败证据保留原义。
Repository: 本地 BLP implementation / contract / authority checkpoint 已获用户明确授权并由本次 closure commit 形成；未 push、tag、发布或更新 Workshop。

## Gate disposition（历史）

- `BLP-1`：Skipped / Not Needed。BLP-0 静态证据已足够选定修复路径，无需临时诊断。
- `BLP-2`：Skipped / Not Needed。未生成或安装诊断产物。
- `BLP-3`：Complete / RuntimeVerified。已同步 source/live item 文本并删除无效全树扫描路径。
- `BLP-4`：Complete / RuntimeVerified。用户已验证中英文展开项、页面重开与重启持久化。
- `BLP-4F`：Reverted / Failed。直接删除主题字号 override 会缩小全部中英文下拉框；不得恢复该实现。
- `BLP-4G`：Installed / Superseded by BLP-4H。原始字号恢复成功，但 `94%` 仍未避开全部已确认遮挡。
- `BLP-4H`：Installed in one shared-tree artifact with SM-1F / Runtime Failed；缩放已执行但仍未避开箭头，隔离 DLL 从未安装。
- `BLP-4I`：Installed / RuntimeVerified。两个长英文关闭态值使用实际字体宽度与箭头安全区动态适配；活动合并 DLL 已同时
  审计 BLP-4I 与 SM-1F，用户确认运行效果完美。
- `BLP-5`：Authority / Final Limitation / Local Repository Checkpoint / Closure Complete。

## Shared verification matrix

- 中文模式：当前值与全部展开项均为中文，不出现 `SimplifiedChinese`、`HealthBarRight` 等原始枚举名。
- 英文模式：当前值与全部展开项均为友好英文；页面内切换语言后重新展开立即生效。
- 关闭并重开设置页、完整重启游戏后，语言和其他配置仍正确持久化。
- BaseLib 标准布局、恢复默认、左侧选择、高亮、鼠标和控制器焦点行为无回归。
- 修复严格限定于 Damage Forecast；稳定/测试构建不能替代用户在匹配游戏版本上的运行时验证。

## Completion and closure requirements

- 正式方案建立在已确认的弹层生命周期或 BaseLib 官方本地化合同上，不保留全树逐帧文本扫描。
- 所有实际枚举下拉项通过中英文展开态验证；静态确认不得写成运行时确认。
- 每次更新游戏文件后立即停止，说明更新内容、测试步骤、预期结果和反馈项，等待用户亲自测试。
- 最终只更正仍然有效的 current authority；`AUD-0008` 和 Phase 12B 保留为不可改写的历史证据。
- 按 `task-closure-standard.md` 记录结果、当前状态、Authority 与获批 checkpoint；不自动执行 Git、发布或 Workshop。

本卡已关闭，只作为 BaseLib 下拉本地化实现、失败尝试和运行验证的历史 authority；后续语言扩展或布局变化必须另开任务并重新获批。
