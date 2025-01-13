using Antlr4.Runtime;
using System.Collections.Concurrent;

namespace ToolDependancyBuilder
{
    public class ToolPositionCalculator
    {
        private Func<Dictionary<char, float>, float> _CoordinateFunc;
        private readonly ConcurrentQueue<string> _log;

        public ToolPositionCalculator(ConcurrentQueue<string> log)
        {
            _log = log;
        }
        public void CreateFunction(string xExprStr, List<char> deviceNames)
        {
            var _deviceNames = deviceNames;
            var errorListener = new CustomErrorListener();
            try
            {
                var inputStream = new AntlrInputStream(xExprStr);
                var lexer = new ToolExpressionLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new ToolExpressionParser(tokenStream);
                var exprContext = parser.expression();
                parser.RemoveErrorListeners(); // Remove the default error listeners

                lexer.RemoveErrorListeners(); // Remove default error listener for lexer
                lexer.AddErrorListener(errorListener); // Add custom error listener
                parser.RemoveErrorListeners(); // Remove default error listener for parser
                parser.AddErrorListener(errorListener); // Add custom error listener

                var builder = new CustomBuilder(deviceNames);

                _CoordinateFunc = builder.CompileExprToDelegate(exprContext).Compile();
            }
            catch (Exception ex)
            {
                _log.Enqueue(ex.Message);
            }
            if (errorListener.Errors.Any())
            {
                foreach (var error in errorListener.Errors)
                {
                    _log.Enqueue(error);
                }
            }
        }

        public float CalculateXPosition(Dictionary<char, float> devicePositions)
        {
            if (_CoordinateFunc is not null)
                return _CoordinateFunc(devicePositions);
            return 0f;
        }

        public Func<Dictionary<char, float>, float> GetFunction() 
        {
            return _CoordinateFunc;
        }
    }
}
