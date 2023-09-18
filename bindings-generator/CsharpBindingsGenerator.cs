using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace bindings_generator
{
    internal class CsharpBindingsGenerator : ILibrary
    {
        TreeSitterPaths m_paths;
        string m_outputDir = "";
        public CsharpBindingsGenerator(TreeSitterPaths argPaths, string outputDir)
        {
            Debug.Assert(argPaths.IsOk);
            m_paths = argPaths;
            m_outputDir = outputDir;
        }

        void ILibrary.Postprocess(Driver driver, ASTContext ctx)
        {

        }

        void ILibrary.Preprocess(Driver driver, ASTContext ctx)
        {
        }

        /// <summary>
        /// This is the first method called and here you should setup all the options needed for Clang to correctly parse your code. 
        /// You can get at the options through a property in driver object.
        /// 
        /// The essential ones are
        /// 
        /// Parsing:
        /// - Defines
        /// - Include Directories
        /// - Headers
        /// - Libraries
        /// - Library Directories
        /// 
        /// Generator:
        /// - Output Language (C# or C++/CLI)
        /// - Output namespace 
        /// - Output Directory
        /// 
        /// </summary>
        /// <param name="driver"></param>
        void ILibrary.Setup(Driver driver)
        {
            var options = driver.Options;
            options.GeneratorKind = GeneratorKind.CSharp;

            // Module, i.e. the namespace to use for the module
            var module = options.AddModule("tree_sitter");

            // Includes and Libraries
            module.IncludeDirs.Add(m_paths.IncludePath);
            module.Headers.AddRange(m_paths.HeaderFiles);
            //module.LibraryDirs.Add(m_paths.LibraryPath);
            //module.Libraries.AddRange(m_paths.LibraryFiles);

            // Output directory for bindings
            options.OutputDir = m_outputDir;
        }

        void ILibrary.SetupPasses(Driver driver)
        {
        }
    }
}
