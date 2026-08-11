using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aprillz.MewUI.VisualStudio.Session
{
    /// <summary>
    /// Resolves preview-capable executable projects on disk (plan.md 4.5.1/4.7): the project
    /// nearest to the active document first, otherwise executable projects under the solution.
    /// </summary>
    internal static class ProjectLocator
    {
        private static readonly Regex _outputTypePattern =
            new Regex(@"<OutputType>\s*(WinExe|Exe)\s*</OutputType>", RegexOptions.IgnoreCase);

        internal static string FindNearestProject(string filePath, string rootDirectory)
        {
            string directory = Path.GetDirectoryName(filePath);
            while (directory != null)
            {
                string project = Directory.EnumerateFiles(directory, "*.csproj").OrderBy(p => p).FirstOrDefault();
                if (project != null)
                {
                    return project;
                }
                if (rootDirectory != null && string.Equals(
                        Path.GetFullPath(directory).TrimEnd('\\'),
                        Path.GetFullPath(rootDirectory).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                directory = Path.GetDirectoryName(directory);
            }
            return null;
        }

        internal static List<string> FindExecutableProjects(string rootDirectory)
        {
            var results = new List<string>();
            if (rootDirectory == null || !Directory.Exists(rootDirectory))
            {
                return results;
            }
            foreach (string project in Directory.EnumerateFiles(rootDirectory, "*.csproj", SearchOption.AllDirectories))
            {
                if (project.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0
                    || project.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0
                    || project.IndexOf(@"\node_modules\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
                if (IsExecutableProject(project))
                {
                    results.Add(project);
                }
            }
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        internal static bool IsExecutableProject(string projectPath)
        {
            try
            {
                return _outputTypePattern.IsMatch(File.ReadAllText(projectPath));
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
