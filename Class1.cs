// ---------- FILE: TPAPlugin.cs ----------
// RocketMod TPA plugin (2025) - /tpa, /tpaccept, /tpdeny, /tpcancel
// Build this file into TPAPlugin.dll using the .csproj below.
// Place the compiled DLL at: Rocket/Plugins/TPAPlugin/TPAPlugin.dll
// On first run, the config XML (below) will auto-generate. You can also pre-create it.

using Rocket.API;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Timers;
using UnityEngine;

namespace TPAPlugin
{
    public class TPAConfig : IRocketPluginConfiguration
    {
        public ushort RequestTimeoutSeconds;
        public ushort TeleportDelaySeconds;
        public ushort CooldownSeconds;
        public bool CancelOnMove;
        public float CancelOnMoveDistance;
        public void LoadDefaults()
        {
            RequestTimeoutSeconds = 60;
            TeleportDelaySeconds = 3;
            CooldownSeconds = 15;
            CancelOnMove = true;
            CancelOnMoveDistance = 0.8f;
        }
    }

    public class TPAPlugin : RocketPlugin<TPAConfig>
    {
        internal static readonly Dictionary<ulong, TPARequest> Pending = new Dictionary<ulong, TPARequest>();
        internal static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();

        protected override void Load()
        {
            Rocket.Core.Logging.Logger.Log("[TPA] Loaded: /tpa, /tpaccept, /tpdeny, /tpcancel");
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
        }
        protected override void Unload()
        {
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            Pending.Clear();
            Cooldowns.Clear();
        }
        private void OnPlayerDisconnected(UnturnedPlayer p)
        {
            var affected = Pending.Where(kv => kv.Key == p.CSteamID.m_SteamID || kv.Value.RequesterId == p.CSteamID.m_SteamID)
                                  .Select(kv => kv.Key).ToList();
            foreach (var key in affected) Pending.Remove(key);
        }
        internal static bool IsOnCooldown(UnturnedPlayer r, out int remaining)
        {
            remaining = 0;
            if (!Cooldowns.TryGetValue(r.CSteamID.m_SteamID, out var until)) return false;
            var now = DateTime.UtcNow; if (now >= until) return false;
            remaining = (int)Math.Ceiling((until - now).TotalSeconds); return true;
        }
        internal static void StartCooldown(UnturnedPlayer r)
        {
            var cfg = (R.Plugins.GetPlugin("TPAPlugin") as TPAPlugin)?.Configuration.Instance;
            int cd = cfg?.CooldownSeconds ?? 15; Cooldowns[r.CSteamID.m_SteamID] = DateTime.UtcNow.AddSeconds(cd);
        }
        internal static bool TryFindPlayer(string q, out UnturnedPlayer p)
        {
            p = null;
            var matches = Provider.clients.Select(c => UnturnedPlayer.FromCSteamID(c.playerID.steamID))
                .Where(x => x != null && (x.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || x.CharacterName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            if (matches.Count == 1) { p = matches[0]; return true; }
            return false;
        }
        internal static void Msg(IRocketPlayer pl, string m) => UnturnedChat.Say(pl, m, Color.cyan);
        internal static void Err(IRocketPlayer pl, string m) => UnturnedChat.Say(pl, m, Color.red);
    }

    public class TPARequest
    {
        public ulong RequesterId;
        public Vector3 RequesterPos;
        public DateTime CreatedUtc;
        public Timer ExpiryTimer;
        public bool IsActive = true;
        public TPARequest(ulong requesterId, Vector3 requesterPos, double timeoutSeconds, System.Action onExpire)
        {
            RequesterId = requesterId; RequesterPos = requesterPos; CreatedUtc = DateTime.UtcNow;
            ExpiryTimer = new Timer(timeoutSeconds * 1000) { AutoReset = false };
            ExpiryTimer.Elapsed += (s, e) => { IsActive = false; onExpire?.Invoke(); }; ExpiryTimer.Start();
        }
        public void Cancel()
        {
            IsActive = false; if (ExpiryTimer != null) { ExpiryTimer.Stop(); ExpiryTimer.Dispose(); ExpiryTimer = null; }
        }
    }

    // /tpa <player>
    public class CommandTPA : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpa";
        public string Help => "Request to teleport to a player";
        public string Syntax => "/tpa <player>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "tpa.request" };
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var caller = (UnturnedPlayer)ic;
            if (cmd.Length < 1) { TPAPlugin.Err(caller, "Usage: /tpa <player>"); return; }
            if (TPAPlugin.IsOnCooldown(caller, out int remain)) { TPAPlugin.Err(caller, $"Cooldown: {remain}s"); return; }
            if (!TPAPlugin.TryFindPlayer(string.Join(" ", cmd), out var target) || target.CSteamID.m_SteamID == caller.CSteamID.m_SteamID)
            { TPAPlugin.Err(caller, "Player not found or invalid target."); return; }
            if (TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var existing)) { existing.Cancel(); TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID); }
            var cfg = (R.Plugins.GetPlugin("TPAPlugin") as TPAPlugin)?.Configuration.Instance; ushort timeout = cfg?.RequestTimeoutSeconds ?? (ushort)60;
            var req = new TPARequest(caller.CSteamID.m_SteamID, caller.Position, timeout, () =>
            {
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
                TPAPlugin.Err(target, "TPA request expired."); TPAPlugin.Err(caller, "Your TPA request expired.");
            });
            TPAPlugin.Pending[target.CSteamID.m_SteamID] = req;
            TPAPlugin.Msg(caller, $"Sent TPA to {target.DisplayName}. Expires in {timeout}s.");
            TPAPlugin.Msg(target, $"{caller.DisplayName} wants to teleport to you. /tpaccept or /tpdeny.");
        }
    }

    // /tpcancel
    public class CommandTPCancel : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpcancel";
        public string Help => "Cancel your outgoing TPA request";
        public string Syntax => "/tpcancel";
        public List<string> Aliases => new List<string> { "tpacancel" };
        public List<string> Permissions => new List<string> { "tpa.cancel" };
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var caller = (UnturnedPlayer)ic;
            var pair = TPAPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == caller.CSteamID.m_SteamID);
            if (pair.Key == 0) { TPAPlugin.Err(caller, "You have no outgoing TPA request."); return; }
            pair.Value.Cancel(); TPAPlugin.Pending.Remove(pair.Key); TPAPlugin.Msg(caller, "TPA canceled.");
            var target = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(pair.Key)); if (target != null) TPAPlugin.Msg(target, $"{caller.DisplayName} canceled their TPA.");
        }
    }

    // /tpaccept
    public class CommandTPAccept : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpaccept";
        public string Help => "Accept the pending TPA request";
        public string Syntax => "/tpaccept";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "tpa.accept" };
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var target = (UnturnedPlayer)ic; // teleport destination
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var req) || !req.IsActive) { TPAPlugin.Err(target, "No pending TPA."); return; }
            var requester = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(req.RequesterId));
            if (requester == null) { TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID); TPAPlugin.Err(target, "Requester offline."); return; }
            var cfg = (R.Plugins.GetPlugin("TPAPlugin") as TPAPlugin)?.Configuration.Instance; int delay = cfg?.TeleportDelaySeconds ?? 3;
            bool cancelOnMove = cfg?.CancelOnMove ?? true; float cancelDist = cfg?.CancelOnMoveDistance ?? 0.8f;
            req.Cancel(); TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
            Vector3 startPos = requester.Position; TPAPlugin.Msg(requester, $"Teleporting to {target.DisplayName} in {delay}s. Don't move."); TPAPlugin.Msg(target, $"Accepting TPA from {requester.DisplayName}...");
            Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(async () =>
            {
                int waited = 0; while (waited < delay)
                {
                    await System.Threading.Tasks.Task.Delay(1000); waited++;
                    if (cancelOnMove && Vector3.Distance(startPos, requester.Position) > cancelDist)
                    { TPAPlugin.Err(requester, "Teleport canceled: you moved."); TPAPlugin.Msg(target, $"{requester.DisplayName}'s TP canceled (moved)."); return; }
                }
                var targetAgain = UnturnedPlayer.FromCSteamID(target.CSteamID); if (targetAgain == null) { TPAPlugin.Err(requester, "Target offline."); return; }
                try { requester.Teleport(targetAgain.Position, targetAgain.Rotation); TPAPlugin.Msg(requester, $"Teleported to {targetAgain.DisplayName}."); TPAPlugin.Msg(targetAgain, $"{requester.DisplayName} teleported to you."); TPAPlugin.StartCooldown(requester); }
                catch (Exception ex) { Rocket.Core.Logging.Logger.LogException(ex); TPAPlugin.Err(requester, "Teleport failed."); }
            });
        }
    }

    // /tpdeny
    public class CommandTPDeny : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpdeny";
        public string Help => "Deny the pending TPA request";
        public string Syntax => "/tpdeny";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "tpa.deny" };
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var target = (UnturnedPlayer)ic;
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var req) || !req.IsActive) { TPAPlugin.Err(target, "No pending TPA."); return; }
            req.Cancel(); TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
            var requester = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(req.RequesterId)); if (requester != null) TPAPlugin.Err(requester, $"Your TPA to {target.DisplayName} was denied.");
            TPAPlugin.Msg(target, "TPA denied.");
        }
    }
}



