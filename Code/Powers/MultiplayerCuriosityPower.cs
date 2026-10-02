using System.Threading.Tasks;
using ActsFromThePastMultiplayerBalance.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace ActsFromThePastMultiplayerBalance.Code.Powers;

public sealed class MultiplayerCuriosityPower: MultiplayerBalancePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        // Group gate: see MultiplayerBalanceGate. Applied instances stop acting when
        // the AwakenedOne group (or the master switch) is off.
        if (!MultiplayerBalanceGate.AwakenedOneEnabled)
		{
			return;
		}
        if (cardPlay.Card.Type == CardType.Power)
        {
            await Cmd.Wait(0.5f);
            // D08: stamp ExtraDamageSource.Curiosity on the instance the engine
            // actually attaches, so the shared power can be gated per group.
            await ExtraDamagePower.ApplyFromSourceAsync(cardPlay.Card.Owner.Creature, base.Owner, base.Amount, ExtraDamageSource.Curiosity);
        }
    }
}