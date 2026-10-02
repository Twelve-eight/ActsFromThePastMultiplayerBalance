using System.Collections.Generic;
using System.Threading.Tasks;
using ActsFromThePast.Acts.TheBeyond.Enemies;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
namespace ActsFromThePastMultiplayerBalance.Patches;

/// <summary>
/// D08 (2026-10-02): RebirthMove cleanup split out of <see cref="AwakenedOnePatch"/>
/// so it survives the AwakenedOne behavior-group switch.
///
/// Registration contract (ModEntry):
/// - Master off: InstallPatches is skipped entirely, so this class is never installed.
/// - Master on, AwakenedOne group off: <see cref="AwakenedOnePatch"/> is skipped (no
///   replacement is applied, the original AFTP AfterAddedToRoom runs untouched) and
///   this cleanup still removes a leftover MultiplayerCuriosityPower left in an old
///   save, after the original RebirthMove completes.
/// - Master on, group on: both classes run; this cleanup removes the replacement
///   after the original rebirth lifecycle completes.
/// - If this class fails to install, ModEntry.DisableGroupFor turns the AwakenedOne
///   group off (fail closed): no new replacement powers are created, so no new
///   instances can rely on a cleanup that is not installed.
///
/// Async contract: the original RebirthMove task is awaited first and its result is
/// preserved for the caller. Exceptions from the original task propagate unchanged
/// (this wrapper does not catch). PowerCmd.Remove exceptions also propagate; they
/// are never swallowed. Removal only ever targets MultiplayerCuriosityPower, which
/// is this mod's own replacement, so the original AFTP lifecycle is not altered.
/// </summary>
[HarmonyPatch(typeof(AwakenedOne))]
public static class AwakenedOneRebirthCleanupPatch
{
	[HarmonyPostfix]
	[HarmonyPatch("RebirthMove")]
	static void RebirthMovePatch(AwakenedOne __instance, ref Task __result, IReadOnlyList<Creature> targets)
	{
		Task original = __result;
		__result = RebirthMoveAsync(__instance, original);
	}

	static async Task RebirthMoveAsync(AwakenedOne instance, Task original)
	{
		await original;
		await PowerCmd.Remove<MultiplayerCuriosityPower>(instance.Creature);
	}
}