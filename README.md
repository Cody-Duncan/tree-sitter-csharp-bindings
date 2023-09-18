# tree-sitter-csharp-bindings

Generates C# Bindings for [Tree-Sitter](https://github.com/tree-sitter/tree-sitter).  
This uses the nuget package for [CppSharp](https://github.com/mono/CppSharp) to generate C# bindings.

- `tree_sitter.dll` - native C++ Windows DLL. Contains [Tree-Sitter's](https://github.com/tree-sitter/tree-sitter) C library code.
- `tree-sitter-bindings.dll` - .NET managed C# DLL. Exposes the C# API bindings.
- ***$TODO*** generate `tree-sitter-<language>.dll` and `tree-sitter-<language>-bindings.dll` - Libraries that wrap a target language's grammar and parser. E.G. [tree-sitter-python](https://github.com/tree-sitter/tree-sitter-python) and [tree-sitter-c-sharp](https://github.com/tree-sitter/tree-sitter-c-sharp).

## Prerequisites

- [Git](https://git-scm.com/) must be installed and accessible from the system PATH.
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
    - Visual C++ Modules
    - .NET 6.0 and C# Modules
- [powershell](https://learn.microsoft.com/en-us/powershell/) - used to execute `download_tree_sitter_repo.ps1` to clone [Tree-Sitter](https://github.com/tree-sitter/tree-sitter) into a `./tree-sitter` subdirectory.

## How to Build

1. Open `tree-sitter-csharp-bindings.sln` in Visual Studio 2022
2. Build Solution
    - See output window for any errors

## Outputs

Into `./out/binding_libs`

`tree-sitter-bindings.dll` - .NET managed C# DLL. Exposes the C# API bindings and wraps all the pInvokes to call into the native library`tree_sitter.dll`. 
- tree-sitter-bindings.deps.json
- tree-sitter-bindings.dll
- tree-sitter-bindings.pdb

`tree_sitter.dll` - native C++ DLL. Contains [Tree-Sitter's](https://github.com/tree-sitter/tree-sitter) C library code.
- tree_sitter.dll
- tree_sitter.exp
- tree_sitter.lib
- tree_sitter.pdb

## How to use these DLLs

### Executables

1. Create a new C# Executable project
2. Reference `tree-sitter-bindings.dll` as a dependency.
3. `tree_sitter.dll` CANNOT be referenced as a dependency. This needs to be copied into the output executable's directory manually.
    - ***$TODO***: figure out how to fix this error. Happens when a C# project references `tree_sitter.dll` as a dependency: `"Error: Metadata file could not be opened -- PE image doesn't contain managed metadata"` 

***Workaround*** 

Add this to your .csproj file to copy `tree_sitter.dll` as a postbuild step.  
see `bindings-test.csproj` for an example:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    ...
    <TreeSitterCsharpBindingsDir>PATH_TO_tree-sitter-csharp-bindings_HERE</TreeSitterCsharpBindingsDir>
    <TreeSitterDllOutputDir>$(TreeSitterCsharpBindingsDir)\out\binding_libs\$(Platform)\$(Configuration)\net6.0</TreeSitterDllOutputDir>
  </PropertyGroup>

  ...

  <ItemGroup>
    <_LibTreeSitterBinaries Include="$(TreeSitterDllOutputDir)\*.*" />
  </ItemGroup>

  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Message Text="Copying tree-sitter-csharp-bindings Binaries from $(TreeSitterDllOutputDir) to project output directory $(ProjectDir)$(OutDir)" Importance="high" />
    <Copy SourceFiles="@(_LibTreeSitterBinaries)" DestinationFolder="$(ProjectDir)$(OutDir)" />
  </Target> 
</Project>
```

### Libraries

1. Create a new C# Library project
2. Reference `tree-sitter-bindings.dll` as a dependency.

Any executable projects that use this library will need to copy `tree_sitter.dll` into the .exe's directory. See `Executables` above for notes.


## Sample Code

- ***$TODO*** Better wrapper classes in tree-sitter-bindings.
- ***$TODO*** Better sample code.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tree_sitter;

namespace bindings_test
{
    class Parser : IDisposable
    {
        TSParser m_parser;

        public Parser()
        {
            m_parser = api.TsParserNew();
        }

        public void Dispose()
        {
            api.TsParserDelete(m_parser);
        }

        public void SetLanguage()
        {
            api.TsParserSetLanguage(m_parser, null);
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            using (var parser = new Parser())
            {
                Console.WriteLine("Made a Parser");
                parser.SetLanguage();
            }
            Console.WriteLine("Deleted a Parser");
            return 0;
        }
    }
}

```
