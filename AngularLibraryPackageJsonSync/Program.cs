using AngularLibraryPackageJsonSync.Models;
using AngularLibraryPackageJsonSync.Services;
using AngularLibraryPackageJsonSync.Util;

namespace AngularLibraryPackageJsonSync;

class Program
{
    // Dry run arg name setup
    private static readonly List<string> DryRunArgNames = ["dry-run", "dr"];

    // File name setup
    private const string AngularJsonFileName = "angular.json";
    private const string PackageJsonFileName = "package.json";
    private const string AngularLibraryType = "library";
    
    private static async Task Main(string[] args)
    {
        try
        {
            var isDryRun = ConsoleArgsUtil.ArgExists(args, DryRunArgNames);
            var fileService = new FileService(AngularJsonFileName, PackageJsonFileName, isDryRun);

            // Get angular.json in current folder or parent folders
            var angularJsonFileInfo = fileService.GetAngularJsonFileInfo(Environment.CurrentDirectory);
            if (angularJsonFileInfo == null)
            {
                throw new Exception($"{AngularJsonFileName} file could not be found in {Environment.CurrentDirectory} or parent directories. Are you sure you are in an Angular directory?");
            }

            var rootAngularDir = angularJsonFileInfo.Directory!;
            // Get package.json in angular.json folder
            var rootPackageJsonFileInfo = FileService.GetFileInfoFromDirectory(rootAngularDir.FullName, PackageJsonFileName);
            if (rootPackageJsonFileInfo == null)
            {
                throw new Exception($"{PackageJsonFileName} file could not be found in {rootAngularDir.FullName}. Are you sure you are in an Angular directory?");
            }

            // Search angular.json for libraries
            var angularJson = await FileService.ParseJsonFromFileInfoAsync<AngularJson>(angularJsonFileInfo);
            if (angularJson?.AngularProjects == null)
            {
                throw new Exception($"{AngularJsonFileName} file at {angularJsonFileInfo.FullName} could not be parsed.");
            }

            if (angularJson.AngularProjects.Count == 0)
            {
                throw new Exception($"{AngularJsonFileName} file at {angularJsonFileInfo.FullName} does not have any projects.");
            }

            // Get Root package.json packages
            var rootPackageJsonPackages = await fileService.BuildRootPackagesAsync(rootPackageJsonFileInfo);

            // Search angular.json for libraries
            var libraryProjects = angularJson.GetMatchingLibraryProjects(AngularLibraryType);
            foreach (var libraryProject in libraryProjects)
            {
                // Sync root package.json values with library package.json
                await fileService.SyncLibraryPackagesAsync(rootAngularDir, libraryProject, rootPackageJsonPackages);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}