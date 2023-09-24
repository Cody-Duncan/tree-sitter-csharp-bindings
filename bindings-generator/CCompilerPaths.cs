using CppSharp.AST;
using CppSharp.Types.Std;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bindings_generator
{
    public class CCompilerPaths
    {
        

        /// <summary>
        /// Either Ok or an Error indicating a problem with assembling the path.
        /// </summary>
        PathError m_pathError = PathError.Ok();
        string m_moduleName = "";
        string m_repoPath = "";
        string m_includePath = "";
        IEnumerable<string>? m_headerFiles = null;
        string? m_libraryPath = "";
        IEnumerable<string>? m_libraryFiles = null;

        public bool IsOk { get { return m_pathError != null && m_pathError.ErrorType == PathError.PathErrorType.Ok; } }
        public string ModuleName { get { return m_moduleName; } }
        public string RepoPath { get { return m_repoPath; } }
        public string IncludePath { get { return m_includePath; } }
        public IEnumerable<string> HeaderFiles { get { return m_headerFiles ?? Enumerable.Empty<string>(); } }
        public string LibraryPath { get { return m_libraryPath ?? ""; } }
        public IEnumerable<string> LibraryFiles { get { return m_libraryFiles ?? Enumerable.Empty<string>(); } }

        private static CCompilerPaths FromPathError(PathError pathError)
        {
            var outError = new CCompilerPaths();
            outError.m_pathError = pathError;
            return outError;
        }

        public static CCompilerPaths AssembleTreeSitterPaths(DirectoryInfo treeSitterRepoPath)
        {
            if (!Directory.Exists(treeSitterRepoPath.FullName))
            {
                return CCompilerPaths.FromPathError(PathError.DirectoryMissing(treeSitterRepoPath.FullName));
            }

            string treeSitterIncludePath = "";
            {
                const string RelativeIncludePath = @"\lib\include\tree_sitter";
                string IncludePath = Path.Join(treeSitterRepoPath.FullName.AsSpan(), RelativeIncludePath.AsSpan());
                if (!Directory.Exists(IncludePath))
                {
                    return CCompilerPaths.FromPathError(PathError.DirectoryMissing(IncludePath));
                }
                treeSitterIncludePath = IncludePath;
            }
            
            var headerFiles = Directory
                .EnumerateFiles(treeSitterIncludePath, "*.*", SearchOption.AllDirectories)
                .Where(s => FileExtensions.cHeaderExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                .ToList();
            
            if (!headerFiles.Any())
            {
                return CCompilerPaths.FromPathError(PathError.HeadersNotFound(treeSitterIncludePath));
            }

            CCompilerPaths paths = new CCompilerPaths();
            paths.m_moduleName = treeSitterRepoPath.Name.Replace('-', '_'); // can't have '-' in module name
            paths.m_pathError = PathError.Ok();
            paths.m_repoPath = treeSitterRepoPath.FullName;
            paths.m_includePath = treeSitterIncludePath;
            paths.m_headerFiles = headerFiles;

            return paths;
        }

        public static CCompilerPaths AssembleLanguagePaths(DirectoryInfo languageRepoPath)
        {
            if (!Directory.Exists(languageRepoPath.FullName))
            {
                return CCompilerPaths.FromPathError(PathError.DirectoryMissing(languageRepoPath.FullName));
            }

            string treeSitterIncludePath = "";
            {
                const string RelativeIncludePath = @"\src\";
                string IncludePath = Path.Join(languageRepoPath.FullName.AsSpan(), RelativeIncludePath.AsSpan());
                if (!Directory.Exists(IncludePath))
                {
                    return CCompilerPaths.FromPathError(PathError.DirectoryMissing(IncludePath));
                }
                treeSitterIncludePath = IncludePath;
            }

            // This doesn't have any headers *yet*.
            // Need to generate them from the source files.
            var sourceFiles = Directory
                .EnumerateFiles(treeSitterIncludePath, "*.*", SearchOption.TopDirectoryOnly) // want only the parser.c and scanner.c, skip tree_sitter/
                .Where(s => FileExtensions.cSourceExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                .ToList();

            if (!sourceFiles.Any())
            {
                return CCompilerPaths.FromPathError(PathError.HeadersNotFound(treeSitterIncludePath));
            }

            CCompilerPaths paths = new CCompilerPaths();
            paths.m_moduleName = languageRepoPath.Name.Replace('-', '_'); // can't have '-' in module name
            paths.m_pathError = PathError.Ok();
            paths.m_repoPath = languageRepoPath.FullName;
            paths.m_includePath = treeSitterIncludePath;
            paths.m_headerFiles = sourceFiles;

            return paths;
        }

        public string GenerateErrorMessage()
        {
            string outError = "<UNKNOWN>";

            if (m_pathError == null)
            {
                outError = "ERROR: TreeSitterPaths.PathError was null.";
            }
            else
            {
                outError = m_pathError.GenerateErrorMessage();
            }

            return outError;
        }
    }
}
