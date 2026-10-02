namespace ActsFromThePastMultiplayerBalance;

/// <summary>
/// Master and per-behavior switches for the AFTP multiplayer balance patches.
///
/// Contract (D01-aftp-mp, 2026-10-02):
/// - Defaults are true for the master switch and every behavior group, so the
///   pre-switch behavior is preserved for existing configs.
/// - ModEntry (F03) must copy the persisted config values into these properties
///   BEFORE calling Harmony.PatchAll(). When <see cref="MasterEnabled"/> is false
///   the mod must skip PatchAll entirely; the per-group properties still gate the
///   runtime paths of any patch that is installed.
/// - Turning a group off must not silently change unrelated vanilla behavior: the
///   patched postfixes keep the authoritative AFTP lifecycle result and only swap
///   in the multiplayer replacement power while the group is enabled.
/// - This class carries no BaseLib dependency; it is a plain static gate so both
///   the patch layer and the power layer can read it without a config instance.
/// </summary>
public static class MultiplayerBalanceGate
{
    /// <summary>Master switch. False disables every balance behavior.</summary>
    public static bool MasterEnabled { get; set; } = true;

    /// <summary>Transient: FadingPower + multiplayer-threshold Shifting power.</summary>
    public static bool TransientGroupEnabled { get; set; } = true;

    /// <summary>AwakenedOne: multiplayer Curiosity replacement and its Rebirth cleanup.</summary>
    public static bool AwakenedOneGroupEnabled { get; set; } = true;

    /// <summary>GremlinNob: multiplayer Enrage replacement applied after Bellow.</summary>
    public static bool GremlinNobGroupEnabled { get; set; } = true;

    /// <summary>GremlinMad: multiplayer Angry replacement.</summary>
    public static bool GremlinMadGroupEnabled { get; set; } = true;

    /// <summary>ShiftingStrengthDownPower relabeling to the multiplayer power tooltip.</summary>
    public static bool ShiftingLabelGroupEnabled { get; set; } = true;

    public static bool TransientEnabled => MasterEnabled && TransientGroupEnabled;
    public static bool AwakenedOneEnabled => MasterEnabled && AwakenedOneGroupEnabled;
    public static bool GremlinNobEnabled => MasterEnabled && GremlinNobGroupEnabled;
    public static bool GremlinMadEnabled => MasterEnabled && GremlinMadGroupEnabled;
    public static bool ShiftingLabelEnabled => MasterEnabled && ShiftingLabelGroupEnabled;
}
