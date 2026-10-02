using HarmonyLib;
using ActsFromThePast.Powers;
using ActsFromThePastMultiplayerBalance.Code.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
namespace ActsFromThePastMultiplayerBalance.Patches;

// BAL-4 fix (astra-advice 2026-09-12): the three getter prefixes declared an
// UNUSED __instance parameter typed as Transient — Harmony binds instance
// parameters by TYPE, and the patched instances are ShiftingStrengthDownPower,
// so the mismatch was a live runtime contract violation even though the build
// was green. The parameters were never used: deleted.
//
// D01 (2026-10-02): every getter now honours MultiplayerBalanceGate.ShiftingLabelEnabled.
// Returning true leaves the authoritative AFTP getter in place, so disabling the
// group restores the original label instead of half-patched UI.
[HarmonyPatch(typeof(ShiftingStrengthDownPower))]
public static class ShiftingStrengthDownPowerPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(ShiftingStrengthDownPower), "OriginModel", MethodType.Getter)]
	static bool OriginModelPatch(ref AbstractModel __result)
	{
		if (!MultiplayerBalanceGate.ShiftingLabelEnabled)
		{
			return true;
		}
		__result = ModelDb.Power<MultiplayerShiftingPower>();
		return false;
	}

    [HarmonyPrefix]
	[HarmonyPatch(typeof(ShiftingStrengthDownPower), "Title", MethodType.Getter)]
	static bool TitlePatch(ref LocString __result)
	{
		if (!MultiplayerBalanceGate.ShiftingLabelEnabled)
		{
			return true;
		}
		__result = ModelDb.Power<MultiplayerShiftingPower>().Title;
		return false;
	}

    [HarmonyPrefix]
	[HarmonyPatch(typeof(ShiftingStrengthDownPower), "ExtraHoverTips", MethodType.Getter)]
	static bool ExtraHoverTipsPatch(ref IEnumerable<IHoverTip> __result)
	{
		if (!MultiplayerBalanceGate.ShiftingLabelEnabled)
		{
			return true;
		}
		__result = 
        [
            HoverTipFactory.FromPower<MultiplayerShiftingPower>(),
            HoverTipFactory.FromPower<StrengthPower>()
        ];
		return false;
	}
}
