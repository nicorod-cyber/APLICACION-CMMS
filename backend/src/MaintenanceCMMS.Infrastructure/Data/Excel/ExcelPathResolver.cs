namespace MaintenanceCMMS.Infrastructure.Data.Excel;

internal static class ExcelPathResolver
{
    public static string Resolve(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var candidates = new List<string>
        {
            Path.GetFullPath(configuredPath, Directory.GetCurrentDirectory())
        };

        AddParentCandidates(candidates, Directory.GetCurrentDirectory(), configuredPath);
        AddParentCandidates(candidates, AppContext.BaseDirectory, configuredPath);

        var existing = candidates.FirstOrDefault(Directory.Exists);
        if (existing is not null)
        {
            return existing;
        }

        var parentCandidate = candidates.FirstOrDefault(candidate =>
            Directory.Exists(Path.GetDirectoryName(candidate)));

        return parentCandidate ?? candidates[0];
    }

    private static void AddParentCandidates(List<string> candidates, string startPath, string configuredPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null)
        {
            candidates.Add(Path.GetFullPath(Path.Combine(current.FullName, configuredPath)));
            current = current.Parent;
        }
    }
}

