using System.Text.Json.Serialization;

namespace CommonSDK.ModGateway;

public class ModMetadata
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string[] Author { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;
    public int LoadOrder { get; set; } = 0;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
    
    [JsonIgnore]
    public string Directory { get; set; }
}