using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bindings_generator
{
    public class PathError
    {
        public enum PathErrorType
        {
            /// <summary>
            /// Ok. No Error
            /// </summary>
            Ok,

            /// <summary>
            /// Could not find the given directory. Check AdditionalInfo for directory path.
            /// </summary>
            DirectoryNotFound,

            /// <summary>
            /// Could not find the given file. Check AdditionalInfo for file path.
            /// </summary>
            FileNotFound,

            /// <summary>
            /// Could not find any header files. Check AdditionalInfo for include path.
            /// </summary>
            HeadersNotFound,

            /// <summary>
            /// Could not find any library files. Check AdditionalInfo for library path.
            /// </summary>
            LibrariesNotFound,

            /// <summary>
            /// Could not find any the tree-sitter library `tree-sitter.lib`. Check AdditionalInfo for library path.
            /// </summary>
            TreeSitterLibraryNotFound,
        }

        PathErrorType m_type;
        string? m_additionalInfo;

        public static PathError Ok()
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.Ok;
            return outError;
        }

        public static PathError DirectoryMissing(string path)
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.DirectoryNotFound;
            outError.m_additionalInfo = path;
            return outError;
        }

        public static PathError FileMissing(string path)
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.FileNotFound;
            outError.m_additionalInfo = path;
            return outError;
        }

        public static PathError HeadersNotFound(string includePath)
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.HeadersNotFound;
            outError.m_additionalInfo = includePath;
            return outError;
        }
        public static PathError LibrariesNotFound(string libraryPath)
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.LibrariesNotFound;
            outError.m_additionalInfo = libraryPath;
            return outError;
        }

        public static PathError TreeSitterLibraryNotFound(string libraryPath)
        {
            var outError = new PathError();
            outError.m_type = PathErrorType.TreeSitterLibraryNotFound;
            outError.m_additionalInfo = libraryPath;
            return outError;
        }

        public bool IsOk { get { return m_type == PathErrorType.Ok; } }
        public PathErrorType ErrorType { get { return m_type; } }

        public string? AdditionalInfo
        {
            get { return m_additionalInfo; }
        }

        public string GenerateErrorMessage()
        {
            StringBuilder sb = new StringBuilder("", 128);

            switch (m_type)
            {
                case PathErrorType.Ok:
                    sb.Append("OK. No Error");
                    break;
                case PathErrorType.DirectoryNotFound:
                    sb.AppendFormat("ERROR:{0}: Could not find directory \"{1}\"", m_type.ToString(), m_additionalInfo);
                    break;
                case PathErrorType.FileNotFound:
                    sb.AppendFormat("ERROR:{0}: Could not find file \"{1}\"", m_type.ToString(), m_additionalInfo);
                    break;
                case PathErrorType.HeadersNotFound:
                    sb.AppendFormat("ERROR:{0}: Could not find tree sitter headers. Include Directory: \"{1}\"", m_type.ToString(), m_additionalInfo);
                    break;
                case PathErrorType.LibrariesNotFound:
                    sb.AppendFormat("ERROR:{0}: Could not find tree sitter libraries. Library Directory: \"{1}\"", m_type.ToString(), m_additionalInfo);
                    break;
                case PathErrorType.TreeSitterLibraryNotFound:
                    sb.AppendFormat("ERROR:{0}: Could not find tree sitter library `tree-sitter.lib`. Library Directory: \"{1}\"", m_type.ToString(), m_additionalInfo);
                    break;
                default:
                    sb.Append("ERROR:UNKNOWN ERROR");
                    break;
            }

            return sb.ToString();
        }
    }
}
