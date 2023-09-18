using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tree_sitter;

namespace bindings_test
{
    class Parser : IDisposable
    {
        TSParser m_parser;

        public Parser()
        {
            m_parser = api.TsParserNew();
        }

        public void Dispose()
        {
            api.TsParserDelete(m_parser);
        }

        public void SetLanguage()
        {
            api.TsParserSetLanguage(m_parser, null);
        }
    }

    internal class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            using (var parser = new Parser())
            {
                Console.WriteLine("Made a Parser");
                parser.SetLanguage();
            }
            Console.WriteLine("Deleted a Parser");
            return 0;
        }
    }
}
