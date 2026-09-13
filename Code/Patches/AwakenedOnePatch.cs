using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using ActsFromThePast.Powers;
using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using System.Collections.Generic;
namespace ActsFromThePastMultiplayerBalance.Patches;

[HarmonyPatch(typeof(AwakenedOne))]
public static class AwakenedOnePatch
{
	[HarmonyPrefix]
	[HarmonyPatch("AfterAddedToRoom")]
	static bool AfterAddedToRoomPatch(AwakenedOne __instance, ref Task __result)
	{
		__result = AfterAddedToRoomAsync(__instance);
		return false;
	}

    static Task BaseAfterAddedToRoom()
    {
        return Task.CompletedTask;
    }

	static async Task AfterAddedToRoomAsync(AwakenedOne instance)
	{
		await BaseAfterAddedToRoom();
        int RegenAmount = (int?)typeof(AwakenedOne)?.GetProperty("RegenAmount", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) ?? 10;
        int CuriosityAmount = (int?)typeof(AwakenedOne)?.GetProperty("CuriosityAmount", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) ?? 1;
		int StartingStrength = (int?)typeof(AwakenedOne)?.GetProperty("StartingStrength", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance) ?? 0;
        await PowerCmd.Apply<RegenEnemyPower>(new ThrowingPlayerChoiceContext(), instance.Creature, RegenAmount, instance.Creature, null);
        await PowerCmd.Apply<MultiplayerCuriosityPower>(new ThrowingPlayerChoiceContext(), instance.Creature, CuriosityAmount, instance.Creature, null);
        await PowerCmd.Apply<UnawakenedPower>(new ThrowingPlayerChoiceContext(), instance.Creature, 1, instance.Creature, null);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), instance.Creature, StartingStrength, instance.Creature, null);
        MethodInfo? methodInfo = typeof(AwakenedOne).GetMethod("OnParticleDeath", BindingFlags.NonPublic | BindingFlags.Instance);
        if (methodInfo != null)
        {
            var OnParticleDeath = (Action<Creature>)Delegate.CreateDelegate(typeof(Action<Creature>), instance, methodInfo);
            instance.Creature.Died += OnParticleDeath;
        }
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
		await original;
		await PowerCmd.Remove<MultiplayerCuriosityPower>(instance.Creature);
	}
}
