namespace ContractClause.Shared;

/// <summary>
/// Local-dev convenience: loads KEY=VALUE pairs from the nearest .env file
/// (walking up from the app base directory) into process environment
/// variables. Already-set variables win, and in Azure no .env exists so this
/// is a no-op — deployed apps keep getting configuration from app settings.
/// </summary>
public static class DotEnv
{
    public static void Load() => Load(AppContext.BaseDirectory);

    public static void Load(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(file))
                continue;

            foreach (var line in File.ReadAllLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = trimmed[..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim();
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
    }
}
