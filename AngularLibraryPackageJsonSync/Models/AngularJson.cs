using System.Text.Json.Serialization;

namespace AngularLibraryPackageJsonSync.Models;

public class AngularJson
{
    [JsonPropertyName("projects")]
    public Dictionary<string, AngularProject> AngularProjects { get; set; }
}