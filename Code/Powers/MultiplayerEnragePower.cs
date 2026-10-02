using System.Threading.Tasks;
using ActsFromThePastMultiplayerBalance.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace ActsFromThePastMultiplayerBalance.Code.Powers;

public sealed class MultiplayerEnragePower: MultiplayerBalancePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        // Group gate: see MultiplayerBalanceGate. Applied instances stop acting when
        // the GremlinNob group (or the master switch) is off.
        if (!MultiplayerBalanceGate.GremlinNobEnabled)
		{
			return;
		}
        if (cardPlay.Card.Type == CardType.Skill)
        {
            await Cmd.Wait(0.5f);
            await PowerCmd.Apply<ExtraDamagePower>(new ThrowingPlayerChoiceContext(), cardPlay.Card.Owner.Creature, base.Amount, base.Owner, null);
        }
    }
}
