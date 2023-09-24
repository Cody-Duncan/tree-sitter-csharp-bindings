# bindings-generator

This is the foundation of generating the bindings for [Tree-Sitter](https://github.com/tree-sitter/tree-sitter).

This uses the nuget package for [CppSharp](https://github.com/mono/CppSharp) to generate C# bindings from Tree-Sitter's C Library.

## Prebuild

Runs `download_tree_sitter_repo.ps1` to downlod a copy of the tree sitter repository into `$(SolutionDir)/tree-sitter`
```
powershell.exe -ExecutionPolicy Bypass -NoProfile -NonInteractive -File $(SolutionDir)\download_tree_sitter_repo.ps1
```

## Runtime

This is run by libtree-sitter's Pre-build step.

Executed with args:
```
bindings-generator.exe $(SolutionDir)\tree-sitter $(SolutionDir)\out
```

1. Reads the tree-sitter library for its `lib/include/tree-sitter` header files.  
2. Generates C# bindings -> `$(SolutionDir)/out/generated_csharp_bindings_source`
3. Fixes the C# bindings. The generator creates an implicit cast that isn't allowed, so that gets patched.
4. Copies the C Header files from   
`$(SolutionDir)/tree-sitter/lib/include/tree-sitter`  
-> `$(SolutionDir)/out/generated_c_dll_headers/tree_sitter`
5. Adds `__declspec(dllexport)` to all the C API functions in the copied headers.
$TODO: Add update about the headers for the language libraries.

And Done!

## Next Solution Build Steps

### libtree-sitter
`libtree-sitter` will consume the C Header files, and compile a native dll with all of the C API functions exported.

### tree-sitter-bindings
`tree-sitter-bindings` will consume the C# bindings, and compile a managed dll with those two files.

