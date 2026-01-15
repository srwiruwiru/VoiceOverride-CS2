using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Localization;

using VoiceOverride.Utils;
using VoiceOverride.Configs;
using VoiceOverride.Services;

namespace VoiceOverride.Commands;

public class GrantPermissionCommand
{
    private readonly BaseConfig _config;
    private readonly TemporaryPermissionService _tempPermissionService;
    private readonly IStringLocalizer _localizer;
    private readonly List<string> _registeredCommands = new();

    public GrantPermissionCommand(BaseConfig config, TemporaryPermissionService tempPermissionService, IStringLocalizer localizer)
    {
        _config = config;
        _tempPermissionService = tempPermissionService;
        _localizer = localizer;
    }

    public void RegisterCommands(BasePlugin plugin)
    {
        foreach (var commandName in _config.GrantPermissionCommands)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                continue;

            var fullCommandName = commandName.StartsWith("css_") ? commandName : $"css_{commandName}";

            if (_registeredCommands.Contains(fullCommandName))
            {
                Logger.LogWarning("Commands", $"Command {fullCommandName} is already registered, skipping");
                continue;
            }

            plugin.AddCommand(fullCommandName, _localizer["command.grant_description"], OnGrantCommand);
            _registeredCommands.Add(fullCommandName);

            Logger.LogInfo("Commands", $"Registered grant permission command: {fullCommandName}");
        }
    }

    public void UnregisterCommands()
    {
        _registeredCommands.Clear();
    }

    private void OnGrantCommand(CCSPlayerController? player, CommandInfo commandInfo)
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

        if (!AdminManager.PlayerHasPermissions(player, _config.AdminPermissionFlag))
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.no_permission", _config.AdminPermissionFlag]}");
            return;
        }

        if (commandInfo.ArgCount < 2)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.grant_missing_target"]}");
            return;
        }

        var targetIdentifier = commandInfo.GetArg(1);
        var target = FindTargetPlayer(targetIdentifier);

        if (target == null)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.target_not_found", targetIdentifier]}");
            return;
        }

        if (target.IsBot)
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.target_is_bot"]}");
            return;
        }

        if (AdminManager.PlayerHasPermissions(target, _config.AdminPermissionFlag))
        {
            commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["error.target_already_admin", target.PlayerName]}");
            return;
        }

        if (_tempPermissionService.HasTemporaryPermission(target))
        {
            if (_tempPermissionService.RevokeTemporaryPermission(target))
            {
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["message.permission_revoked", target.PlayerName]}");

                if (target.IsValid)
                {
                    target.PrintToChat($"{_localizer["common.prefix"]} {_localizer["message.permission_revoked_target"]}");
                }
            }
        }
        else
        {
            if (_tempPermissionService.GrantTemporaryPermission(target))
            {
                commandInfo.ReplyToCommand($"{_localizer["common.prefix"]} {_localizer["message.permission_granted", target.PlayerName]}");

                if (target.IsValid)
                {
                    target.PrintToChat($"{_localizer["common.prefix"]} {_localizer["message.permission_granted_target"]}");
                }
            }
        }
    }

    private CCSPlayerController? FindTargetPlayer(string identifier)
    {
        var players = Utilities.GetPlayers();

        var exactMatch = players.FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.PlayerName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return exactMatch;

        var partialMatch = players.FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.PlayerName.Contains(identifier, StringComparison.OrdinalIgnoreCase));
        if (partialMatch != null)
            return partialMatch;

        if (identifier.StartsWith("#") && int.TryParse(identifier.Substring(1), out int userid))
        {
            return players.FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.UserId == userid);
        }

        if (int.TryParse(identifier, out int slot))
        {
            return players.FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.Slot == slot);
        }

        return null;
    }
}