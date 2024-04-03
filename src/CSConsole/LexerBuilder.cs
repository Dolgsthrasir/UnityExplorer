using System.Text;
using UnityExplorer.CSConsole.Lexers;

namespace UnityExplorer.CSConsole
{
    public struct MatchInfo
    {
        public int startIndex;
        public int endIndex;
        public bool isStringOrComment;
        public bool matchToEndOfLine;
        public string htmlColorTag;
    }

    public class LexerBuilder
    {
        public const char WHITESPACE = ' ';
        public readonly HashSet<char> IndentOpenChars = new() { '{', '(' };
        public readonly HashSet<char> IndentCloseChars = new() { '}', ')' };

        private readonly Lexer[] lexers;
        private readonly HashSet<char> delimiters = new();

        private readonly StringLexer stringLexer = new();
        private readonly CommentLexer commentLexer = new();

        public LexerBuilder()
        {
            this.lexers = new Lexer[]
            {
                this.commentLexer, this.stringLexer,
                new SymbolLexer(),
                new NumberLexer(),
                new KeywordLexer(),
            };

            foreach (Lexer matcher in this.lexers)
            {
                foreach (char c in matcher.Delimiters)
                {
                    if (!this.delimiters.Contains(c)) this.delimiters.Add(c);
                }
            }
        }

        /// <summary>The last committed index for a match or no-match. Starts at -1 for a new parse.</summary>
        public int CommittedIndex { get; private set; }
        /// <summary>The index of the character we are currently parsing, at minimum it will be CommittedIndex + 1.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>The current character we are parsing, determined by CurrentIndex.</summary>
        public char Current => !this.EndOfInput ? this.currentInput[this.CurrentIndex] : WHITESPACE;
        /// <summary>The previous character (CurrentIndex - 1), or whitespace if no previous character.</summary>
        public char Previous => this.CurrentIndex >= 1 ? this.currentInput[this.CurrentIndex - 1] : WHITESPACE;

        /// <summary>Returns true if CurrentIndex is >= the current input length.</summary>
        public bool EndOfInput => this.CurrentIndex > this.currentEndIdx;
        /// <summary>Returns true if EndOfInput or current character is a new line.</summary>
        public bool EndOrNewLine => this.EndOfInput || IsNewLine(this.Current);

        public static bool IsNewLine(char c) => c == '\n' || c == '\r';

        private string currentInput;
        private int currentStartIdx;
        private int currentEndIdx;

        /// <summary>
        /// Parse the range of the string with the Lexer and build a RichText-highlighted representation of it.
        /// </summary>
        /// <param name="input">The entire input string which you want to parse a section (or all) of</param>
        /// <param name="startIdx">The first character you want to highlight</param>
        /// <param name="endIdx">The last character you want to highlight</param>
        /// <param name="leadingLines">The amount of leading empty lines you want before the first character in the return string.</param>
        /// <returns>A string which contains the amount of leading lines specified, as well as the rich-text highlighted section.</returns>
        public string BuildHighlightedString(string input, int startIdx, int endIdx, int leadingLines, int caretIdx, out bool caretInStringOrComment)
        {
            caretInStringOrComment = false;

            if (string.IsNullOrEmpty(input) || endIdx <= startIdx)
                return input;

            this.currentInput = input;
            this.currentStartIdx = startIdx;
            this.currentEndIdx = endIdx;

            StringBuilder sb = new();

            for (int i = 0; i < leadingLines; i++)
                sb.Append('\n');

            int lastUnhighlighted = startIdx;
            foreach (MatchInfo match in this.GetMatches())
            {
                // append non-highlighted text between last match and this
                for (int i = lastUnhighlighted; i < match.startIndex; i++)
                    sb.Append(input[i]);

                // append the highlighted match
                sb.Append(match.htmlColorTag);
                for (int i = match.startIndex; i <= match.endIndex && i <= this.currentEndIdx; i++)
                    sb.Append(input[i]);
                sb.Append(SignatureHighlighter.CLOSE_COLOR);

                // update the last unhighlighted start index
                lastUnhighlighted = match.endIndex + 1;

                int matchEndIdx = match.endIndex;
                if (match.matchToEndOfLine)
                {
                    while (input.Length - 1 >= matchEndIdx)
                    {
                        matchEndIdx++;
                        if (IsNewLine(input[matchEndIdx]))
                            break;
                    }
                }

                // check caretIdx to determine inStringOrComment state
                if (caretIdx >= match.startIndex && (caretIdx <= (matchEndIdx + 1) || (caretIdx >= input.Length && matchEndIdx >= input.Length - 1)))
                    caretInStringOrComment = match.isStringOrComment;
            }

            // Append trailing unhighlighted input
            while (lastUnhighlighted <= endIdx)
            {
                sb.Append(input[lastUnhighlighted]);
                lastUnhighlighted++;
            }

            return sb.ToString();
        }


        // Match builder, iterates through each Lexer and returns all matches found.

