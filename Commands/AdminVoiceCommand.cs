using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;
using VoiceOverride.Models;
using VoiceOverride.Configs;
using VoiceOverride.Services;

namespace VoiceOverride.Commands;

public class AdminVoiceCommand
{
    private readonly BaseConfig _config;
    private readonly VoiceMuteService _muteService;
    private readonly TemporaryPermissionService _tempPermissionService;
    private readonly IStringLocalizer _localizer;
    private readonly List<string> _registeredCommands = new();

    public AdminVoiceCommand(BaseConfig config, VoiceMuteService muteService, TemporaryPermissionService tempPermissionService, IStringLocalizer localizer)
    {
        _config = config;
        _muteService = muteService;
        _tempPermissionService = tempPermissionService;
        _localizer = localizer;
    }

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

    private TeamFilter? ParseTeamFilter(string? arg)
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

    private string GetMuteMessageKey(TeamFilter teamFilter, bool useTimer)
    {
        if (useTimer)
        {
            return teamFilter switch
            {
                TeamFilter.CT => "message.players_muted_timer_ct",
                TeamFilter.T => "message.players_muted_timer_t",
                _ => "message.players_muted_timer"
            };
        }

        return teamFilter switch
        {
            TeamFilter.CT => "message.players_muted_ct",
            TeamFilter.T => "message.players_muted_t",
            _ => "message.players_muted"
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
                var messageKey = GetMuteMessageKey(teamFilter.Value, true);
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer[messageKey, _config.TimerDuration]}");
            }
        }
        else
        {
            _muteService.ToggleMute(player, teamFilter.Value);
            if (_muteService.IsMuted(player))
            {
                var messageKey = GetMuteMessageKey(teamFilter.Value, false);
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer[messageKey]}");
            }
            else
            {
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["message.players_unmuted"]}");
            }
        }
    }
}