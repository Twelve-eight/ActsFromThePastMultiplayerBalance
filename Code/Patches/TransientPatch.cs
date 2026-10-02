using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
using System;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(Transient))]
public static class TransientPatch
{
	[HarmonyPostfix]
	[HarmonyPatch("AfterAddedToRoom")]
	static void AfterAddedToRoomPatch(Transient __instance, ref Task __result)
	{
		Task original = __result;
		__result = AfterAddedToRoomAsync(__instance, original);
	}

	static async Task AfterAddedToRoomAsync(Transient instance, Task original)
	{
		// Preserve the authoritative AFTP lifecycle first: the original
		// AfterAddedToRoom applies FadingPower with the ascension value and
		// initializes _multiplayerDamageMultiplier from the player count, which the
		// previous prefix replacement had dropped. Only after the original task
		// completes do we swap in the multiplayer-threshold Shifting power.
		await original;
		if (!MultiplayerBalanceGate.TransientEnabled)
		{
			return;
		}
		int playerCount = Math.Max(instance.Creature.CombatState?.Players.Count ?? 1, 1);
		ShiftingPower? originalPower = instance.Creature.GetPower<ShiftingPower>();
		if (originalPower == null)
		{
			// Version drift: the original no longer applies ShiftingPower. Fail closed
			// and keep whatever the authoritative lifecycle produced.
			ModEntry.Logger.Error($"[{ModEntry.ModId}] Transient.AfterAddedToRoom did not apply ShiftingPower; keeping the original lifecycle result.");
			return;
		}
		MultiplayerShiftingPower? replacement = await PowerCmd.Apply<MultiplayerShiftingPower>(new ThrowingPlayerChoiceContext(), instance.Creature, playerCount, instance.Creature, null);
		if (replacement == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] Transient: MultiplayerShiftingPower could not be applied; keeping the original ShiftingPower.");
			return;
		}
		await PowerCmd.Remove(originalPower);
	}
}
