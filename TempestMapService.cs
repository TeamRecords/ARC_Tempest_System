using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Timers;
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
        private DbProviderFactory _factory;
        private bool _isRunning;

        internal TempestMapService(TempestConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _timer = new Timer(GetRefreshIntervalMilliseconds())
            {
                AutoReset = false
            };
            _timer.Elapsed += OnTimerElapsed;
        }

        public void Start()
        {
            if (string.IsNullOrWhiteSpace(_config.Map_Connection_String))
            {
                Logger.LogWarning("[TempestMap] Map bridge skipped: Map_Connection_String is empty.");
                return;
            }

            string providerName = string.IsNullOrWhiteSpace(_config.Map_Provider_Invariant_Name)
                ? "MySql.Data.MySqlClient"
                : _config.Map_Provider_Invariant_Name;

            try
            {
                _factory = DbProviderFactories.GetFactory(providerName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Unable to resolve the database provider '{providerName}': {ex}");
                return;
            }

            try
            {
                EnsureSchema();
                lock (_syncRoot)
                {
                    _isRunning = true;
                }

                _timer.Start();
                Logger.Log("[TempestMap] Tactical map bridge started.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to start tactical map bridge: {ex}");
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _isRunning = false;
            }

            _timer.Stop();
            _timer.Dispose();
        }

        internal void TrackPlayer(UnturnedPlayer player)
        {
            if (player == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (!_isRunning)
                {
                    return;
                }
            }

            try
            {
                SteamPlayer steamPlayer = PlayerTool.getSteamPlayer(player.CSteamID);
                if (steamPlayer == null)
                {
                    return;
                }

                using (var connection = CreateConnection())
                {
                    connection.Open();
                    UpsertPlayers(connection, new[] { steamPlayer });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to track player {player.CSteamID.m_SteamID}: {ex}");
            }
        }

        internal void MarkPlayerOffline(ulong steamId)
        {
            if (steamId == 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (!_isRunning)
                {
                    return;
                }
            }

            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "UPDATE tempest_player_positions SET is_online = 0, last_seen_utc = UTC_TIMESTAMP() WHERE steam_id = @steamId;";
                        var steamParam = command.CreateParameter();
                        steamParam.ParameterName = "@steamId";
                        steamParam.Value = steamId;
                        command.Parameters.Add(steamParam);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to mark player {steamId} offline: {ex}");
            }
        }

        private void EnsureSchema()
        {
            using (var connection = CreateConnection())
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS tempest_map_metadata (
    id TINYINT UNSIGNED NOT NULL PRIMARY KEY DEFAULT 1,
    map_name VARCHAR(120) NOT NULL,
    level_size INT NOT NULL,
    last_synced_utc DATETIME NOT NULL,
    UNIQUE KEY uq_tempest_map_metadata_id (id)
);";
                    command.ExecuteNonQuery();
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS tempest_player_positions (
    steam_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    character_name VARCHAR(120) NOT NULL,
    group_name VARCHAR(120) NULL,
    position_x DOUBLE NOT NULL,
    position_y DOUBLE NOT NULL,
    position_z DOUBLE NOT NULL,
    rotation_y DOUBLE NOT NULL,
    is_online BIT NOT NULL DEFAULT 1,
    last_seen_utc DATETIME NOT NULL,
    INDEX idx_tempest_player_positions_last_seen (last_seen_utc)
);";
                    command.ExecuteNonQuery();
                }
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (_syncRoot)
            {
                if (!_isRunning)
                {
                    return;
                }
            }

            try
            {
                CaptureSnapshot();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Failed to capture snapshot: {ex}");
            }
            finally
            {
                lock (_syncRoot)
                {
                    if (_isRunning)
                    {
                        _timer.Interval = GetRefreshIntervalMilliseconds();
                        _timer.Start();
                    }
                }
            }
        }

        private void CaptureSnapshot()
        {
            var clients = Provider.clients ?? new List<SteamPlayer>();

            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    UpsertMetadata(connection);

                    if (clients.Count > 0)
                    {
                        UpsertPlayers(connection, clients);
                    }

                    PurgeStalePlayers(connection);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TempestMap] Snapshot failure: {ex}");
            }
        }

        private void UpsertMetadata(IDbConnection connection)
        {
            string mapName = Level.info != null ? Level.info.name : Level.levelName;
            int size = Level.size;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO tempest_map_metadata (id, map_name, level_size, last_synced_utc)
VALUES (1, @mapName, @levelSize, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE
    map_name = VALUES(map_name),
    level_size = VALUES(level_size),
    last_synced_utc = VALUES(last_synced_utc);";

                var nameParam = command.CreateParameter();
                nameParam.ParameterName = "@mapName";
                nameParam.Value = string.IsNullOrWhiteSpace(mapName) ? "Unknown" : mapName;
                command.Parameters.Add(nameParam);

                var sizeParam = command.CreateParameter();
                sizeParam.ParameterName = "@levelSize";
                sizeParam.Value = size;
                command.Parameters.Add(sizeParam);

                command.ExecuteNonQuery();
            }
        }

        private void UpsertPlayers(IDbConnection connection, IEnumerable<SteamPlayer> players)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO tempest_player_positions
    (steam_id, character_name, group_name, position_x, position_y, position_z, rotation_y, is_online, last_seen_utc)
