using CppSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace bindings_generator
{
    public enum ArgumentsResult
    {
        Ok,
        Error
    }

    public struct Arguments
    {
        public string treeSitterRepoPath;
        public string outputPath;

        public static (ArgumentsResult, Arguments?) ErrorOutput() { return (ArgumentsResult.Error, null); }
    }

    class Program
    {
        // This specifies a subdirectory under the outputPath to put the generated csharp bindings.
        // E.G. CLI argument outputPath = "C:\tree-sitter-csharp-bindings\out"
        //      bindingsOutputSubdirectory = "csharp_bindings";
        //      Then the csharp bindings will be placed into  "C:\tree-sitter-csharp-bindings\out\csharp_bindings"
        const string OutputSubdirectory_CSharpBindings = "csharp_bindings";

        // This specifies a subdirectory under the outputPath to put the fixed C library include headers.
        // E.G. CLI argument outputPath = "C:\tree-sitter-csharp-bindings\out"
        //      fixedIncludesOutputSubdirectory = "tree_sitter_dll_includes";
        //      Then the fixed C library include headers will be placed into "C:\tree-sitter-csharp-bindings\out\tree_sitter_dll_includes"
        const string OutputSubdirectory_FixedCIncludes = "tree_sitter_dll_includes\\tree_sitter";

        public static readonly string[] csharpExt = { "cs" };

        enum MainResult : int
        {
            Ok = 0,
            CLI_ArgumentError = 1,
            RepoPathsError = 2,
            FailedToCreate_OutputSubdirectory_FixedCIncludes = 3,
            EmptyIncludeFilepath = 4,
            BindingGenerationError = 5,
        }

        static (ArgumentsResult, Arguments?) getCommandLineArguments(string[] args)
        {
            string? treeSitterRepoPath;
            string? outputPath;

            if (args.Length == 0)
            {
                Console.WriteLine($"Must specify a path for the tree-sitter repository (arg 0).\nE.G. `{System.AppDomain.CurrentDomain.FriendlyName}.exe <PATH/TO/tree-sitter> <OUTPUT/BINDINGS/PATH>`");
                return Arguments.ErrorOutput();
            }
            if (args.Length == 1)
            {
                Console.WriteLine($"Must specify a path to output the generated bindings (arg 1).\nE.G. `{System.AppDomain.CurrentDomain.FriendlyName}.exe <PATH/TO/tree-sitter> <OUTPUT/BINDINGS/PATH>`");
                return Arguments.ErrorOutput();
            }

            {
                // Check that the tree-sitter repository exists:
                string treeSitterPathArg = Path.GetFullPath(args[0]);
                Console.WriteLine($"Checking if repository exists at {treeSitterPathArg}");
                if (!Directory.Exists(treeSitterPathArg))
                {
                    Console.WriteLine($"Tree-Sitter repository path does not exist. Path: {treeSitterPathArg}");
                    return Arguments.ErrorOutput();
                }
                else
                {
                    treeSitterRepoPath = treeSitterPathArg;
                }
            }

            {
                // User-Provided output path
                string argPath = args[1];
                bool isRelativePath = !Path.IsPathRooted(argPath); // <- this isn't a good test of relative or absolute paths. E.G. /output/ is considered absolute.
                if (isRelativePath)
                {
                    outputPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(), argPath));
                    Console.WriteLine($"Converting relative path {argPath} to absolute path {outputPath}.");
                }
                else
                {
                    Console.WriteLine($"Output directory is absolute: {argPath}");
                    outputPath = Path.GetFullPath(argPath);
                }

                Console.WriteLine($"Output path specified as {outputPath}");
            }

            return (ArgumentsResult.Ok, new Arguments() { outputPath = outputPath, treeSitterRepoPath = treeSitterRepoPath });
        }

        static int Main(string[] args)
        {
            (ArgumentsResult result, Arguments? outStructArgs) = getCommandLineArguments(args);

            // `outStructArgs != null assured by `result == ArgumentsResult.Ok`
            // but the redundant statement allows the compiler to prove that structArgs is non-null
            if (result != ArgumentsResult.Ok || outStructArgs == null)
            {
                Console.WriteLine($"ERROR: Invalid command line argument.");
                return (int)MainResult.CLI_ArgumentError;
            }

            // The arguments are good. Know where the tree-sitter repository is and where to output the generated bindings.
            Arguments structArgs = outStructArgs.Value;

            // Search for the include paths, library paths, include files, library files, everything that a C linker might want.
            TreeSitterPaths paths = TreeSitterPaths.AssemblePaths(structArgs.treeSitterRepoPath);
            if (!paths.IsOk)
            {
                // Failed to find the headers. Cannot generate bindings
                Console.WriteLine($"ERROR: problem with structure in the Tree-Sitter repo path:\n{paths.GenerateErrorMessage()}");
                return (int)MainResult.RepoPathsError;
            }

            // GENERATE BINDINGS!
            {
                string bindingsPath = Path.Join(structArgs.outputPath, OutputSubdirectory_CSharpBindings);
                ConsoleDriver.Run(new CsharpBindingsGenerator(paths, bindingsPath));
                

                // Fix the CSharp Bindings because there's a missing explicit cast that causes compilation error
                if (!Directory.Exists(bindingsPath))
                {
                    Console.WriteLine($"ERROR: Failed to generate bindings into: \n{bindingsPath}");
                    return (int)MainResult.BindingGenerationError;
                }
                else
                {
                    Console.WriteLine($"Generated Bindings may be found in:\n{structArgs.outputPath}");
                }

                {
                    var outputCSharpFiles = Directory
                        .EnumerateFiles(bindingsPath, "*.*", SearchOption.AllDirectories)
                        .Where(s => csharpExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                        .ToList();

                    var outputFileNames = outputCSharpFiles.Select(filepath => Path.GetFileName(filepath));
                    Console.WriteLine($"Manually fixing a generation error on C# Bindings in:\n{bindingsPath} -> {outputFileNames}");
                    CSharpBindingsFix.FixImplictCast(outputCSharpFiles);
                }
            }

            // Tree-Sitters C includes headers don't have the dllexport markup.
            // In order to compile a native .dll that the generated C# bindings can pInvoke, the API needs to be exported to the DLL.
            // Therefore, the includes need new markup.
            {
                // Create OutputSubdirectory_FixedCIncludes
                string fixedIncludesDir = Path.Join(structArgs.outputPath, OutputSubdirectory_FixedCIncludes);
                System.IO.Directory.CreateDirectory(fixedIncludesDir);
                if (!Directory.Exists(fixedIncludesDir))
                {
                    Console.WriteLine($"ERROR: Failed to create output directory for updated include headers at path: {fixedIncludesDir}");
                    return (int)MainResult.FailedToCreate_OutputSubdirectory_FixedCIncludes;
                }

                // Copy the C Header files from the Tree-Sitter repo into OutputSubdirectory_FixedCIncludes
                foreach (var includeFilepath in paths.HeaderFiles)
                {
                    if (String.IsNullOrEmpty(includeFilepath))
                    {
                        Console.WriteLine($"ERROR: Tried to copy an Include File, but path was null or empty.");
                        return (int)MainResult.EmptyIncludeFilepath;
                    }
                    string filename = Path.GetFileName(includeFilepath);
                    string fixedIncludeFilePath = Path.Join(fixedIncludesDir, filename);
                    File.Copy(includeFilepath, fixedIncludeFilePath, overwrite: true);
                }

                // Fix up the headers.
                // Adds `#define DllExport   __declspec( dllexport )` to the header
                // Applies DllExport to every function signature.
                {
                    var outputCHeaderFiles = Directory
                        .EnumerateFiles(fixedIncludesDir, "*.*", SearchOption.AllDirectories)
                        .Where(s => TreeSitterPaths.cHeaderExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                        .ToList();

                    CHeaderDllExporter.AddDllExportToCAPI(outputCHeaderFiles);
                }
            }

            Console.WriteLine($"SUCCESS: Finished generating bindings");
            return (int) MainResult.Ok;
        }
    }
}
