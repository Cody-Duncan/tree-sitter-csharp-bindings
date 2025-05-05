# Archived

This repository has been archived, and is no longer maintained.

Use this <https://github.com/tree-sitter/csharp-tree-sitter>.  
That repository officially supports C# bindings to tree-sitter libraries.  


# tree-sitter-csharp-bindings

Generates C# Bindings for [Tree-Sitter](https://github.com/tree-sitter/tree-sitter).  
This uses the NuGet package [CppSharp](https://github.com/mono/CppSharp) to generate C# bindings.

Generates

- `tree_sitter.dll` - native C++ Windows DLL. Contains [Tree-Sitter's](https://github.com/tree-sitter/tree-sitter) C library code.
- `tree_sitter_<language>.dll` - native C++ Windows DLLs. Each one contains a tree-sitter grammar's C code for parsing.
- `TreeSitterBindings.dll` - .NET managed C# DLL. Exposes the C# API bindings.

These libraries can be linked into a C# .exe or .dll to run a tree-sitter parser with a given grammar.
By default, the example grammar is [tree-sitter-python](https://github.com/tree-sitter/tree-sitter-python).

## Prerequisites

- [Git](https://git-scm.com/) must be installed and accessible from the system PATH.
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
    - Visual C++ Modules
    - .NET 6.0 and C# Modules
- [powershell](https://learn.microsoft.com/en-us/powershell/) - used to execute `download_tree_sitter_repo.ps1` to clone [Tree-Sitter](https://github.com/tree-sitter/tree-sitter) into a `./tree-sitter` subdirectory.

## How to Build

run the `build.ps1` script
```pwsh
.\build.ps1
```

## Adding More Grammars

In `CMakeLists.txt` find the section labeled

```cmake
## START EDIT HERE 
...
## 1. ADD NEW GRAMMARS HERE
## 2. UPDATE GRAMMARS LIST
...
## END EDIT HERE
```

1. ADD NEW GRAMMARS HERE - Add a `FetchContent_Declare` for the new grammar, e.g. 'tree_sitter_ruby'.
```cmake
## ADD NEW GRAMMARS HERE

FetchContent_Declare(
  tree_sitter_ruby
  GIT_REPOSITORY https://github.com/tree-sitter/tree-sitter-ruby.git)
```

2. UPDATE GRAMMARS LIST - Add the declaration name ('tree_sitter_ruby') to the existing `TREE_SITTER_GRAMMARS_LIST`.
```cmake
set(TREE_SITTER_GRAMMARS_LIST tree_sitter_python tree_sitter_ruby)
```

## Outputs

The libraries are output to `./build/lib`

Libraries:

- `TreeSitterBindings.dll`
- `tree_sitter.dll`
- `tree_sitter_<language>.dll` for each language specified at the top of CMakeLists.txt

## Sample Code

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
            // This will just test whether the first grammar can compile.
            tree_sitter.TSLanguage language = tree_sitter_python.tree_sitter_python.TreeSitterPython();
            api.TsParserSetLanguage(m_parser, language);
        }

        public ParseTree Parse(string content)
        {
            var tree = api.TsParserParseString(m_parser, null, content, (uint)content.Length);
            return new ParseTree(tree);
        }
    }

    class Node
    {
        TSNode m_node;
        public Node(TSNode node)
        { 
            m_node = node; 
        }

        public string Type
        {
            get
            {
                return api.TsNodeType(m_node);
            }
        }

        public ushort Symbol
        {
            get
            {
                return api.TsNodeSymbol(m_node);
            }
        }
    }

    class ParseTree
    {
        TSTree m_tree;
        public ParseTree(TSTree tree)
        {
            m_tree = tree;
        }

        public Node Root 
        {
            get 
            { 
                var node = api.TsTreeRootNode(m_tree); ;
                return new Node(node);
            } 
        }
    }

    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            using (var parser = new Parser())
            {
                Console.WriteLine("Made a Parser");
                parser.SetLanguage();
                var tree = parser.Parse("print('Hello, world!')");
                if (tree == null)
                {
                    Console.WriteLine("Parsed Tree is NULL");
                }
                var node = tree.Root;
                Console.WriteLine($"Root Node's type is `{node.Type}`");
            }
            Console.WriteLine("Deleted a Parser");
            return 0;
        }
    }
}
```

See also the generated TreeSitterBindingsTest project, which compiles GeneratedCSharpTestProgram/Program.cs.
