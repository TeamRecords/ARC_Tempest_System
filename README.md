# ARC Tempest System

The **ARC Tempest System** is a modern teleport-request suite for RocketMod servers running the 2025-era build of Unturned (tested with version 3.25.9.0). It replaces the classic one-way TPA workflow with a richer, player-friendly toolset that keeps permissions optional and teleports reliable.

## 💫 Commands
- `/tpa <player>` – request to teleport yourself to another survivor.
- `/tphere <player>` – request another survivor to teleport to you.
- `/tpaccept` – accept the latest Tempest request that targets you.
- `/tpdeny` – deny the latest Tempest request targeting you.
- `/tpcancel` – cancel your own outgoing Tempest request.
- `/tempest cmds` – view every Tempest command and its syntax in-game.

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
