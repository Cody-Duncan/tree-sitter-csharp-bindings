# tree-sitter-bindings

This project consumes `tree_sitter.dll` and `tree-sitter-bindings.dll` to test that the bindings work.

## Build Steps

1. Builds `bindings-test.exe` -> `$(SolutionDir)\bindings-test\bin\$(Platform)\$(Configuration)\net6.0\`

1. **Post-Build**: `bindings-test` copies `tree_sitter.dll` and `tree-sitter-bindings.dll` into its output directory. `bindings-test.exe` will load `tree-sitter-bindings.dll`, which imports `tree_sitter.dll`, to execute tree-sitter code.