using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class TimerSettings
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("DurationSeconds")]
    public int DurationSeconds { get; set; } = 30;
}