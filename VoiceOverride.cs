using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

using VoiceOverride.Configs;
using VoiceOverride.Services;
using VoiceOverride.Commands;

namespace VoiceOverride;

[MinimumApiVersion(355)]
public class VoiceOverride : BasePlugin, IPluginConfig<BaseConfig>
{
    public override string ModuleName => "VoiceOverride";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "luca.uy";
    public override string ModuleDescription => "Allows admins to mute all non-admin players for clear communication";

    public required BaseConfig Config { get; set; }

    private VoiceMuteService? _muteService;
    private TemporaryPermissionService? _tempPermissionService;
    private AdminVoiceCommand? _adminVoiceCommand;
    private GrantPermissionCommand? _grantPermissionCommand;

    public override void Load(bool hotReload)
    {
        Utils.Logger.Config = Config;

        RegisterEventHandler<EventGameEnd>(OnGameEnd);

        Utils.Logger.LogInfo("Core", "Plugin loaded successfully");
    }

    public void OnConfigParsed(BaseConfig config)
    {
        Config = config;
        Utils.Logger.Config = config;

        if (_muteService != null)
        {
            _muteService.Cleanup();
        }

        if (_tempPermissionService != null)
        {
            _tempPermissionService.Cleanup();
        }

        if (_adminVoiceCommand != null)
        {
            _adminVoiceCommand.UnregisterCommands();
        }

        if (_grantPermissionCommand != null)
        {
            _grantPermissionCommand.UnregisterCommands();
        }

        InitializeServices();

        Utils.Logger.LogInfo("Config", "Configuration loaded successfully");
    }

    private void InitializeServices()
    {
        if (Config == null) return;

        _tempPermissionService = new TemporaryPermissionService(Localizer);
        _muteService = new VoiceMuteService(Config, Localizer);
        _adminVoiceCommand = new AdminVoiceCommand(Config, _muteService, _tempPermissionService, Localizer);
        _grantPermissionCommand = new GrantPermissionCommand(Config, _tempPermissionService, Localizer);

        _adminVoiceCommand.RegisterCommands(this);
        _grantPermissionCommand.RegisterCommands(this);
    }

    private HookResult OnGameEnd(EventGameEnd @event, GameEventInfo info)
    {
        if (_tempPermissionService != null)
        {
            var playersWithPermission = _tempPermissionService.GetPlayersWithTemporaryPermission();

            foreach (var player in playersWithPermission)
            {
                if (player != null && player.IsValid)
                {
                    player.PrintToChat($"{Localizer["common.prefix"]} {Localizer["message.permission_expired"]}");
                }
            }

            _tempPermissionService.ClearAllTemporaryPermissions();
            Utils.Logger.LogInfo("Core", "Temporary permissions cleared due to map change");
        }

        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        _muteService?.Cleanup();
        _tempPermissionService?.Cleanup();
        Utils.Logger.LogInfo("Core", "Plugin unloaded successfully");
    }
}