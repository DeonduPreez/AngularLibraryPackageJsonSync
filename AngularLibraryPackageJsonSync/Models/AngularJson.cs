using System.Text.Json.Serialization;

namespace AngularLibraryPackageJsonSync.Models;

public class AngularJson
{
    [JsonPropertyName("projects")]
    public required Dictionary<string, AngularProject> AngularProjects { get; set; }
    
    public IEnumerable<KeyValuePair<string, AngularProject>> GetMatchingLibraryProjects(string angularLibraryType)
    {
        return AngularProjects.Where((p) => string.Equals(p.Value.ProjectType, angularLibraryType, StringComparison.CurrentCultureIgnoreCase));
    }
}