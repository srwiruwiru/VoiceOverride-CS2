using System.Text.Json.Serialization;

namespace VoiceOverride.Configs;

public class CommandsSettings
{
    [JsonPropertyName("VoiceMuteCommands")]
    public List<string> VoiceMuteCommands { get; set; } = ["css_adminvoice", "css_admv"];

    [JsonPropertyName("GrantPermissionCommands")]
    public List<string> GrantPermissionCommands { get; set; } = ["css_grantvoice", "css_gv"];

    [JsonPropertyName("AdminPermissions")]
    public List<string> AdminPermissions { get; set; } = ["@css/root", "@css/generic"];
}