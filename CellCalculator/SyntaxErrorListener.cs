using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

public class SyntaxError
{
    public int Line { get; init; }
    public int CharPositionInLine { get; init; }
    public string Message { get; init; } = "";
}

public class SyntaxErrorListener : BaseErrorListener
{
    public List<SyntaxError> Errors { get; } = new List<SyntaxError>();
    public override void SyntaxError(
        [NotNull] IRecognizer recognizer,
        [Nullable] IToken offendingSymbol,
        int line,
        int charPositionInLine,
        [NotNull] string msg,
        [Nullable] RecognitionException e)
    {
        string symbolText = offendingSymbol?.Text ?? "EOF";

        Errors.Add(new SyntaxError
        {
            Line = line,
            CharPositionInLine = charPositionInLine,
            Message = $"({symbolText}): {msg}"
        });

    }

    public bool HasErrors() => Errors.Count > 0;
}