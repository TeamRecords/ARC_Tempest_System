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
using Rocket.Unturned;

using Logger = Rocket.Core.Logging.Logger;

namespace ARC_TPA_Commands
{
    public enum TempestRequestType
    {
        TeleportToTarget,
        SummonTarget
    }

    public class TempestConfig : IRocketPluginConfiguration
    {
        public ushort RequestTimeoutSeconds;
        public ushort TeleportDelaySeconds;
        public ushort CooldownSeconds;
        public bool CancelOnMove;
        public float CancelOnMoveDistance;
        public bool Use_Permissions;
        public bool Enable_Map_Bridge;
        public string Map_Connection_String;
        public string Map_Provider_Invariant_Name;
        public ushort Map_Refresh_Interval_Seconds;
        public ushort Map_Player_Stale_Minutes;
        public string Map_Share_Url;

        public void LoadDefaults()
        {
            RequestTimeoutSeconds = 30;
            TeleportDelaySeconds = 3;
            CooldownSeconds = 15;
            CancelOnMove = true;
            CancelOnMoveDistance = 0.8f;
            Use_Permissions = false;
            Enable_Map_Bridge = false;
            Map_Connection_String = "Server=localhost;Port=3306;Database=tempest_map;Uid=tempest;Pwd=ChangeMe!;SslMode=Preferred;";
            Map_Provider_Invariant_Name = "MySql.Data.MySqlClient";
            Map_Refresh_Interval_Seconds = 5;
            Map_Player_Stale_Minutes = 2;
            Map_Share_Url = "https://tempest.arcfoundation.net/map";
        }
    }

    public class TempestPlugin : RocketPlugin<TempestConfig>
    {
        internal static TempestPlugin Instance;
        internal static readonly Dictionary<ulong, TempestRequest> Pending = new Dictionary<ulong, TempestRequest>();
        internal static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();
        internal static TempestConfig Config => Instance?.Configuration.Instance;
        internal static bool PermissionsEnabled => Config?.Use_Permissions ?? false;
        internal static TempestMapService MapService { get; private set; }
        internal static bool MapBridgeEnabled => Config?.Enable_Map_Bridge ?? false;
        internal static string MapShareUrl => Config?.Map_Share_Url;

        internal static List<string> ResolvePermissions(List<string> requiredPermissions)
        {
            if (!PermissionsEnabled)
            {
                // Returning null signals to RocketMod that the command requires no
                // specific permission nodes. Supplying an empty list incorrectly
                // causes RocketMod to fall back to its default permission checks,
                // which prevents non-owners from executing the Tempest commands
                // when permissions are globally disabled. Rocket will treat the
                // null value as "no permission required" and therefore skip any
                // permission lookups that would otherwise trigger the "no
                // permission" warning for normal players.
                return null;
            }

            // Ensure Rocket always receives a concrete list when permissions are
            // enabled. Returning the same static list instance can allow third
            // parties to mutate it, so provide a defensive copy instead.
            if (requiredPermissions == null || requiredPermissions.Count == 0)
            {
                return new List<string>();
            }

            return new List<string>(requiredPermissions);
        }

        protected override void Load()
        {
            Instance = this;
            Logger.Log("[ARC Tempest] Loaded: /tpa, /tphere, /tpaccept, /tpdeny, /tpcancel, /tempest cmds, /tmap");
            if (MapBridgeEnabled)
            {
                MapService = new TempestMapService(Config);
                MapService.Start();
                if (!string.IsNullOrWhiteSpace(Config?.Map_Share_Url))
                {
                    Logger.Log($"[ARC Tempest] Tactical map live at {Config.Map_Share_Url}");
                }
            }
            else
            {
                Logger.Log("[ARC Tempest] Tactical map bridge disabled. Enable Enable_Map_Bridge in the configuration to activate it.");
            }

            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            MapService?.Dispose();
            MapService = null;

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
            MapService?.MarkPlayerOffline(steamId);
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

                string label = GetRequestLabel(request.Type);

                if (targetId == steamId)
                {
                    var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
                    if (requester != null)
                    {
                        Err(requester, $"Your {label} for {player.DisplayName} was cancelled because they disconnected.");
                    }
                }
                else
                {
                    var target = UnturnedPlayer.FromCSteamID(new CSteamID(targetId));
                    if (target != null)
                    {
                        Err(target, $"{request.RequesterName} disconnected. Their {label} was cancelled.");
                    }
                }
            }

            Cooldowns.Remove(steamId);
        }

        private void OnPlayerConnected(UnturnedPlayer player)
        {
            if (player == null)
            {
                return;
            }

            MapService?.TrackPlayer(player);
        }

