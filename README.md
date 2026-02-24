# VoiceOverride CS2
Allows admins to mute all non-admin players for clear communication during competitive matches.

## 🚀 Installation
### Basic Installation
1. Install [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) and [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
2. Download the [latest release](https://github.com/srwiruwiru/VoiceOverride-CS2/releases/latest) from the releases page
3. Extract and upload to your game server: `csgo/addons/counterstrikesharp/plugins/VoiceOverride/`
4. Start server and configure the generated config file at `csgo/addons/counterstrikesharp/configs/plugins/VoiceOverride/`
---

## 📋 Configuration
### Commands
Controls which in-game commands are registered and who is allowed to use them.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `VoiceMuteCommands` | `string[]` | `["css_adminvoice", "css_admv"]` | Commands that toggle the voice mute for non-admin players. |
| `GrantPermissionCommands` | `string[]` | `["css_grantvoice", "css_gv"]` | Commands that grant/revoke temporary voice permission to a player. |
| `AdminPermissions` | `string[]` | `["@css/root", "@css/generic"]` | List of CounterStrikeSharp permission flags that identify an admin. A player is considered an admin if they hold **any** of the listed flags. |

> **Tip — `AdminPermissions`:** You can combine as many flags as needed. To restrict the plugin to root-only: `["@css/root"]`.

```json
"Commands": {
  "VoiceMuteCommands": ["css_adminvoice", "css_admv"],
  "GrantPermissionCommands": ["css_grantvoice", "css_gv"],
  "AdminPermissions": ["@css/root", "@css/generic"]
}
```
---

### Timer
When enabled, the voice mute automatically lifts after a fixed number of seconds instead of requiring a manual toggle.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `false` | Enables the auto-unmute timer. |
| `DurationSeconds` | `int` | `30` | Seconds until the mute is automatically lifted. |

```json
"Timer": {
  "Enabled": true,
  "DurationSeconds": 30
}
```
---

### Threshold
Automatically mutes all non-admin players once the server reaches a certain number of human players.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Enabled` | `bool` | `false` | Enables the player-count threshold mute. |
| `PlayerCount` | `int` | `10` | Minimum number of human players required to trigger the mute. The mute is lifted automatically when the player count drops back below this value. |

```json
"Threshold": {
  "Enabled": true,
  "PlayerCount": 10
}
```
---

### Behavior
Extra automatic muting behaviors tied to game events.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `MuteNonAdminsOnAdminVoice` | `bool` | `true` | Voice-activated mute — while any admin is speaking, all non-admin players are muted. The mute lifts once the admin stops talking (with an optional grace period controlled by **Timer**). |
| `MuteNonAdminsOnMatchEnd` | `bool` | `false` | Mutes all non-admin players as soon as the match-end panel appears. The mute is cleared at the start of the next map. |

```json
"Behavior": {
  "MuteNonAdminsOnAdminVoice": false,
  "MuteNonAdminsOnMatchEnd": false
}
```
---

## 🎮 Commands
All commands require the player to hold one of the flags defined in `Commands.AdminPermissions`, or a temporary permission granted via `GrantPermissionCommands`.

### Voice Mute (`VoiceMuteCommands`)
Default aliases: `css_adminvoice` / `css_admv`

```
css_adminvoice <@all|@ct|@t>
```

| Argument | Description |
|----------|-------------|
| `@all` | Mutes/unmutes players from **both** teams. |
| `@ct` | Mutes/unmutes **Counter-Terrorist** players only. |
| `@t` | Mutes/unmutes **Terrorist** players only. |

Running the command while a mute is already active unmutes all affected players.  
If **Timer** is enabled, the mute lifts automatically after `DurationSeconds`.

---

### Grant Temporary Permission (`GrantPermissionCommands`)
Default aliases: `css_grantvoice` / `css_gv`

```
css_grantvoice <name|#userid|slot>
```

Grants the target player temporary admin-level voice permission for the current map.  
Running the command again on the same player **revokes** the permission.  
All temporary permissions are cleared automatically on map change.

---