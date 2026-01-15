using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;

namespace VoiceOverride.Services;

public class TemporaryPermissionService
{
    private readonly IStringLocalizer _localizer;
    private readonly HashSet<ulong> _temporaryAdmins = new();

    public TemporaryPermissionService(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    public bool HasTemporaryPermission(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.SteamID == 0)
            return false;

        return _temporaryAdmins.Contains(player.SteamID);
    }

    public bool GrantTemporaryPermission(CCSPlayerController target)
    {
        if (target == null || !target.IsValid || target.SteamID == 0)
            return false;

        if (_temporaryAdmins.Contains(target.SteamID))
        {
            Logger.LogDebug("TempPermission", $"Player {target.PlayerName} already has temporary permission");
            return false;
        }

        _temporaryAdmins.Add(target.SteamID);
        Logger.LogInfo("TempPermission", $"Granted temporary permission to {target.PlayerName} (SteamID: {target.SteamID})");
        return true;
    }

    public bool RevokeTemporaryPermission(CCSPlayerController target)
    {
        if (target == null || !target.IsValid || target.SteamID == 0)
            return false;

        if (!_temporaryAdmins.Contains(target.SteamID))
        {
            Logger.LogDebug("TempPermission", $"Player {target.PlayerName} doesn't have temporary permission");
            return false;
        }

        _temporaryAdmins.Remove(target.SteamID);
        Logger.LogInfo("TempPermission", $"Revoked temporary permission from {target.PlayerName} (SteamID: {target.SteamID})");
        return true;
    }

    public List<CCSPlayerController> GetPlayersWithTemporaryPermission()
    {
        var players = new List<CCSPlayerController>();
        foreach (var player in Utilities.GetPlayers())
        {
            if (player != null && player.IsValid && HasTemporaryPermission(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    public int GetTemporaryAdminCount()
    {
        return _temporaryAdmins.Count;
    }

    public void ClearAllTemporaryPermissions()
    {
        var count = _temporaryAdmins.Count;
        _temporaryAdmins.Clear();
        Logger.LogInfo("TempPermission", $"Cleared all temporary permissions ({count} players)");
    }

    public void Cleanup()
    {
        ClearAllTemporaryPermissions();
    }
}