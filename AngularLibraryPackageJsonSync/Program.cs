using System.Text.Json;
using System.Text.Json.Nodes;
using AngularLibraryPackageJsonSync.Models;

namespace AngularLibraryPackageJsonSync;

class Program
{
    private const string AngularJsonFileName = "angular.json";
    private const string PackageJsonFileName = "package.json";

    private const string AngularLibraryType = "library";

    private const string Dependencies = "dependencies";
    private const string DevDependencies = "devDependencies";
    private const string PeerDependencies = "peerDependencies";

    private static async Task Main(string[] args)
    {
        try
        {
            // Get angular.json in current folder or parent folders
            var angularJsonFileInfo = GetAngularJsonFileInfo(Environment.CurrentDirectory);
            if (angularJsonFileInfo == null)
            {
                throw new Exception($"{AngularJsonFileName} file could not be found in {Environment.CurrentDirectory} or parent directories. Are you sure you are in an Angular directory?");
            }

            var rootAngularDir = angularJsonFileInfo.Directory!;
            // Get package.json in angular.json folder
            var rootPackageJsonFileInfo = GetFileInfoFromDirectory(rootAngularDir.FullName, PackageJsonFileName);
            if (rootPackageJsonFileInfo == null)
            {
                throw new Exception($"{PackageJsonFileName} file could not be found in {rootAngularDir.FullName}. Are you sure you are in an Angular directory?");
            }

            // Search angular.json for libraries
            var angularJson = await ParseJsonFromFileInfo<AngularJson>(angularJsonFileInfo);
            if (angularJson?.AngularProjects == null)
            {
                throw new Exception($"{AngularJsonFileName} file at {angularJsonFileInfo.FullName} could not be parsed.");
            }

            if (angularJson.AngularProjects.Count == 0)
            {
                throw new Exception($"{AngularJsonFileName} file at {angularJsonFileInfo.FullName} does not have any projects.");
            }

            // Get Root package.json packages
            var rootPackageJsonPackages = await BuildRootPackages(rootPackageJsonFileInfo);

            // Search angular.json for libraries
            var libraryProjects = angularJson.AngularProjects.Where((p) => string.Equals(p.Value.ProjectType, AngularLibraryType, StringComparison.CurrentCultureIgnoreCase));

            foreach (var libraryProject in libraryProjects)
            {
                // Sync root package.json values with library package.json
                await SyncLibraryPackages(rootAngularDir, libraryProject, rootPackageJsonPackages);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static async Task<Dictionary<string, string>> BuildRootPackages(FileInfo packageJsonFileInfo)
    {
        var rootPackageJson = await GetJsonNodeFromFileInfo(packageJsonFileInfo);
        if (rootPackageJson == null)
        {
            throw new Exception($"{PackageJsonFileName} file at {packageJsonFileInfo.FullName} could not be parsed.");
        }

        var rootPackages = new Dictionary<string, string>();

        if (rootPackageJson[Dependencies] != null)
        {
            foreach (var package in rootPackageJson[Dependencies]!.AsObject())
            {
                if (rootPackages.ContainsKey(package.Key))
                {
                    throw new Exception($"{packageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
                }

                rootPackages.Add(package.Key, package.Value!.ToString());
            }
        }

        if (rootPackageJson[DevDependencies] != null)
        {
            foreach (var package in rootPackageJson[DevDependencies]!.AsObject())
            {
                if (rootPackages.ContainsKey(package.Key))
                {
                    throw new Exception($"{packageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
                }

                rootPackages.Add(package.Key, package.Value!.ToString());
            }
        }

        return rootPackages;
    }

    private static FileInfo? GetAngularJsonFileInfo(string currentDirectory)
    {
        if (!Path.Exists(currentDirectory))
        {
            return null;
        }

        var directoryInfo = new DirectoryInfo(currentDirectory);

        var fileInfo = GetFileInfoFromDirectory(currentDirectory, AngularJsonFileName);
        if (directoryInfo.Parent == null)
        {
            return null;
        }

        return fileInfo ?? GetAngularJsonFileInfo(directoryInfo.Parent.FullName);
    }

    private static FileInfo? GetFileInfoFromDirectory(string currentDirectory, string fileName)
    {
        var currentDirFiles = Directory.GetFiles(currentDirectory).ToList();
        var angularJsonFile = currentDirFiles.FirstOrDefault((f) =>
        {
            var lastIndexChar = '\\';
            var lastIndex = f.LastIndexOf(lastIndexChar);
            if (lastIndex == -1)
            {
                lastIndexChar = '/';
                lastIndex = f.LastIndexOf(lastIndexChar);
            }

            return f[lastIndex..].Equals(lastIndexChar + fileName, StringComparison.CurrentCultureIgnoreCase);
        });

        if (string.IsNullOrWhiteSpace(angularJsonFile))
        {
            return null;
        }

        return new FileInfo(angularJsonFile);
    }

    private static async Task<T?> ParseJsonFromFileInfo<T>(FileInfo fileInfo)
    {
        await using var fileStream = fileInfo.OpenRead();
        return await JsonSerializer.DeserializeAsync<T>(fileStream);
    }

    private static async Task<JsonNode?> GetJsonNodeFromFileInfo(FileInfo fileInfo)
    {
        await using var fileStream = fileInfo.OpenRead();
        return await JsonNode.ParseAsync(fileStream);
    }

    private static async Task SyncLibraryPackages(DirectoryInfo rootAngularDir, KeyValuePair<string, AngularProject> libraryProject, Dictionary<string, string> rootPackageJsonPackages)
    {
        var libraryPackageJsonFileInfo = GetFileInfoFromDirectory(Path.Combine(rootAngularDir.FullName, libraryProject.Value.Root), PackageJsonFileName);
        if (libraryPackageJsonFileInfo == null)
        {
            Console.WriteLine($"{PackageJsonFileName} file could not be found in {libraryProject.Value.Root} for library {libraryProject.Key}. Are you sure you are in an Angular directory?");
            return;
        }

        var libraryPackageJson = await GetJsonNodeFromFileInfo(libraryPackageJsonFileInfo);
        if (libraryPackageJson == null)
        {
            throw new Exception($"{PackageJsonFileName} file at {libraryPackageJsonFileInfo.FullName} could not be parsed.");
        }

        var updatedPackages = new Dictionary<string, string>();
        var removedPackages = new List<string>();
        if (libraryPackageJson[Dependencies] != null)
        {
            var newPackageJsonDependencies = new Dictionary<string, string>();
            foreach (var package in libraryPackageJson[Dependencies]!.AsObject())
            {
                if (updatedPackages.ContainsKey(package.Key))
                {
                    throw new Exception($"{libraryPackageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
                }

                var rootPackageJsonEquivalent = rootPackageJsonPackages.FirstOrDefault((p) => p.Key == package.Key);
                if (rootPackageJsonEquivalent.Equals(default(KeyValuePair<string, string>)))
                {
                    removedPackages.Add(package.Key);
                    continue;
                }

                newPackageJsonDependencies.Add(package.Key, rootPackageJsonEquivalent.Value);
                updatedPackages.Add(package.Key, $"From {Dependencies} - {package.Value!} -> {rootPackageJsonEquivalent.Value}");
            }

            libraryPackageJson[Dependencies]!.ReplaceWith(newPackageJsonDependencies);
        }

        if (libraryPackageJson[PeerDependencies] != null)
        {
            var newPackageJsonPeerDependencies = new Dictionary<string, string>();
            foreach (var package in libraryPackageJson[PeerDependencies]!.AsObject())
            {
                if (updatedPackages.ContainsKey(package.Key))
                {
                    throw new Exception($"{libraryPackageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
                }

                var rootPackageJsonEquivalent = rootPackageJsonPackages.FirstOrDefault((p) => p.Key == package.Key);
                if (rootPackageJsonEquivalent.Equals(default(KeyValuePair<string, string>)))
                {
                    removedPackages.Add(package.Key);
                    continue;
                }

                newPackageJsonPeerDependencies.Add(package.Key, rootPackageJsonEquivalent.Value);
                updatedPackages.Add(package.Key, $"From {PeerDependencies} - {package.Value!} -> {rootPackageJsonEquivalent.Value}");
            }

            libraryPackageJson[PeerDependencies]!.ReplaceWith(newPackageJsonPeerDependencies);
        }

        await SavePackageJson(libraryPackageJsonFileInfo, libraryPackageJson);
    }

    private static async Task SavePackageJson(FileInfo packageJsonFileInfo, JsonNode json)
    {
        await using var fs = File.Create(packageJsonFileInfo.FullName);
        await using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions
        {
            Indented = true
        });
        using var document = JsonDocument.Parse(json.ToJsonString(), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
        }
        else
        {
            return;
        }

        foreach (var property in root.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();

        await writer.FlushAsync();
    }
}