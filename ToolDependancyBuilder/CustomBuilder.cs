using Antlr4.Runtime.Misc;
using System.Linq.Expressions;

namespace ToolDependancyBuilder
{ 
    public class CustomBuilder : ToolExpressionBaseVisitor<Expression>
    {
        private ParameterExpression _devicesParam = Expression.Parameter(typeof(Dictionary<char, float>), "devices");
        private List<char> _devNames;

        public CustomBuilder(List<char> devNames)
        {
            _devNames = devNames;
        }

        public override Expression VisitDeviceNameExpression([NotNull] ToolExpressionParser.DeviceNameExpressionContext context)
        {
            char deviceName = context.DEVICE_NAME().GetText().ToCharArray()[0];

            if (!_devNames.Contains(deviceName))
                throw new ArgumentException($"Could not find device with name {deviceName}");
            
            Expression key = Expression.Constant(deviceName);
            Expression deviceValue = Expression.Property(_devicesParam, "Item", key);

            return deviceValue;
        }

        public override Expression VisitTrigOpExpression([NotNull] ToolExpressionParser.TrigOpExpressionContext context)
        {
            Expression argument = Visit(context.expression()); // Recursively visit the argument of the trig function

            // Ensure the argument is of type double
            if (argument.Type != typeof(double))
            {
                argument = Expression.Convert(argument, typeof(double));
            }

            var trigFunc = context.trigFunc().GetText();

            // Assuming the Math methods are correctly referenced (note: there's no Math.Ctan or Math.Acos method in .NET)
            return trigFunc switch
            {
                "sin" => Expression.Call(typeof(Math).GetMethod("Sin"), argument),
                "cos" => Expression.Call(typeof(Math).GetMethod("Cos"), argument),
                "tan" => Expression.Call(typeof(Math).GetMethod("Tan"), argument),
                // "ctan" is not a standard .NET Math method. You might need to implement this yourself.
                "asin" => Expression.Call(typeof(Math).GetMethod("Asin"), argument),
                "acos" => Expression.Call(typeof(Math).GetMethod("Acos"), argument),
                // Add other cases as needed.
                _ => throw new NotImplementedException($"Trigonometric function '{trigFunc}' is not implemented.")
            };
        }

        public override Expression VisitConstant([NotNull] ToolExpressionParser.ConstantContext context)
        {
            var akaka = context.STRING();

            if (context.INTEGER() is { } i)
            {
                return Expression.Constant((int.Parse(i.GetText())));
            }

            if (context.FLOAT() is { } f)
                return Expression.Constant(float.Parse(f.GetText()));

            if (context.STRING() is { } s) 
            {
                var akakakak = s.GetText();
                if (s.GetText().Equals("pi") || s.GetText().Equals("Pi"))
                    return Expression.Constant(3.14159f);
            }
            throw new NotImplementedException($"Unknown constant.");
        }

        public override Expression VisitAddOpExpression([NotNull] ToolExpressionParser.AddOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.addOp().GetText();
            
            if (left.Type != typeof(float))
            {
                left = Expression.Convert(left, typeof(float));
            }
            if (right.Type != typeof(float))
            {
                right = Expression.Convert(right, typeof(float));
            }
            
            return op switch
            {
                "+" => Expression.Add(left, right),
                "-" => Expression.Subtract(left, right),
                _ => throw new NotImplementedException(),
            };
        }

        public override Expression VisitMultOpExpression([NotNull] ToolExpressionParser.MultOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            var op = context.multOp().GetText();

            // Ensure both operands are of type float, converting if necessary
            if (left.Type != typeof(float))
            {
                left = Expression.Convert(left, typeof(float));
            }
            if (right.Type != typeof(float))
            {
                right = Expression.Convert(right, typeof(float));
            }

            return op switch
            {
                "*" => Expression.Multiply(left, right),
                "/" => Expression.Divide(left, right),
                "%" => Expression.Modulo(left, right),
                _ => throw new NotImplementedException(),
            };
        }

        public override Expression VisitParenthesizedExpression([NotNull] ToolExpressionParser.ParenthesizedExpressionContext context)
        {
            return Visit(context.expression());
        }

        public override Expression VisitDoubleExpression([NotNull] ToolExpressionParser.DoubleExpressionContext context)
        {
            throw new Exception("Double expression found");
        }

        public override Expression VisitPowerOpExpression([NotNull] ToolExpressionParser.PowerOpExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            // Ensure arguments are of type double for Math.Pow
            if (left.Type != typeof(double))
            {
                left = Expression.Convert(left, typeof(double));
            }
            if (right.Type != typeof(double))
            {
                right = Expression.Convert(right, typeof(double));
            }

            // Call Math.Pow, which returns double
            var powerExpr = Expression.Call(typeof(Math).GetMethod("Pow"), left, right);

            // Convert result back to float if necessary
            return Expression.Convert(powerExpr, typeof(float));
        }

        public Expression<Func<Dictionary<char, float>, float>> CompileExprToDelegate(ToolExpressionParser.ExpressionContext context)
        {
            var body = this.Visit(context);

            // Convert the result to float if it's not already a float
            // This is necessary since operations might result in a 'double' type
            if (body.Type != typeof(float))
            {
                body = Expression.Convert(body, typeof(float));
            }

            // Compile the expression into a delegate that takes a dictionary and returns a float
            return Expression.Lambda<Func<Dictionary<char, float>, float>>(body, _devicesParam);
        }
    }
}
