using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;
using VoiceOverride.Models;
using VoiceOverride.Configs;

namespace VoiceOverride.Services;

public class VoiceMuteService(BaseConfig config, IStringLocalizer localizer, TemporaryPermissionService tempPermissionService)
{
    private readonly BaseConfig _config = config;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly TemporaryPermissionService _tempPermissionService = tempPermissionService;
    private readonly Dictionary<CCSPlayerController, bool> _mutedStates = [];
    private readonly Dictionary<CCSPlayerController, TeamFilter> _activeTeamFilters = [];
    private readonly Dictionary<CCSPlayerController, DateTime> _muteStartTimes = [];
    private readonly Dictionary<CCSPlayerController, Timer> _activeTimers = [];
    private bool _isGlobalMuted = false;
    private bool _isThresholdMuted = false;

    private bool _isVoiceMuted = false;
    private Timer? _voiceGraceTimer = null;
    private readonly HashSet<int> _activeSpeakers = [];

    public bool IsGlobalMuted => _isGlobalMuted;
    public bool IsThresholdMuteActive => _isThresholdMuted;
    public bool IsVoiceMuted => _isVoiceMuted;

    public bool AnyAdminMuteActive()
    {
        return _mutedStates.ContainsValue(true);
    }

    public bool IsMuted(CCSPlayerController admin)
    {
        return _mutedStates.GetValueOrDefault(admin, false);
    }

    public void ToggleMute(CCSPlayerController admin, TeamFilter teamFilter = TeamFilter.All)
    {
        if (IsMuted(admin))
        {
            UnmuteAll(admin);
        }
        else
        {
            MuteAll(admin, teamFilter);
        }
    }

    public void MuteAll(CCSPlayerController admin, TeamFilter teamFilter = TeamFilter.All)
    {
        if (admin == null || !admin.IsValid)
            return;

        if (IsMuted(admin))
        {
            Logger.LogDebug("VoiceMute", $"Admin {admin.PlayerName} already has mute active");
            return;
        }

        var mutedCount = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot)
                continue;

            if (player == admin)
                continue;

            if (IsPrivilegedPlayer(player))
                continue;

            if (!ShouldMutePlayer(player, teamFilter))
                continue;

            try
            {
                if (admin.IsValid)
                {
                    admin.SetListenOverride(player, ListenOverride.Mute);
                    mutedCount++;
                    Logger.LogDebug("VoiceMute", $"Muted {player.PlayerName} for admin {admin.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("VoiceMute", $"Error muting {player.PlayerName}: {ex.Message}");
            }
        }

        _mutedStates[admin] = true;
        _activeTeamFilters[admin] = teamFilter;
        _muteStartTimes[admin] = DateTime.Now;

        Logger.LogInfo("VoiceMute", $"Admin {admin.PlayerName} muted {mutedCount} players");

        if (_config.Timer.Enabled && _config.Timer.DurationSeconds > 0)
        {
            StartTimer(admin);
        }
    }

    public void UnmuteAll(CCSPlayerController admin)
    {
        if (admin == null || !admin.IsValid)
        {
            if (admin != null)
            {
                CleanupPlayer(admin);
            }
            return;
        }

        if (!IsMuted(admin))
        {
            Logger.LogDebug("VoiceMute", $"Admin {admin.PlayerName} does not have mute active");
            return;
        }

        var teamFilter = _activeTeamFilters.GetValueOrDefault(admin, TeamFilter.All);
        var unmutedCount = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot)
                continue;

            if (player == admin)
                continue;

            if (HasAdminPermission(player))
                continue;

            if (!ShouldMutePlayer(player, teamFilter))
                continue;

            try
            {
                if (admin.IsValid)
                {
                    admin.SetListenOverride(player, ListenOverride.Default);
                    unmutedCount++;
                    Logger.LogDebug("VoiceMute", $"Unmuted {player.PlayerName} for admin {admin.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("VoiceMute", $"Error unmuting {player.PlayerName}: {ex.Message}");
            }
        }

        _mutedStates[admin] = false;
        _activeTeamFilters.Remove(admin);
        _muteStartTimes.Remove(admin);

        StopTimer(admin);

        Logger.LogInfo("VoiceMute", $"Admin {admin.PlayerName} unmuted {unmutedCount} players");
    }

