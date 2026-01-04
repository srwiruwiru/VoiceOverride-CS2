using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class BaseConfig : BasePluginConfig
{

    [JsonPropertyName("Commands")]
    public List<string> Commands { get; set; } = new() { "css_adminvoice", "css_admv" };

    [JsonPropertyName("PermissionFlag")]
    public string PermissionFlag { get; set; } = "@css/root";

    [JsonPropertyName("UseTimer")]
    public bool UseTimer { get; set; } = false;

    [JsonPropertyName("TimerDuration")]
    public int TimerDuration { get; set; } = 30;

    [JsonPropertyName("EnableDebug")]
    public bool EnableDebug { get; set; } = true;
}