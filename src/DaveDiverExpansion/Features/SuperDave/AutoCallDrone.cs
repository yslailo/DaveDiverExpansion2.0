using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using DaveDiverExpansion.Helpers;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DaveDiverExpansion.Features.SuperDave;

/// <summary>
/// Automatically calls the salvage (lift) drone for a large fish when Dave gets close, instead
/// of the player having to aim at it and long-press the interact key.
///
/// The drone interaction is exposed through <see cref="FishInteractionBody"/> as an
/// <c>AttachBalloonCommand_SO</c>.  For ordinary "drone-only" fish it is the *main* interaction
/// (<c>InteractionType == Calldrone</c>), but large aggressive fish such as the Thresher Shark
/// expose it as a *sub* interaction (<c>SubInteractionType == Calldrone</c>, main == null), so
/// both are probed.  Whether the drone may be called at all is left to the game's own
/// CheckAvailableInteraction / CheckAvailableSubInteraction — an asleep/dead test is far too
/// strict (the game allows the drone on a Thresher Shark that is neither).  We then invoke the
/// command exactly like the game's hold-to-call flow (DownExecute -&gt; SuccessInteraction) once
/// per fish per cooldown.
/// </summary>
public static class AutoCallDrone
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<float> Radius;
    public static ConfigEntry<float> Cooldown;

    private const float TickInterval = 0.2f;
    private static float _tickTimer;
    private static readonly Dictionary<long, float> _lastCall = new();

    // Fish we already called a drone on this dive.  The game does not reliably flip the
    // sub-interaction unavailable after the call, so a plain cooldown kept spawning a fresh
    // drone every few seconds on the same fish (seen in the field log).  Call exactly once.
    private static readonly HashSet<long> _droned = new();
    private static float _pruneTimer;

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind(
            "Diving", "Diving - Auto Call Drone", false,
            "Automatically call the salvage drone on a large fish the game allows it on (no need to aim + hold the interact key) when Dave is close.");
        Radius = config.Bind(
            "Diving", "Diving - Auto Call Drone: Radius", 6f,
            "Radius (meters) around Dave in which a downed large fish triggers an automatic drone call (float, default 6f).");
        Cooldown = config.Bind(
            "Diving", "Diving - Auto Call Drone: Cooldown", 3f,
            "Minimum seconds between drone calls on the same fish (float, default 3f).");
    }

    public static void Tick(PlayerCharacter player)
    {
        if (!SuperDaveCore.Enabled.Value || !Enabled.Value || player == null) return;

        _tickTimer += Time.deltaTime;
        if (_tickTimer < TickInterval) return;
        _tickTimer = 0f;

        try
        {
            var origin = player.transform.position;
            float radius = Radius.Value;
            float now = Time.time;
            bool droneAvail = player.IsDroneAvailable;

            PurgeStaleCooldowns(now);
            if (now - _pruneTimer > 5f) { _pruneTimer = now; PruneDroned(); }
            Diag(player, origin, radius, droneAvail);

            if (!droneAvail) return;

            foreach (var fish in EntityRegistry.AllFish)
            {
                if (fish == null || fish.gameObject == null) continue;
                if (Vector3.Distance(origin, fish.transform.position) > radius) continue;
                if (!TryGetDroneCommand(fish, out var command, out bool isSub)) continue;

                long key;
                try { key = fish.Pointer.ToInt64(); } catch { continue; }

                // One drone per fish per dive — see _droned comment.
                if (_droned.Contains(key)) continue;

                // Let the game decide whether the drone may be called on this fish right now —
                // this is the very check that makes the "hold Q" prompt appear.  The old
                // asleep||dead test is WRONG on its own: the Thresher Shark exposes its
                // Calldrone sub-interaction while neither asleep nor dead (confirmed in-game).
                // It is only kept as a fallback for the rare case the native check throws.
                bool available;
                try { available = isSub ? fish.CheckAvailableSubInteraction(player) : fish.CheckAvailableInteraction(player); }
                catch { available = false; }
                if (!available && !IsDroneEligible(fish)) continue;

                if (_lastCall.TryGetValue(key, out float last) && now - last < Cooldown.Value) continue;

                if (CallDrone(fish, player, command, isSub))
                    _droned.Add(key);
                _lastCall[key] = now;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning("[AutoCallDrone] " + e.Message);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Drone-command detection
    //
    // IMPORTANT: Il2CppInterop returns the command from GetInteractionCommand()/GetSubInteractionCommand()
    // as a *BaseInteractionCommand_SO*-typed wrapper (see Il2CppObjectPool.Get<T>, which creates an
    // instance of exactly T).  Therefore `command is AttachBalloonCommand_SO` is ALWAYS false and
    // must not be used.  Match on the native IL2CPP class name instead, and probe both the main
    // and the sub interaction command.
    // ---------------------------------------------------------------------------------------
    private static bool TryGetDroneCommand(FishInteractionBody fish, out BaseInteractionCommand_SO command, out bool isSub)
    {
        command = null;
        isSub = false;

        // 1) Main interaction command (InteractionType == Calldrone).
        try
        {
            var main = fish.GetInteractionCommand();
            if (IsDroneCommand(main)) { command = main; return true; }
        }
        catch { }

        // 2) Sub interaction command (SubInteractionType == Calldrone) — this is what large fish
        //    like the Thresher Shark actually use (main == null, sub == AttachBalloonCommand_SO).
        try
        {
            var io = fish.TryCast<IInteractionObject>();
            if (io != null)
            {
                var sub = io.GetSubInteractionCommand();
                if (IsDroneCommand(sub)) { command = sub; isSub = true; return true; }
            }
        }
        catch { }

        // 3) Fallback: the typed balloon command field configured on the prefab.  NOTE: this
        //    field is also non-null on many ordinary fish, so it must be gated by the enum.
        try
        {
            bool calldrone =
                fish.InteractionType == FishInteractionBody.FishInteractionType.Calldrone ||
                fish.SubInteractionType == FishInteractionBody.FishSubInteractionType.Calldrone;
            if (calldrone && fish.balloonCommand != null)
            {
                command = fish.balloonCommand;
                isSub = fish.SubInteractionType == FishInteractionBody.FishSubInteractionType.Calldrone;
                return true;
            }
        }
        catch { }

        return false;
    }

    private static bool IsDroneCommand(BaseInteractionCommand_SO command)
    {
        string name = NativeClassName(command);
        return name == "AttachBalloonCommand_SO" || name == "CallDroneCommand_SO";
    }

    private static bool CallDrone(FishInteractionBody fish, PlayerCharacter player, BaseInteractionCommand_SO command, bool isSub)
    {
        // Mirror the game's hold-to-call: give the command its target, then the command's own
        // virtual methods spawn / attach the lift drone.  Il2CppInterop generates the
        // IInteractionObject interface as its own wrapper class, so an explicit cast is needed.
        var target = fish.TryCast<IInteractionObject>();
        if (target == null)
        {
            Plugin.Log.LogWarning($"[AutoCallDrone] could not cast {fish.gameObject.name} to IInteractionObject; skipped.");
            return false;
        }
        try
        {
            command.targetObject = target;
            command.DownExecute(player);
            command.SuccessInteraction(player);
            Plugin.Debug($"[AutoCallDrone] called drone on {fish.gameObject.name} (cmd={NativeClassName(command)}, sub={isSub})");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[AutoCallDrone] drone call failed on {fish.gameObject.name}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// A large fish only needs the salvage drone once it is "downed": asleep (aura /
    /// tranquilizer) or dead (killed / poisoned).  Alive fish are ignored.
    /// </summary>
    private static bool IsDroneEligible(FishInteractionBody fish)
        => ToxicAura.IsFishAsleep(fish) || IsDead(fish);

    private static bool IsDead(FishInteractionBody fish)
    {
        try
        {
            var damageable = fish.gameObject.GetComponent<Damageable>();
            return damageable != null && damageable.IsDeadState();
        }
        catch { return false; }
    }

    private static void PurgeStaleCooldowns(float now)
    {
        if (_lastCall.Count < 64) return;
        var stale = new List<long>();
        foreach (var kv in _lastCall)
            if (now - kv.Value > 60f) stale.Add(kv.Key);
        foreach (var key in stale) _lastCall.Remove(key);
    }

    /// <summary>
    /// Drop guard entries whose fish no longer exists, so a recycled native pointer cannot
    /// suppress a drone call on a genuinely new fish.
    /// </summary>
    private static void PruneDroned()
    {
        if (_droned.Count == 0) return;
        var live = new HashSet<long>();
        foreach (var f in EntityRegistry.AllFish)
        {
            if (f == null) continue;
            try { live.Add(f.Pointer.ToInt64()); } catch { }
        }
        _droned.RemoveWhere(k => !live.Contains(k));
    }

    // DebugLog-only: every 2s list what is around the player, so we can see the real
    // InteractionType / SubInteractionType / command classes / state of nearby fish.
    private static float _diagTimer;
    private static void Diag(PlayerCharacter player, Vector3 origin, float radius, bool droneAvail)
    {
        if (Plugin.DebugLog?.Value != true) return;
        _diagTimer += Time.deltaTime;
        if (_diagTimer < 2f) return;
        _diagTimer = 0f;

        int inRadius = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var fish in EntityRegistry.AllFish)
        {
            if (fish == null || fish.gameObject == null) continue;
            if (Vector3.Distance(origin, fish.transform.position) > radius) continue;
            inRadius++;
            if (inRadius > 12) continue;

            BaseInteractionCommand_SO main = null, sub = null;
            try { main = fish.GetInteractionCommand(); } catch { }
            try { var io = fish.TryCast<IInteractionObject>(); if (io != null) sub = io.GetSubInteractionCommand(); } catch { }
            bool balloon = false;
            try { balloon = fish.balloonCommand != null; } catch { }
            bool drone = false, isSub = false;
            try { drone = TryGetDroneCommand(fish, out _, out isSub); } catch { }
            bool mavail = false, savail = false;
            try { mavail = fish.CheckAvailableInteraction(player); } catch { }
            try { savail = fish.CheckAvailableSubInteraction(player); } catch { }
            bool droned = false;
            try { droned = _droned.Contains(fish.Pointer.ToInt64()); } catch { }
            sb.Append($" [{fish.gameObject.name}: type={fish.InteractionType}/{fish.SubInteractionType} main={NativeClassName(main)} sub={NativeClassName(sub)} balloon={balloon} drone={drone}/{(isSub ? "sub" : "main")} droned={droned} mavail={mavail} savail={savail} asleep={ToxicAura.IsFishAsleep(fish)} dead={IsDead(fish)} {ToxicAura.DescribeBuffState(fish)}]");
        }
        Plugin.Debug($"[AutoCallDrone] diag droneAvail={droneAvail} inRadius={inRadius}{sb}");
    }

    // Resolve the real IL2CPP class name of an interop object (the managed GetType() may lie).
    private static string NativeClassName(BaseInteractionCommand_SO command)
    {
        if (command == null || command.Pointer == IntPtr.Zero) return "null";
        try
        {
            IntPtr cls = command.ObjectClass;
            if (cls == IntPtr.Zero) return "?";
            return Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(cls)) ?? "?";
        }
        catch { return "!"; }
    }
}
