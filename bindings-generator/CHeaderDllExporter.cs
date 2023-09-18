using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace bindings_generator
{
    internal class CHeaderDllExporter
    {
        internal static string AddDllExporeToFunctionLine(Match m)
        {
            var functionLine = m.Groups.Values.First().Value;
            return "DllExport " + functionLine;
        }

        internal static void AddDllExportToCAPI(IEnumerable<string> C_HeaderFiles)
        {
            foreach (var includeFilepath in C_HeaderFiles)
            {
                string inFileContents = File.ReadAllText(includeFilepath);

                char firstNewlineChar = inFileContents.First(c => c == '\r' || c == '\n');
                string newLine = (firstNewlineChar == '\r') ? "\r\n" : "\n";

                // insert `#define DllExport   __declspec( dllexport )` just before the first include
                Regex includeRx = new Regex(@"#include .*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                Match includeMatch = includeRx.Match(inFileContents);
                string intermediateFileContents =
                    includeMatch.Success
                    ? inFileContents.Insert(includeMatch.Index, $"#define DllExport   __declspec( dllexport ){newLine}")
                    : inFileContents;

                // const char *ts_language_field_name_for_id(const TSLanguage *, TSFieldId);
                // ^(...)$                      Capture the entire line in one big group
                // ^                            Start of Line. 
                // ([\w]+ )+\*?                 The return value of the function. E.G. `const char *`
                // (ts_[\w]+\((.*\);)?)         The function signature on this line. E.G. `ts_language_field_count(const TSLanguage *);`
                //                                   or it might cut off early `ts_language_symbol_for_name(`
                //     ts_[\w]+                 The function name. E.G. `ts_language_field_count`
                //     \(                       The start parenthesis for the function. Always present.
                //     (.*\);)?                 The rest of the function arguments and semi-colon. This is optionally present. E.G. `const TSLanguage *, TSSymbol);`
                // $                            End of Line

                Regex functionLineRegex = new Regex(@"^(([\w]+ )+\*?(ts_[\w]+\((.*\);)?))$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
                string outFileContents = functionLineRegex.Replace(intermediateFileContents, new MatchEvaluator(AddDllExporeToFunctionLine));

                File.WriteAllText(includeFilepath, outFileContents);
            }
        }
    }
}
