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
    public class TreeSitterPaths
    {
        public static readonly string[] cHeaderExt = { "h", "hpp" };
        public static readonly string[] cLibraryExt = { "lib" };

        /// <summary>
        /// Either Ok or an Error indicating a problem with assembling the path.
        /// </summary>
        PathError m_pathError = PathError.Ok();
        string m_repoPath = "";
        string m_includePath = "";
        IEnumerable<string>? m_headerFiles = null;
        string? m_libraryPath = "";
        IEnumerable<string>? m_libraryFiles = null;

        public bool IsOk { get { return m_pathError != null && m_pathError.ErrorType == PathError.PathErrorType.Ok; } }
        public string RepoPath { get { return m_repoPath; } }
        public string IncludePath { get { return m_includePath; } }
        public IEnumerable<string> HeaderFiles { get { return m_headerFiles ?? Enumerable.Empty<string>(); } }
        public string LibraryPath { get { return m_libraryPath ?? ""; } }
        public IEnumerable<string> LibraryFiles { get { return m_libraryFiles ?? Enumerable.Empty<string>(); } }

        private static TreeSitterPaths FromPathError(PathError pathError)
        {
            var outError = new TreeSitterPaths();
            outError.m_pathError = pathError;
            return outError;
        }

        private static (PathError, string?) GetTreeSitterLibraryPathFromCargoBuiltOutput(string treeSitterRepoPath)
        {
            // tree-sitter/target
            const string RelativeTargetPath = @"\target";
            string TargetPath = Path.Join(treeSitterRepoPath.AsSpan(), RelativeTargetPath.AsSpan());
            if (!Directory.Exists(TargetPath))
            {
                return (PathError.DirectoryMissing(TargetPath), null);
            }

            // tree-sitter/target/release
            const string RelativeBuildTypePath = @"\release";
            string BuildTypePath = Path.Join(TargetPath.AsSpan(), RelativeBuildTypePath.AsSpan());
            if (!Directory.Exists(BuildTypePath))
            {
                return (PathError.DirectoryMissing(BuildTypePath), null);
            }

            // tree-sitter/target/release/build
            const string RelativeBuildPath = @"\build";
            string BuildPath = Path.Join(BuildTypePath.AsSpan(), RelativeBuildPath.AsSpan());
            if (!Directory.Exists(BuildPath))
            {
                return (PathError.DirectoryMissing(BuildPath), null);
            }

            // tree-sitter/target/build/tree-sitter-a9af0677696da2ca
            // tree-sitter/target/build/tree-sitter-b7f16b8f34ef9feb
            // search for a hashed directory names that start with 'tree-sitter'
            IEnumerable<string>? treeSitterBuildOutputDirectories = Directory.EnumerateDirectories(BuildPath, "*.*", SearchOption.AllDirectories).Where(dirPath =>
            {
                string? dirName = new DirectoryInfo(dirPath).Name;
                return dirName?.StartsWith("tree-sitter") ?? false;
            });

            // tree-sitter/target/build/tree-sitter-a9af0677696da2ca/out/tree-sitter.lib
            // tree-sitter/target/build/tree-sitter-a9af0677696da2ca/out/tree-sitter.a
            // search tree-sitter directories for files that start with 'tree-sitter'
            IEnumerable<string>? treeSitterNamedFiles = treeSitterBuildOutputDirectories.AsEnumerable()
                .SelectMany(dependencyDirectory =>
                {
                    var files = Directory.GetFiles(dependencyDirectory, "*.*", SearchOption.AllDirectories);
                    return files.Where(filePath => Path.GetFileName(filePath).StartsWith("tree-sitter"));
                });

            // tree-sitter/target/build/tree-sitter-a9af0677696da2ca/out/tree-sitter.lib
            // get the one and only 'tree-sitter.lib'
            string? treeSitterLibraryPath = treeSitterNamedFiles
                .Where(filePath => Path.GetFileName(filePath).Equals("tree-sitter.lib")).FirstOrDefault();

            // tree-sitter/target/build/tree-sitter-a9af0677696da2ca/out/
            // get the parent directory of 'tree-sitter.lib'
            string? treeSitterLibraryDir = Path.GetDirectoryName(treeSitterLibraryPath);

            if (treeSitterLibraryPath == null)
            {
                return (PathError.TreeSitterLibraryNotFound(BuildPath), null);
            }
            if (treeSitterLibraryDir == null)
            {
                return (PathError.LibrariesNotFound(BuildPath), null);
            }

            return (PathError.Ok(), treeSitterLibraryDir);
        }

        private static (PathError, string? libraryPath, List<string>? libraryFilenames) GetLibraryPathAndFilesFromCargoBuildOutput(string treeSitterRepoPath)
        {
            (PathError pathError, string? treeSitterLibraryPath) = GetTreeSitterLibraryPathFromCargoBuiltOutput(treeSitterRepoPath);

            if (!pathError.IsOk)
            {
                return (pathError, null, null);
            }
            Debug.Assert(pathError.IsOk && treeSitterLibraryPath != null);

            var libraryFiles =
                Directory.EnumerateFiles(treeSitterLibraryPath, "*.*", SearchOption.AllDirectories)
                .Where(filePath => cLibraryExt.Contains(Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant()))
                .Select(filePath => Path.GetFileName(filePath))
                .ToList();

            if (!libraryFiles.Any())
            {
                return (PathError.LibrariesNotFound(treeSitterLibraryPath), null, null);
            }
            else if (!libraryFiles.Any(file => Path.GetFileName(file).Equals("tree-sitter.lib")))
            {
                return (PathError.LibrariesNotFound(treeSitterLibraryPath), null, null);
            }

            return (PathError.Ok(), treeSitterLibraryPath, libraryFiles);
        }

        public static TreeSitterPaths AssemblePaths(string treeSitterRepoPath)
        {
            if (!Directory.Exists(treeSitterRepoPath))
            {
                return TreeSitterPaths.FromPathError(PathError.DirectoryMissing(treeSitterRepoPath));
            }

            string treeSitterIncludePath = "";
            {
                const string RelativeIncludePath = @"\lib\include\tree_sitter";
                string IncludePath = Path.Join(treeSitterRepoPath.AsSpan(), RelativeIncludePath.AsSpan());
                if (!Directory.Exists(IncludePath))
                {
                    return TreeSitterPaths.FromPathError(PathError.DirectoryMissing(IncludePath));
                }
                treeSitterIncludePath = IncludePath;
            }
            
            var headerFiles = Directory
                .EnumerateFiles(treeSitterIncludePath, "*.*", SearchOption.AllDirectories)
                .Where(s => cHeaderExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                .ToList();
            
            if (!headerFiles.Any())
            {
                return TreeSitterPaths.FromPathError(PathError.HeadersNotFound(treeSitterIncludePath));
            }

            (PathError pathError, string? libraryPath, List<string>? libraryFilenames) = GetLibraryPathAndFilesFromCargoBuildOutput(treeSitterRepoPath);

            libraryPath = "D:\\repo\\tree-sitter-bindings\\tree-sitter-csharp-bindings\\out\\libtree-sitter\\x64\\Debug";
            libraryFilenames = new List<string> { "tree-sitter.lib" };

            TreeSitterPaths paths = new TreeSitterPaths();
            paths.m_pathError = PathError.Ok();
            paths.m_repoPath = treeSitterRepoPath;
            paths.m_includePath = treeSitterIncludePath;
            paths.m_headerFiles = headerFiles;
            paths.m_libraryPath = libraryPath;
            paths.m_libraryFiles = libraryFilenames;

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
