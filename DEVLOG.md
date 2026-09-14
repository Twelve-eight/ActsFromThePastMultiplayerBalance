# DEVLOG - ActsFromThePastMultiplayerBalance

## 2026-09-13: BAL-4 修复 + 首次可加载化 (主会话单线, 输入序列见 docs/session-log-2026-09-12-13.md)

### 用户输入 → 工作
1. (astra-advice 二轮, 用户「全部开工」) BAL-4: ShiftingStrengthDownPowerPatch 三个 getter
   的未使用 `__instance` 参数声明为 `Transient` 类型, 而被 patch 的实例是
   `ShiftingStrengthDownPower` —— Harmony 实例参数按类型绑定, 类型不符是运行期契约违规
   (构建绿不代表合法)。参数从未使用, 按建议**删除** (源码级修正, 提交见 git)。
2. 同批部署时发现本 mod **从未具备可加载形态**: csproj 引用的 `mod_manifest.json`
   不存在于仓库 → 补齐 manifest (id/版本 0.1.0, 依赖 BaseLib 3.4.5 + ActsFromThePast
   1.0.5), 首次部署到实机 mods/。

### 状态
- 构建 0 错误; 已部署实机 (mod 首次可被加载)。
- **未验证**: Transient/AwakenedOne 多人实战、阈值跨越、复活动画义务、非目标怪物对照
  ——全部依赖真实双端/实机游玩 (用户阻塞队列)。
- 遗留: mod 无 pck (has_pck=false), localization 文件夹未打包 —— 如需中文化需后续开 pck。

## 2026-09-14 astra 第三轮审查交接记录

第三轮隔离构建 exit 0, 0 warnings. 当前源代码已删除 `ShiftingStrengthDownPower` getter 中错误的 `Transient __instance` 参数; binary recheck 没有执行当前 getter ClassProcessor 或真实战斗. 仍需验证 Transient/AwakenedOne, 阈值, RebirthMove, 非目标怪物和多人.

证据: `G:\\omp works\\astra-advice-evidence\\2026-09-14\\third-review-summary.json`. 当前 advice 修改未提交, 未操作游戏或部署.

## 2026-09-14 astra 第四轮复核

当前 2953700 源码相对第三轮无新增变化. Release 0 warnings/0 errors. 三个 getter 的真实 Harmony ClassProcessor 安装成功; Rebirth wrapper 在原 Task 未完成时等待, 原异常保持可观察. Math.Max 和累计 crossings 公式已在当前源码, 旧 advice 中 BAL-4 仍未改的段落已纠正.

实际多人伤害阈值/回合重置/成功复活与动画仍未运行; 不把 Task fixture 当整场战斗. AfterAddedToRoom no-op 基类与反射硬编码回落仍需维护. AFTP 主包的状态/特效建议在 ../aftp-upstream/astra-advice.md, 不混入 Balance 平衡职责.

证据: ../astra-advice-evidence/2026-09-14/round4/binary-boundaries.json. 未改产品源码, 未部署/操作游戏/push.
