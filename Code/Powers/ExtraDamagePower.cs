using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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

/// <summary>
/// Provenance of one <see cref="ExtraDamagePower"/> instance.
///
/// D08 (2026-10-02): the power is shared by three balance groups
/// (Awakened One Curiosity, Gremlin Nob Enrage, Gremlin Mad Angry). The marker is
/// a plain int-backed enum stored per instance; it is never a static/global value,
/// so concurrent applies cannot cross-talk. <see cref="ExtraDamageSource.Unknown"/> is the default:
/// instances that predate this marker (legacy in-flight instances) or instances
/// applied by code outside the three source powers keep it and fail closed.
///
/// Deliberately NOT a [SavedProperty]: the engine never persists PowerModel
/// instances (SerializableRun has no powers; NetFullCombatState.PowerState carries
/// only id+amount), so a SavedProperty would add no persistence while changing
/// ModelIdSerializationCache.Hash and the multiplayer handshake/replay hash. The
/// marker stays deterministic on every peer because it is stamped from the same
/// deterministic Apply call.
/// </summary>
public enum ExtraDamageSource
{
    /// <summary>No proven source. Fail closed: deals no extra damage and refuses new stacks.</summary>
    Unknown = 0,

    /// <summary>Applied by MultiplayerCuriosityPower (Awakened One group).</summary>
    Curiosity = 1,

    /// <summary>Applied by MultiplayerEnragePower (Gremlin Nob group).</summary>
    Enrage = 2,

    /// <summary>Applied by MultiplayerAngryPower (Gremlin Mad group).</summary>
    Angry = 3,
}

public sealed class ExtraDamagePower: MultiplayerBalancePower
{
    private ExtraDamageSource _source = ExtraDamageSource.Unknown;
    private bool _unknownSourceWarned;
    private bool _sourceMismatchWarned;

    // All three in-mod source paths use this gate. It is keyed by the same
    // (target, applier) identity that PowerCmd uses for InstancedPerApplier stacking.
    // ConditionalWeakTable keeps both combat creatures weakly held; unrelated pairs
    // do not block one another and dead creatures do not stay rooted here.
    private sealed class ApplyGate
    {
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
    }

    private sealed class ApplyGateSet
    {
        internal readonly ApplyGate NullApplier = new();
        internal readonly ConditionalWeakTable<Creature, ApplyGate> ByApplier = new();

        internal ApplyGate For(Creature? applier)
        {
            return applier == null
                ? NullApplier
                : ByApplier.GetValue(applier, static _ => new ApplyGate());
        }
    }

    private static readonly ConditionalWeakTable<Creature, ApplyGateSet> ApplyGates = new();

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    /// <summary>
    /// Source group that created this instance. Set exactly once on a mutable
    /// candidate immediately before the engine may attach it. Stacking onto an
    /// existing instance never rewrites it, so a later apply from another group
    /// cannot forge provenance.
    /// </summary>
    public ExtraDamageSource Source
    {
        get => _source;
        private set
        {
            AssertMutable();
            _source = value;
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (!wasRemovalPrevented && creature == base.Applier)
		{
			await PowerCmd.Remove(this);
		}
	}

	public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		// Master gate: stop every instance when the mod is fully disabled.
		if (!MultiplayerBalanceGate.MasterEnabled)
		{
			return 0m;
		}
		// Per-source group gate: an instance whose source group is off stops
		// amplifying damage, while instances from other groups keep working.
		// Unknown provenance (legacy in-flight instance) fails closed.
		if (!IsSourceGroupEnabled(Source))
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

	private static bool IsSourceGroupEnabled(ExtraDamageSource source)
	{
		return source switch
		{
			ExtraDamageSource.Curiosity => MultiplayerBalanceGate.AwakenedOneEnabled,
			ExtraDamageSource.Enrage => MultiplayerBalanceGate.GremlinNobEnabled,
			ExtraDamageSource.Angry => MultiplayerBalanceGate.GremlinMadEnabled,
			_ => false,
		};
	}

	/// <summary>
	/// Applies the shared power for one of the three source groups. The mutable
	/// candidate is stamped before the engine can attach it, and existing-instance
	/// merges are admitted only after the same-source check.
	///
	/// Engine contract (PowerCmd.Apply / FindExistingInstanceForStacking):
	/// - InstancedPerApplier merges into the existing instance of the same
	///   (target, applier) pair and returns that same instance; a fresh instance is
	///   attached only when none existed.
	/// - The return value is null when combat is ending, the target cannot receive
	///   powers, or the modified amount became 0.
	/// - The overload used below accepts a prebuilt mutable candidate and returns
	///   after either merging into an existing instance or attaching that candidate;
	///   a zero/blocked application attaches nothing.
	///
	/// An existing instance with provenance is only stacked onto when the source
	/// matches. An existing instance without provenance is never adopted (no
	/// fabricated source): it fails closed and refuses new stacks.
	/// </summary>
	internal static async Task ApplyFromSourceAsync(Creature? target, Creature? applier, decimal amount, ExtraDamageSource source)
	{
		if (target == null || !IsSourceGroupEnabled(source))
		{
			return;
		}

		ApplyGate gate = ApplyGates.GetValue(target, static _ => new ApplyGateSet()).For(applier);
		await gate.Semaphore.WaitAsync();
		try
		{
			// Re-check after waiting: a fail-closed group decision may have changed
			// while this source was queued behind another apply.
			if (!IsSourceGroupEnabled(source))
			{
				return;
			}

			// Keep the provenance decision and the engine's merge/new-instance decision
			// in one serialized critical section for every in-mod source path.
			ExtraDamagePower? existing = target.GetPowerInstances<ExtraDamagePower>().FirstOrDefault(p => p.Applier == applier);
			if (existing != null)
			{
				if (existing.Source == ExtraDamageSource.Unknown)
				{
					existing.WarnUnknownSource(source);
					return;
				}
				if (existing.Source != source)
				{
					existing.WarnSourceMismatch(source);
					return;
				}
			}

			// Create the mutable candidate ourselves so its provenance is set before
			// PowerCmd can attach it. If an existing matching instance is found inside
			// PowerCmd, this candidate is discarded and only that already-provenanced
			// instance is modified.
			ExtraDamagePower candidate = (ExtraDamagePower)ModelDb.Power<ExtraDamagePower>().ToMutable();
			candidate.Source = source;
			await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), candidate, target, amount, applier, null);

			// A candidate is provenance-safe only if the engine actually attached this
			// exact object. Existing-instance merges are already covered by the
			// pre-check above; a null/zero/blocked application attaches nothing.
			if (!target.GetPowerInstances<ExtraDamagePower>().Contains(candidate))
			{
				return;
			}
		}
		finally
		{
			gate.Semaphore.Release();
		}
	}

	private void WarnUnknownSource(ExtraDamageSource incoming)
	{
		if (_unknownSourceWarned)
		{
			return;
		}
		_unknownSourceWarned = true;
		ModEntry.Logger.Warn($"[{ModEntry.ModId}] ExtraDamagePower instance has no provenance (legacy in-flight instance); failing closed: it adds no damage and {incoming} will not stack onto it.");
	}

	private void WarnSourceMismatch(ExtraDamageSource incoming)
	{
		if (_sourceMismatchWarned)
		{
			return;
		}
		_sourceMismatchWarned = true;
		ModEntry.Logger.Warn($"[{ModEntry.ModId}] ExtraDamagePower instance provenance {Source} does not match incoming {incoming}; keeping the original provenance and refusing the stack.");
	}
}