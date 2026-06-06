namespace BeenThere.Web;

/// <summary>
/// Loads key=value pairs from a .env file into IConfiguration.
/// Searches from the current directory up to the repo root so the file
/// can live at the solution root rather than inside any project folder.
/// </summary>
internal static class EnvFile
{
    internal static void Load(WebApplicationBuilder builder)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadAllLines(candidate))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    {
                        continue;
                    }

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        builder.Configuration[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                break;
            }
            dir = dir.Parent;
        }
    }
}
