using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bindings_generator
{
    internal class LanguageSourcePaths
    {
        public static readonly string[] cSourceExt = { "c", "cc" };

        string m_moduleName = "";
        string m_repoPath = "";
        string m_sourcePath = "";
        IEnumerable<string> m_sourceFiles = Enumerable.Empty<string>();

        public string ModuleName { get { return m_moduleName; } }
        public string RepoPath { get { return m_repoPath; } }
        public string SourcePath { get { return m_sourcePath; } }
        public IEnumerable<string> SourceFiles { get { return m_sourceFiles; } }

        public static (PathError, LanguageSourcePaths?) AssembleLanguageSourcePaths(DirectoryInfo languageRepoPath)
        {
            if (!Directory.Exists(languageRepoPath.FullName))
            {
                return (PathError.DirectoryMissing(languageRepoPath.FullName), null);
            }

            string treeSitterIncludePath = "";
            {
                const string RelativeIncludePath = @"\src\";
                string IncludePath = Path.Join(languageRepoPath.FullName.AsSpan(), RelativeIncludePath.AsSpan());
                if (!Directory.Exists(IncludePath))
                {
                    return (PathError.DirectoryMissing(IncludePath), null);
                }
                treeSitterIncludePath = IncludePath;
            }

            // This doesn't have any headers *yet*.
            // Need to generate them from the source files.
            var sourceFiles = Directory
                .EnumerateFiles(treeSitterIncludePath, "*.*", SearchOption.TopDirectoryOnly) // want only the parser.c and scanner.c, skip tree_sitter/
                .Where(s => cSourceExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                .ToList();

            if (!sourceFiles.Any())
            {
                return (PathError.HeadersNotFound(treeSitterIncludePath), null);
            }

            LanguageSourcePaths paths = new LanguageSourcePaths();
            paths.m_moduleName = languageRepoPath.Name.Replace('-', '_'); // can't have '-' in module name
            paths.m_repoPath = languageRepoPath.FullName;
            paths.m_sourcePath = treeSitterIncludePath;
            paths.m_sourceFiles = sourceFiles;

            return (PathError.Ok(), paths);
        }
    }
}
