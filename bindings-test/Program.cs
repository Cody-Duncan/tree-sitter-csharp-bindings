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
            tree_sitter.TSLanguage language = tree_sitter_python.tree_sitter_python.TreeSitterPython();
            api.TsParserSetLanguage(m_parser, language);
        }

        public ParseTree Parse(string content)
        {
            var tree = api.TsParserParseString(m_parser, null, content, (uint)content.Length);
            return new ParseTree(tree);
        }
    }

    class Node
    {
        TSNode m_node;
        public Node(TSNode node)
        { 
            m_node = node; 
        }

        public string Type
        {
            get
            {
                return api.TsNodeType(m_node);
            }
        }

        public ushort Symbol
        {
            get
            {
                return api.TsNodeSymbol(m_node);
            }
        }
    }

    class ParseTree
    {
        TSTree m_tree;
        public ParseTree(TSTree tree)
        {
            m_tree = tree;
        }

        public Node Root 
        {   get 
            { 
                var node = api.TsTreeRootNode(m_tree); ;
                return new Node(node);
            } 
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
                var tree = parser.Parse("print('Hello, world!')");
                if (tree == null)
                {
                    Console.WriteLine("Parsed Tree is NULL");
                }
                var node = tree.Root;
                Console.WriteLine($"Root Node's type is `{node.Type}`");
            }
            Console.WriteLine("Deleted a Parser");
            return 0;
        }
    }
}