VALUES (@steamId, @characterName, @groupName, @positionX, @positionY, @positionZ, @rotationY, 1, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE
    character_name = VALUES(character_name),
    group_name = VALUES(group_name),
    position_x = VALUES(position_x),
    position_y = VALUES(position_y),
    position_z = VALUES(position_z),
    rotation_y = VALUES(rotation_y),
    is_online = 1,
    last_seen_utc = VALUES(last_seen_utc);";

                var steamIdParam = command.CreateParameter();
                steamIdParam.ParameterName = "@steamId";
                command.Parameters.Add(steamIdParam);

                var nameParam = command.CreateParameter();
                nameParam.ParameterName = "@characterName";
                command.Parameters.Add(nameParam);

                var groupParam = command.CreateParameter();
                groupParam.ParameterName = "@groupName";
                command.Parameters.Add(groupParam);

                var xParam = command.CreateParameter();
                xParam.ParameterName = "@positionX";
                command.Parameters.Add(xParam);

                var yParam = command.CreateParameter();
                yParam.ParameterName = "@positionY";
                command.Parameters.Add(yParam);

                var zParam = command.CreateParameter();
                zParam.ParameterName = "@positionZ";
                command.Parameters.Add(zParam);

                var rotationParam = command.CreateParameter();
                rotationParam.ParameterName = "@rotationY";
                command.Parameters.Add(rotationParam);

                foreach (var steamPlayer in players.Where(p => p != null && p.player != null))
                {
                    Vector3 position = steamPlayer.player.transform.position;
                    Vector3 rotation = steamPlayer.player.transform.rotation.eulerAngles;
                    ulong steamId = steamPlayer.playerID.steamID.m_SteamID;
                    string characterName = !string.IsNullOrWhiteSpace(steamPlayer.playerID.characterName)
                        ? steamPlayer.playerID.characterName
                        : steamPlayer.playerID.nickName;
                    string groupName = steamPlayer.playerID.groupName;

                    steamIdParam.Value = steamId;
                    nameParam.Value = string.IsNullOrWhiteSpace(characterName) ? "Unknown Survivor" : characterName;
                    groupParam.Value = string.IsNullOrWhiteSpace(groupName) ? (object)DBNull.Value : groupName;
                    xParam.Value = Math.Round(position.x, 3, MidpointRounding.AwayFromZero);
                    yParam.Value = Math.Round(position.y, 3, MidpointRounding.AwayFromZero);
                    zParam.Value = Math.Round(position.z, 3, MidpointRounding.AwayFromZero);
                    rotationParam.Value = Math.Round(rotation.y, 3, MidpointRounding.AwayFromZero);

                    command.ExecuteNonQuery();
                }
            }
        }

        private void PurgeStalePlayers(IDbConnection connection)
        {
            int staleMinutes = Math.Max(1, _config.Map_Player_Stale_Minutes);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"UPDATE tempest_player_positions SET is_online = 0 WHERE is_online = 1 AND last_seen_utc < DATE_SUB(UTC_TIMESTAMP(), INTERVAL {staleMinutes} MINUTE);";
                command.ExecuteNonQuery();
            }
        }

        private IDbConnection CreateConnection()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException("Database provider factory has not been initialized.");
            }

            var connection = _factory.CreateConnection();
            if (connection == null)
            {
                throw new InvalidOperationException("Failed to create a database connection using the configured provider.");
            }

            connection.ConnectionString = _config.Map_Connection_String;
            return connection;
        }

        private double GetRefreshIntervalMilliseconds()
        {
            return Math.Max(1, _config.Map_Refresh_Interval_Seconds) * 1000d;
        }
    }
}
