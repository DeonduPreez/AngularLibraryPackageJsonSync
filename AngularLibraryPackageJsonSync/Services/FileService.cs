using System.Text.Json;
using System.Text.Json.Nodes;
using AngularLibraryPackageJsonSync.Models;

namespace AngularLibraryPackageJsonSync.Services;

public class FileService(string angularJsonFileName, string packageJsonFileName, bool isDryRun = true)
{
    private string mAngularJsonFileName = angularJsonFileName;
    private string mPackageJsonFileName = packageJsonFileName;
    private bool mIsDryRun = isDryRun;

    private const string Dependencies = "dependencies";
    private const string DevDependencies = "devDependencies";
    private const string PeerDependencies = "peerDependencies";

    public async Task<Dictionary<string, string>> BuildRootPackagesAsync(FileInfo packageJsonFileInfo)
    {
        var rootPackageJson = await GetJsonNodeFromFileInfoAsync(packageJsonFileInfo);
        if (rootPackageJson == null)
        {
            throw new Exception($"{mPackageJsonFileName} file at {packageJsonFileInfo.FullName} could not be parsed.");
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

        if (rootPackageJson[DevDependencies] == null)
        {
            return rootPackages;
        }

        foreach (var package in rootPackageJson[DevDependencies]!.AsObject())
        {
            if (rootPackages.ContainsKey(package.Key))
            {
                throw new Exception($"{packageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
            }

            rootPackages.Add(package.Key, package.Value!.ToString());
        }

        return rootPackages;
    }

    public FileInfo? GetAngularJsonFileInfo(string currentDirectory)
    {
        while (true)
        {
            if (!Path.Exists(currentDirectory))
            {
                return null;
            }

            var directoryInfo = new DirectoryInfo(currentDirectory);

            var fileInfo = GetFileInfoFromDirectory(currentDirectory, mAngularJsonFileName);
            if (directoryInfo.Parent == null)
            {
                return null;
            }

            if (fileInfo != null) return fileInfo;
            currentDirectory = directoryInfo.Parent.FullName;
        }
    }

    public static FileInfo? GetFileInfoFromDirectory(string currentDirectory, string fileName)
    {
        var currentDirFiles = Directory.GetFiles(currentDirectory).ToList();
        var angularJsonFile = currentDirFiles.FirstOrDefault((f) =>
        {
            var lastIndexChar = '\\';
            var lastIndex = f.LastIndexOf(lastIndexChar);
            if (lastIndex != -1)
            {
                return f[lastIndex..].Equals(lastIndexChar + fileName, StringComparison.CurrentCultureIgnoreCase);
            }

            lastIndexChar = '/';
            lastIndex = f.LastIndexOf(lastIndexChar);

            return f[lastIndex..].Equals(lastIndexChar + fileName, StringComparison.CurrentCultureIgnoreCase);
        });

        return string.IsNullOrWhiteSpace(angularJsonFile) ? null : new FileInfo(angularJsonFile);
    }

    public static async Task<T?> ParseJsonFromFileInfoAsync<T>(FileInfo fileInfo)
    {
        await using var fileStream = fileInfo.OpenRead();
        return await JsonSerializer.DeserializeAsync<T>(fileStream);
    }

    public async Task SyncLibraryPackagesAsync(DirectoryInfo rootAngularDir, KeyValuePair<string, AngularProject> libraryProject, Dictionary<string, string> rootPackageJsonPackages)
    {
        var libraryPackageJsonFileInfo = GetFileInfoFromDirectory(Path.Combine(rootAngularDir.FullName, libraryProject.Value.Root), mPackageJsonFileName);
        if (libraryPackageJsonFileInfo == null)
        {
            Console.WriteLine($"{mPackageJsonFileName} file could not be found in {libraryProject.Value.Root} for library {libraryProject.Key}. Are you sure you are in an Angular directory?");
            return;
        }

        var libraryPackageJson = await GetJsonNodeFromFileInfoAsync(libraryPackageJsonFileInfo);
        if (libraryPackageJson == null)
        {
            throw new Exception($"{mPackageJsonFileName} file at {libraryPackageJsonFileInfo.FullName} could not be parsed.");
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
                    throw new Exception(
                        $"{libraryPackageJsonFileInfo.FullName} contains multiple entries for package with name {package.Key}. Please fix this before running this tool again");
                }

                var rootPackageJsonEquivalent = rootPackageJsonPackages.FirstOrDefault((p) => p.Key == package.Key);
                if (rootPackageJsonEquivalent.Equals(default(KeyValuePair<string, string>)))
                {
                    removedPackages.Add(package.Key);
                    continue;
                }
                
                if (package.Value!.ToString() == rootPackageJsonEquivalent.Value)
                {
                    newPackageJsonDependencies.Add(package.Key, package.Value!.ToString());
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
                
                if (package.Value!.ToString() == rootPackageJsonEquivalent.Value)
                {
                    newPackageJsonPeerDependencies.Add(package.Key, package.Value!.ToString());
                    continue;
                }

                newPackageJsonPeerDependencies.Add(package.Key, rootPackageJsonEquivalent.Value);
                updatedPackages.Add(package.Key, $"From {PeerDependencies} - {package.Value!} -> {rootPackageJsonEquivalent.Value}");
            }

            libraryPackageJson[PeerDependencies]!.ReplaceWith(newPackageJsonPeerDependencies);
        }
        
                
        if (updatedPackages.Count == 0 && removedPackages.Count == 0)
        {
            Console.WriteLine("No changes were necessary");
            return;
        }
        
        if (updatedPackages.Count > 0)
        {
            Console.WriteLine("Updating the following packages:");
            foreach (var package in updatedPackages)
            {
                Console.WriteLine($"{package.Key} {package.Value}");
            }
        }

        if (removedPackages.Count > 0)
        {
            Console.WriteLine("Removing the following packages:");
            foreach (var package in removedPackages)
            {
                Console.WriteLine(package);
            }
        }

        if (mIsDryRun)
        {
            Console.WriteLine("Dry run is enabled, changes have not been saved");
            return;
        }

        await SavePackageJsonAsync(libraryPackageJsonFileInfo, libraryPackageJson);
    }

    private static async Task<JsonNode?> GetJsonNodeFromFileInfoAsync(FileInfo fileInfo)
    {
        await using var fileStream = fileInfo.OpenRead();
        return await JsonNode.ParseAsync(fileStream);
    }

    private static async Task SavePackageJsonAsync(FileSystemInfo packageJsonFileInfo, JsonNode json)
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
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        writer.WriteStartObject();

        foreach (var property in root.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();

        await writer.FlushAsync();
    }
}