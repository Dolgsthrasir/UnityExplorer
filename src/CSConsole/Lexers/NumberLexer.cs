namespace UnityExplorer.CSConsole.Lexers
{
    public class NumberLexer : Lexer
    {
        // Maroon
        protected override Color HighlightColor => new(0.58f, 0.33f, 0.33f, 1.0f);

        private bool IsNumeric(char c) => char.IsNumber(c) || c == '.';

        public override bool TryMatchCurrent(LexerBuilder lexer)
        {
            // previous character must be whitespace or delimiter
            if (!lexer.IsDelimiter(lexer.Previous, true))
                return false;

            if (!this.IsNumeric(lexer.Current))
                return false;

            while (!lexer.EndOfInput)
            {
                lexer.Commit();
                if (!this.IsNumeric(lexer.PeekNext()))
                    break;
            }

            return true;
        }
    }

}
