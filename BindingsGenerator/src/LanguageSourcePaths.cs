using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bindings_generator
{
    internal class LanguageSourcePaths
    {
        string m_moduleName = "";
        string m_repoPath = "";
        string m_sourcePath = "";
        IEnumerable<string> m_sourceFiles = Enumerable.Empty<string>();

        /// <summary>
        /// The name of the language Grammar module in lower_snake_case. 
        /// E.G. tree-sitter-python from <https://github.com/tree-sitter/tree-sitter-python> would have ModuleName="tree_sitter_python"
        /// </summary>
        public string ModuleName { get { return m_moduleName; } }

        /// <summary>
        /// Path to the directory the repoistory is stored.
        /// </summary>
        public string RepoPath { get { return m_repoPath; } }

        /// <summary>
        /// Path to the directory that the source files in teh repository are stored.
        /// </summary>
        public string SourcePath { get { return m_sourcePath; } }

        /// <summary>
        /// Paths to each source file (.c, .cc) in the repository.
        /// </summary>
        public IEnumerable<string> SourceFiles { get { return m_sourceFiles; } }

        public static string TrimEnd(string inputText, string value, StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase)
        {
            if (!string.IsNullOrEmpty(value))
            {
                while (!string.IsNullOrEmpty(inputText) && inputText.EndsWith(value, comparisonType))
                {
                    inputText = inputText[0..(inputText.Length - value.Length)];
                }
            }

            return inputText;
        }

        public static string GetModuleNameFromRepoPath(DirectoryInfo repoPath)
        {
            return TrimEnd(repoPath.Name, "-src").Replace('-', '_');
        }

        public static (PathError, LanguageSourcePaths?) AssembleLanguageSourcePaths(DirectoryInfo languageRepoPath)
        {
            if (!Directory.Exists(languageRepoPath.FullName))
            {
                return (PathError.DirectoryMissing(languageRepoPath.FullName), null);
            }

            // find the /src directory. Going to generate headers from them.
            string sourcePath = "";
            {
                const string RelativeSrcPath = @"\src\";
                string repoSrcPath = Path.Join(languageRepoPath.FullName.AsSpan(), RelativeSrcPath.AsSpan());
                if (!Directory.Exists(repoSrcPath))
                {
                    return (PathError.DirectoryMissing(repoSrcPath), null);
                }
                sourcePath = repoSrcPath;
            }

            // This doesn't have any headers *yet*.
            // Need to generate them from the source files.
            var sourceFiles = Directory
                .EnumerateFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly) // want only the parser.c and scanner.c, skip tree_sitter/
                .Where(s => FileExtensions.cSourceExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                .ToList();

            if (!sourceFiles.Any())
            {
                return (PathError.SourceFilesNotFound(sourcePath), null);
            }

            LanguageSourcePaths paths = new LanguageSourcePaths();

            // cmake will store the repo in a directory like 'tree_sitter_python-src'.
            // git would store the repo in a directory like 'tree-sitter-python' by default.
            // Trim off the '-src', convert all the dashes to underscores -> 'tree_sitter_python'
            paths.m_moduleName = LanguageSourcePaths.GetModuleNameFromRepoPath(languageRepoPath);
            paths.m_repoPath = languageRepoPath.FullName;
            paths.m_sourcePath = sourcePath;
            paths.m_sourceFiles = sourceFiles;

            return (PathError.Ok(), paths);
        }
    }
}
