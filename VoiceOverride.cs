using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

using VoiceOverride.Configs;
using VoiceOverride.Services;
using VoiceOverride.Commands;

namespace VoiceOverride;

[MinimumApiVersion(354)]
public class VoiceOverride : BasePlugin, IPluginConfig<BaseConfig>
{
    public override string ModuleName => "VoiceOverride";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "luca.uy";
    public override string ModuleDescription => "Allows admins to mute all non-admin players for clear communication";

    public required BaseConfig Config { get; set; }

    private VoiceMuteService? _muteService;
    private AdminVoiceCommand? _adminVoiceCommand;

    public override void Load(bool hotReload)
    {
        Utils.Logger.Config = Config;

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

        if (_adminVoiceCommand != null)
        {
            _adminVoiceCommand.UnregisterCommands();
        }

        InitializeServices();

        Utils.Logger.LogInfo("Config", "Configuration loaded successfully");
    }

    private void InitializeServices()
    {
        if (Config == null) return;

        _muteService = new VoiceMuteService(Config, Localizer);
        _adminVoiceCommand = new AdminVoiceCommand(Config, _muteService, Localizer);
        _adminVoiceCommand.RegisterCommands(this);
    }

    public override void Unload(bool hotReload)
    {
        _muteService?.Cleanup();
        Utils.Logger.LogInfo("Core", "Plugin unloaded successfully");
    }
}