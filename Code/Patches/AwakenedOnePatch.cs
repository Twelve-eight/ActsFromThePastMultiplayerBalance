using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(AwakenedOne))]
public static class AwakenedOnePatch
{
	[HarmonyPostfix]
	[HarmonyPatch("AfterAddedToRoom")]
	static void AfterAddedToRoomPatch(AwakenedOne __instance, ref Task __result)
	{
		Task original = __result;
		__result = AfterAddedToRoomAsync(__instance, original);
	}

	static async Task AfterAddedToRoomAsync(AwakenedOne instance, Task original)
	{
		// Preserve the authoritative AFTP lifecycle first: RegenEnemyPower,
		// CuriosityPower with the original Players.Count >= 2 ? 1 : CuriosityAmount
		// reduction, UnawakenedPower, StartingStrength and the OnParticleDeath
		// subscription all run in the original task. The multiplayer replacement
		// only swaps the Curiosity power after the original completes, reusing the
		// amount the original computed instead of re-deriving it from constants.
		await original;
		if (!MultiplayerBalanceGate.AwakenedOneEnabled)
		{
			return;
		}
		CuriosityPower? originalPower = instance.Creature.GetPower<CuriosityPower>();
		if (originalPower == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] AwakenedOne.AfterAddedToRoom did not apply CuriosityPower; keeping the original lifecycle result.");
			return;
		}
		int amount = originalPower.Amount;
		MultiplayerCuriosityPower? replacement = await PowerCmd.Apply<MultiplayerCuriosityPower>(new ThrowingPlayerChoiceContext(), instance.Creature, amount, instance.Creature, null);
		if (replacement == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] AwakenedOne: MultiplayerCuriosityPower could not be applied; keeping the original CuriosityPower.");
			return;
		}
		await PowerCmd.Remove(originalPower);
	}

	[HarmonyPostfix]
	[HarmonyPatch("RebirthMove")]
	static void RebirthMovePatch(AwakenedOne __instance, ref Task __result, IReadOnlyList<Creature> targets)
	{
		Task original = __result;
		__result = RebirthMoveAsync(__instance, original);
	}

	static async Task RebirthMoveAsync(AwakenedOne instance, Task original)
	{
		// Await the original RebirthMove to completion so its revival animation / HP / model
		// transform finish before the caller's awaited task resolves. Exceptions from the
		// original task propagate normally through this wrapper; they are not swallowed.
		// The replacement power is cleaned up unconditionally: it must not survive the
		// rebirth even if the group was toggled off after it had been applied.
		await original;
		await PowerCmd.Remove<MultiplayerCuriosityPower>(instance.Creature);
	}
}