        public IEnumerable<MatchInfo> GetMatches()
        {
            this.CommittedIndex = this.currentStartIdx - 1;
            this.Rollback();

            while (!this.EndOfInput)
            {
                this.SkipWhitespace();
                bool anyMatch = false;
                int startIndex = this.CommittedIndex + 1;

                foreach (Lexer lexer in this.lexers)
                {
                    if (lexer.TryMatchCurrent(this))
                    {
                        anyMatch = true;

                        yield return new MatchInfo
                        {
                            startIndex = startIndex,
                            endIndex = this.CommittedIndex,
                            htmlColorTag = lexer.ColorTag,
                            isStringOrComment = lexer is StringLexer || lexer is CommentLexer,
                        };
                        break;
                    }
                    else
                        this.Rollback();
                }

                if (!anyMatch)
                {
                    this.CurrentIndex = this.CommittedIndex + 1;
                    this.Commit();
                }
            }
        }

        // Methods used by the Lexers for interfacing with the current parse process

        public char PeekNext(int amount = 1)
        {
            this.CurrentIndex += amount;
            return this.Current;
        }

        public void Commit()
        {
            this.CommittedIndex = Math.Min(this.currentEndIdx, this.CurrentIndex);
        }

        public void Rollback()
        {
            this.CurrentIndex = this.CommittedIndex + 1;
        }

        public void RollbackBy(int amount)
        {
            this.CurrentIndex = Math.Max(this.CommittedIndex + 1, this.CurrentIndex - amount);
        }

        public bool IsDelimiter(char character, bool orWhitespace = false, bool orLetterOrDigit = false)
        {
            return this.delimiters.Contains(character)
                || (orWhitespace && char.IsWhiteSpace(character))
                || (orLetterOrDigit && char.IsLetterOrDigit(character));
        }

        private void SkipWhitespace()
        {
            // peek and commit as long as there is whitespace
            while (!this.EndOfInput && char.IsWhiteSpace(this.Current))
            {
                this.Commit();
                this.PeekNext();
            }

            if (!char.IsWhiteSpace(this.Current)) this.Rollback();
        }

        #region Auto Indenting

        // Using the Lexer for indenting as it already has what we need to tokenize strings and comments.
        // At the moment this only handles when a single newline or close-delimiter is composed.
        // Does not handle copy+paste or any other characters yet.

        public string IndentCharacter(string input, ref int caretIndex)
        {
            int lastCharIndex = caretIndex - 1;
            char c = input[lastCharIndex];

            // we only want to indent for new lines and close indents
            if (!IsNewLine(c) && !this.IndentCloseChars.Contains(c))
                return input;

            // perform a light parse up to the caret to determine indent level
            this.currentInput = input;
            this.currentStartIdx = 0;
            this.currentEndIdx = lastCharIndex;
            this.CommittedIndex = -1;
            this.Rollback();

            int indent = 0;

            while (!this.EndOfInput)
            {
                if (this.CurrentIndex >= lastCharIndex)
                {
                    // reached the caret index
                    if (indent <= 0)
                        break;

                    if (IsNewLine(c))
                        input = this.IndentNewLine(input, indent, ref caretIndex);
                    else // closing indent
                        input = this.IndentCloseDelimiter(input, indent, lastCharIndex, ref caretIndex);

                    break;
                }

                // Try match strings and comments (Lexer will commit to the end of the match)
                if (this.stringLexer.TryMatchCurrent(this) || this.commentLexer.TryMatchCurrent(this))
                {
                    this.PeekNext();
                    continue;
                }

                // Still parsing, check indent

                if (this.IndentOpenChars.Contains(this.Current))
                    indent++;
                else if (this.IndentCloseChars.Contains(this.Current))
                    indent--;

                this.Commit();
                this.PeekNext();
            }

            return input;
        }

        private string IndentNewLine(string input, int indent, ref int caretIndex)
        {
            // continue until the end of line or next non-whitespace character.
            // if there's a close-indent on this line, reduce the indent level.
            while (this.CurrentIndex < input.Length - 1)
            {
                this.CurrentIndex++;
                char next = input[this.CurrentIndex];
                if (IsNewLine(next))
                    break;
                if (char.IsWhiteSpace(next))
                    continue;
                else if (this.IndentCloseChars.Contains(next))
                    indent--;

                break;
            }

            if (indent > 0)
            {
                input = input.Insert(caretIndex, new string('\t', indent));
                caretIndex += indent;
            }

            return input;
        }

        private string IndentCloseDelimiter(string input, int indent, int lastCharIndex, ref int caretIndex)
        {
            if (this.CurrentIndex > lastCharIndex)
            {
                return input;
            }

            // lower the indent level by one as we would not have accounted for this closing symbol
            indent--;

            // go back from the caret to the start of the line, calculate how much indent we need to adjust.
            while (this.CurrentIndex > 0)
            {
                this.CurrentIndex--;
                char prev = input[this.CurrentIndex];
                if (IsNewLine(prev))
                    break;
                if (!char.IsWhiteSpace(prev))
                {
                    // the line containing the closing bracket has non-whitespace characters before it. do not indent.
                    indent = 0;
                    break;
                }
                else if (prev == '\t')
                    indent--;
            }

            if (indent > 0)
            {
                input = input.Insert(caretIndex, new string('\t', indent));
                caretIndex += indent;
            }
            else if (indent < 0)
            {
                // line is overly indented
                input = input.Remove(lastCharIndex - 1, -indent);
                caretIndex += indent;
            }

            return input;
        }

        #endregion
    }
}