        internal static string GetRequestLabel(TempestRequestType type)
        {
            switch (type)
            {
                case TempestRequestType.SummonTarget:
                    return "summon request";
                default:
                    return "teleport request";
            }
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
                failureMessage = "You cannot target yourself.";
                return false;
            }

            player = match;
            return true;
        }

        internal static void ExecuteRequestCommand(UnturnedPlayer requester, string[] command, TempestRequestType type)
        {
            bool isSummon = type == TempestRequestType.SummonTarget;
            ulong requesterId = requester.CSteamID.m_SteamID;

            string usage = isSummon
                ? "Usage: /tphere <player>. Use /tpcancel to cancel your outgoing request."
                : "Usage: /tpa <player>. Use /tpcancel to cancel your outgoing request.";

            if (command.Length < 1)
            {
                var status = Pending.FirstOrDefault(kv => kv.Value.RequesterId == requesterId && kv.Value.Type == type);
                if (status.Key != 0 && status.Value.IsActive)
                {
                    var targetPlayer = UnturnedPlayer.FromCSteamID(new CSteamID(status.Key));
                    var remaining = status.Value.GetSecondsRemaining();
                    string pendingTargetName = targetPlayer != null ? targetPlayer.DisplayName : "(offline player)";
                    string pendingLabel = isSummon ? "summon" : "teleport";
                    Msg(requester, $"Pending {pendingLabel} request {(isSummon ? "for" : "to")} {pendingTargetName}. {remaining}s remaining. Use /tpcancel to cancel.");
                }
                else
                {
                    Msg(requester, usage);
                    if (IsOnCooldown(requester, out var remainingCd))
                    {
                        Msg(requester, $"Cooldown remaining: {remainingCd}s");
                    }
                }

                return;
            }

            if (IsOnCooldown(requester, out int cooldownRemaining))
            {
                Err(requester, $"Cooldown: {cooldownRemaining}s");
                return;
            }

            if (!TryFindPlayer(string.Join(" ", command), requesterId, out var target, out var failure))
            {
                Err(requester, failure);
                return;
            }

            ulong targetId = target.CSteamID.m_SteamID;

            if (Pending.TryGetValue(targetId, out var existing))
            {
                var previousRequester = UnturnedPlayer.FromCSteamID(new CSteamID(existing.RequesterId));
                existing.Cancel();
                Pending.Remove(targetId);

                if (previousRequester != null && previousRequester.CSteamID.m_SteamID != requesterId)
                {
                    Err(previousRequester, $"Your {GetRequestLabel(existing.Type)} for {target.DisplayName} was replaced by another player.");
                }
            }

            var cfg = Config;
            ushort timeout = cfg?.RequestTimeoutSeconds ?? (ushort)30;
            string requesterName = requester.DisplayName;
            string targetName = target.DisplayName;

            string requesterExpired = isSummon
                ? $"Your summon request for {targetName} expired."
                : $"Your teleport request to {targetName} expired.";
            string targetExpired = isSummon
                ? $"The summon request from {requesterName} expired."
                : $"The teleport request from {requesterName} expired.";

            var request = new TempestRequest(requesterId, targetId, requesterName, type, timeout, () =>
            {
                Pending.Remove(targetId);

                var targetOnline = UnturnedPlayer.FromCSteamID(new CSteamID(targetId));
                if (targetOnline != null)
                {
                    Err(targetOnline, targetExpired);
                }

                var requesterOnline = UnturnedPlayer.FromCSteamID(new CSteamID(requesterId));
                if (requesterOnline != null)
                {
                    Err(requesterOnline, requesterExpired);
                }
            });

            Pending[targetId] = request;

            string sentMessage = isSummon
                ? $"Sent summon request for {target.DisplayName}. Expires in {timeout}s. Use /tpcancel to cancel."
                : $"Sent teleport request to {target.DisplayName}. Expires in {timeout}s. Use /tpcancel to cancel.";
            string targetNotice = isSummon
                ? $"{requester.DisplayName} wants to teleport you to them. Use /tpaccept or /tpdeny."
                : $"{requester.DisplayName} wants to teleport to you. Use /tpaccept or /tpdeny.";

            Msg(requester, sentMessage);
            Msg(target, targetNotice);
            Msg(target, $"Request will auto-expire in {request.GetSecondsRemaining()}s.");
        }
    }

    public class TempestRequest
    {
        public ulong RequesterId { get; }
        public ulong TargetId { get; }
        public string RequesterName { get; }
        public TempestRequestType Type { get; }
        public DateTime CreatedUtc { get; }
        public double TimeoutSeconds { get; }
        public Timer ExpiryTimer { get; private set; }
        public bool IsActive { get; private set; } = true;

        public TempestRequest(ulong requesterId, ulong targetId, string requesterName, TempestRequestType type, double timeoutSeconds, System.Action onExpire)
        {
            RequesterId = requesterId;
            TargetId = targetId;
            RequesterName = requesterName;
            Type = type;
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
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.tpa" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpa";
        public string Help => "Send a teleport request";
        public string Syntax => "/tpa <player>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;
            TempestPlugin.ExecuteRequestCommand(player, command, TempestRequestType.TeleportToTarget);
        }
    }

    public class CommandTPHere : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.tphere" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tphere";
        public string Help => "Request another player to teleport to you";
        public string Syntax => "/tphere <player>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;
            TempestPlugin.ExecuteRequestCommand(player, command, TempestRequestType.SummonTarget);
        }
    }

    public class CommandTPCancel : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.cancel" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpcancel";
        public string Help => "Cancel your outgoing Tempest request";
        public string Syntax => "/tpcancel";
        public List<string> Aliases => new List<string> { "tpacancel" };
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;
            var pair = TempestPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == player.CSteamID.m_SteamID);
            if (pair.Key == 0)
            {
                TempestPlugin.Err(player, "You have no outgoing Tempest request.");
                return;
            }

            pair.Value.Cancel();
            TempestPlugin.Pending.Remove(pair.Key);
            string label = TempestPlugin.GetRequestLabel(pair.Value.Type);
            TempestPlugin.Msg(player, $"{char.ToUpperInvariant(label[0]) + label.Substring(1)} canceled.");

            var target = UnturnedPlayer.FromCSteamID(new CSteamID(pair.Key));
            if (target != null)
            {
                TempestPlugin.Msg(target, $"{player.DisplayName} canceled their {label}.");
            }
        }
    }

    public class CommandTPAccept : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.accept" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpaccept";
        public string Help => "Accept the pending Tempest request";
        public string Syntax => "/tpaccept";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var target = (UnturnedPlayer)caller;
            if (!TempestPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var request) || !request.IsActive)
            {
                TempestPlugin.Err(target, "No pending Tempest request.");
                return;
            }

            var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester == null)
            {
                TempestPlugin.Pending.Remove(target.CSteamID.m_SteamID);
                TempestPlugin.Err(target, "Requester offline.");
                request.Cancel();
                return;
            }

            var cfg = TempestPlugin.Config;
            int delay = cfg?.TeleportDelaySeconds ?? 3;
            bool cancelOnMove = cfg?.CancelOnMove ?? true;
            float cancelDistance = cfg?.CancelOnMoveDistance ?? 0.8f;

            request.Cancel();
            TempestPlugin.Pending.Remove(target.CSteamID.m_SteamID);

            UnturnedPlayer teleportingPlayer;
            UnturnedPlayer destinationPlayer;
            string teleporterNotice;
            string destinationNotice;

            switch (request.Type)
            {
                case TempestRequestType.SummonTarget:
                    teleportingPlayer = target;
                    destinationPlayer = requester;
                    teleporterNotice = $"Teleporting to {requester.DisplayName} in {delay}s. Don't move.";
                    destinationNotice = $"Accepting summon request for {target.DisplayName}...";
                    break;
                default:
                    teleportingPlayer = requester;
                    destinationPlayer = target;
                    teleporterNotice = $"Teleporting to {target.DisplayName} in {delay}s. Don't move.";
                    destinationNotice = $"Accepting teleport request from {requester.DisplayName}...";
                    break;
            }

            TempestPlugin.Msg(teleportingPlayer, teleporterNotice);
            TempestPlugin.Msg(destinationPlayer, destinationNotice);

            TaskDispatcher.QueueOnMainThread(async () =>
            {
                ulong teleporterId = teleportingPlayer.CSteamID.m_SteamID;
                ulong destinationId = destinationPlayer.CSteamID.m_SteamID;
                Vector3 startPos = teleportingPlayer.Position;

                int waited = 0;
                while (waited < delay)
                {
                    await Task.Delay(1000);
                    waited++;

                    var teleporterLive = UnturnedPlayer.FromCSteamID(new CSteamID(teleporterId));
                    var destinationLive = UnturnedPlayer.FromCSteamID(new CSteamID(destinationId));

                    if (teleporterLive == null)
                    {
                        TempestPlugin.Err(destinationLive, "Teleport cancelled: teleporter went offline.");
                        return;
                    }

                    if (destinationLive == null)
                    {
                        TempestPlugin.Err(teleporterLive, "Teleport cancelled: destination went offline.");
                        return;
                    }

                    if (cancelOnMove && Vector3.Distance(startPos, teleporterLive.Position) > cancelDistance)
                    {
                        TempestPlugin.Err(teleporterLive, "Teleport canceled: you moved.");
                        TempestPlugin.Msg(destinationLive, $"Teleport canceled: {teleporterLive.DisplayName} moved.");
                        return;
                    }
                }

                try
                {
                    var teleporterLive = UnturnedPlayer.FromCSteamID(new CSteamID(teleporterId));
                    var destinationLive = UnturnedPlayer.FromCSteamID(new CSteamID(destinationId));

                    if (teleporterLive == null || destinationLive == null)
                    {
                        if (teleporterLive != null)
                        {
                            TempestPlugin.Err(teleporterLive, "Teleport failed: destination offline.");
                        }
                        if (destinationLive != null)
                        {
                            TempestPlugin.Err(destinationLive, "Teleport failed: teleporter offline.");
                        }
                        return;
                    }

                    teleporterLive.Teleport(destinationLive.Position, destinationLive.Rotation);
                    TempestPlugin.Msg(teleporterLive, $"Teleported to {destinationLive.DisplayName}.");
                    TempestPlugin.Msg(destinationLive, $"{teleporterLive.DisplayName} teleported to you.");

                    var cooldownPlayer = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
                    if (cooldownPlayer != null)
                    {
                        TempestPlugin.StartCooldown(cooldownPlayer);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    var teleporterLive = UnturnedPlayer.FromCSteamID(new CSteamID(teleporterId));
                    TempestPlugin.Err(teleporterLive, "Teleport failed.");
                }
            });
        }
    }

    public class CommandTPDeny : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.deny" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpdeny";
        public string Help => "Deny the pending Tempest request";
        public string Syntax => "/tpdeny";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var target = (UnturnedPlayer)caller;
            if (!TempestPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var request) || !request.IsActive)
            {
                TempestPlugin.Err(target, "No pending Tempest request.");
                return;
            }

            request.Cancel();
            TempestPlugin.Pending.Remove(target.CSteamID.m_SteamID);

            var requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester != null)
            {
                string label = TempestPlugin.GetRequestLabel(request.Type);
                TempestPlugin.Err(requester, $"Your {label} involving {target.DisplayName} was denied.");
            }

            string targetLabel = TempestPlugin.GetRequestLabel(request.Type);
            TempestPlugin.Msg(target, $"{char.ToUpperInvariant(targetLabel[0]) + targetLabel.Substring(1)} denied.");
        }
    }

    public class CommandTempest : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.cmds" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tempest";
        public string Help => "ARC Tempest System command list";
        public string Syntax => "/tempest cmds";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var player = (UnturnedPlayer)caller;

            if (command.Length == 1 && string.Equals(command[0], "cmds", StringComparison.OrdinalIgnoreCase))
            {
                TempestPlugin.Msg(player, "ARC Tempest System Commands:");
                TempestPlugin.Msg(player, "/tpa <player> - request to teleport to another player.");
                TempestPlugin.Msg(player, "/tphere <player> - request another player to teleport to you.");
                TempestPlugin.Msg(player, "/tpaccept - accept the latest Tempest request targeting you.");
                TempestPlugin.Msg(player, "/tpdeny - deny the latest Tempest request targeting you.");
                TempestPlugin.Msg(player, "/tpcancel - cancel your outgoing Tempest request.");
                TempestPlugin.Msg(player, "/tmap - receive a link to the live Tempest tactical map.");
                TempestPlugin.Msg(player, "/tempest cmds - show this help menu.");
            }
            else
            {
                TempestPlugin.Msg(player, "Usage: /tempest cmds");
            }
        }
    }

    public class CommandTempestMap : IRocketCommand
    {
        private static readonly List<string> RequiredPermissions = new List<string> { "tempest.map" };

        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tmap";
        public string Help => "Displays a link to the Tempest tactical map";
        public string Syntax => "/tmap";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => TempestPlugin.ResolvePermissions(RequiredPermissions);

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
            {
                return;
            }

            if (!TempestPlugin.MapBridgeEnabled)
            {
                TempestPlugin.Err(player, "The tactical map is currently offline. Please contact a server administrator.");
                return;
            }

            string mapUrl = TempestPlugin.MapShareUrl;
            if (string.IsNullOrWhiteSpace(mapUrl))
            {
                TempestPlugin.Err(player, "The tactical map URL is not configured. Please contact a server administrator.");
                return;
            }

            TempestPlugin.Msg(player, $"Tempest tactical map: {mapUrl}");
        }
    }
}
