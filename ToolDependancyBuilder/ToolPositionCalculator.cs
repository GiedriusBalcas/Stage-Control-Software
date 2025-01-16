using Antlr4.Runtime;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ToolDependancyBuilder
{
    public class ToolPositionCalculator
    {
        private Func<Dictionary<char, float>, float> _CoordinateFunc;
        private readonly ILogger<ToolPositionCalculator> _logger;

        public ToolPositionCalculator(ILogger<ToolPositionCalculator> logger)
        {
            _logger = logger;
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
                _logger.LogDebug(ex.Message);
            }
            if (errorListener.Errors.Any())
            {
                foreach (var error in errorListener.Errors)
                {
                    _logger.LogDebug(error);
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
