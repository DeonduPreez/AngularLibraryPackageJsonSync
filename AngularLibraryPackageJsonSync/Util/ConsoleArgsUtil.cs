namespace AngularLibraryPackageJsonSync.Util;

public static class ConsoleArgsUtil
{
    public static bool ArgExists(IReadOnlyList<string> args, string argName)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].ToLower();
            if (arg == $"-{argName?.ToLower()}")
            {
                return true;
            }
        }

        return false;
    }
}