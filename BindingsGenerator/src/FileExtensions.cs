using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bindings_generator
{
    public static class FileExtensions
    {
        public static readonly string[] cHeaderExt = { "h", "hpp" };
        public static readonly string[] cSourceExt = { "c", "cc" };
        public static readonly string[] cLibraryExt = { "lib" };
        public static readonly string[] csharpExt = { "cs" };
    }
}
