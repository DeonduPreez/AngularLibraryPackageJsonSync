using System.Text.Json.Serialization;

namespace AngularLibraryPackageJsonSync.Models;

public class AngularProject
{
    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; }

    [JsonPropertyName("root")]
    public string Root { get; set; }
}