    private void StartTimer(CCSPlayerController admin)
    {
        if (admin == null || !admin.IsValid)
            return;

        StopTimer(admin);

        var adminRef = admin;
        var timer = new Timer(_ =>
        {
            Server.NextFrame(() =>
            {
                if (adminRef != null && adminRef.IsValid && IsMuted(adminRef))
                {
                    UnmuteAll(adminRef);
                    try
                    {
                        if (adminRef.IsValid)
                        {
                            var message = $"{_localizer["common.prefix"]} {_localizer["message.auto_unmuted", _config.Timer.DurationSeconds]}";
                            adminRef.PrintToChat(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("VoiceMute", $"Error sending message to admin: {ex.Message}");
                    }
                }
            });
        }, null, TimeSpan.FromSeconds(_config.Timer.DurationSeconds), Timeout.InfiniteTimeSpan);

        _activeTimers[admin] = timer;
        Logger.LogDebug("VoiceMute", $"Started {_config.Timer.DurationSeconds}s timer for admin {admin.PlayerName}");
    }

    private void StopTimer(CCSPlayerController admin)
    {
        if (_activeTimers.TryGetValue(admin, out var timer))
        {
            timer?.Dispose();
            _activeTimers.Remove(admin);
            Logger.LogDebug("VoiceMute", $"Stopped timer for admin {admin.PlayerName}");
        }
    }

    public void SetGlobalMute(bool mute)
    {
        _isGlobalMuted = mute;
        if (!mute) _isThresholdMuted = false;
        var listenOverride = mute ? ListenOverride.Mute : ListenOverride.Default;
        int count = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot)
                continue;

            if (mute && HasAdminPermission(player))
                continue;

            foreach (var listener in Utilities.GetPlayers())
            {
                if (listener == null || !listener.IsValid || listener.IsBot)
                    continue;

                try
                {
                    listener.SetListenOverride(player, listenOverride);
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"Error setting global mute for {player.PlayerName} -> {listener.PlayerName}: {ex.Message}");
                }
            }
            count++;
        }
        Logger.LogInfo("VoiceMute", $"Global mute {(mute ? "enabled" : "disabled")}. Affected {count} speakers.");

