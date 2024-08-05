using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    public class ParserWrapper
    {
        public InputVisitor Visitor { get; set; } = new InputVisitor(new ParserState(), new Definitions());
        public Definitions DefinitionLibrary { get; set; } = new Definitions();
        public ParserState State { get; set; } = new ParserState();
        public void ReadInput(string input)
        {
            var inputStream = new AntlrInputStream(input);
            var lexer = new GrammarSyntaxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new GrammarSyntaxParser(tokenStream);
            var _tree = parser.program();
            Visitor = new InputVisitor(State, DefinitionLibrary);
            Visitor.Visit(_tree);
        }


    }
}
