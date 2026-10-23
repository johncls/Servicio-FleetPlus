namespace FleetPulse.Api;

internal static class LocalEnvFile
{
    public static void Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".env.example")))
            directory = directory.Parent;

        if (directory is null)
            return;

        var path = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
