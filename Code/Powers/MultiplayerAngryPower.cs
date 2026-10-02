using System.Threading.Tasks;
using ActsFromThePastMultiplayerBalance.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace ActsFromThePastMultiplayerBalance.Code.Powers;

public sealed class MultiplayerAngryPower: MultiplayerBalancePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Group gate: see MultiplayerBalanceGate. Applied instances stop acting when
        // the GremlinMad group (or the master switch) is off.
        if (!MultiplayerBalanceGate.GremlinMadEnabled)
		{
			return;
		}
        if (target != Owner || dealer == null || result.UnblockedDamage <= 0 || !props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered))
        {
            return;
        }
        Flash();
        // D08: stamp ExtraDamageSource.Angry on the instance the engine actually
        // attaches, so the shared power can be gated per group.
        await ExtraDamagePower.ApplyFromSourceAsync(dealer, base.Owner, base.Amount, ExtraDamageSource.Angry);
    }
}