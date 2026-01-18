using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class BaseConfig : BasePluginConfig
{
    [JsonPropertyName("VoiceMuteCommands")]
    public List<string> VoiceMuteCommands { get; set; } = new() { "css_adminvoice", "css_admv" };

    [JsonPropertyName("GrantPermissionCommands")]
    public List<string> GrantPermissionCommands { get; set; } = new() { "css_grantvoice", "css_gv" };

    [JsonPropertyName("AdminPermissionFlag")]
    public string AdminPermissionFlag { get; set; } = "@css/root";

    [JsonPropertyName("UseTimer")]
    public bool UseTimer { get; set; } = false;

    [JsonPropertyName("TimerDuration")]
    public int TimerDuration { get; set; } = 30;

    [JsonPropertyName("EnableDebug")]
    public bool EnableDebug { get; set; } = false;
}