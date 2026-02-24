using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class BehaviorSettings
{
    [JsonPropertyName("MuteNonAdminsOnAdminVoice")]
    public bool MuteNonAdminsOnAdminVoice { get; set; } = true;

    [JsonPropertyName("MuteNonAdminsOnMatchEnd")]
    public bool MuteNonAdminsOnMatchEnd { get; set; } = false;
}