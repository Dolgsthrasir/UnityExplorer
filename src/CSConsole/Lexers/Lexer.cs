namespace UnityExplorer.CSConsole.Lexers
{
    public abstract class Lexer
    {
        public virtual IEnumerable<char> Delimiters => Enumerable.Empty<char>();

        protected abstract Color HighlightColor { get; }

        public string ColorTag => this.colorTag ?? (this.colorTag = "<color=#" + this.HighlightColor.ToHex() + ">");
        private string colorTag;

        public abstract bool TryMatchCurrent(LexerBuilder lexer);
    }
}
