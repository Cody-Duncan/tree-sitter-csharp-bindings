using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// this file does nothing
// 
// It exists because Std.cs and tree_sitter.cs are linked files to $(SolutionDir)\out\csharp_bindings\, which doesn't exist yet.
// They're generated after bindings-generator.exe is built and run via the pre-build step of the libtree-sitter project.
//
// If this project cannot open **any** .cs fails, the project itself fails to open, skips building, and the solution fails to build.
//
// Having this Nothing.cs is a quick-fix thats lets this project open.
// $(SolutionDir)\out\hold.txt is also necessary to let this project build. It makes sure the /out directory exists after cloning the repository.
