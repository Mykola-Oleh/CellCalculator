using System;
using System.Numerics;
using Antlr4.Runtime.Misc;

namespace CellCalculator
{
    public class EvalResult
    {
        public BigInteger Value { get; }
        public string? Error { get; }

        public EvalResult(BigInteger value)
        {
            Value = value;
            Error = null;
        }

        public EvalResult(string error)
        {
            Error = error;
            Value = 0;
        }

        public bool IsError => Error != null;
    }

    public class ExprEvaluatorVisitor : SpreadsheetBaseVisitor<EvalResult>
    {
        private readonly Func<string, EvalResult> resolveCell;

        public ExprEvaluatorVisitor(Func<string, EvalResult> resolveCell)
        {
            this.resolveCell = resolveCell;
        }

        public override EvalResult VisitCompileUnit([NotNull] SpreadsheetParser.CompileUnitContext context)
        {
            return Visit(context.expression());
        }

        public override EvalResult VisitNumberExpr([NotNull] SpreadsheetParser.NumberExprContext context)
        {
            var txt = context.NUMBER().GetText();
            try
            {
                var bi = BigInteger.Parse(txt);
                return new EvalResult(bi);
            }
            catch (Exception)
            {
                return new EvalResult($"Помилкове числове значення: {txt}");
            }
        }

        public override EvalResult VisitCellReferenceExpr([NotNull] SpreadsheetParser.CellReferenceExprContext context)
        {
            var token = context.CELL_REF().GetText();
            return resolveCell(token.ToUpperInvariant());
        }

        public override EvalResult VisitParenthesizedExpr([NotNull] SpreadsheetParser.ParenthesizedExprContext context)
        {
            return Visit(context.expression());
        }

        public override EvalResult VisitUnarySignExpr([NotNull] SpreadsheetParser.UnarySignExprContext context)
        {
            var operand = Visit(context.expression());
            if (operand.IsError) return operand;

            var op = context.unaryOp.Text;

            try
            {
                if (op == "+") return operand;
                if (op == "-") return new EvalResult(BigInteger.Negate(operand.Value));

                return new EvalResult($"Невідомий унарний оператор: {op}");
            }
            catch (Exception ex)
            {
                return new EvalResult($"Помилка унарної операції '{op}': {ex.Message}");
            }
        }

        public override EvalResult VisitMultiplicativeExpr([NotNull] SpreadsheetParser.MultiplicativeExprContext context)
        {
            var left = Visit(context.expression(0));
            if (left.IsError) return left;
            var right = Visit(context.expression(1));
            if (right.IsError) return right;

            var op = context.operatorToken.Text.ToLowerInvariant();

            try
            {
                switch (op)
                {
                    case "*": return new EvalResult(left.Value * right.Value);
                    case "/":
                        if (right.Value.IsZero) return new EvalResult("Помилка: Ділення на нуль");
                        return new EvalResult(left.Value / right.Value);
                    case "mod":
                        if (right.Value.IsZero) return new EvalResult("Помилка: Modulo на нуль");
                        return new EvalResult(left.Value % right.Value);
                    case "div":
                        if (right.Value.IsZero) return newEvalDivByZero();
                        return new EvalResult(left.Value / right.Value);
                    default: return new EvalResult($"Невідомий оператор: {op}");
                }
            }
            catch (Exception ex)
            {
                return new EvalResult($"Помилка при обчисленні '{op}': {ex.Message}");
            }

            static EvalResult newEvalDivByZero() => new EvalResult("Помилка: Цілочисельне div на нуль");
        }

        public override EvalResult VisitAdditiveExpr([NotNull] SpreadsheetParser.AdditiveExprContext context)
        {
            var left = Visit(context.expression(0));
            if (left.IsError) return left;
            var right = Visit(context.expression(1));
            if (right.IsError) return right;

            var op = context.operatorToken.Text;

            try
            {
                if (op == "+") return new EvalResult(left.Value + right.Value);
                if (op == "-") return new EvalResult(left.Value - right.Value);

                return new EvalResult($"Невідомий оператор: {op}");
            }
            catch (Exception ex)
            {
                return new EvalResult($"Помилка при обчисленні '{op}': {ex.Message}");
            }
        }

        public override EvalResult VisitFunctionCallExpr([NotNull] SpreadsheetParser.FunctionCallExprContext context)
        {
            var funcContext = context.functionCall();
            var name = funcContext.GetChild(0).GetText().ToLowerInvariant();
            var arg = Visit(funcContext.expression());

            if (arg.IsError) return arg;

            try
            {
                switch (name)
                {
                    case "inc": return new EvalResult(arg.Value + 1);
                    case "dec": return new EvalResult(arg.Value - 1);
                    default: return new EvalResult($"Помилка: Невідома функція '{name}'");
                }
            }
            catch (Exception ex)
            {
                return new EvalResult($"Помилка виконання функції '{name}': {ex.Message}");
            }
        }

        public override EvalResult VisitInvalidRefExpr([NotNull] SpreadsheetParser.InvalidRefExprContext context)
        {
            return new EvalResult("Некоректне посилання на клітинку");
        }
    }
}