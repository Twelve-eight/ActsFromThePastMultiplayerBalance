using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(GremlinNob))]
public static class GremlinNobPatch
{
	[HarmonyPostfix]
	[HarmonyPatch("Bellow")]
	static void BellowPatch(GremlinNob __instance, ref Task __result, IReadOnlyList<Creature> targets)
	{
		Task original = __result;
		__result = BellowAsync(__instance, original);
	}

	static async Task BellowAsync(GremlinNob instance, Task original)
	{
		// Preserve the authoritative AFTP Bellow: bellow SFX, banter, scream VFX,
		// screen shake and the original Players.Count > 2 ? 1 : EnrageAmount
		// arithmetic all run first. The multiplayer replacement power only swaps in
		// after the original completes, reusing the amount the original computed.
		await original;
		if (!MultiplayerBalanceGate.GremlinNobEnabled)
		{
			return;
		}
		EnragePower? originalPower = instance.Creature.GetPower<EnragePower>();
		if (originalPower == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] GremlinNob.Bellow did not apply EnragePower; keeping the original lifecycle result.");
			return;
		}
		int amount = originalPower.Amount;
		MultiplayerEnragePower? replacement = await PowerCmd.Apply<MultiplayerEnragePower>(new ThrowingPlayerChoiceContext(), instance.Creature, amount, instance.Creature, null);
		if (replacement == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] GremlinNob: MultiplayerEnragePower could not be applied; keeping the original EnragePower.");
			return;
		}
		await PowerCmd.Remove(originalPower);
	}
}
