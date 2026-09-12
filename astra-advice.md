# Astra advice - ActsFromThePastMultiplayerBalance

日期: 2026-09-12. 主会话单线. 本轮构建此本地分支成功, 0 警告/0 错误; 另反编译当前工坊 3785039319 的同名 DLL, 确認下述前三项结构也存在于实际发布包. 未进行真实怪物战斗.

本仓是外部内容的互操作分支, 不因它能构建就把其原版语义当成 StS1 权威. 修改前先确认用户采用哪种多人平衡规则, 不替用户调数值.

## P1 BAL-1: 人数阈值被 Math.Min(count,1) 固定为 1

位置: `Code/Patches/TransientPatch.cs:29-33`.

当前传给 MultiplayerShiftingPower 的 Amount 是 Math.Min(Players.Count ?? 1,1). 对正常 1/2/3/4 人局都为 1. 文案/计数器显示的多人分摊阈值不能实际变化.

当前工坊反编译也为同一 Math.Min. 这不是本地 fork 独有问题.

建议先确认 Amount 的产品含义, 若是至少 1 的玩家人数, 对应 Math.Max(count,1). 但不要只改这一个字符马上发布: BAL-2 会在 Amount>1 时暴露.

## P1 BAL-2: 跨多次伤害阈值的余数没有参与减力量

位置: `Code/Powers/MultiplayerShiftingPower.cs:39,47-55`.

DisplayAmount 使用累计 DamageReceived % Amount, 但实际减力用 `result.TotalDamage / Amount`. 两次 1 伤, threshold=2, 两次各自整除都为 0, 总共打满阈值仍没减力; 显示与行为分叉.

建议根据累计阈值跨越次数结算, 如 `(before + damage)/threshold - before/threshold`, 同时明确本回合累计与回合重置. 是否用 TotalDamage 还是 UnblockedDamage 必须对照约定, 不一并凭直觉改掉.

验收: 1 人行为不变; 2 人 1+1/3+1/大单次/多段击; 3/4 人跨阈值与余数; 回合重置; 伤害为 0; 显示倒计数与实际减力一致. BAL-1/BAL-2 必须同批验证.

## P1 BAL-3: RebirthMove postfix 丢弃原始异步任务

位置: `Code/Patches/AwakenedOnePatch.cs:49-59`.

Postfix 把 `__result` 换成只删除 MultiplayerCuriosityPower 的 Task, 未等待原 RebirthMove. 原任务已经启动, 不会因替换结果而取消; 调用者却只等待新的短任务, 复活逻辑/动画/模型转换可能仍在后台执行或异常无人观察.

当前工坊 DLL 也保留同一结构. SOURCE 直接确认, 本轮没运行复活战斗.

建议 wrapper 捕获原 Task, await 原任务完成, 再做额外清理, 将整个 wrapper 回传. 原任务异常按正常机制传播, 不能 catch 掉冒充成功. 若设计要求清理发生在原复活之前, 则需要明确前置链, 不让调度偶然决定顺序.

验收: 复活动画/HP/状态转换完成前, 外层等待不得完成; 原过程抛异常能被外层观察; 附加清理只一次; 单人/多人均不提前进入下一步.

## P2 BAL-4: Power getter 补丁的 __instance 类型写成 Monster

位置: `Code/Patches/ShiftingStrengthDownPowerPatch.cs:17,25,33`.

目标是 ShiftingStrengthDownPower, 参数却是 Transient. 该参数没使用, 但类型不符合被 patch 实例. 当前编译能通过不代表 Harmony 的运行期桥接/IL 合法; 当前日志声称加载成功也未证明所有 getter 已被执行.

建议删未使用 __instance 或改成正确的 power 类型. 逐个 getter 安装和调用验证, 不把 PatchAll 的末尾日志当类型检查.

## 其他维护建议

- AfterAddedToRoom 全量 prefix 替代中用 BaseAfterAddedToRoom()=>CompletedTask 是硬编码 no-op, 不是调用真实基类. 上游若增加初始化, 本补丁会吞掉. 尽量仅替换相关 power/数值, 或明确按版本复制完整协议并验证, 不把 no-op 当稳定适配层.
- 反射 RegenAmount/CuriosityAmount 等失败时硬回落 10/1/0, 会静默改变高进阶平衡. 应报告不支持版本/缺成员, 不把默认值伪装为原版值.
- package BaseLib 3.1.* 与实机 3.4.7 有漂移, 当前构建使用的解析版本必须记录. "最低版本" 不等于行为一致.
- 保留 upstream 与本 fork 的职责: 不要直接改工坊二进制然后忘记源码/回滚. 需要修实际用户环境时走可审计发布/兼容补丁.

## 推荐顺序

1. 明确人数阈值契约, BAL-1+BAL-2 一次性验证.
2. BAL-3 保留原 Task 完成契约.
3. BAL-4 和反射成员类型/签名验证.
4. 实际 Transient 与 AwakenedOne 多人场景, 然后再考虑发布.

证据: `../astra-advice-evidence/2026-09-12/binary-inputs.json`, `build-results.json`. 工坊源码复核的本地反编译在 `G:/omp works/.tmp/sts-workspace-audit-1789171823600/decompiled/LiveBalance*.cs`, 不作为可公开源码上传.

本轮未调整平衡, 未打游戏. 不把 SOURCE 问题冒充实机复现, 也不要用没看见报错否定这些控制流缺陷.

## 附录: 用跨阈值与异步完成义务检查平衡补丁

通用流程见 [总建议附录](../astra-advice.md).

1. 对每个人数/阈值公式手算 1/2/3/4 人. Math.Min/Max 都能编译, 但算出来永远为 1 时已经反证 "随人数缩放". 期待值来自规则, 不是把当前公式再算一遍.
2. 对累计机制比较一次 2 与两次 1, 记录阈值前后累计量与实际结算差量. 如果规则要求累计等价, 每击单独整除通常会丢余数; 如果规则本来逐击, 则不能硬套累计等价. 先明确量的含义.
3. 修阈值后, 原先只在 Amount=1 下成立的下游逻辑都要重验. 不把上游数字修对当作整条行为已修好.
4. Postfix 替换 Task 时, 问调用者是否仍等待原怪物动作和新增清理都完成. 原 Task 已启动不代表替换结果会取消它, 更不代表异常还能被外层观察.
5. 反射失败后的默认数值必须有来源和适用条件. 找不到字段是版本信号, 不是自动批准用低进阶默认值.

最短复验: 多人数表 -> 多段合计跨阈值 -> 回合重置 -> 复活 Task 未完成时外层不能前进 -> 原动作异常可观察 -> 非目标怪物不变. 当前工坊与本地 fork 分别绑定版本, 不互借成功证明.
