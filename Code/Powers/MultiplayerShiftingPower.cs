using System.Collections.Generic;
using System.Threading.Tasks;
using ActsFromThePast.Powers;
using ActsFromThePastMultiplayerBalance.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace ActsFromThePastMultiplayerBalance.Code.Powers;

public sealed class MultiplayerShiftingPower: MultiplayerBalancePower
{
	private int _damageReceived;
	public int DamageReceived
	{
		get
		{
			return _damageReceived;
		}
		set
		{
			AssertMutable();
			_damageReceived = value;
			base.DynamicVars["CountDown"].BaseValue = DisplayAmount;
			InvokeDisplayAmountChanged();
		}
	}
    protected override IEnumerable<DynamicVar> CanonicalVars => 
	[
		new DynamicVar("CountDown", DisplayAmount)
	];

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => base.Amount != 0 ? base.Amount - (DamageReceived % base.Amount) : 0;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        DamageReceived = 0;
		return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.TotalDamage <= 0)
		{
			return;
		}
        Flash();
		int before = DamageReceived;
		int threshold = base.Amount;
		if (threshold <= 0)
		{
			// No defined threshold: accumulate the damage for the countdown but never reduce.
			DamageReceived += result.TotalDamage;
			return;
		}
		int crossings = (before + result.TotalDamage) / threshold - before / threshold;
		DamageReceived += result.TotalDamage;
		if (crossings > 0)
		{
			await PowerCmd.Apply<ShiftingStrengthDownPower>(new ThrowingPlayerChoiceContext(), base.Owner, crossings, base.Owner, null);
		}
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == base.Owner.Side)
		{
			DamageReceived = 0;
		}
		return Task.CompletedTask;
    }
}