        var key = mute ? "message.global_mute" : "message.global_unmute";
        Server.PrintToChatAll($"{_localizer["common.prefix"]} {_localizer[key]}");
    }

    public void EnableThresholdMute()
    {
        if (_isThresholdMuted) return;
        _isThresholdMuted = true;
        int count = 0;

        var players = Utilities.GetPlayers();
        foreach (var speaker in players)
        {
            if (speaker == null || !speaker.IsValid || speaker.IsBot)
                continue;

            if (IsPrivilegedPlayer(speaker))
                continue;

            foreach (var listener in players)
            {
                if (listener == null || !listener.IsValid || listener.IsBot)
                    continue;

                try
                {
                    listener.SetListenOverride(speaker, ListenOverride.Mute);
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"Error applying threshold mute for {speaker.PlayerName} -> {listener.PlayerName}: {ex.Message}");
                }
            }
            count++;
        }

        Logger.LogInfo("VoiceMute", $"Threshold mute enabled. Muted {count} non-admin speakers.");
        Server.PrintToChatAll($"{_localizer["common.prefix"]} {_localizer["message.threshold_mute_enabled", _config.Threshold.PlayerCount]}");
    }

    public void DisableThresholdMute()
    {
        if (!_isThresholdMuted) return;
        _isThresholdMuted = false;
        int count = 0;

        var players = Utilities.GetPlayers();
        foreach (var speaker in players)
        {
            if (speaker == null || !speaker.IsValid || speaker.IsBot)
                continue;

            if (IsPrivilegedPlayer(speaker))
                continue;

            foreach (var listener in players)
            {
                if (listener == null || !listener.IsValid || listener.IsBot)
                    continue;

                try
                {
                    listener.SetListenOverride(speaker, ListenOverride.Default);
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"Error restoring threshold mute for {speaker.PlayerName} -> {listener.PlayerName}: {ex.Message}");
                }
            }
            count++;
        }

        Logger.LogInfo("VoiceMute", $"Threshold mute disabled. Restored {count} non-admin speakers.");
        Server.PrintToChatAll($"{_localizer["common.prefix"]} {_localizer["message.threshold_mute_disabled", _config.Threshold.PlayerCount]}");
    }

    public void ApplyThresholdMuteToNewPlayer(CCSPlayerController newPlayer)
    {
        if (!_isThresholdMuted) return;
        if (newPlayer == null || !newPlayer.IsValid || newPlayer.IsBot) return;
        if (IsPrivilegedPlayer(newPlayer)) return;

        foreach (var listener in Utilities.GetPlayers())
        {
            if (listener == null || !listener.IsValid || listener.IsBot)
                continue;

            try
            {
                listener.SetListenOverride(newPlayer, ListenOverride.Mute);
            }
            catch (Exception ex)
            {
                Logger.LogError("VoiceMute", $"Error applying threshold mute to new player {newPlayer.PlayerName} -> {listener.PlayerName}: {ex.Message}");
            }
        }

        Logger.LogDebug("VoiceMute", $"Applied threshold mute to newly connected player {newPlayer.PlayerName}");
    }

    public bool HasAdminPermission(CCSPlayerController player)
    {
        var permissions = _config.Commands.AdminPermissions;
        if (permissions == null || permissions.Count == 0)
            return false;

        return permissions.Any(flag => AdminManager.PlayerHasPermissions(player, flag));
    }

    private bool IsPrivilegedPlayer(CCSPlayerController player)
    {
        return HasAdminPermission(player) || _tempPermissionService.HasTemporaryPermission(player);
    }

    private static bool ShouldMutePlayer(CCSPlayerController player, TeamFilter teamFilter)
    {
        if (teamFilter == TeamFilter.All)
            return true;

        var teamNum = player.TeamNum;

        if (teamNum < 2 || teamNum > 3)
            return false;

        if (teamFilter == TeamFilter.CT && teamNum == 3)
            return true;

        if (teamFilter == TeamFilter.T && teamNum == 2)
            return true;

        return false;
    }

    public void CleanupPlayer(CCSPlayerController player)
    {
        if (player == null) return;

        if (_mutedStates.ContainsKey(player))
        {
            UnmuteAll(player);
        }

        _mutedStates.Remove(player);
        _activeTeamFilters.Remove(player);
        _muteStartTimes.Remove(player);
        StopTimer(player);
    }

    public void OnAdminStartedSpeaking(int playerSlot)
    {
        _activeSpeakers.Add(playerSlot);
        if (_voiceGraceTimer != null)
        {
            _voiceGraceTimer.Dispose();
            _voiceGraceTimer = null;
            Logger.LogDebug("VoiceMute", $"Grace timer cancelled — privileged player (slot {playerSlot}) resumed speaking");
        }

        if (_isVoiceMuted)
            return;

        _isVoiceMuted = true;
        int count = 0;

        foreach (var speaker in Utilities.GetPlayers())
        {
            if (speaker == null || !speaker.IsValid || speaker.IsBot)
                continue;

            if (IsPrivilegedPlayer(speaker))
                continue;

            foreach (var listener in Utilities.GetPlayers())
            {
                if (listener == null || !listener.IsValid || listener.IsBot)
                    continue;

                try
                {
                    listener.SetListenOverride(speaker, ListenOverride.Mute);
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"[VoiceMute] Error applying voice mute for {speaker.PlayerName}: {ex.Message}");
                }
            }
            count++;
        }

        Logger.LogInfo("VoiceMute", $"Voice-activated mute enabled. Muted {count} non-admin speakers.");
        Server.PrintToChatAll($"{_localizer["common.prefix"]} {_localizer["message.voice_mute_started"]}");
    }

    public void OnAdminStoppedSpeaking(int playerSlot)
    {
        _activeSpeakers.Remove(playerSlot);

        if (_activeSpeakers.Count > 0)
            return;

        if (!_isVoiceMuted)
            return;

        if (_config.Timer.Enabled && _config.Timer.DurationSeconds > 0)
        {
            _voiceGraceTimer?.Dispose();
            _voiceGraceTimer = new Timer(_ =>
            {
                Server.NextFrame(() =>
                {
                    if (_activeSpeakers.Count == 0)
                    {
                        DisableVoiceMute();
                    }
                    _voiceGraceTimer = null;
                });
            }, null, TimeSpan.FromSeconds(_config.Timer.DurationSeconds), Timeout.InfiniteTimeSpan);

            Logger.LogDebug("VoiceMute", $"Voice grace timer started ({_config.Timer.DurationSeconds}s)");
        }
        else
        {
            DisableVoiceMute();
        }
    }

    public void DisableVoiceMute()
    {
        if (!_isVoiceMuted)
            return;

        _isVoiceMuted = false;
        _activeSpeakers.Clear();

        _voiceGraceTimer?.Dispose();
        _voiceGraceTimer = null;

        int count = 0;

        foreach (var speaker in Utilities.GetPlayers())
        {
            if (speaker == null || !speaker.IsValid || speaker.IsBot)
                continue;

            if (IsPrivilegedPlayer(speaker))
                continue;

            foreach (var listener in Utilities.GetPlayers())
            {
                if (listener == null || !listener.IsValid || listener.IsBot)
                    continue;

                try
                {
                    listener.SetListenOverride(speaker, ListenOverride.Default);
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"[VoiceMute] Error removing voice mute for {speaker.PlayerName}: {ex.Message}");
                }
            }
            count++;
        }

        Logger.LogInfo("VoiceMute", $"Voice-activated mute disabled. Restored {count} non-admin speakers.");
        Server.PrintToChatAll($"{_localizer["common.prefix"]} {_localizer["message.voice_mute_ended"]}");
    }

    public void Cleanup()
    {
        foreach (var timer in _activeTimers.Values)
        {
            timer?.Dispose();
        }
        _activeTimers.Clear();
        _mutedStates.Clear();
        _activeTeamFilters.Clear();
        _muteStartTimes.Clear();

        _voiceGraceTimer?.Dispose();
        _voiceGraceTimer = null;
        _activeSpeakers.Clear();
    }
}