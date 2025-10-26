using Xunit;
using CellCalculator;
using System.Collections.Generic;
using System.Linq;
namespace Tests
{
    public class SpreadsheetModelTest
    {
        private (string displayValue, bool hasError) Evaluate(string expressionToTest, Dictionary<string, string>? dependencies = null)
        {
            var model = new SpreadsheetModel(10, 10);

            if (dependencies != null)
            {
                foreach (var dep in dependencies)
                {
                    if (model.Cells.ContainsKey(dep.Key))
                    {
                        model.Cells[dep.Key].Expression = dep.Value;
                    }
                }
            }

            model.Cells["A1"].Expression = expressionToTest;

            model.RecalculateAll();

            return (model.GetCellDisplay("A1"), model.Cells["A1"].HasError);
        }

        [Theory]
        [InlineData(1, 1, "A1")]
        [InlineData(26, 1, "Z1")]
        [InlineData(27, 1, "AA1")]
        [InlineData(703, 100, "AAA100")]
        public void TestAddressConversion(int col, int row, string expected)
        {
            string result = SpreadsheetModel.ToAddr(col, row);

            Xunit.Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("5 + 3", "8")]
        [InlineData("10 * (2 + 1)", "30")]
        [InlineData("inc(10)", "11")]
        [InlineData("dec(5)", "4")]
        [InlineData("10 div 3", "3")]
        [InlineData("10 mod 3", "1")]
        public void TestArithmeticCalculations(string formula, string expected)
        {
            var (result, hasError) = Evaluate(formula);

            Xunit.Assert.Equal(expected, result);
            Xunit.Assert.False(hasError);
        }

        [Fact]
        public void TestDependencyCalculations()
        {
            var dependencies = new Dictionary<string, string>
            {
                { "B1", "10" },
                { "C1", "inc(B1)" }
            };
            var formula = "B1 + C1";
            var expected = "21";

            var (result, hasError) = Evaluate(formula, dependencies);

            Xunit.Assert.Equal(expected, result);
            Xunit.Assert.False(hasError);
        }

        [Theory]
        [InlineData("5 * (10", "Syntax error")]
        [InlineData("inc(1, 2)", "Syntax error")]
        public void TestSyntaxErrors(string formula, string expectedErrorStart)
        {
            var (result, hasError) = Evaluate(formula);

            Xunit.Assert.True(hasError);
            Xunit.Assert.StartsWith(expectedErrorStart, result);
        }

        [Fact]
        public void TestCycleErrors()
        {
            var dependencies = new Dictionary<string, string>
            {
                { "B1", "A1 + 1" }
            };
            var formula = "B1 * 2";
            var expectedError = "CYCLE";

            var (result, hasError) = Evaluate(formula, dependencies);

            Xunit.Assert.True(hasError);
            Xunit.Assert.Equal(expectedError, result);
        }

        [Fact]
        public void TestReferenceErrors()
        {
            var dependencies = new Dictionary<string, string>
            {
                { "B1", "10 / / 5" }
            };
            var formula = "B1 + 5";
            var expectedError = "Ref error:";

            var (result, hasError) = Evaluate(formula, dependencies);

            Xunit.Assert.True(hasError);
            Xunit.Assert.StartsWith(expectedError, result);
        }
    }
}
