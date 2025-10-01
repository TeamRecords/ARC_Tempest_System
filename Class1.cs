using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace ARC_TPA_Commands
{
    public class TPAConfig : IRocketPluginConfiguration
    {
        public ushort RequestTimeoutSeconds;
        public ushort TeleportDelaySeconds;
        public ushort CooldownSeconds;
        public bool CancelOnMove;
        public float CancelOnMoveDistance;
        public bool Use_Permissions;

        public void LoadDefaults()
        {
            RequestTimeoutSeconds = 60;
            TeleportDelaySeconds = 3;
            CooldownSeconds = 15;
            CancelOnMove = true;
            CancelOnMoveDistance = 0.8f;
            Use_Permissions = false;
        }
    }

    public class TPAPlugin : RocketPlugin<TPAConfig>
    {
        internal static TPAPlugin Instance;
        internal static readonly Dictionary<ulong, TPARequest> Pending = new Dictionary<ulong, TPARequest>();
        internal static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();

        internal static TPAConfig Config => Instance?.Configuration.Instance;
        internal static bool PermissionsEnabled => Config?.Use_Permissions ?? false;

        protected override void Load()
        {
            Instance = this;
            Logger.Log("[TPA] Loaded: /tpa, /tpaccept, /tpdeny, /tpcancel");
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        protected override void Unload()
        {
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            foreach (var request in Pending.Values)
            {
                request.Cancel();
            }

            Pending.Clear();
            Cooldowns.Clear();
            Instance = null;
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            if (player == null)
            {
                return;
            }

            ulong steamId = player.CSteamID.m_SteamID;
            var affectedTargets = Pending.Where(kv => kv.Key == steamId || kv.Value.RequesterId == steamId)
                                          .Select(kv => kv.Key)
                                          .ToList();

            foreach (var targetId in affectedTargets)
            {
                if (!Pending.TryGetValue(targetId, out var request))
                {
                    continue;
                }

                Pending.Remove(targetId);
                request.Cancel();

                if (targetId == steamId)
                {
                    var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
                    if (requester != null)
                    {
                        Err(requester, $"Your TPA request to {player.DisplayName} was cancelled because they disconnected.");
                    }
                }
                else
                {
                    var target = UnturnedPlayer.FromCSteamID(new CSteamID(targetId));
                    if (target != null)
                    {
                        Err(target, $"{request.RequesterName} disconnected. Their TPA request was cancelled.");
                    }
                }
            }

            Cooldowns.Remove(steamId);
        }

        internal static void Msg(UnturnedPlayer player, string message)
        {
            if (player == null)
            {
                return;
            }

            UnturnedChat.Say(player, message, Color.cyan);
        }

        internal static void Err(UnturnedPlayer player, string message)
        {
            if (player == null)
            {
                return;
            }

            UnturnedChat.Say(player, message, Color.red);
        }

        internal static void StartCooldown(UnturnedPlayer player)
        {
            if (player == null)
            {
                return;
            }

            int cooldown = Config?.CooldownSeconds ?? 15;
            if (cooldown <= 0)
            {
                Cooldowns.Remove(player.CSteamID.m_SteamID);
                return;
            }

            Cooldowns[player.CSteamID.m_SteamID] = DateTime.UtcNow.AddSeconds(cooldown);
        }

        internal static bool IsOnCooldown(UnturnedPlayer player, out int remainingSeconds)
        {
            remainingSeconds = 0;

            if (player == null)
            {
                return false;
            }

            if (!Cooldowns.TryGetValue(player.CSteamID.m_SteamID, out var until))
            {
                return false;
            }

            int remaining = (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds);
            if (remaining <= 0)
            {
                Cooldowns.Remove(player.CSteamID.m_SteamID);
                return false;
            }

            remainingSeconds = remaining;
            return true;
        }

        internal static bool TryFindPlayer(string query, ulong callerId, out UnturnedPlayer player, out string failureMessage)
        {
            player = null;
            failureMessage = null;

            query = query?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                failureMessage = "You must specify a player name.";
                return false;
            }

            var matches = Provider.clients
                .Select(c => UnturnedPlayer.FromCSteamID(c.playerID.steamID))
                .Where(p => p != null && (p.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          p.CharacterName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (matches.Count == 0)
            {
                failureMessage = $"No players matched '{query}'.";
                return false;
            }

            if (matches.Count > 1)
            {
                var preview = matches.Take(5).Select(m => m.DisplayName).ToList();
                failureMessage = $"Multiple matches: {string.Join(", ", preview)}" +
                                  (matches.Count > preview.Count ? "..." : string.Empty);
                return false;
            }

            var match = matches[0];
            if (match.CSteamID.m_SteamID == callerId)
            {
                failureMessage = "You cannot send a TPA to yourself.";
                return false;
            }

            player = match;
            return true;
        }
    }

    public class TPARequest
    {
        public ulong RequesterId { get; }
        public string RequesterName { get; }
        public Vector3 RequesterPos { get; }
        public DateTime CreatedUtc { get; }
        public double TimeoutSeconds { get; }
        public Timer ExpiryTimer { get; private set; }
        public bool IsActive { get; private set; } = true;

        public TPARequest(ulong requesterId, string requesterName, Vector3 requesterPos, double timeoutSeconds, System.Action onExpire)
        {
            RequesterId = requesterId;
            RequesterName = requesterName;
            RequesterPos = requesterPos;
            CreatedUtc = DateTime.UtcNow;
            TimeoutSeconds = timeoutSeconds;

            ExpiryTimer = new Timer(timeoutSeconds * 1000) { AutoReset = false };
            ExpiryTimer.Elapsed += (s, e) =>
            {
                IsActive = false;
                onExpire?.Invoke();
            };
            ExpiryTimer.Start();
        }

        public void Cancel()
        {
            IsActive = false;
            if (ExpiryTimer != null)
            {
                ExpiryTimer.Stop();
                ExpiryTimer.Dispose();
                ExpiryTimer = null;
            }
        }

        public int GetSecondsRemaining()
        {
            if (!IsActive)
            {
                return 0;
            }

            double remaining = TimeoutSeconds - (DateTime.UtcNow - CreatedUtc).TotalSeconds;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
        }
    }

    public class CommandTPA : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.request" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpa";
        public string Help => "Send a teleport request";
        public string Syntax => "/tpa <player>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;

            if (command.Length < 1)
            {
                var status = TPAPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == player.CSteamID.m_SteamID);
                if (status.Key != 0 && status.Value.IsActive)
                {
                    var targetPlayer = UnturnedPlayer.FromCSteamID(new CSteamID(status.Key));
                    var remaining = status.Value.GetSecondsRemaining();
                    string targetName = targetPlayer != null ? targetPlayer.DisplayName : "(offline player)";
                    TPAPlugin.Msg(player, $"Pending request to {targetName}. {remaining}s remaining. Use /tpcancel to cancel.");
                }
                else
                {
                    TPAPlugin.Msg(player, "Usage: /tpa <player>. Use /tpcancel to cancel your outgoing request.");
                    if (TPAPlugin.IsOnCooldown(player, out var remainingCd))
                    {
                        TPAPlugin.Msg(player, $"Cooldown remaining: {remainingCd}s");
                    }
                }

                return;
            }

            if (TPAPlugin.IsOnCooldown(player, out int cooldownRemaining))
            {
                TPAPlugin.Err(player, $"Cooldown: {cooldownRemaining}s");
                return;
            }

            if (!TPAPlugin.TryFindPlayer(string.Join(" ", command), player.CSteamID.m_SteamID, out var target, out var failure))
            {
                TPAPlugin.Err(player, failure);
                return;
            }

            if (TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var existing))
            {
                var previousRequester = UnturnedPlayer.FromCSteamID(new CSteamID(existing.RequesterId));
                existing.Cancel();
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);

                if (previousRequester != null && previousRequester.CSteamID.m_SteamID != player.CSteamID.m_SteamID)
                {
                    TPAPlugin.Err(previousRequester, $"Your TPA request to {target.DisplayName} was replaced by another player.");
                }
            }

            var cfg = TPAPlugin.Config;
            ushort timeout = cfg?.RequestTimeoutSeconds ?? (ushort)60;
            var request = new TPARequest(player.CSteamID.m_SteamID, player.DisplayName, player.Position, timeout, () =>
            {
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
                TPAPlugin.Err(target, "TPA request expired.");
                TPAPlugin.Err(player, "Your TPA request expired.");
            });

            TPAPlugin.Pending[target.CSteamID.m_SteamID] = request;
            TPAPlugin.Msg(player, $"Sent TPA to {target.DisplayName}. Expires in {timeout}s. Use /tpcancel to cancel.");
            TPAPlugin.Msg(target, $"{player.DisplayName} wants to teleport to you. Use /tpaccept or /tpdeny.");
            TPAPlugin.Msg(target, $"Request will auto-expire in {request.GetSecondsRemaining()}s.");
        }
    }

    public class CommandTPCancel : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.cancel" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpcancel";
        public string Help => "Cancel your outgoing TPA request";
        public string Syntax => "/tpcancel";
        public List<string> Aliases => new List<string> { "tpacancel" };
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;
            var pair = TPAPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == player.CSteamID.m_SteamID);
            if (pair.Key == 0)
            {
                TPAPlugin.Err(player, "You have no outgoing TPA request.");
                return;
            }

            pair.Value.Cancel();
            TPAPlugin.Pending.Remove(pair.Key);
            TPAPlugin.Msg(player, "TPA canceled.");

            var target = UnturnedPlayer.FromCSteamID(new CSteamID(pair.Key));
            if (target != null)
            {
                TPAPlugin.Msg(target, $"{player.DisplayName} canceled their TPA.");
            }
        }
    }

    public class CommandTPAccept : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.accept" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpaccept";
        public string Help => "Accept the pending TPA request";
        public string Syntax => "/tpaccept";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var target = (UnturnedPlayer)caller;
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var request) || !request.IsActive)
            {
                TPAPlugin.Err(target, "No pending TPA.");
                return;
            }

            var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester == null)
            {
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
                TPAPlugin.Err(target, "Requester offline.");
                request.Cancel();
                return;
            }

            var cfg = TPAPlugin.Config;
            int delay = cfg?.TeleportDelaySeconds ?? 3;
            bool cancelOnMove = cfg?.CancelOnMove ?? true;
            float cancelDistance = cfg?.CancelOnMoveDistance ?? 0.8f;

            request.Cancel();
            TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);

            Vector3 startPos = requester.Position;
            TPAPlugin.Msg(requester, $"Teleporting to {target.DisplayName} in {delay}s. Don't move.");
            TPAPlugin.Msg(target, $"Accepting TPA from {requester.DisplayName}...");

            TaskDispatcher.QueueOnMainThread(async () =>
            {
                int waited = 0;
                while (waited < delay)
                {
                    await Task.Delay(1000);
                    waited++;

                    if (cancelOnMove && Vector3.Distance(startPos, requester.Position) > cancelDistance)
                    {
                        Err(requester, "Teleport canceled: you moved.");
                        Msg(target, $"{requester.DisplayName}'s teleport canceled (they moved).");
                        return;
                    }
                }

                var updatedTarget = UnturnedPlayer.FromCSteamID(target.CSteamID);
                if (updatedTarget == null)
                {
                    Err(requester, "Teleport failed: target offline.");
                    return;
                }

                try
                {
                    requester.Teleport(updatedTarget.Position, updatedTarget.Rotation);
                    Msg(requester, $"Teleported to {updatedTarget.DisplayName}.");
                    Msg(updatedTarget, $"{requester.DisplayName} teleported to you.");
                    StartCooldown(requester);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    Err(requester, "Teleport failed.");
                }
            });
        }
    }

    public class CommandTPDeny : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.deny" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpdeny";
        public string Help => "Deny the pending TPA request";
        public string Syntax => "/tpdeny";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var target = (UnturnedPlayer)caller;
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var request) || !request.IsActive)
            {
                TPAPlugin.Err(target, "No pending TPA.");
                return;
            }

            request.Cancel();
            TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);

            var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester != null)
            {
                TPAPlugin.Err(requester, $"Your TPA to {target.DisplayName} was denied.");
            }

            TPAPlugin.Msg(target, "TPA denied.");
        }
    }
}
