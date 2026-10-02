using System;
using System.Reflection;
using BaseLib.Config;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ActsFromThePastMultiplayerBalance;

[ModInitializer("Init")]
public static class ModEntry
{
    private static Harmony? _harmony;
    public const string ModId = "ActsFromThePastMultiplayerBalance";
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Init()
    {
        Log.Info($"[{ModId}] Initializing...");

        // F03 (2026-10-02): register the persisted switches (BaseLib settings page)
        // and snapshot them BEFORE any Harmony registration. Fail closed: if the
        // configuration cannot be created/loaded, no patch is installed at all.
        if (!TryRegisterConfigAndSnapshotGate())
        {
            Log.Info($"[{ModId}] Loaded without multiplayer balance patches (configuration unavailable).");
            return;
        }

        // F03 contract (D01 C9): MasterEnabled=false skips patch registration entirely.
        // Per-group switches are additionally enforced at the registration boundary
        // below (each patch class belongs to exactly one group) and again at runtime
        // by MultiplayerBalanceGate inside every patch/power, so both the registration
        // entry and the runtime entry honor a group switch.
        if (!MultiplayerBalanceGate.MasterEnabled)
        {
            Log.Info($"[{ModId}] Master switch is off; skipping Harmony registration - all multiplayer balance behavior stays vanilla AFTP.");
            Log.Info($"[{ModId}] Loaded successfully.");
            return;
        }

        InstallPatches();
        Log.Info($"[{ModId}] Loaded successfully.");
    }

    /// <summary>
    /// Registers every Harmony patch class of this assembly with ONE failure boundary
    /// per class. PatchAll() aborts on the first failure and cannot skip a single
    /// behavior group, so registration is done per class:
    /// - a group switch that is off skips exactly its patch class (registration gate);
    /// - a class whose target drifted fails alone, logs an error, and leaves the
    ///   remaining groups working (fail closed per group, never a silent success).
    /// </summary>
    private static void InstallPatches()
    {
        _harmony = new Harmony($"com.kziz3988.{ModId}");

        Type[] types;
        try
        {
            types = typeof(ModEntry).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            Logger.Error($"[{ModId}] Could not enumerate patch classes; no balance patch was installed (fail closed): {e}");
            return;
        }

        int applied = 0;
        int skipped = 0;
        int failed = 0;
        foreach (Type type in types)
        {
            // inherit:true matches PatchAll's class discovery; every patch class in this
            // assembly is a top-level static class, so this changes nothing today and only
            // stays correct if a nested patch class is ever added.
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
            {
                continue;
            }

            if (!IsPatchGroupEnabled(type))
            {
                skipped++;
                Log.Info($"[{ModId}] Behavior group disabled by config; skipping Harmony patch class {type.Name}.");
                continue;
            }

            try
            {
                var result = _harmony.CreateClassProcessor(type).Patch();
                int installed = 0;
                if (result != null)
                {
                    foreach (var method in result)
                    {
                        if (method != null)
                        {
                            installed++;
                        }
                    }
                }
                if (installed > 0)
                {
                    applied++;
                }
                else
                {
                    failed++;
                    DisableGroupFor(type);
                    Logger.Error($"[{ModId}] Harmony patch class {type.Name} installed no patches (target method drift or Prepare declined); that group's balance behavior is disabled.");
                }
            }
            catch (Exception e)
            {
                failed++;
                DisableGroupFor(type);
                Logger.Error($"[{ModId}] Harmony patch class {type.Name} failed to install; that group's balance behavior is disabled: {e}");
            }
        }

        if (failed > 0)
        {
            Logger.Error($"[{ModId}] Harmony registration finished with {failed} failed patch class(es); {applied} applied, {skipped} skipped by config.");
        }
        else
        {
            Log.Info($"[{ModId}] Harmony registration: {applied} patch class(es) applied, {skipped} skipped by config.");
        }
    }

    /// <summary>
    /// Maps each patch class of this assembly to its behavior group. The mapping is
    /// explicit on purpose: a new patch class that is not listed here is NOT covered
    /// by any group switch, so it is refused (fail closed) and logged instead of
    /// silently changing behavior without a switch.
    /// </summary>
    private static bool IsPatchGroupEnabled(Type patchClass)
    {
        if (patchClass == typeof(Patches.TransientPatch)) return MultiplayerBalanceGate.TransientEnabled;
        if (patchClass == typeof(Patches.AwakenedOnePatch)) return MultiplayerBalanceGate.AwakenedOneEnabled;
        if (patchClass == typeof(Patches.GremlinNobPatch)) return MultiplayerBalanceGate.GremlinNobEnabled;
        if (patchClass == typeof(Patches.GremlinMadPatch)) return MultiplayerBalanceGate.GremlinMadEnabled;
        if (patchClass == typeof(Patches.ShiftingStrengthDownPowerPatch)) return MultiplayerBalanceGate.ShiftingLabelEnabled;

        Logger.Error($"[{ModId}] Harmony patch class {patchClass.Name} has no behavior-group mapping; refusing to install it (add it to IsPatchGroupEnabled first).");
        return false;
    }

