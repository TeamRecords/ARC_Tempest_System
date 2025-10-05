using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web.Script.Serialization;
using Rocket.Core.Logging;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace ARC_TPA_Commands
{
    internal sealed class TempestMapService : IDisposable
    {
        private readonly TempestConfig _config;
        private readonly Timer _timer;
        private readonly object _syncRoot = new object();
        private bool _isRunning;
        private bool _isSending;
        private HttpClient _httpClient;
        private Uri _endpoint;
        private readonly JavaScriptSerializer _serializer;

        internal TempestMapService(TempestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _timer = new Timer(GetRefreshIntervalMilliseconds())
            {
                AutoReset = false
            };
            _timer.Elapsed += OnTimerElapsed;
            _serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 64
            };
        }

        public void Start()
        {
            string endpoint = _config.Map_Live_Api_Url;
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Logger.LogWarning("[TempestMap] Live sync skipped: Map_Live_Api_Url is empty.");
                return;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsedEndpoint))
            {
                Logger.LogError($"[TempestMap] Live sync skipped: Map_Live_Api_Url '{endpoint}' is not a valid absolute URI.");
                return;
            }

            try
            {
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(10, _config.Map_Refresh_Interval_Seconds + 5))
                };
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ARC-Tempest-LiveSync/1.0");
                _endpoint = parsedEndpoint;

                lock (_syncRoot)
                {
                    _isRunning = true;
                    _isSending = false;
                }

                _timer.Start();
                Task.Run(() => SendSnapshotIfNeeded());
                Logger.Log("[TempestMap] Tactical map live sync started.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to start tactical map live sync: {ex}");
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _isRunning = false;
                _isSending = false;
            }

            _timer.Stop();
            _timer.Dispose();
            _httpClient?.Dispose();
        }

        internal void TrackPlayer(UnturnedPlayer player)
        {
            if (player == null)
            {
                return;
            }

            RequestSnapshot();
        }

        internal void MarkPlayerOffline(ulong steamId)
        {
            if (steamId == 0)
            {
                return;
            }

            RequestSnapshot();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            RequestSnapshot();
        }

        private void RequestSnapshot()
        {
            Task.Run(() => SendSnapshotIfNeeded());
        }

        private void SendSnapshotIfNeeded()
        {
            bool shouldSend;
            lock (_syncRoot)
            {
                shouldSend = _isRunning && !_isSending;
                if (shouldSend)
                {
                    _isSending = true;
                }
            }

            if (!shouldSend)
            {
                RescheduleTimer();
                return;
            }

            try
            {
                PushSnapshot();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to capture snapshot: {ex}");
            }
            finally
            {
                lock (_syncRoot)
                {
                    _isSending = false;
                }

                RescheduleTimer();
            }
        }

        private void RescheduleTimer()
        {
            lock (_syncRoot)
            {
                if (!_isRunning)
                {
                    return;
                }

                _timer.Stop();
                _timer.Interval = GetRefreshIntervalMilliseconds();
                _timer.Start();
            }
        }

        private void PushSnapshot()
        {
            if (_httpClient == null || _endpoint == null)
            {
                return;
            }

            var snapshot = BuildSnapshot();
            if (snapshot == null)
            {
                return;
            }

            string json = _serializer.Serialize(snapshot);
            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(_config.Map_Live_Api_Key))
                {
                    request.Headers.Add("X-Server-Key", _config.Map_Live_Api_Key);
                }

                var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    string body = string.Empty;
                    try
                    {
                        body = response.Content != null
                            ? response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                            : string.Empty;
                    }
                    catch
                    {
                        body = string.Empty;
                    }

                    Logger.LogError($"[TempestMap] Live sync rejected ({(int)response.StatusCode} {response.ReasonPhrase}). Body: {body}");
                }
            }
        }

        private object BuildSnapshot()
        {
            var clients = Provider.clients ?? new List<SteamPlayer>();
            var players = new List<object>(clients.Count);
            DateTime capturedAt = DateTime.UtcNow;

            foreach (var steamPlayer in clients.Where(p => p != null && p.player != null))
            {
                try
                {
                    Vector3 position = steamPlayer.player.transform.position;
                    Vector3 rotation = steamPlayer.player.transform.rotation.eulerAngles;
                    ulong steamId = steamPlayer.playerID.steamID.m_SteamID;
                    string characterName = !string.IsNullOrWhiteSpace(steamPlayer.playerID.characterName)
                        ? steamPlayer.playerID.characterName
                        : steamPlayer.playerID.nickName;
                    string groupName = steamPlayer.playerID.groupName;
                    byte health = steamPlayer.player.life != null ? steamPlayer.player.life.health : (byte)0;

                    players.Add(new
                    {
                        steamId = steamId.ToString(),
                        characterName = string.IsNullOrWhiteSpace(characterName) ? "Unknown Survivor" : characterName,
                        groupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                        position = new
                        {
                            x = Math.Round(position.x, 3, MidpointRounding.AwayFromZero),
                            y = Math.Round(position.y, 3, MidpointRounding.AwayFromZero),
                            z = Math.Round(position.z, 3, MidpointRounding.AwayFromZero)
                        },
                        rotationY = Math.Round(rotation.y, 3, MidpointRounding.AwayFromZero),
                        health = Math.Min(100, Math.Max(0, (int)health)),
                        isOnline = true,
                        lastSeenUtc = capturedAt.ToString("o")
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[TempestMap] Failed to serialize player telemetry: {ex}");
                }
            }

            string mapName = Level.info != null ? Level.info.name : Level.levelName;
            int levelSize = Level.size;

            return new
            {
                capturedAt = capturedAt.ToString("o"),
                map = new
                {
                    name = string.IsNullOrWhiteSpace(mapName) ? "Unknown" : mapName,
                    levelSize,
                    shareUrl = TempestPlugin.MapShareUrl
                },
                players
            };
        }

        private double GetRefreshIntervalMilliseconds()
        {
            return Math.Max(1, _config.Map_Refresh_Interval_Seconds) * 1000d;
        }
    }
}
