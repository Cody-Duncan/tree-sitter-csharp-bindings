using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace bindings_generator
{
    internal class CSharpBindingsFix
    {
        /// <summary>
        /// There's a generation error in `tree_sitter.cs`.
        /// `sbyte** FieldNames` is implemented as `return ((__Internal*)__Instance)->field_names;`.
        /// This causes `Error CS0266 Cannot implicitly convert type 'System.IntPtr' to 'sbyte**'.`
        /// This quick and easy but I'm unsure if it works fix is just to add an explicit cast to these lines: `return (sbyte**)...`
        /// </summary>
        /// <param name="CSharpBindingsFiles"></param>
        internal static void FixImplictCast(IEnumerable<string> CSharpBindingsFiles)
        {
            foreach (var includeFilepath in CSharpBindingsFiles)
            {
                string inFileContents = File.ReadAllText(includeFilepath);

                char firstNewlineChar = inFileContents.First(c => c == '\r' || c == '\n');
                string newLine = (firstNewlineChar == '\r') ? "\r\n" : "\n";

                Regex getterLineRx = new Regex(@"^(\s*)return \(\(__Internal\*\)__Instance\)->(symbol_names|field_names);", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
                string outFileContents = getterLineRx.Replace(inFileContents, new MatchEvaluator((match) => {
                    if (match.Groups.Count >= 3)
                    {
                        var indentation = match.Groups[1].Value;
                        var fieldName = match.Groups[2].Value;
                        return $"{indentation}//UNTESTED FIX: Automatically added explicit cast to (sbyte**), otherwise \"Error CS0266 Cannot implicitly convert type 'System.IntPtr' to 'sbyte**'.\"{newLine}"
                            + $"{indentation}return (sbyte**)((__Internal*)__Instance)->{fieldName};";
                    }
                    return match.Value; // return unchanged string
                }));

                File.WriteAllText(includeFilepath, outFileContents);
            }
        }
    }
}
