using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
using System;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(Transient))]
public static class TransientPatch
{
	[HarmonyPrefix]
	[HarmonyPatch("AfterAddedToRoom")]
	static bool AfterAddedToRoomPatch(Transient __instance, ref Task __result)
	{
		__result = AfterAddedToRoomAsync(__instance);
		return false;
	}

    static Task BaseAfterAddedToRoom()
    {
        return Task.CompletedTask;
    }

	static async Task AfterAddedToRoomAsync(Transient instance)
	{
		await BaseAfterAddedToRoom();
		await PowerCmd.Apply<FadingPower>(new ThrowingPlayerChoiceContext(), instance.Creature, AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 6, 5), instance.Creature, null);
        await PowerCmd.Apply<MultiplayerShiftingPower>(new ThrowingPlayerChoiceContext(), instance.Creature, Math.Max(instance.Creature.CombatState?.Players.Count ?? 1, 1), instance.Creature, null);
	}
}
