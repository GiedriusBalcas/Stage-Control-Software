using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    internal class UserCommand : CustomFunction
    {
        private readonly List<string> _parameters;
        private readonly GrammarSyntaxParser.BlockContext _body;
        private readonly List<string> _variableList;
        private ParserState _state;
        private Definitions _definitionsLibrary;
        private string _commandName;

        public UserCommand(List<string> parameters, GrammarSyntaxParser.BlockContext body, List<string> variableList, ParserState state, Definitions definitionsLibrary, string functionName)
        {
            _parameters = parameters;
            _body = body;
            _variableList = variableList;
            _state = state;
            _definitionsLibrary = definitionsLibrary;
            _commandName = functionName;
        }

        public override object? Execute(params object[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                _definitionsLibrary.AddVariable(_variableList[i], args[i]);
            }

            var visitor = new InputVisitor(_state, _definitionsLibrary, _commandName);
            try
            {
                visitor.Visit(_body);
            }
            catch (FunctionReturnException ex)
            {
                return ex.ReturnValue;
            }
            return null;
        }
    }
}
