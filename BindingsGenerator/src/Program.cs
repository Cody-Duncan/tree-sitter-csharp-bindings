using CppSharp;
using CppSharp.Types.Std;
using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;
using static bindings_generator.Program;

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
            FailedToCreate_OutputSubdirectory_TestProgram = 11,
        }

        internal class InputPaths
        {
            public InputPaths(DirectoryInfo reposPath, List<DirectoryInfo> treeSitterGrammarsPaths) 
            {
                this.treeSitterRepoPath = reposPath;
                this.treeSitterGrammarsPaths = treeSitterGrammarsPaths;
            }

            public DirectoryInfo treeSitterRepoPath { get; init; }
            public List<DirectoryInfo> treeSitterGrammarsPaths { get; init; }
        }

        internal class OutputPaths
        {
            public OutputPaths(DirectoryInfo cSharpBindingsOutputPath, DirectoryInfo fixedCIncludesOutputPath, DirectoryInfo? cSharpTestProgramOutputPath)
            {
                this.cSharpBindingsOutputPath = cSharpBindingsOutputPath;
                this.fixedCIncludesOutputPath = fixedCIncludesOutputPath;
                this.cSharpTestProgramOutputPath = cSharpTestProgramOutputPath;
            }

            // This specifies a directory to put the generated csharp bindings source files.
            public DirectoryInfo cSharpBindingsOutputPath { get; init; }

            // This specifies a directory to put the C library header source files.
            public DirectoryInfo fixedCIncludesOutputPath { get; init; }

            // (OPTIONAL) This specifies a directory to put the generated test program source file.
            public DirectoryInfo? cSharpTestProgramOutputPath { get; init; }
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
                    var outputFileNames = string.Join(',', outputCSharpFiles.Select(filepath => Path.GetFileName(filepath)));
                    Console.WriteLine($"Manually fixing a generation error on C# Bindings in:\n{outputFileNames} -> {outputPaths.cSharpBindingsOutputPath.FullName}");

                    CSharpBindingsFix.FixImplictCast(outputCSharpFiles);
                }
            }

            // Tree-Sitters C includes headers don't have the dllexport markup.
            // In order to compile a native .dll that the generated C# bindings can pInvoke, the API needs to be exported to the DLL.
            // Therefore, the includes need new markup.
            {
                string moduleName = LanguageSourcePaths.GetModuleNameFromRepoPath(treeSitterRepoPath);
                string fixedIncludeModuleDir = Path.Join(outputPaths.fixedCIncludesOutputPath.FullName, moduleName);

                var headFilenames = string.Join(',', paths.HeaderFiles.Select(filepath => Path.GetFileName(filepath)));
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
                    if (string.IsNullOrEmpty(includeFilepath))
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

        static (MainResult, string? HeaderOutputPath) GenerateHeaderForLanguage(LanguageSourcePaths languagePaths, OutputPaths outputPaths)
        {
            const string parserFileName = "parser.c";
            string? parserFile = Directory
                .EnumerateFiles(languagePaths.SourcePath, "*.c", SearchOption.TopDirectoryOnly)
                .Where(s => Path.GetFileName(s).Equals(parserFileName)).FirstOrDefault();

            if (parserFile == null)
            {
                Console.WriteLine($"ERROR: could not find {parserFileName} in repo source path:\n{languagePaths.SourcePath}");
                return (MainResult.ParserFileNotFound, null);
            }

            const string headerStartMarker = "#ifdef __cplusplus";
            var parserText = File.ReadAllText(parserFile).ToString();

            int headerStartIndex = parserText.IndexOf(headerStartMarker);
            string headerCoreText = parserText.Substring(headerStartIndex);

            string expectedLanguageFuncName = $"tree_sitter{languagePaths.ModuleName}()";

            Regex languageFuncStartRx = new Regex(@"extern const TSLanguage \*tree_sitter_(\w+)\(void\)");
            Match languageFuncStartMatch = languageFuncStartRx.Match(headerCoreText);
            if (!languageFuncStartMatch.Success)
            {
                Console.WriteLine($"ERROR: could not find {expectedLanguageFuncName} function in parser file:\n{parserFile}");
                return (MainResult.LanguageFunctionNotFound, null);
            }

            Regex languageFuncEndRx = new Regex(@"return &language;\r?\n}");
            Match languageFuncEndMatch = languageFuncEndRx.Match(headerCoreText);
            if (!languageFuncEndMatch.Success)
            {
                Console.WriteLine($"ERROR: could not find the end of {expectedLanguageFuncName} function in parser file:\n{parserFile}");
                return (MainResult.LanguageFunctionNotFound, null);
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
            string relativeSourcePath = Path.GetRelativePath(languagePaths.RepoPath, parserFile);
            string sourceString = $"//Generated from {languagePaths.SourcePath}".Replace('\\', '/');
            var headerCoreTextFinal = headerCoreTextWithLanguageFunction.Insert(0, $"{sourceString}{newLine}{newLine}{TSLexerDeclaration}{newLine}{TSLanguageDeclaration}{newLine}{newLine}");

            // write out the parser header
            string HeaderOutputDir = Path.Join(outputPaths.fixedCIncludesOutputPath.FullName, languagePaths.ModuleName);
            string OutputFilepath = Path.Join(HeaderOutputDir, $"{languagePaths.ModuleName}.h");

            // Create OutputSubdirectory_FixedCIncludes
            System.IO.Directory.CreateDirectory(HeaderOutputDir);
            if (!Directory.Exists(HeaderOutputDir))
            {
                Console.WriteLine($"ERROR: Failed to create output directory for generate parser include headers at path: {HeaderOutputDir}");
                return (MainResult.FailedToCreate_OutputSubdirectory_FixedCIncludes, null);
            }

            Console.WriteLine($"Generating parser header for {languagePaths.ModuleName} to {OutputFilepath}");
            File.WriteAllText(OutputFilepath, headerCoreTextFinal, Encoding.UTF8);

            return (MainResult.Ok, OutputFilepath);
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
            //
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
            // Need to copy the function signatures from parser.c, and turn them into a header that exports to a Dll.
            // Then the generted C# bindings can bind to those exported symbols.
            (MainResult generateHeaderResult, string? headerOutputPath) = GenerateHeaderForLanguage(paths, outputPaths);
            if (generateHeaderResult != MainResult.Ok || headerOutputPath == null)
            {
                return generateHeaderResult;
            }

            // GENERATE BINDINGS!
            {
                GenerateLanguageCSharpBinding.Generate(paths.ModuleName, outputPaths.cSharpBindingsOutputPath.FullName);
            }

            Console.WriteLine($"SUCCESS: Finished generating bindings for {languageRepoPath.Name}");
            return MainResult.Ok;
        }

        static string? GetModuleNameOfGrammar(DirectoryInfo grammarPath)
        {
            (PathError result, LanguageSourcePaths? paths) = LanguageSourcePaths.AssembleLanguageSourcePaths(grammarPath);

            if (!result.IsOk)
            {
                return null;
            }

            if (paths == null)
            {
                return null;
            }

            return paths.ModuleName;
        }

        static MainResult GenerateTestProgram(InputPaths inputPaths, OutputPaths outputPaths)
        {
            if (inputPaths.treeSitterGrammarsPaths.Any() && outputPaths.cSharpTestProgramOutputPath != null)
            {
                // Create the output directory, if it doesn't exist
                if (!outputPaths.cSharpTestProgramOutputPath.Exists)
                {
                    DirectoryInfo createdDir = System.IO.Directory.CreateDirectory(outputPaths.cSharpTestProgramOutputPath.FullName);
                    if (!createdDir.Exists)
                    {
                        Console.WriteLine($"ERROR: Failed to create output directory for generate test program at path: {outputPaths.cSharpTestProgramOutputPath}");
                        return MainResult.FailedToCreate_OutputSubdirectory_TestProgram;
                    }
                }

                DirectoryInfo firstGrammarPath = inputPaths.treeSitterGrammarsPaths.First();
                string? moduleName = GetModuleNameOfGrammar(firstGrammarPath);
                if (moduleName == null)
                {
                    Console.WriteLine($"ERROR: Failed to get module name from path: {firstGrammarPath}");
                    return MainResult.RepoPathsError;
                }

                GenerateBindingsTestProgram.Generate(moduleName, outputPaths.cSharpTestProgramOutputPath.FullName);
            }

            return MainResult.Ok;
        }

        static MainResult GenerateBindings(InputPaths inputPaths, OutputPaths outputPaths)
        {
            if (!inputPaths.treeSitterRepoPath.Exists)
            {
                Console.WriteLine($"ERROR: Failed to find the tree-sitter repository at: \n{inputPaths.treeSitterRepoPath.FullName}");
                return MainResult.TreeSitterRepoNotFound;
            }

            if (!inputPaths.treeSitterGrammarsPaths.Any())
            {
                Console.WriteLine($"ERROR: language repositories (e.g. tree-sitter-python) not found at: \n{inputPaths.treeSitterGrammarsPaths}");
                return MainResult.AnyLanguageRepoNotFound;
            }

            var treeSitterBindingrResult = GenerateTreeSitterBindings(inputPaths.treeSitterRepoPath, outputPaths);
            if (treeSitterBindingrResult != MainResult.Ok)
            {
                return treeSitterBindingrResult;
            }

            foreach(var languageDirectory in inputPaths.treeSitterGrammarsPaths)
            {
                var languageBindingResult = GenerateLanguageBindings(languageDirectory, outputPaths);
                if (languageBindingResult != MainResult.Ok)
                {
                    return languageBindingResult;
                }
            }

            var testProgramResult = GenerateTestProgram(inputPaths, outputPaths);
            if (testProgramResult != MainResult.Ok)
            {
                return testProgramResult;
            }

            return MainResult.Ok;
        }

        static int Main(string[] args)
        {
            var inputTreeSitterReposPath = new Option<DirectoryInfo>(
                name: "--TreeSitterRepoPath",
                description: "The directory where the tree-sitter repository has been downloaded");
            inputTreeSitterReposPath.IsRequired = true;

            var inputTreeSitterGrammarPaths = new Option<List<DirectoryInfo>>(
                name: "--TreeSitterGrammarPaths",
                description: "The directories where tree-sitter-<language> grammar repositories have been downloaded");
            inputTreeSitterGrammarPaths.IsRequired = true;

            var cSharpBindingsOutputPath = new Option<DirectoryInfo>(
                name: "--CSharpBindingsOutputPath",
                description: "The directory where the generated C# bindings will be written.");
            cSharpBindingsOutputPath.IsRequired = true;

            var cGeneratedHeadersOutputPath = new Option<DirectoryInfo>(
                name: "--CGeneratedHeadersOutputPath",
                description: "The directory where the generated C headers will be written.");
            cGeneratedHeadersOutputPath.IsRequired = true;

            var cSharpTestProgramOutputPath = new Option<DirectoryInfo>(
                name: "--CSharpTestProgramOutputPath",
                description: "The directory where the source file for a test program that uses the bindings will be written.");
            cSharpTestProgramOutputPath.IsRequired = false;

            var rootCommand = new RootCommand("Generates C# Bindings for Tree-Sitter library and Tree-Sitter Language Grammar libraries.");
            rootCommand.AddOption(inputTreeSitterReposPath);
            rootCommand.AddOption(inputTreeSitterGrammarPaths);
            rootCommand.AddOption(cSharpBindingsOutputPath);
            rootCommand.AddOption(cGeneratedHeadersOutputPath);
            rootCommand.AddOption(cSharpTestProgramOutputPath);

            rootCommand.SetHandler((inTreeSitterReposPath, intTreeSitterGrammarsPaths, inCSharpBindingsOutputPath, inCGeneratedHeadersOutputPath, inCSharpTestProgramOutputPath) =>
            {
                Console.WriteLine(inTreeSitterReposPath.FullName);
                InputPaths input = new InputPaths(inTreeSitterReposPath, intTreeSitterGrammarsPaths);
                OutputPaths output = new OutputPaths(inCSharpBindingsOutputPath, inCGeneratedHeadersOutputPath, inCSharpTestProgramOutputPath);
                GenerateBindings(input, output);
            },
            inputTreeSitterReposPath,
            inputTreeSitterGrammarPaths,
            cSharpBindingsOutputPath,
            cGeneratedHeadersOutputPath,
            cSharpTestProgramOutputPath);

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
