using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class ThresholdSettings
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("PlayerCount")]
    public int PlayerCount { get; set; } = 10;
}