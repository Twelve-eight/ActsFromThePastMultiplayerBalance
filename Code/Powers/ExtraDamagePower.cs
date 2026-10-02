using System.Threading.Tasks;
using ActsFromThePastMultiplayerBalance.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace ActsFromThePastMultiplayerBalance.Code.Powers;

public sealed class ExtraDamagePower: MultiplayerBalancePower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (!wasRemovalPrevented && creature == base.Applier)
		{
			await PowerCmd.Remove(this);
		}
	}

	public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		// Master gate only: this power is shared by the Enrage/Curiosity/Angry
		// replacement groups, so it must stop amplifying damage whenever the mod's
		// master switch is off, even for instances that were already applied.
		if (!MultiplayerBalanceGate.MasterEnabled)
		{
			return 0m;
		}
		if (target != base.Owner)
		{
			return 0m;
		}
		if (!props.IsPoweredAttack())
		{
			return 0m;
		}
		if (dealer == base.Applier)
		{
			return base.Amount;
		}
		return 0m;
	}
}
