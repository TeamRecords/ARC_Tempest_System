# ARC Tempest System

The **ARC Tempest System** is a modern teleport-request suite for RocketMod servers running the 2025-era build of Unturned (tested with version 3.25.9.0). It replaces the classic one-way TPA workflow with a richer, player-friendly toolset that keeps permissions optional and teleports reliable.

## 💫 Commands
- `/tpa <player>` – request to teleport yourself to another survivor.
- `/tphere <player>` – request another survivor to teleport to you.
- `/tpaccept` – accept the latest Tempest request that targets you.
- `/tpdeny` – deny the latest Tempest request targeting you.
- `/tpcancel` – cancel your own outgoing Tempest request.
- `/tempest cmds` – view every Tempest command and its syntax in-game.
- `/tmap` – receive a link to the live Tempest tactical map.

Additional behavior:
- Requests automatically expire after **30 seconds**.
- Accepted teleports honour the configurable delay and cancel immediately if the travelling player moves too far (when `CancelOnMove` is enabled).
- Cooldowns apply to the player who issued the request once the teleport completes.

## 🔧 Configuration (`TempestPlugin.configuration.xml`)
```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration>
  <RequestTimeoutSeconds>30</RequestTimeoutSeconds>
  <TeleportDelaySeconds>3</TeleportDelaySeconds>
  <CooldownSeconds>15</CooldownSeconds>
  <CancelOnMove>true</CancelOnMove>
  <CancelOnMoveDistance>0.8</CancelOnMoveDistance>
  <Use_Permissions>false</Use_Permissions>
  <Enable_Map_Bridge>false</Enable_Map_Bridge>
  <Map_Connection_String>Server=localhost;Port=3306;Database=tempest_map;Uid=tempest;Pwd=ChangeMe!;SslMode=Preferred;</Map_Connection_String>
  <Map_Provider_Invariant_Name>MySql.Data.MySqlClient</Map_Provider_Invariant_Name>
  <Map_Refresh_Interval_Seconds>5</Map_Refresh_Interval_Seconds>
  <Map_Player_Stale_Minutes>2</Map_Player_Stale_Minutes>
  <Map_Share_Url>https://tempest.arcfoundation.net/map</Map_Share_Url>
</Configuration>
```

Set `Use_Permissions` to `true` if you want RocketMod permission nodes; the system uses:
- `tempest.tpa`
- `tempest.tphere`
- `tempest.accept`
- `tempest.deny`
- `tempest.cancel`
- `tempest.cmds`

Leave `Use_Permissions` as `false` (default) to allow everyone to use the Tempest commands without extra setup.

### Tactical map bridge

- `Enable_Map_Bridge` – toggles the MySQL telemetry bridge.
- `Map_Connection_String` – standard MySQL connection string used by both schema migrations and telemetry inserts.
- `Map_Provider_Invariant_Name` – ADO.NET provider name (defaults to `MySql.Data.MySqlClient`).
- `Map_Refresh_Interval_Seconds` – how often (in seconds) the plugin polls player positions.
- `Map_Player_Stale_Minutes` – marks players offline when they have not been seen for this many minutes.
- `Map_Share_Url` – link returned to players when they run `/tmap`.

The bridge uses `DbProviderFactories` so you can swap in `MySqlConnector` or another provider by adjusting the invariant name and placing the correct assembly alongside your Rocket installation.

## 🗺️ Tempest Tactical Map Web UI

A brand-new Next.js 15 + Tailwind CSS 4 project lives in [`tempest-map/`](tempest-map/). It renders the Unturned map, polls the MySQL database populated by the plugin, and offers a polished control room experience with live player markers.

### Running locally

```bash
cd tempest-map
pnpm install # or npm/yarn
cp .env.example .env.local # create this file with the variables listed below
pnpm dev
```

Recommended environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `TEMPEST_MAP_DB_HOST` | `127.0.0.1` | MySQL host |
| `TEMPEST_MAP_DB_PORT` | `3306` | MySQL port |
| `TEMPEST_MAP_DB_USER` | `tempest` | Database username |
| `TEMPEST_MAP_DB_PASSWORD` | `ChangeMe!` | Database password |
| `TEMPEST_MAP_DB_NAME` | `tempest_map` | Database name |
| `TEMPEST_MAP_DB_POOL_SIZE` | `8` | Connection pool size |
| `NEXT_PUBLIC_TEMPEST_MAP_REFRESH_MS` | `5000` | Client-side polling interval |
| `TEMPEST_MAP_REFRESH_SECONDS` | `5` | Server-side fallback refresh interval displayed in UI |

Deploy the app with `pnpm run build` and `pnpm start`. When hosted at `https://tempest.arcfoundation.net` the `/map` route matches the default `/tmap` chat response.

## 🛠️ Building
1. Open `ARC_TPA_Commands.sln` (or the `ARC_TPA_Commands.csproj`) in Visual Studio 2022 or newer.
2. Ensure the RocketMod and Unturned assemblies in the project file point to your server installation (Unturned 3.25.9.0).
3. Build the project in **Release** mode for .NET Framework **4.8**. The compiled DLL will appear in `bin/Release/ARC_TPA_Commands.dll`.
4. Deploy the DLL to `Rocket/Plugins/ARC_Tempest/` (or another folder name of your choosing) on your server, then restart RocketMod to generate the configuration file.

## ✅ Highlights
- Unified handling for both classic teleport-to-player and summon-style requests.
- Automatic cleanup when either player disconnects or the timer expires.
- Informative feedback messages for every scenario (timeouts, denials, movement cancels, and more).
- Designed for future expansion under the ARC Tempest banner.

Feel free to extend the system with additional commands—Tempest is built to scale as new ideas arrive.
