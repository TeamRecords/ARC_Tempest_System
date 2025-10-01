// ---------- FILE: TPAPlugin.cs ----------
// RocketMod TPA plugin (2025) - /tpa, /tpaccept, /tpdeny, /tpcancel
// Build this file into TPAPlugin.dll using the .csproj below.
// Place the compiled DLL at: Rocket/Plugins/TPAPlugin/TPAPlugin.dll
// On first run, the config XML (below) will auto-generate. You can also pre-create it.

using Rocket.API;
using Rocket.Core;
using Rocket.Core.Logging;
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
        internal static TPAPlugin Instance;
        internal static readonly Dictionary<ulong, TPARequest> Pending = new Dictionary<ulong, TPARequest>();
        internal static readonly Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();

        internal static TPAConfig Config => Instance?.Configuration.Instance;
        internal static bool PermissionsEnabled => Config?.Use_Permissions ?? false;

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("[TPA] Loaded: /tpa, /tpaccept, /tpdeny, /tpcancel");
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
        }
        protected override void Unload()
        {
            Instance = null;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            foreach (var request in Pending.Values)
            {
                request.Cancel();
            }
            Pending.Clear();
            Cooldowns.Clear();
        }
        private void OnPlayerDisconnected(UnturnedPlayer p)
        {
            var affected = Pending.Where(kv => kv.Key == p.CSteamID.m_SteamID || kv.Value.RequesterId == p.CSteamID.m_SteamID)
                                  .Select(kv => kv.Key).ToList();
            foreach (var key in affected)
            {
                if (Pending.TryGetValue(key, out var req))
                {
                    req.Cancel();
                    Pending.Remove(key);
                }
            }
        }
            int cd = Config?.CooldownSeconds ?? 15; Cooldowns[r.CSteamID.m_SteamID] = DateTime.UtcNow.AddSeconds(cd);
        }
        internal static bool TryFindPlayer(string query, ulong callerId, out UnturnedPlayer player, out string failureMessage)
        {
            failureMessage = null;
            player = null;

            query = query?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                failureMessage = "You must specify a player name.";
                return false;
            }

            var matches = Provider.clients.Select(c => UnturnedPlayer.FromCSteamID(c.playerID.steamID))
                .Where(x => x != null && (x.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || x.CharacterName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (matches.Count == 0)
            {
                failureMessage = $"No players matched '{query}'.";
                return false;
            }

            if (matches.Count > 1)
            {
                var preview = matches.Take(5).Select(m => m.DisplayName).ToList();
                failureMessage = $"Multiple matches: {string.Join(", ", preview)}" + (matches.Count > preview.Count ? "..." : string.Empty);
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
        public ulong RequesterId;
        public string RequesterName;
        public Vector3 RequesterPos;
        public DateTime CreatedUtc;
        public double TimeoutSeconds;
        public Timer ExpiryTimer;
        public bool IsActive = true;
        public TPARequest(ulong requesterId, string requesterName, Vector3 requesterPos, double timeoutSeconds, System.Action onExpire)
        {
            RequesterId = requesterId; RequesterName = requesterName; RequesterPos = requesterPos; CreatedUtc = DateTime.UtcNow;
            TimeoutSeconds = timeoutSeconds;
            ExpiryTimer = new Timer(timeoutSeconds * 1000) { AutoReset = false };
            ExpiryTimer.Elapsed += (s, e) => { IsActive = false; onExpire?.Invoke(); };
            ExpiryTimer.Start();
        }
        public void Cancel()
        {
            IsActive = false; if (ExpiryTimer != null) { ExpiryTimer.Stop(); ExpiryTimer.Dispose(); ExpiryTimer = null; }
        }
        public int GetSecondsRemaining()
        {
            if (!IsActive) return 0;
            double remaining = TimeoutSeconds - (DateTime.UtcNow - CreatedUtc).TotalSeconds;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
        }
    }
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.request" };
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var caller = (UnturnedPlayer)ic;
            if (cmd.Length < 1)
            {
                var status = TPAPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == caller.CSteamID.m_SteamID);
                if (status.Key != 0 && status.Value.IsActive)
                {
                    var targetPlayer = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(status.Key));
                    var remaining = status.Value.GetSecondsRemaining();
                    string targetName = targetPlayer != null ? targetPlayer.DisplayName : "(offline player)";
                    TPAPlugin.Msg(caller, $"Pending request to {targetName}. {remaining}s remaining. Use /tpcancel to cancel.");
                }
                else
                {
                    TPAPlugin.Msg(caller, "Usage: /tpa <player>. Use /tpcancel to cancel your outgoing request.");
                    if (TPAPlugin.IsOnCooldown(caller, out var remainingCd))
                    {
                        TPAPlugin.Msg(caller, $"Cooldown remaining: {remainingCd}s");
                    }
                }
                return;
            }

            if (TPAPlugin.IsOnCooldown(caller, out int remain)) { TPAPlugin.Err(caller, $"Cooldown: {remain}s"); return; }

            if (!TPAPlugin.TryFindPlayer(string.Join(" ", cmd), caller.CSteamID.m_SteamID, out var target, out var failure))
            {
                TPAPlugin.Err(caller, failure);
                return;
            }

            if (TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var existing))
            {
                var previousRequester = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(existing.RequesterId));
                existing.Cancel();
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);

                if (previousRequester != null && previousRequester.CSteamID.m_SteamID != caller.CSteamID.m_SteamID)
                {
                    TPAPlugin.Err(previousRequester, $"Your TPA request to {target.DisplayName} was replaced by another player.");
                }
            }

            var cfg = TPAPlugin.Config; ushort timeout = cfg?.RequestTimeoutSeconds ?? (ushort)60;
            var req = new TPARequest(caller.CSteamID.m_SteamID, caller.DisplayName, caller.Position, timeout, () =>
            {
                TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
                TPAPlugin.Err(target, "TPA request expired.");
                TPAPlugin.Err(caller, "Your TPA request expired.");
            });
            TPAPlugin.Pending[target.CSteamID.m_SteamID] = req;
            TPAPlugin.Msg(caller, $"Sent TPA to {target.DisplayName}. Expires in {timeout}s. Use /tpcancel to cancel.");
            TPAPlugin.Msg(target, $"{caller.DisplayName} wants to teleport to you. Use /tpaccept or /tpdeny.");
            TPAPlugin.Msg(target, $"Request will auto-expire in {req.GetSecondsRemaining()}s.");
        }
    }
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.cancel" };
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var caller = (UnturnedPlayer)ic;
            var pair = TPAPlugin.Pending.FirstOrDefault(kv => kv.Value.RequesterId == caller.CSteamID.m_SteamID);
            if (pair.Key == 0) { TPAPlugin.Err(caller, "You have no outgoing TPA request."); return; }
        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.accept" };
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var target = (UnturnedPlayer)ic; // teleport destination
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var req) || !req.IsActive) { TPAPlugin.Err(target, "No pending TPA."); return; }
            var requester = UnturnedPlayer.FromCSteamID(new Steamworks.CSteamID(req.RequesterId));
            if (requester == null) { TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID); TPAPlugin.Err(target, "Requester offline."); return; }
            var cfg = TPAPlugin.Config; int delay = cfg?.TeleportDelaySeconds ?? 3;
            bool cancelOnMove = cfg?.CancelOnMove ?? true; float cancelDist = cfg?.CancelOnMoveDistance ?? 0.8f;
    }

        private static readonly List<string> RequiredPermissions = new List<string> { "tpa.deny" };
        public List<string> Permissions => TPAPlugin.PermissionsEnabled ? RequiredPermissions : new List<string>();
        public void Execute(IRocketPlayer ic, string[] cmd)
        {
            var target = (UnturnedPlayer)ic;
            if (!TPAPlugin.Pending.TryGetValue(target.CSteamID.m_SteamID, out var req) || !req.IsActive) { TPAPlugin.Err(target, "No pending TPA."); return; }
            req.Cancel(); TPAPlugin.Pending.Remove(target.CSteamID.m_SteamID);
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



