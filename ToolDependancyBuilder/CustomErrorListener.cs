using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace ToolDependancyBuilder
{
    public class CustomErrorListener : BaseErrorListener, IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public List<string> Errors { get; } = new List<string>();

        public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] int offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
        {
            string error = $"Line {line}:{charPositionInLine} {msg}";
            Errors.Add(error);
        }

        public override void SyntaxError(IRecognizer recognizer, [Nullable] IToken offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
        {
            string error = $"Line {line}:{charPositionInLine} {msg}";
            Errors.Add(error);
        }
    }
}