    /// <summary>
    /// Fail-closed switch for a patch class that could not be installed cleanly:
    /// turning its group gate off makes any partially installed prefix/postfix of
    /// that class return to the authoritative AFTP path at runtime, and it is the
    /// same state the user would get from the matching settings switch.
    /// </summary>
    private static void DisableGroupFor(Type patchClass)
    {
        if (patchClass == typeof(Patches.TransientPatch)) MultiplayerBalanceGate.TransientGroupEnabled = false;
        else if (patchClass == typeof(Patches.AwakenedOnePatch)) MultiplayerBalanceGate.AwakenedOneGroupEnabled = false;
        else if (patchClass == typeof(Patches.GremlinNobPatch)) MultiplayerBalanceGate.GremlinNobGroupEnabled = false;
        else if (patchClass == typeof(Patches.GremlinMadPatch)) MultiplayerBalanceGate.GremlinMadGroupEnabled = false;
        else if (patchClass == typeof(Patches.ShiftingStrengthDownPowerPatch)) MultiplayerBalanceGate.ShiftingLabelGroupEnabled = false;
    }

    /// <summary>
    /// Creates the BaseLib <see cref="SimpleModConfig"/> (which loads the persisted
    /// mod_configs/ActsFromThePastMultiplayerBalance.cfg when present, or writes the
    /// all-true defaults on first run) and copies its values into the plain static
    /// <see cref="MultiplayerBalanceGate"/> before Harmony registration.
    /// </summary>
    private static bool TryRegisterConfigAndSnapshotGate()
    {
        try
        {
            ModConfigRegistry.Register(ModId, new ActsFromThePastMultiplayerBalanceConfig());

            MultiplayerBalanceGate.MasterEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableMultiplayerBalance;
            MultiplayerBalanceGate.TransientGroupEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableTransientBalance;
            MultiplayerBalanceGate.AwakenedOneGroupEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableAwakenedOneBalance;
            MultiplayerBalanceGate.GremlinNobGroupEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableGremlinNobBalance;
            MultiplayerBalanceGate.GremlinMadGroupEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableGremlinMadBalance;
            MultiplayerBalanceGate.ShiftingLabelGroupEnabled = ActsFromThePastMultiplayerBalanceConfig.EnableShiftingLabel;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error($"[{ModId}] Config registration/snapshot failed; Harmony registration is skipped (fail closed): {e}");
            return false;
        }
    }
}

/// <summary>
/// Runtime switches (Settings -> Mod Settings), SimpleModConfig auto-UI.
///
/// Contract (F03, 2026-10-02):
/// - All six properties default to true, so a fresh install or an existing config
///   without these keys keeps the historical behavior.
/// - Values are snapshotted once by <see cref="ModEntry.Init"/> before Harmony
///   registration; a change takes effect on the NEXT game start. This keeps the
///   installed patch set and any run/pool determinism stable for the current session.
/// - Master off: no patch is registered. Group off: that group's patch class is not
///   registered at all, and the runtime gate inside the remaining patch/power code
///   also returns to the authoritative AFTP path, which keeps old saves that still
///   contain Multiplayer*Power instances inert instead of broken.
/// - The balance Power classes stay registered in ModelDb (the engine instantiates
///   every AbstractModel subtype in loaded mod assemblies after the mod initializers,
///   and PowerModel has no auto-add opt-out). Their ModelIds are therefore unchanged
///   for existing saves; the switches only gate behavior, never model identity.
/// </summary>
[ConfigHoverTipsByDefault]
internal sealed class ActsFromThePastMultiplayerBalanceConfig : SimpleModConfig
{
    /// <summary>Master switch. False: no balance patch is registered at all.</summary>
    public static bool EnableMultiplayerBalance { get; set; } = true;

    /// <summary>Transient: multiplayer Shifting threshold replacement.</summary>
    public static bool EnableTransientBalance { get; set; } = true;

    /// <summary>Awakened One: multiplayer Curiosity replacement.</summary>
    public static bool EnableAwakenedOneBalance { get; set; } = true;

    /// <summary>Gremlin Nob: multiplayer Enrage replacement.</summary>
    public static bool EnableGremlinNobBalance { get; set; } = true;

    /// <summary>Gremlin Mad: multiplayer Angry replacement.</summary>
    public static bool EnableGremlinMadBalance { get; set; } = true;

    /// <summary>Shifting Strength tooltip relabeling to the multiplayer variant.</summary>
    public static bool EnableShiftingLabel { get; set; } = true;
}