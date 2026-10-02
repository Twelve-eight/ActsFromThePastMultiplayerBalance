using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
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
		//
		// D08 (2026-10-02): RebirthMove cleanup moved to
		// AwakenedOneRebirthCleanupPatch so it also runs when this behavior group is
		// off (this class is then skipped by ModEntry, leaving the original AFTP
		// lifecycle untouched).
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
}