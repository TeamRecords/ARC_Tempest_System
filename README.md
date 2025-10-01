# Unturned RocketMod TPA Plugin

A lightweight **Teleport Request (TPA) plugin** for Unturned servers running RocketMod.  
It adds familiar multiplayer commands for requesting, accepting, denying, and canceling teleports between players.

## ✨ Features
- `/tpa <player>` – request to teleport to another player  
- `/tpaccept` – accept a pending request  
- `/tpdeny` – deny a request  
- `/tpcancel` – cancel your outgoing request  
- Configurable request timeout, teleport delay, cooldowns, and “cancel-on-move” settings.  
- Minimal dependencies (just RocketMod and Unturned).

---

## 📦 Download & Build

1. **Download the Source**  
   Place the `TPAPlugin.cs` and `TPAPlugin.csproj` files in a folder named `TPAPlugin`.

2. **Set Up References**  
   Open `TPAPlugin.csproj` in Visual Studio (2022 recommended).  
   Ensure the `HintPath` references point to your Unturned server’s libraries:  
   - `Rocket.API.dll`, `Rocket.Core.dll`, `Rocket.Unturned.dll`  
   - `Assembly-CSharp.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`

3. **Compile**  
   - Select **Release** build.  
   - Target **.NET Framework 3.5** (compatible with Unturned 3.x + RocketMod).  
   - Build → Output: `bin/Release/TPAPlugin.dll`

4. **Deploy to Server**  
   - Upload `TPAPlugin.dll` to:  
     ```
     Rocket/Plugins/TPAPlugin/
     ```
   - Start or restart your Unturned server.  
   - A config file (`TPAPlugin.configuration.xml`) will auto-generate, or you can use the template provided below.

---

## ⚙️ Configuration (`TPAPlugin.configuration.xml`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Configuration>
  <RequestTimeoutSeconds>60</RequestTimeoutSeconds>
  <TeleportDelaySeconds>3</TeleportDelaySeconds>
  <CooldownSeconds>15</CooldownSeconds>
  <CancelOnMove>true</CancelOnMove>
  <CancelOnMoveDistance>0.8</CancelOnMoveDistance>
</Configuration>
