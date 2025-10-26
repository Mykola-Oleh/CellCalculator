using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace CellCalculator
{
    public class SpreadsheetModel
    {
        public int Rows { get; set; }
        public int Cols { get; set; }
        public Dictionary<string, Cell> Cells { get; set; } = new Dictionary<string, Cell>();

        private static readonly Regex cellRefRegex = new Regex(@"[A-Za-z]+\d+", RegexOptions.Compiled);

        public SpreadsheetModel(int rows = 10, int cols = 10)
        {
            Rows = rows;
            Cols = cols;
            for (int r = 1; r <= Rows; r++)
            {
                for (int c = 1; c <= Cols; c++)
                {
                    var addr = ToAddr(c, r);
                    Cells[addr] = new Cell { Address = addr, Expression = "" };
                }
            }
        }

        public static string ToAddr(int col, int row)
        {
            string s = "";
            while (col > 0)
            {
                int rem = (col - 1) % 26;
                s = (char)('A' + rem) + s;
                col = (col - 1) / 26;
            }
            return s + row.ToString();
        }

        public static string ToColName(int col)
        {
            string s = "";
            while (col > 0)
            {
                int rem = (col - 1) % 26;
                s = (char)('A' + rem) + s;
                col = (col - 1) / 26;
            }
            return s;
        }

        public void CopyDataFrom(SpreadsheetModel oldModel)
        {
            foreach (var oldCell in oldModel.Cells)
            {
                if (this.Cells.TryGetValue(oldCell.Key, out var newCell))
                {
                    newCell.Expression = oldCell.Value.Expression;
                }
            }
        }

        public (List<SyntaxError> errors, bool ok) SyntaxCheck(string expr)
        {
            var input = new AntlrInputStream(expr);
            var lexer = new SpreadsheetLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new SpreadsheetParser(tokens);
            var errorListener = new SyntaxErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            parser.compileUnit();
            return (errorListener.Errors, errorListener.Errors.Count == 0);
        }

        public void RecalculateAll()
        {
            var deps = new Dictionary<string, HashSet<string>>();
            foreach (var kv in Cells)
            {
                deps[kv.Key] = new HashSet<string>();
                var expr = kv.Value.Expression?.Trim();
                if (string.IsNullOrEmpty(expr)) continue;
                var refs = ExtractCellRefs(expr);
                foreach (var r in refs)
                {
                    if (Cells.ContainsKey(r))
                        deps[kv.Key].Add(r);
                }
            }

            var cycle = FindCycle(deps);
            if (cycle != null)
            {
                foreach (var addr in cycle)
                {
                    if (Cells.ContainsKey(addr))
                    {
                        Cells[addr].DisplayValue = "CYCLE";
                        Cells[addr].HasError = true;
                    }
                }
            }

            var indegree = new Dictionary<string, int>();
            foreach (var k in deps.Keys) indegree[k] = 0;

            var reverseDeps = new Dictionary<string, HashSet<string>>();
            foreach (var k in deps.Keys) reverseDeps[k] = new HashSet<string>();

            foreach (var kvp in deps)
            {
                string u = kvp.Key;
                foreach (string v in kvp.Value)
                {
                    if (deps.ContainsKey(v))
                    {
                        if (!reverseDeps.ContainsKey(v)) reverseDeps[v] = new HashSet<string>();
                        reverseDeps[v].Add(u);
                        indegree[u]++;
                    }
                }
            }

            var q = new Queue<string>();
            foreach (var k in indegree.Keys) if (indegree[k] == 0) q.Enqueue(k);

            var order = new List<string>();
            while (q.Count > 0)
            {
                var n = q.Dequeue();
                order.Add(n);

                if (reverseDeps.TryGetValue(n, out var neighbors))
                {
                    foreach (var m in neighbors)
                    {
                        if (!indegree.ContainsKey(m)) continue;
                        indegree[m]--;
                        if (indegree[m] == 0) q.Enqueue(m);
                    }
                }
            }

            foreach (var addr in order)
            {
                if (Cells[addr].HasError && Cells[addr].DisplayValue == "CYCLE") continue;

                var cell = Cells[addr];
                cell.HasError = false;
                cell.DisplayValue = "";
                var expr = (cell.Expression ?? "").Trim();
                if (string.IsNullOrEmpty(expr)) { cell.DisplayValue = ""; continue; }

                var input = new AntlrInputStream(expr);
                var lexer = new SpreadsheetLexer(input);
                var tokens = new CommonTokenStream(lexer);
                var parser = new SpreadsheetParser(tokens);
                var errListener = new SyntaxErrorListener();
                parser.RemoveErrorListeners();
                parser.AddErrorListener(errListener);
                var tree = parser.compileUnit();

                if (errListener.Errors.Count > 0)
                {
                    var first = errListener.Errors.First();
                    cell.DisplayValue = $"Syntax error at {first.Line}:{first.CharPositionInLine}";
                    cell.HasError = true;
                    continue;
                }

                var visitor = new ExprEvaluatorVisitor((cellRef) =>
                {
                    if (!Cells.ContainsKey(cellRef)) return new EvalResult($"Unknown cell {cellRef}");
                    var depCell = Cells[cellRef];

                    if (depCell.HasError) return new EvalResult($"Ref error: {depCell.DisplayValue ?? "ERR"}");

                    if (string.IsNullOrEmpty(depCell.Expression)) return new EvalResult(BigInteger.Zero);

                    if (BigInteger.TryParse(depCell.DisplayValue, out var bi)) return new EvalResult(bi);

                    return new EvalResult($"Ref value is not a number: {depCell.DisplayValue}");
                });

                var result = visitor.Visit(tree);

                if (result == null)
                {
                    cell.HasError = true;
                    cell.DisplayValue = "ПОМИЛКА: Неправильний вираз.";
                }
                else if (result.Error != null)
                {
                    cell.HasError = true;
                    cell.DisplayValue = result.Error;
                }
                else
                {
                    cell.DisplayValue = result.Value.ToString();
                    cell.HasError = false;
                }
            }
        }

        public IEnumerable<string> ExtractCellRefs(string expr)
        {
            foreach (Match m in cellRefRegex.Matches(expr))
                yield return m.Value.ToUpperInvariant();
        }

        public List<string>? FindCycle(Dictionary<string, HashSet<string>> deps)
        {
            var visited = new HashSet<string>();
            var onStack = new HashSet<string>();
            var path = new Stack<string>();

            foreach (var node in deps.Keys)
            {
                if (!visited.Contains(node))
                {
                    var cycle = Dfs(node, deps, visited, onStack, path);
                    if (cycle != null)
                    {
                        return cycle;
                    }
                }
            }
            return null;
        }

        private List<string>? Dfs(string node, Dictionary<string, HashSet<string>> deps,
                                 HashSet<string> visited, HashSet<string> onStack, Stack<string> path)
        {
            visited.Add(node);
            onStack.Add(node);
            path.Push(node);

            if (deps.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!deps.ContainsKey(neighbor)) continue;

                    if (!visited.Contains(neighbor))
                    {
                        var cycle = Dfs(neighbor, deps, visited, onStack, path);
                        if (cycle != null)
                        {
                            return cycle;
                        }
                    }
                    else if (onStack.Contains(neighbor))
                    {
                        var cycle = new List<string>();
                        cycle.Add(neighbor);
                        foreach (var item in path)
                        {
                            cycle.Add(item);
                            if (item == neighbor) break;
                        }
                        cycle.Reverse();
                        return cycle;
                    }
                }
            }

            onStack.Remove(node);
            path.Pop();
            return null;
        }

        public string GetCellDisplay(string address)
        {
            if (!Cells.ContainsKey(address)) return "";
            return Cells[address].DisplayValue ?? "";
        }
    }
}