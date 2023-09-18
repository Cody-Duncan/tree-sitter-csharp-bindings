# tree-sitter-bindings

This project compiles a .NET managed DLL with the C# bindings for `tree_sitter.dll`. 


## Source Files

There's nothing in this directory because all the source files are linked to external directories.

**Source Files**: linked to generated bindings files in `$(SolutionDir)\out\csharp_bindings`.

- *See* `bindings-generator` for the how the binding `.cs` files are generated.

***$TODO***: Add wrapper classes that make these API's more usable 

## Build Steps

1. Builds `tree-sitter-bindings.dll` -> `$(SolutionDir)\tree-sitter-bindings\bin\$(Platform)\$(Configuration)\net6.0\`

1. **Post-Build** - copies `tree_sitter.dll` from `libtree-sitter` project, and the compiled `tree-sitter-bindings.dll` into ->  
`$(SolutionDir)\out\binding_libs\$(Platform)\$(Configuration)\net6.0\` for deployment to external projects.

## Next Solution Build Steps

### $(SolutionDir)/out/binding_libs

This is the location that `tree_sitter.dll` and `tree-sitter-bindings.dll` are copied into as outputs of this project.

### bindings-test

This project consumes `tree_sitter.dll` and `tree-sitter-bindings.dll` to test that the bindings work.

**Post-Build**: `bindings-test` will copy `tree_sitter.dll` and `tree-sitter-bindings.dll` into its output directory. `bindings-test.exe` will load `tree-sitter-bindings.dll`, which imports `tree_sitter.dll`, to execute tree-sitter code.
