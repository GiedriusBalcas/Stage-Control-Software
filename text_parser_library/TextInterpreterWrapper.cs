using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    public class TextInterpreterWrapper
    {
        public InputVisitor Visitor { get; set; } = new InputVisitor(new ParserState(), new Definitions(), "");
        public Definitions DefinitionLibrary { get; set; } = new Definitions();
        public ParserState State { get; set; } = new ParserState();
        public void ReadInput(string input, string fileName)
        {
            try
            {
                var inputStream = new AntlrInputStream(input);
                var lexer = new GrammarSyntaxLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new GrammarSyntaxParser(tokenStream);
                var _tree = parser.program();
                Visitor = new InputVisitor(State, DefinitionLibrary, fileName);
                Visitor.Visit(_tree);
            }
            catch (Exception ex)
            {
                throw new Exception(Visitor.State.Message);
            }
        }

        public void ReadInput(
        string input, string fileName,
        CancellationToken cancelToken,
        Action<string> statusCallback)
        {
            // Fire an initial status
            statusCallback("Reading main script...");

            var inputStream = new AntlrInputStream(input);
            cancelToken.ThrowIfCancellationRequested();

            var lexer = new GrammarSyntaxLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new GrammarSyntaxParser(tokenStream);

            // Build the parse tree
            var tree = parser.program();

            // Create and configure the visitor
            Visitor = new InputVisitor(State, DefinitionLibrary, fileName);

            // Hook up line visited event for a textual update
            Visitor.LineVisited += (lineNumber, currentFileName) =>
            {
                // You can pass the file name from readFile context
                // If you haven't implemented that yet, just use a default name
                statusCallback($"Now parsing file: {currentFileName}, line {lineNumber}");
                // Check for cancel
                if (cancelToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancelToken);
            };

            // Visit the parse tree
            Visitor.Visit(tree);
        }


    }
}
