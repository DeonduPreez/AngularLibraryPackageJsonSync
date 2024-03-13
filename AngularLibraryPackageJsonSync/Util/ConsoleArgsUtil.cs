namespace AngularLibraryPackageJsonSync.Util;

public static class ConsoleArgsUtil
{
    public static bool ArgExists(string[] args, List<string> argNames)
    {
        foreach (var argName in argNames)
        {
            var argExists = args.Any(arg => arg.Equals($"-{argName.ToLower()}", StringComparison.CurrentCultureIgnoreCase));
            if (argExists) return true;
        }

        return false;
    }
}