using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(GremlinMad))]
public static class GremlinMadPatch
{
	[HarmonyPostfix]
	[HarmonyPatch("AfterAddedToRoom")]
	static void AfterAddedToRoomPatch(GremlinMad __instance, ref Task __result)
	{
		Task original = __result;
		__result = AfterAddedToRoomAsync(__instance, original);
	}

	static async Task AfterAddedToRoomAsync(GremlinMad instance, Task original)
	{
		// Preserve the authoritative AFTP lifecycle first: AngryPower with the
		// original AngryAmount, the OnDeath subscription and the leader-death
		// subscription all run in the original task. The multiplayer replacement
		// only swaps the Angry power after the original completes.
		await original;
		if (!MultiplayerBalanceGate.GremlinMadEnabled)
		{
			return;
		}
		AngryPower? originalPower = instance.Creature.GetPower<AngryPower>();
		if (originalPower == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] GremlinMad.AfterAddedToRoom did not apply AngryPower; keeping the original lifecycle result.");
			return;
		}
		int amount = originalPower.Amount;
		MultiplayerAngryPower? replacement = await PowerCmd.Apply<MultiplayerAngryPower>(new ThrowingPlayerChoiceContext(), instance.Creature, amount, instance.Creature, null);
		if (replacement == null)
		{
			ModEntry.Logger.Error($"[{ModEntry.ModId}] GremlinMad: MultiplayerAngryPower could not be applied; keeping the original AngryPower.");
			return;
		}
		await PowerCmd.Remove(originalPower);
	}
}
