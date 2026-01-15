using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;
using VoiceOverride.Models;
using VoiceOverride.Configs;

namespace VoiceOverride.Services;

public class VoiceMuteService
{
    private readonly BaseConfig _config;
    private readonly IStringLocalizer _localizer;
    private readonly Dictionary<CCSPlayerController, bool> _mutedStates = new();
    private readonly Dictionary<CCSPlayerController, TeamFilter> _activeTeamFilters = new();
    private readonly Dictionary<CCSPlayerController, DateTime> _muteStartTimes = new();
    private readonly Dictionary<CCSPlayerController, Timer> _activeTimers = new();

    public VoiceMuteService(BaseConfig config, IStringLocalizer localizer)
    {
        _config = config;
        _localizer = localizer;
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

            if (HasAdminPermission(player))
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

        if (_config.UseTimer && _config.TimerDuration > 0)
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
            if (adminRef != null && adminRef.IsValid && IsMuted(adminRef))
            {
                UnmuteAll(adminRef);
                try
                {
                    if (adminRef.IsValid)
                    {
                        var message = $"{_localizer["common.prefix"]} {_localizer["message.auto_unmuted", _config.TimerDuration]}";
                        adminRef.PrintToChat(message);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("VoiceMute", $"Error sending message to admin: {ex.Message}");
                }
            }
        }, null, TimeSpan.FromSeconds(_config.TimerDuration), Timeout.InfiniteTimeSpan);

        _activeTimers[admin] = timer;
        Logger.LogDebug("VoiceMute", $"Started {_config.TimerDuration}s timer for admin {admin.PlayerName}");
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

    private bool HasAdminPermission(CCSPlayerController player)
    {
        if (string.IsNullOrEmpty(_config.AdminPermissionFlag))
            return false;

        return AdminManager.PlayerHasPermissions(player, _config.AdminPermissionFlag);
    }

    private bool ShouldMutePlayer(CCSPlayerController player, TeamFilter teamFilter)
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
    }
}