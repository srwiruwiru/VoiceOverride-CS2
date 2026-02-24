using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class BaseConfig : BasePluginConfig
{
    [JsonPropertyName("Commands")]
    public CommandsSettings Commands { get; set; } = new();

    [JsonPropertyName("Timer")]
    public TimerSettings Timer { get; set; } = new();

    [JsonPropertyName("Threshold")]
    public ThresholdSettings Threshold { get; set; } = new();

    [JsonPropertyName("Behavior")]
    public BehaviorSettings Behavior { get; set; } = new();

    [JsonPropertyName("EnableDebug")]
    public bool EnableDebug { get; set; } = false;
}