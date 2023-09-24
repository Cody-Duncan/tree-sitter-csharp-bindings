using CppSharp;
using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;

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
        enum MainResult : int
        {
            Ok = 0,
            CLI_ArgumentError = 1,
            RepoPathsError = 2,
            FailedToCreate_OutputSubdirectory_FixedCIncludes = 3,
            EmptyIncludeFilepath = 4,
            BindingGenerationError = 5,
            ReposNotFound = 6,
            TreeSitterRepoNotFound = 7,
            AnyLanguageRepoNotFound = 8,
            ParserFileNotFound = 9,
            LanguageFunctionNotFound = 10,
        }

        internal class InputPaths
        {
            public InputPaths(DirectoryInfo reposPath) 
            {
                this.reposPath = reposPath;
            }

            public DirectoryInfo reposPath { get; init; }
        }

        internal class OutputPaths
        {
            public OutputPaths(DirectoryInfo cSharpBindingsOutputPath, DirectoryInfo fixedCIncludesOutputPath)
            {
                this.cSharpBindingsOutputPath = cSharpBindingsOutputPath;
                this.fixedCIncludesOutputPath = fixedCIncludesOutputPath;
            }

            // This specifies a subdirectory under the outputPath to put the generated csharp bindings.
            //
            // E.G. CLI argument outputPath = "C:\tree-sitter-csharp-bindings\out"
            //      bindingsOutputSubdirectory = "generated_csharp_bindings_source";
            //      Then the csharp bindings will be placed into  "C:\tree-sitter-csharp-bindings\out\generated_csharp_bindings_source"
            public DirectoryInfo cSharpBindingsOutputPath { get; init; }

            // This specifies a subdirectory under the outputPath to put the fixed C library include headers.
            // E.G. CLI argument outputPath = "C:\tree-sitter-csharp-bindings\out"
            //      fixedIncludesOutputSubdirectory = "generated_c_dll_headers";
            //      Then the fixed C library include headers will be placed into "C:\tree-sitter-csharp-bindings\out\generated_c_dll_headers"
            public DirectoryInfo fixedCIncludesOutputPath { get; init; }
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

        static MainResult GenerateTreeSitterBindings(DirectoryInfo treeSitterRepoPath, OutputPaths outputPaths)
        {
            Console.WriteLine($"START: Generating language bindings for {treeSitterRepoPath.Name}");

            // Search for the include paths, library paths, include files, library files, everything that a C linker might want.
            CCompilerPaths paths = CCompilerPaths.AssembleTreeSitterPaths(treeSitterRepoPath);
            if (!paths.IsOk)
            {
                // Failed to find the headers. Cannot generate bindings
                Console.WriteLine($"ERROR: problem with structure in the Tree-Sitter repo path:\n{paths.GenerateErrorMessage()}");
                return MainResult.RepoPathsError;
            }

            // GENERATE BINDINGS!
            {
                ConsoleDriver.Run(new CsharpBindingsGenerator(paths, outputPaths.cSharpBindingsOutputPath.FullName));

                // Fix the CSharp Bindings because there's a missing explicit cast that causes compilation error
                if (!Directory.Exists(outputPaths.cSharpBindingsOutputPath.FullName))
                {
                    Console.WriteLine($"ERROR: Failed to generate bindings into: \n{outputPaths.cSharpBindingsOutputPath.FullName}");
                    return MainResult.BindingGenerationError;
                }
                else
                {
                    Console.WriteLine($"Generated Bindings may be found in:\n{outputPaths.cSharpBindingsOutputPath.FullName}");
                }

                {
                    var outputCSharpFiles = Directory
                        .EnumerateFiles(outputPaths.cSharpBindingsOutputPath.FullName, "*.*", SearchOption.AllDirectories)
                        .Where(s => FileExtensions.csharpExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                        .ToList();
                    var outputFileNames = String.Join(',', outputCSharpFiles.Select(filepath => Path.GetFileName(filepath)));
                    Console.WriteLine($"Manually fixing a generation error on C# Bindings in:\n{outputFileNames} -> {outputPaths.cSharpBindingsOutputPath.FullName}");

                    CSharpBindingsFix.FixImplictCast(outputCSharpFiles);
                }
            }

            // Tree-Sitters C includes headers don't have the dllexport markup.
            // In order to compile a native .dll that the generated C# bindings can pInvoke, the API needs to be exported to the DLL.
            // Therefore, the includes need new markup.
            {
                string fixedIncludeModuleDir = Path.Join(outputPaths.fixedCIncludesOutputPath.FullName, treeSitterRepoPath.Name.Replace('-', '_'));

                var headFilenames = String.Join(',', paths.HeaderFiles.Select(filepath => Path.GetFileName(filepath)));
                Console.WriteLine($"Adding DLLExport to C header files and writing them to the output directory:\n{headFilenames} -> {fixedIncludeModuleDir}");

                // Create OutputSubdirectory_FixedCIncludes
                System.IO.Directory.CreateDirectory(fixedIncludeModuleDir);
                if (!Directory.Exists(fixedIncludeModuleDir))
                {
                    Console.WriteLine($"ERROR: Failed to create output directory for updated include headers at path: {fixedIncludeModuleDir}");
                    return MainResult.FailedToCreate_OutputSubdirectory_FixedCIncludes;
                }

                // Copy the C Header files from the Tree-Sitter repo into OutputSubdirectory_FixedCIncludes
                foreach (var includeFilepath in paths.HeaderFiles)
                {
                    if (String.IsNullOrEmpty(includeFilepath))
                    {
                        Console.WriteLine($"ERROR: Tried to copy an Include File, but path was null or empty.");
                        return MainResult.EmptyIncludeFilepath;
                    }
                    string filename = Path.GetFileName(includeFilepath);
                    string fixedIncludeFilePath = Path.Join(fixedIncludeModuleDir, filename);
                    File.Copy(includeFilepath, fixedIncludeFilePath, overwrite: true);
                }

                // Fix up the headers.
                // Adds `#define DllExport   __declspec( dllexport )` to the header
                // Applies DllExport to every function signature.
                {
                    var outputCHeaderFiles = Directory
                        .EnumerateFiles(fixedIncludeModuleDir, "*.*", SearchOption.AllDirectories)
                        .Where(s => FileExtensions.cHeaderExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
                        .ToList();

                    CHeaderDllExporter.AddDllExportToCAPI(outputCHeaderFiles);
                }
            }

            return MainResult.Ok;
        }

        static MainResult GenerateLanguageBindings(DirectoryInfo languageRepoPath, OutputPaths outputPaths)
        {
            Console.WriteLine($"START: Generating bindings for Language repo {languageRepoPath.Name}");

            (PathError result, LanguageSourcePaths? paths) = LanguageSourcePaths.AssembleLanguageSourcePaths(languageRepoPath);
            if (!result.IsOk)
            {
                // Failed to find the souce files. Cannot generate bindings
                Console.WriteLine($"ERROR: problem with structure in the repo path:\n{result.GenerateErrorMessage()}");
                return MainResult.RepoPathsError;
            }

            if (paths == null)
            {
                // this is likely a programming error...
                // This should NOT be null if result.IsOk
                Console.WriteLine($"ERROR: failed to generate language source paths from repo:\n{result.GenerateErrorMessage()}");
                return MainResult.RepoPathsError;
            }

            // Generate header files from these source files.
            // 
            // Tree-sitter's parser generates 2 files: parser.c, and scanner.c
            // No headers.
            // The symbols we want to bind are found in parser.c
            // E.G. at the bottom of tree-sitter-python/src/parser.c
            // ```cpp
            //      # ifdef __cplusplus
            //      extern "C" {
            //      #endif
            //      void* tree_sitter_python_external_scanner_create(void);
            //      void tree_sitter_python_external_scanner_destroy(void*);
            //      bool tree_sitter_python_external_scanner_scan(void*, TSLexer*, const bool*);
            //      unsigned tree_sitter_python_external_scanner_serialize(void*, char*);
            //      void tree_sitter_python_external_scanner_deserialize(void*, const char*, unsigned);
            //
            //      # ifdef _WIN32
            //      #define extern __declspec(dllexport)
            //      #endif
            //
            //      extern const TSLanguage* tree_sitter_python(void)
            //      {
            //          static const TSLanguage language = {
            //          // LOTS OF STUFF HERE
            //          }
            //          return &language;
            //      }
            //      # ifdef __cplusplus
            //      }   // END extern "C"
            //      #endif
            // ```
            //
            // Need to snip this portion out, and turn it into a header to generate bindings off of.

            // Generate header file
            {

                const string parserFileName = "parser.c";
                string? parserFile = Directory
                    .EnumerateFiles(paths.SourcePath, "*.c", SearchOption.TopDirectoryOnly)
                    .Where(s => Path.GetFileName(s).Equals(parserFileName)).FirstOrDefault();

                if (parserFile == null)
                {
                    Console.WriteLine($"ERROR: could not find {parserFileName} in repo source path:\n{paths.SourcePath}");
                    return MainResult.ParserFileNotFound;
                }

                const string headerStartMarker = "#ifdef __cplusplus";
                var parserText = File.ReadAllText(parserFile).ToString();

                int headerStartIndex = parserText.IndexOf(headerStartMarker);
                string headerCoreText = parserText.Substring(headerStartIndex);

                string expectedLanguageFuncName = $"tree_sitter{paths.ModuleName}()";

                Regex languageFuncStartRx = new Regex(@"extern const TSLanguage \*tree_sitter_(\w+)\(void\)");
                Match languageFuncStartMatch = languageFuncStartRx.Match(headerCoreText);
                if (!languageFuncStartMatch.Success)
                {
                    Console.WriteLine($"ERROR: could not find {expectedLanguageFuncName} function in parser file:\n{parserFile}");
                    return MainResult.LanguageFunctionNotFound;
                }

                Regex languageFuncEndRx = new Regex(@"return &language;\r?\n}");
                Match languageFuncEndMatch = languageFuncEndRx.Match(headerCoreText);
                if (!languageFuncEndMatch.Success)
                {
                    Console.WriteLine($"ERROR: could not find the end of {expectedLanguageFuncName} function in parser file:\n{parserFile}");
                    return MainResult.LanguageFunctionNotFound;
                }

                // Snip out the entire language function
                int languageFuncStart = languageFuncStartMatch.Index;
                int languageFuncEnd = (languageFuncEndMatch.Index + languageFuncEndMatch.Length); // make sure to capture the 'return &language;\r\n}' at the end
                int languageFuncLength = languageFuncEnd - languageFuncStart;
                var headerCoreTextSnipped = headerCoreText.Remove(languageFuncStart, languageFuncLength);

                // put the language function declaration back in: 'extern const TSLanguage *tree_sitter_SOMETHING(void)' + ;'
                char firstNewlineChar = headerCoreTextSnipped.First(c => c == '\r' || c == '\n');
                string newLine = (firstNewlineChar == '\r') ? "\r\n" : "\n";
                var headerCoreTextWithLanguageFunction = headerCoreTextSnipped.Insert(languageFuncStartMatch.Index, languageFuncStartMatch.Value + ";" + newLine);
                
                // add a couple of forward declarations for parameter types, so it can be succesfully parsed
                const string TSLexerDeclaration = "typedef struct TSLexer TSLexer;";
                const string TSLanguageDeclaration = "typedef struct TSLanguage TSLanguage;";
                string relativeSourcePath = Path.GetRelativePath(paths.RepoPath, parserFile);
                string sourceString = $"//Generated from {languageRepoPath.Name}/{relativeSourcePath}".Replace('\\', '/');
                var headerCoreTextFinal = headerCoreTextWithLanguageFunction.Insert(0, $"{sourceString}{newLine}{newLine}{TSLexerDeclaration}{newLine}{TSLanguageDeclaration}{newLine}{newLine}");

                // write out the parser header
                {
                    string languageCModuleName = languageRepoPath.Name.Replace('-', '_');

                    string generatedLanguageParserIncludesModuleDir = Path.Join(outputPaths.fixedCIncludesOutputPath.FullName, languageCModuleName);

                    // Create OutputSubdirectory_FixedCIncludes
                    System.IO.Directory.CreateDirectory(generatedLanguageParserIncludesModuleDir);
                    if (!Directory.Exists(generatedLanguageParserIncludesModuleDir))
                    {
                        Console.WriteLine($"ERROR: Failed to create output directory for generate parser include headers at path: {generatedLanguageParserIncludesModuleDir}");
                        return MainResult.FailedToCreate_OutputSubdirectory_FixedCIncludes;
                    }

                    string generatedLanguageParserHeaderName = $"{languageCModuleName}.h";
                    string generatedLanguageParserHeaderPath= Path.Join(generatedLanguageParserIncludesModuleDir, generatedLanguageParserHeaderName);

                    Console.WriteLine($"Generating parser header for {languageRepoPath.Name} to {generatedLanguageParserHeaderPath}");

                    File.WriteAllText(generatedLanguageParserHeaderPath, headerCoreTextFinal,Encoding.UTF8);
                }
            }

            // GENERATE BINDINGS!
            //{
            //    string bindingsPath = Path.Join(outputPath.FullName, OutputSubdirectory_CSharpBindings);
            //    ConsoleDriver.Run(new CsharpBindingsGenerator(paths, bindingsPath));

            //    // Fix the CSharp Bindings because there's a missing explicit cast that causes compilation error
            //    if (!Directory.Exists(bindingsPath))
            //    {
            //        Console.WriteLine($"ERROR: Failed to generate bindings into: \n{bindingsPath}");
            //        return MainResult.BindingGenerationError;
            //    }
            //    else
            //    {
            //        Console.WriteLine($"Generated Bindings may be found in:\n{outputPath.FullName}");
            //    }

            //    {
            //        var outputCSharpFiles = Directory
            //            .EnumerateFiles(bindingsPath, "*.*", SearchOption.AllDirectories)
            //            .Where(s => csharpExt.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()))
            //            .ToList();
            //        var outputFileNames = String.Join(',', outputCSharpFiles.Select(filepath => Path.GetFileName(filepath)));
            //        Console.WriteLine($"Manually fixing a generation error on C# Bindings in:\n{outputFileNames} -> {bindingsPath}");

            //        CSharpBindingsFix.FixImplictCast(outputCSharpFiles);
            //    }
            //}

            Console.WriteLine($"SUCCESS: Finished generating bindings for {languageRepoPath.Name}");
            return MainResult.Ok;
        }

        static MainResult GenerateBindings(InputPaths inputPaths, OutputPaths outputPaths)
        {
            var repoPaths = Directory.EnumerateDirectories(inputPaths.reposPath.FullName).Select(path => new DirectoryInfo(path)).ToList();

            if (!repoPaths.Any())
            {
                Console.WriteLine($"ERROR: Failed to find any tree-sitter repositores at: \n{inputPaths.reposPath.FullName}");
                return MainResult.TreeSitterRepoNotFound;
            }

            const string treeSitterRepoName = "tree-sitter";
            var treeSitterRepoDirectory = repoPaths.Find(dir => dir.Name == treeSitterRepoName);
            if (treeSitterRepoDirectory == null)
            {
                Console.WriteLine($"ERROR: tree-sitter repository not found at: \n{inputPaths.reposPath.FullName}");
                return MainResult.TreeSitterRepoNotFound;
            }

            var languageDirectories = repoPaths.Where(dir => dir.Name != treeSitterRepoName).ToList();
            if (!languageDirectories.Any())
            {
                Console.WriteLine($"ERROR: language repositories (e.g. tree-sitter-python) not found at: \n{inputPaths.reposPath.FullName}");
                return MainResult.AnyLanguageRepoNotFound;
            }

            var treeSitterBindingrResult = GenerateTreeSitterBindings(treeSitterRepoDirectory, outputPaths);
            if (treeSitterBindingrResult != MainResult.Ok)
            {
                return treeSitterBindingrResult;
            }

            foreach( var languageDirectory in languageDirectories)
            {
                var languageBindingResult = GenerateLanguageBindings(languageDirectory, outputPaths);
                if (languageBindingResult != MainResult.Ok)
                {
                    return languageBindingResult;
                }
            }

            return MainResult.Ok;
        }

        static int Main(string[] args)
        {
            var inputTreeSitterReposPath = new Option<DirectoryInfo>(
                name: "--TreeSitterReposPath",
                description: "The directory where the tree-sitter repository and any tree-sitter-<language> repositories have been downloaded.");
            inputTreeSitterReposPath.IsRequired = true;

            var cSharpBindingsOutputPath = new Option<DirectoryInfo>(
                name: "--CSharpBindingsOutputPath",
                description: "The directory where the generated C# bindings and fixed C headers will be written.");
            cSharpBindingsOutputPath.IsRequired = true;

            var fixedCIncludesIncludePath = new Option<DirectoryInfo>(
                name: "--CIncludesIncludeOutputPath",
                description: "The directory where the generated C# bindings and fixed C headers will be written.");
            fixedCIncludesIncludePath.IsRequired = true;

            var rootCommand = new RootCommand("Generates C# Bindings for Tree-Sitter library and Tree-Sitter Language Grammar libraries.");
            rootCommand.AddOption(inputTreeSitterReposPath);
            rootCommand.AddOption(cSharpBindingsOutputPath);
            rootCommand.AddOption(fixedCIncludesIncludePath);

            rootCommand.SetHandler((treeSitterReposPath, cSharpBindingsOutputPath, fixedCIncludesIncludePath) =>
            {
                InputPaths input = new InputPaths(treeSitterReposPath);
                OutputPaths output = new OutputPaths(cSharpBindingsOutputPath, fixedCIncludesIncludePath);
                GenerateBindings(input, output);
            },
            inputTreeSitterReposPath, cSharpBindingsOutputPath, fixedCIncludesIncludePath);

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
