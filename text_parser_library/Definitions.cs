using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace text_parser_library
{
    public class Definitions
    {
        // This class holds the definitions:

        // variables.
        // functions.
        private Dictionary<string, object?> _variables;
        private Dictionary<string, CustomFunction> _commands;
        public Definitions()
        {
            _variables = new Dictionary<string, object?>();
            _commands = new Dictionary<string, CustomFunction>();
        }

        // Variable management
        public void AddVariable(string name, object? value)
        {
            _variables[name] = value;
        }

        public bool TryGetVariable(string name, out object value)
        {
            #pragma warning disable CS8601 // Possible null reference assignment.
            return _variables.TryGetValue(name, out value);
            #pragma warning restore CS8601 // Possible null reference assignment.
        }

        // Function management
        public void AddFunction(string name, CustomFunction command)
        {
            _commands[name] = command;
        }

        public bool FunctionExists(string name)
        {
            return _commands.ContainsKey(name);
        }

        public object? ExecuteFunction(string name, params object[] args)
        {
            if (_commands.TryGetValue(name, out var command))
            {
                var result = command.Execute(args);

                return result;
            }
            throw new InvalidOperationException($"Function '{name}' not found.");
        }

        public bool TrySetFunctionProperty(string functionName, string propertyName, object? value)
        {
            if (_commands.TryGetValue(functionName, out var function))
            {
                function.SetProperty(propertyName, value);
                return true;
            }
            return false;
        }

        public Definitions MergeDefinitionsLibraries(Definitions firstLibrary, Definitions secondLibrary)
        {
            var newDefinitionsLibrary = firstLibrary;

            foreach (var item in secondLibrary._variables)
            {
                newDefinitionsLibrary.AddVariable(item.Key, item.Value);
            }

            foreach (var item in secondLibrary._commands)
            {
                newDefinitionsLibrary.AddFunction(item.Key, item.Value);
            }

            return newDefinitionsLibrary;
        }
    }
}
