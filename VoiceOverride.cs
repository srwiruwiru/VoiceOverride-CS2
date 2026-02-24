using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using System.Linq;

using VoiceOverride.Configs;
using VoiceOverride.Services;
using VoiceOverride.Commands;

namespace VoiceOverride;

[MinimumApiVersion(362)]
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

    private readonly Dictionary<int, Timer> _voiceSpeakingTimers = [];
    private const int VoiceDebounceMs = 250;

    public override void Load(bool hotReload)
    {
        Utils.Logger.Config = Config;

        // Events
        RegisterEventHandler<EventGameEnd>(OnGameEnd, HookMode.Pre);
        RegisterEventHandler<EventCsWinPanelMatch>(OnWinPanelMatch, HookMode.Pre);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect, HookMode.Pre);

        // Listeners
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientVoice>(OnClientVoice);

        Utils.Logger.LogInfo("Core", "Plugin loaded successfully");
    }

    public void OnConfigParsed(BaseConfig config)
    {
        Config = config;
        Utils.Logger.Config = config;

        _muteService?.Cleanup();

        _tempPermissionService?.Cleanup();

        _adminVoiceCommand?.UnregisterCommands();

        _grantPermissionCommand?.UnregisterCommands();

        InitializeServices();

        Utils.Logger.LogInfo("Config", "Configuration loaded successfully");
    }

    private void InitializeServices()
    {
        if (Config == null) return;

        _tempPermissionService = new TemporaryPermissionService(Localizer);
        _muteService = new VoiceMuteService(Config, Localizer, _tempPermissionService);
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
                    player.PrintToChat($"{Localizer["common.prefix"]} {Localizer["message.permission_expired"]}");
            }

            _tempPermissionService.ClearAllTemporaryPermissions();
            Utils.Logger.LogInfo("Core", "Temporary permissions cleared due to map change");
        }

        return HookResult.Continue;
    }

    private HookResult OnWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        Utils.Logger.LogDebug("Core", "EventCsWinPanelMatch fired.");
        if (Config.MuteNonAdminsOnGameEnd && _muteService != null)
            _muteService.SetGlobalMute(true);

        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        AddTimer(5.0f, () =>
        {
            if (_muteService != null && _muteService.IsGlobalMuted)
            {
                _muteService.SetGlobalMute(false);
            }

            if (_muteService != null && _muteService.IsVoiceMuted)
            {
                _muteService.DisableVoiceMute();
            }
        });
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (!Config.MuteNonAdminsOnPlayerThreshold || _muteService == null)
            return HookResult.Continue;

        var player = @event.Userid;

        Server.NextFrame(() =>
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;

            CheckAndUpdatePlayerThreshold();

            if (_muteService.IsThresholdMuteActive)
            {
                _muteService.ApplyThresholdMuteToNewPlayer(player);
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (_muteService != null)
        {
            var player = @event.Userid;
            if (player != null && player.IsValid && _muteService.HasAdminPermission(player))
            {
                var slot = player.Slot;
                if (_voiceSpeakingTimers.TryGetValue(slot, out var t))
                {
                    t.Dispose();
                    _voiceSpeakingTimers.Remove(slot);
                }
                Server.NextFrame(() => _muteService.OnAdminStoppedSpeaking(slot));
            }
        }

        if (!Config.MuteNonAdminsOnPlayerThreshold || _muteService == null)
            return HookResult.Continue;

        Server.NextFrame(CheckAndUpdatePlayerThreshold);

        return HookResult.Continue;
    }

    private void CheckAndUpdatePlayerThreshold()
    {
        if (_muteService == null || !Config.MuteNonAdminsOnPlayerThreshold)
            return;

        var humanPlayers = Utilities.GetPlayers()
            .Count(p => p != null && p.IsValid && !p.IsBot);

        Utils.Logger.LogDebug("Threshold", $"Player count check: {humanPlayers} / {Config.PlayerThreshold}");

        if (humanPlayers > Config.PlayerThreshold)
        {
            if (!_muteService.IsThresholdMuteActive)
            {
                Utils.Logger.LogInfo("Threshold", $"Threshold exceeded ({humanPlayers} > {Config.PlayerThreshold}). Enabling threshold mute.");
                _muteService.EnableThresholdMute();
            }
        }
        else
        {
            if (_muteService.IsThresholdMuteActive)
            {
                Utils.Logger.LogInfo("Threshold", $"Below threshold ({humanPlayers} <= {Config.PlayerThreshold}). Disabling threshold mute.");
                _muteService.DisableThresholdMute();
            }
        }
    }

    private void OnClientVoice(int playerSlot)
    {
        if (!Config.MuteOnAdminVoice || _muteService == null)
            return;

        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (!_muteService.HasAdminPermission(player))
            return;

        _muteService.OnAdminStartedSpeaking(playerSlot);

        if (_voiceSpeakingTimers.TryGetValue(playerSlot, out var existing))
        {
            existing.Dispose();
            _voiceSpeakingTimers.Remove(playerSlot);
        }

        var slotCapture = playerSlot;
        _voiceSpeakingTimers[playerSlot] = new Timer(_ =>
        {
            Server.NextFrame(() =>
            {
                _voiceSpeakingTimers.Remove(slotCapture);
                _muteService.OnAdminStoppedSpeaking(slotCapture);
            });
        }, null, VoiceDebounceMs, Timeout.Infinite);
    }

    public override void Unload(bool hotReload)
    {
        foreach (var t in _voiceSpeakingTimers.Values)
            t.Dispose();
        _voiceSpeakingTimers.Clear();

        _muteService?.Cleanup();
        _tempPermissionService?.Cleanup();
        Utils.Logger.LogInfo("Core", "Plugin unloaded successfully");
    }
}