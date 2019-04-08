using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ChaseLukeA
{
    public static class DependencyResolver
    {
        public static void Setup(params string[] dependencyPaths)
        {
            string currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var pathsToSearch = new List<string> { currentPath };

            if (dependencyPaths.Any())
            {
                foreach (string path in dependencyPaths)
                    pathsToSearch.Add(path.IsAbsolute() ? path : Path.Combine(currentPath, path));
            }

            // add additional paths to search to PATH for the current process (required to resolve DLL dependencies not directly referenced by this assembly)
            foreach (string path in pathsToSearch)
                Environment.SetEnvironmentVariable("PATH", path + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process), EnvironmentVariableTarget.Process);

            // provide the app's default resolver additional paths to search (for locating assemblies directly referenced by this assembly)
            AppDomain.CurrentDomain.AssemblyResolve += (_, requestedAssembly) =>
            {
                int numberOfMatches = 0;
                string matchingAssembly = "";

                foreach (string path in pathsToSearch)
                {
                    string assembly = Path.Combine(path, new AssemblyName(requestedAssembly.Name).Name + ".dll");

                    if (File.Exists(assembly))
                    {
                        numberOfMatches++;
                        matchingAssembly = assembly;
                    }
                }

                switch (numberOfMatches)
                {
                    case 0:
                        throw new DllNotFoundException($"Requested assembly '{requestedAssembly.Name}' was not found in any of the provided paths");
                    case 1:
                        Debug.WriteLine($"Loading assembly '{matchingAssembly}'");
                        return Assembly.LoadFrom(matchingAssembly);
                    default:
                        throw new AmbiguousMatchException($"Multiple references to requested assembly '{requestedAssembly.Name}' were found");
                }
            };
        }

        private static bool IsAbsolute(this string path) =>
            // is a drive letter path (DOS, OS/2, Windows) [example: `c:\ProgramData\Shared` || `D:/setup`]
            path.Substring(1).StartsWith(@":\") || path.Substring(1).StartsWith(@":/") ||
            // is a UNC (universal naming convention) path [example: `\\host-name\share-name` || `//host-name/share-name`]
            path.StartsWith(@"\\") || path.StartsWith(@"//") ||
            // is a UNIX root directory [example: /usr/bin]
            path.StartsWith(@"/");
    }
}
