# libtree-sitter

This project compiles a native windows DLL with the tree-sitter/lib C files.

There's nothing in this directory because all the source files are linked to external directories.

**Source Files**: linked to .c and .h files in `$(SolutionDir)/tree-sitter/lib/src`. This does NOT include `lib/include`; needs modified include headers that export DLL symbols.

- *See* `bindings-generator` for the prebuiild step that runs `download_tree_sitter_repo.ps1` to download the tree-sitter repository.

**Include Files**: linked to `$(SolutionDir)/out/tree_sitter_dll_includes`. This contains modified copies of the headers from tree-sitter's `lib/include`, with the addition that all C API functions are marked with `__declspec(dllexport)`.

- *See* `bindings-generator` for how these headers are copied from the tree-sitter library and modified.

## Library

This project builds a native dll with all the tree-sitter C API functions exported.

Ouput Directory: `$(SolutionDir)out\$(Platform)\$(Configuration)\`  
E.G. `out\x64\Debug\*`

## Note: The generated binary is `tree_sitter.dll`.  
`tree` UNDERSCORE `sitter.dll`

The reason for this change is that CppSharp (*see* bindings-generator) is given a module name `tree_sitter`, which is used to generate the contents of namespaces and DllImport statements.

E.G. from `$(SolutionDir)out\csharp_bindings\tree_sitter.cs`
```csharp 
namespace tree_sitter
```
```csharp 
DllImport("tree_sitter", ...
```


C# does NOT allow dash/hyphen/minus "`-`" in namespaces!  
Having a "`-`" in the module name generates errand source code.

Since the namespace and the DLL name are generated to match, the .dll needs to be `tree_sitter.dll`.


## Build Steps

1. Pre-build - executes `bindings-generator.exe` to generate the DLL's include C Headeers.
```
$(SolutionDir)bindings-generator\bin\$(Platform)\$(Configuration)\net6.0\bindings-generator.exe $(SolutionDir)tree-sitter $(SolutionDir)out
```
2. Builds `tree_sitter.dll` and debug binaries -> `$(SolutionDir)out\libtree-sitter\$(Platform)\$(Configuration)`

## Next Solution Build Steps

### tree-sitter-bindings

**Post-Build**: `tree-sitter-bindings` will copy its `tree-sitter-bindings.dll` and `tree_sitter.dll` -> `$(SolutionDir)out\binding_libs\$(Platform)\$(Configuration)\net6.0\` for deployment to external projects.

### bindings-test

**Post-Build**: `bindings-test` will copy `tree_sitter.dll` and `tree-sitter-bindings.dll` into its output directory. `bindings-test.exe` will load `tree-sitter-bindings.dll`, which imports `tree_sitter.dll`, to execute tree-sitter code.
