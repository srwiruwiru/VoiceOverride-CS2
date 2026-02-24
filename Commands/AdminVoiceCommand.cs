using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;
using VoiceOverride.Models;
using VoiceOverride.Configs;
using VoiceOverride.Services;

namespace VoiceOverride.Commands;

public class AdminVoiceCommand(BaseConfig config, VoiceMuteService muteService, TemporaryPermissionService tempPermissionService, IStringLocalizer localizer)
{
    private readonly BaseConfig _config = config;
    private readonly VoiceMuteService _muteService = muteService;
    private readonly TemporaryPermissionService _tempPermissionService = tempPermissionService;
    private readonly IStringLocalizer _localizer = localizer;
    private readonly List<string> _registeredCommands = [];

    public void RegisterCommands(BasePlugin plugin)
    {
        foreach (var commandName in _config.VoiceMuteCommands)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                continue;

            var fullCommandName = commandName.StartsWith("css_") ? commandName : $"css_{commandName}";

            if (_registeredCommands.Contains(fullCommandName))
            {
                Logger.LogWarning("Commands", $"Command {fullCommandName} is already registered, skipping");
                continue;
            }

            plugin.AddCommand(fullCommandName, _localizer["command.description"], OnCommand);
            _registeredCommands.Add(fullCommandName);

            Logger.LogInfo("Commands", $"Registered command: {fullCommandName}");
        }
    }

    public void UnregisterCommands()
    {
        _registeredCommands.Clear();
    }

    private static TeamFilter? ParseTeamFilter(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return null;

        var argLower = arg.Trim().ToLower();

        return argLower switch
        {
            "@ct" => TeamFilter.CT,
            "@t" => TeamFilter.T,
            "@all" => TeamFilter.All,
            _ => null
        };
    }

    private static string GetMuteMessageKey(bool useTimer)
    {
        return useTimer ? "message.players_muted_timer" : "message.players_muted";
    }

    private string GetTeamName(TeamFilter teamFilter)
    {
        return teamFilter switch
        {
            TeamFilter.CT => _localizer["common.team_ct"],
            TeamFilter.T => _localizer["common.team_t"],
            _ => _localizer["common.team_all"]
        };
    }

    private bool HasPermission(CCSPlayerController player)
    {
        if (AdminManager.PlayerHasPermissions(player, _config.AdminPermissionFlag))
            return true;

        if (_tempPermissionService.HasTemporaryPermission(player))
            return true;

        return false;
    }

    private void OnCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.players_only"]}");
            return;
        }

        if (!player.IsValid || player.IsBot)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.invalid_player"]}");
            return;
        }

        if (!HasPermission(player))
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.no_permission", _config.AdminPermissionFlag]}");
            return;
        }

        if (commandInfo.ArgCount < 2)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.missing_argument"]}");
            return;
        }

        var teamFilter = ParseTeamFilter(commandInfo.GetArg(1));
        if (!teamFilter.HasValue)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.invalid_argument"]}");
            return;
        }

        var wasMuted = _muteService.IsMuted(player);

        if (_config.UseTimer)
        {
            if (wasMuted)
            {
                _muteService.UnmuteAll(player);
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["message.players_unmuted"]}");
            }
            else
            {
                _muteService.MuteAll(player, teamFilter.Value);
                var messageKey = GetMuteMessageKey(true);
                var teamName = GetTeamName(teamFilter.Value);
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer[messageKey, teamName, _config.TimerDuration]}");
            }
        }
        else
        {
            _muteService.ToggleMute(player, teamFilter.Value);
            if (_muteService.IsMuted(player))
            {
                var messageKey = GetMuteMessageKey(false);
                var teamName = GetTeamName(teamFilter.Value);
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer[messageKey, teamName]}");
            }
            else
            {
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["message.players_unmuted"]}");
            }
        }
    }
}