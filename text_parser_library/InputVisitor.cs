using Antlr4.Runtime.Tree;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace text_parser_library
{
    public class InputVisitor : GrammarSyntaxBaseVisitor<object?>
    {
        private Definitions _definitionsLibrary;
        private string _currentFileName;
        private string _currentFilePath;

        public ParserState State { get;}
        public event Action<int, string>? LineVisited;
        public InputVisitor(ParserState state, Definitions definitionsLibrary, string fileName, string filePath)
        {
            _definitionsLibrary = definitionsLibrary;
            _currentFileName = fileName;
            _currentFilePath = filePath;
            State = state;
        }

        public Definitions GetCurrentLibrary()
        {
            return _definitionsLibrary;
        }

        public void UpdateDefinitions(Definitions definitionLibrary)
        {
            _definitionsLibrary = definitionLibrary;
        }

        public override object? VisitReadFile([NotNull] GrammarSyntaxParser.ReadFileContext context)
        {
            var filePathObj = Visit(context.expression());
            var filePath = filePathObj?.ToString();

            if (filePath is null)
            {
                State.AddMessage($"File Path is undefined.");
                State.SetState(ParserState.States.Error);
                throw new Exception("File Path is undefined.");
            }

            // Check if the filePath is just a file name (no directory information)
            if (!Path.IsPathRooted(filePath) && string.IsNullOrWhiteSpace(Path.GetDirectoryName(filePath)))
            {
                // Ensure _currentFilePath is defined
                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    State.AddMessage("Current file path is undefined, cannot resolve relative file path.");
                    State.SetState(ParserState.States.Error);
                    throw new Exception("Current file path is undefined.");
                }

                // Get the directory of the current file
                string? currentDirectory = Path.GetDirectoryName(_currentFilePath);
                if (currentDirectory == null)
                {
                    State.AddMessage("Unable to determine the directory of the current file.");
                    State.SetState(ParserState.States.Error);
                    throw new Exception("Unable to determine the directory of the current file.");
                }

                // Combine the current directory with the provided file name
                filePath = Path.Combine(currentDirectory, filePath);
            }

            // Handle missing file extension
            if (!Path.HasExtension(filePath))
            {
                // Define the default extension(s) to try
                string[] possibleExtensions = { ".txt" }; // You can add more extensions if needed

                bool fileFound = false;
                string originalFilePath = filePath; // Keep the original for error messages
                foreach (var ext in possibleExtensions)
                {
                    string tempPath = filePath + ext;
                    if (File.Exists(tempPath))
                    {
                        filePath = tempPath;
                        fileFound = true;
                        break;
                    }
                }

                if (!fileFound)
                {
                    // Optionally, you can inform the user about the assumed extension
                    State.AddMessage($"File '{originalFilePath}' does not exist. Tried adding extensions: {string.Join(", ", possibleExtensions)}.");
                    State.SetState(ParserState.States.Error);
                    throw new FileNotFoundException($"File '{originalFilePath}' not found with extensions: {string.Join(", ", possibleExtensions)}.");
                }
            }
            else
            {
                // Verify that the file exists
                if (!File.Exists(filePath))
                {
                    State.AddMessage($"File '{filePath}' does not exist.");
                    State.SetState(ParserState.States.Error);
                    throw new FileNotFoundException($"File '{filePath}' does not exist.");
                }
            }

            string fileName = Path.GetFileName(filePath);
            string content;
            try
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    content = sr.ReadToEnd();
                }

                var inputStream = new AntlrInputStream(content);
                var lexer = new GrammarSyntaxLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new GrammarSyntaxParser(tokenStream);
                var _tree = parser.program();
                var visitor = new InputVisitor(State, _definitionsLibrary, fileName, filePath);
                visitor.Visit(_tree);
                _definitionsLibrary = visitor.GetCurrentLibrary();
            }
            catch (Exception ex)
            {
                State.AddMessage($"Error encountered in file: {fileName}. Message: {ex.Message}");
                State.SetState(ParserState.States.Error);
                throw new Exception($"Error reading file '{fileName}': {ex.Message}", ex);
            }

            return base.VisitReadFile(context);
        }

        //public override object VisitReadFile([NotNull] GrammarSyntaxParser.ReadFileContext context)
        //{
        //    var filePath = Visit(context.expression())?.ToString();
        //    if (filePath is null)
        //    {
        //        State.AddMessage($"File Path is undefined.");
        //        State.SetState(ParserState.States.Error);
        //        throw new Exception();
        //    }
        //    string fileName = Path.GetFileName(filePath);
        //    string content;
        //    try
        //    {
        //        using (StreamReader sr = new StreamReader(filePath))
        //        {
        //            content = sr.ReadToEnd();
        //        }

        //        var inputStream = new AntlrInputStream(content);
        //        var lexer = new GrammarSyntaxLexer(inputStream);
        //        var tokenStream = new CommonTokenStream(lexer);
        //        var parser = new GrammarSyntaxParser(tokenStream);
        //        var _tree = parser.program();
        //        var visitor = new InputVisitor(State, _definitionsLibrary, fileName, filePath);
        //        visitor.Visit(_tree);
        //        _definitionsLibrary = visitor.GetCurrentLibrary();
        //    }
        //    catch(Exception ex)
        //    {
        //        State.AddMessage($"Error encountered in file: {fileName}. Message: {ex.Message}");
        //        State.SetState(ParserState.States.Error);
        //        throw new Exception();
        //    }



        //    return base.VisitReadFile(context);
        //}
        private void CheckState()
        {
            if (State.CurrentState == ParserState.States.Error)
                throw new Exception(State.Message);
        }

        public override object? VisitLine([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.LineContext context)
        {
            var lineNum = context.start.Line;
            State.UpdateLineNumber(lineNum);

            // Fire the event
            LineVisited?.Invoke(lineNum, _currentFileName);

            if (State.CurrentState != ParserState.States.Error)
                return base.VisitLine(context);
            else
                throw new Exception();
        }

        public override object? Visit([Antlr4.Runtime.Misc.NotNull] IParseTree tree)
        {
            //CheckState();
            var result = base.Visit(tree);
            return result;
        }

        public override object? VisitReturnExpression([NotNull] GrammarSyntaxParser.ReturnExpressionContext context)
        {
            var returnValue = Visit(context.expression());
            throw new FunctionReturnException(returnValue);
        }


        public override object? VisitFunctionPropertyAssignment([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.FunctionPropertyAssignmentContext context)
        {
            var functionName = context.IDENTIFIER(0).GetText();
            var propertyName = context.IDENTIFIER(1).GetText();
            var value = Visit(context.expression());


            try
            {
                var result = _definitionsLibrary.TrySetFunctionProperty(functionName, propertyName, value);
                if(!result)
                {
                    State.AddMessage($"Error encountered on when trying to assign value to {functionName}.{propertyName}.");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
                }

            }
            catch (Exception ex)
            {
                State.AddMessage($"Exception thrown trying to assign {functionName}.{propertyName} with value {value}: {ex.Message}");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }

            return base.VisitFunctionPropertyAssignment(context);
        }

        public override object? VisitEqualsAssignment(GrammarSyntaxParser.EqualsAssignmentContext context)
        {
            var varName = context.IDENTIFIER().GetText();
            var value = Visit(context.expression());

            //_variableList[varName] = value;
            _definitionsLibrary.AddVariable(varName, value);

            return null;
        }

        public override object? VisitUpdateAssignment(GrammarSyntaxParser.UpdateAssignmentContext context)
        {
            var varName = context.IDENTIFIER().GetText();
            var value = Visit(context.expression());

            var op = context.updateOp().GetText();


            if (!_definitionsLibrary.TryGetVariable(varName,out object currentValue))
            {
                State.AddMessage($"Variable {varName} is not defined");
                State.SetState( ParserState.States.Error);
                throw new Exception();
            }
            else
            {
                switch (op)
                {
                    case "+=":
                        _definitionsLibrary.AddVariable(varName, Add(currentValue, value));
                        break;
                    case "-=":
                        _definitionsLibrary.AddVariable(varName, Subtract(currentValue, value));
                        break;
                    default:
                        State.AddMessage($"operator type of {op} in update assignment is undefined");
                        State.SetState(ParserState.States.Error);
                        throw new Exception();
                }
            }

            return null;
        }

        public override object? VisitDecrementAssignment(GrammarSyntaxParser.DecrementAssignmentContext context)
        {
            var varName = context.IDENTIFIER().GetText();
            var op = context.decrementOp().GetText();

            if (!_definitionsLibrary.TryGetVariable(varName, out object currentValue))
            {
                State.AddMessage($"Variable {varName} is not defined in decrement assignment");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
            else
            {
                switch (op)
                {
                    case "++":
                        _definitionsLibrary.AddVariable(varName, Add(currentValue, 1));
                        break;
                    case "--":
                        _definitionsLibrary.AddVariable(varName, Subtract(currentValue, 1));
                        break;
                    default:
                        State.AddMessage($"Operator type of {op} is not defined");
                        State.SetState(ParserState.States.Error);
                        throw new Exception();
                }
            }
            return null;
        }

        public override object? VisitConstant(GrammarSyntaxParser.ConstantContext context)
        {
            if (context.INTEGER() is { } i)
                return int.Parse(i.GetText());
            if (context.FLOAT() is { } f)
                return float.Parse(f.GetText());
            if (context.STRING() is { } s)
                return s.GetText()[1..^1];
            if (context.BOOL() is { } b)
                return b.GetText() == "true";
            if (context.NULL() is { })
                return null;

            State.AddMessage($"Constant of undefined type.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        public override object? VisitIdentifierExpression(GrammarSyntaxParser.IdentifierExpressionContext context)
        {
            var varName = context.IDENTIFIER().GetText();

            if (!_definitionsLibrary.TryGetVariable(varName, out object currentValue))
            {
                State.AddMessage($"Variable {varName} is not defined");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }

            return currentValue;
        }

        public override object? VisitAddOpExpression(GrammarSyntaxParser.AddOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.addOp().GetText();

            object? result;

            switch (op)
            {
                case "+":
                    result = Add(left, right);
                    break;
                case "-":
                    result = Subtract(left, right);
                    break;
                default:
                    State.AddMessage($"Invalid addition operation operator type: {op}");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
            }

            return result;
        }

        public override object? VisitMultOpExpression(GrammarSyntaxParser.MultOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            var op = context.multOp().GetText();

            object? result;

            switch (op)
            {
                case "*":
                    result = Multiply(left, right);
                    break;
                case "/":
                    result = Divide(left, right);
                    break;
                case "%":
                    result = Modulo(left, right);
                    break;
                default:
                    State.AddMessage($"Invalid multiplication operation operator type: {op}");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
            }
            return result;
        }

        public override object? VisitPowOpExpression([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.PowOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            var op = context.powOp().GetText();


            if (left is int l && right is int r)
                return (float)Math.Pow(l, r);
            if (left is float fl && right is float fr)
                return (float)Math.Pow(fl, fr);
            if (left is int lInt && right is float rFloat)
                return (float)Math.Pow(lInt, rFloat);
            if (left is float lFloat && right is int rInt)
                return (float)Math.Pow(lFloat, rInt);

            State.AddMessage($"Cannot multiply values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        public override object? VisitBoolOpExpression(GrammarSyntaxParser.BoolOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            var op = context.boolOp().GetText();

            object? result;

            switch (op)
            {
                case "&&":
                    result = AndOperation(left, right);
                    break;
                case "||":
                    result = OrOperation(left, right);
                    break;
                case "^^":
                    result = XorOperation(left, right);
                    break;
                default:
                    State.AddMessage($"Invalid bool operation operator type: {op}");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
            }

            return result;
        }

        public override object? VisitFunctionDefinition([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.FunctionDefinitionContext context)
        {
            var functionName = context.IDENTIFIER().GetText();
            var argNames = context.paramList().IDENTIFIER().Select(arg => arg.GetText()).ToList();
            var block = context.block();
            var parameters = new List<string>();

            var userFunction = new UserCommand(parameters, block,argNames,State, _definitionsLibrary, functionName, _currentFilePath);
            _definitionsLibrary.AddFunction(functionName, userFunction);
            

            return userFunction;
        }

        public override object? VisitFunctionCall([NotNull] GrammarSyntaxParser.FunctionCallContext context)
        {
            var name = context.IDENTIFIER().GetText();
            if (!_definitionsLibrary.FunctionExists(name))
            {
                State.AddMessage($"Function {name} is not defined.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }


            var args = context.expression().Select(e => Visit(e)).ToArray();
            if (args is null || args.Any(val => val is null))
                throw new Exception("Arguments were null.");
            object? result = null;

            try
            {
                result = _definitionsLibrary.ExecuteFunction(name, args!);

            }catch (Exception ex)
            {
                State.AddMessage($"Exception thrown in {name} command: {ex.Message}");
                State.SetState(ParserState.States.Error);
                throw;
            }

            return result;
        }


        public override object? VisitParenthesizedExpression([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.ParenthesizedExpressionContext context)
        {
            return Visit(context.expression());
        }

        public override object? VisitNotExpression([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.NotExpressionContext context)
        {
            var value = Visit(context.expression());
            if (value is bool val)
                return !val;
            else
            {
                State.AddMessage($"Cant perform NOT operation on variable of type{value?.GetType()}.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
        }

        public override object? VisitTrigFunctionExpression([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.TrigFunctionExpressionContext context)
        {
            var value = Visit(context.expression());

            // Attempt to convert any numeric value to double
            if (value is IConvertible convertible)
            {
                double doubleValue = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);

                // Retrieve the function name and convert it to lowercase for case-insensitive comparison
                string function = context.trigFunction().GetText().ToLower();

                float result;

                // Determine and apply the trigonometric function based on the function name
                switch (function)
                {
                    case "sin":
                        result = (float)Math.Sin(doubleValue);
                        break;
                    case "cos":
                        result = (float)Math.Cos(doubleValue);
                        break;
                    case "tan":
                        result = (float)Math.Tan(doubleValue);
                        break;
                    case "asin":
                        result = (float)Math.Asin(doubleValue);
                        break;
                    case "acos":
                        result = (float)Math.Acos(doubleValue);
                        break;
                    case "atan":
                        result = (float)Math.Atan(doubleValue);
                        break;
                    default:
                        State.AddMessage($"Trigonometric function '{function}' is not defined.");
                        State.SetState(ParserState.States.Error);
                        throw new Exception();
                }

                return result;
            }
            else
            {
                State.AddMessage($"Expression does not evaluate to a numeric value.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
        }

        private bool XorOperation(object? left, object? right)
        {
            if (left is bool l && right is bool r)
                return l ^ r;
            else
            {
                State.AddMessage($"Unable to perform XOR operation on variables of types {left?.GetType()} and {right?.GetType()}.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
        }

        private bool OrOperation(object? left, object? right)
        {
            if (left is bool l && right is bool r)
                return l | r;
            else
            {
                State.AddMessage($"Unable to perform OR operation on variables of types {left?.GetType()} and {right?.GetType()}.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
        }

        private bool AndOperation(object? left, object? right)
        {
            if (left is bool l && right is bool r)
                return l & r;
            else
            {
                State.AddMessage($"Unable to perform AND operation on variables of types {left?.GetType()} and {right?.GetType()}.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
        }

        private object? Modulo(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l % r;
            if (left is float fl && right is float fr)
                return fl % fr;
            if (left is int lInt && right is float rFloat)
                return lInt % rFloat;
            if (left is float lFloat && right is int rInt)
                return lFloat % rInt;

            State.AddMessage($"Cannot perform modulo operation on values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private object? Divide(object? left, object? right)
        {
            if (left is int l && right is int r)
                return (float)l / r;
            if (left is float fl && right is float fr)
                return fl / fr;
            if (left is int lInt && right is float rFloat)
                return lInt / rFloat;
            if (left is float lFloat && right is int rInt)
                return lFloat / rInt;

            State.AddMessage($"Cannot divide values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private object? Multiply(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l * r;
            if (left is float fl && right is float fr)
                return fl * fr;
            if (left is int lInt && right is float rFloat)
                return lInt * rFloat;
            if (left is float lFloat && right is int rInt)
                return lFloat * rInt;
            

            State.AddMessage($"Cannot multiply values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private object? Subtract(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l - r;
            if (left is float fl && right is float fr)
                return fl - fr;
            if (left is int lInt && right is float rFloat)
                return lInt - rFloat;
            if (left is float lFloat && right is int rInt)
                return lFloat - rInt;

            State.AddMessage($"Cannot subtract values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private object? Add(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l + r;
            if (left is float fl && right is float fr)
                return fl + fr;
            if (left is int lInt && right is float rFloat)
                return lInt + rFloat;
            if (left is float lFloat && right is int rInt)
                return lFloat + rInt;
            if (left is string || right is string)
                return $"{left}{right}";

            State.AddMessage($"Cannot add values of types {left?.GetType()} and {right?.GetType()}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        public override object? VisitIfBlock([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.IfBlockContext context)
        {
            var condition = Visit(context.expression());

            var kaka1 = context.block();
            var kaka2 = context.elseIfBlock();
            if (condition is bool b)
            {
                if (b && context.block() is { } bl)
                    Visit(bl);
                else if (context.elseIfBlock() is { } elsbl)
                    Visit(context.elseIfBlock());
            }
            return null;
        }

        public override object? VisitCompareOpExpression(GrammarSyntaxParser.CompareOpExpressionContext context)
        {
            var l = Visit(context.expression(0));
            var r = Visit(context.expression(1));
            var op = context.compareOp().GetText();

            bool result;

            switch (op)
            {
                case "==":
                    result = IsEquals(l, r);
                    break;
                case "!=":
                    result = NotEquals(l, r);
                    break;
                case ">":
                    result = GreaterThan(l, r);
                    break;
                case "<":
                    result = LessThan(l, r);
                    break;
                case ">=":
                    result = GreaterThanOrEquals(l, r);
                    break;
                case "<=":
                    result = LessThanOrEquals(l, r);
                    break;
                default:
                    State.AddMessage($"Unknown comparison operator {op}");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
            }

            return result;
        }

        private bool LessThanOrEquals(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l <= r;

            if (left is float fl && right is float fr)
                return fl <= fr;

            if (left is int il && right is float flr)
                return il <= flr;

            if (left is float fll && right is int ir)
                return fll <= ir;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private bool GreaterThanOrEquals(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l >= r;

            if (left is float fl && right is float fr)
                return fl >= fr;

            if (left is int il && right is float flr)
                return il >= flr;

            if (left is float fll && right is int ir)
                return fll >= ir;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private bool LessThan(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l < r;

            if (left is float fl && right is float fr)
                return fl < fr;

            if (left is int il && right is float flr)
                return il < flr;

            if (left is float fll && right is int ir)
                return fll < ir;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private bool GreaterThan(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l > r;

            if (left is float fl && right is float fr)
                return fl > fr;

            if (left is int il && right is float flr)
                return il > flr;

            if (left is float fll && right is int ir)
                return fll > ir;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private bool NotEquals(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l != r;

            if (left is float fl && right is float fr)
                return fl != fr;

            if (left is int il && right is float flr)
                return il != flr;

            if (left is float fll && right is int ir)
                return fll != ir;
            if (left is string sl && right is string sr)
                return sl != sr;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        private bool IsEquals(object? left, object? right)
        {
            if (left is int l && right is int r)
                return l == r;

            if (left is float fl && right is float fr)
                return fl == fr;

            if (left is int il && right is float flr)
                return il == flr;

            if (left is float fll && right is int ir)
                return fll == ir;
            if (left is string sl && right is string sr)
                return sl == sr;

            State.AddMessage($"Cannot compare values of types {left?.GetType()} and {right?.GetType()}.");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        public override object? VisitMathOperatorExpression([Antlr4.Runtime.Misc.NotNull] GrammarSyntaxParser.MathOperatorExpressionContext context)
        {
            var value = Visit(context.expression());
            string mathOperatorName = context.mathOperator().GetText().ToLower();

            decimal decValue;

            if (value is float f)
            {
                decValue = (decimal)f;
            }
            else if (value is double d) // Handle double to float conversion
            {
                decValue = (decimal)d;
            }
            else if (value is int integer) // Handle int to float conversion
            {
                decValue = (decimal)integer;
            }
            else
            {
                // Try to parse as float from string or other convertible types
                object? arg = value;
                if (arg != null && decimal.TryParse(arg.ToString(), out decimal parsedFloat))
                {
                    decValue = parsedFloat;
                }
                else
                {

                    State.AddMessage("Wrong input type, expected numeric.");
                    State.SetState(ParserState.States.Error);
                    throw new Exception();
                }
            }

            if (mathOperatorName == "abs")
                return (float)Math.Abs(decValue);

            if (mathOperatorName == "round")
                return (float)Math.Round(decValue);

            if (mathOperatorName == "ceil")
                return (float)Math.Ceiling(decValue);

            if (mathOperatorName == "floor")
                return (float)Math.Floor(decValue);

            State.AddMessage($"Wrong math operator type: {mathOperatorName}");
            State.SetState(ParserState.States.Error);
            throw new Exception();
        }

        public override object? VisitWhileBlock(GrammarSyntaxParser.WhileBlockContext context)
        {
            var condition = Visit(context.expression());

            if (condition is bool conditionBool)
            {
                while (conditionBool)
                {
                    Visit(context.block());
                    condition = Visit(context.expression());

                    if (condition is bool newConditionBool)
                        conditionBool = newConditionBool;
                    else
                    {
                        State.AddMessage($"While loop condition did not evaluate to a boolean after the first iteration.");
                        State.SetState(ParserState.States.Error);
                        throw new Exception();
                    }
                }
            }
            else
            {
                State.AddMessage($"While loop condition is not a boolean expression.");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }

            return null;
        }

        public override object? VisitForBlock(GrammarSyntaxParser.ForBlockContext context)
        {
            Visit(context.assignment(0));
            var condition = Visit(context.expression());

            if (condition is bool conditionBool)
            {
                while (conditionBool)
                {
                    Visit(context.block());
                    Visit(context.assignment(1));
                    // Re-evaluate the condition after executing the block
                    condition = Visit(context.expression());
                    if (condition is bool newConditionBool)
                        conditionBool = newConditionBool;
                    else
                    {
                        State.AddMessage($"For loop condition did not evaluate to a boolean after the first iteration.");
                        State.SetState(ParserState.States.Error);
                        throw new Exception();
                    }
                }
            }
            else
            {
                State.AddMessage($"For loop condition did not evaluate to a boolean");
                State.SetState(ParserState.States.Error);
                throw new Exception();
            }
            return null;
        }
    }
    public class FunctionReturnException : Exception
    {
        public object? ReturnValue { get; }

        public FunctionReturnException(object? returnValue)
        {
            ReturnValue = returnValue;
        }
    }
}
