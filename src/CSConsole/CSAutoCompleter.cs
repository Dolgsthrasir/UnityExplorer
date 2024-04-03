using System.Text;
using UnityExplorer.CSConsole.Lexers;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI.Models;

namespace UnityExplorer.CSConsole
{
    public class CSAutoCompleter : ISuggestionProvider
    {
        public InputFieldRef InputField => ConsoleController.Input;

        public bool AnchorToCaretPosition => true;

        bool ISuggestionProvider.AllowNavigation => true;

        public void OnSuggestionClicked(Suggestion suggestion)
        {
            ConsoleController.InsertSuggestionAtCaret(suggestion.UnderlyingValue);
            AutoCompleteModal.Instance.ReleaseOwnership(this);
        }

        private readonly HashSet<char> delimiters = new()
        {
            '{',
            '}',
            ',',
            ';',
            '<',
            '>',
            '(',
            ')',
            '[',
            ']',
            '=',
            '|',
            '&',
            '?'
        };

        private readonly List<Suggestion> suggestions = new();

        public void CheckAutocompletes()
        {
            if (string.IsNullOrEmpty(this.InputField.Text))
            {
                AutoCompleteModal.Instance.ReleaseOwnership(this);
                return;
            }

            this.suggestions.Clear();

            int caret = Math.Max(0, Math.Min(this.InputField.Text.Length - 1, this.InputField.Component.caretPosition - 1));
            int startIdx = caret;

            // If the character at the caret index is whitespace or delimiter,
            // or if the next character (if it exists) is not whitespace,
            // then we don't want to provide suggestions.
            if (char.IsWhiteSpace(this.InputField.Text[caret])
                ||
                this.delimiters.Contains(this.InputField.Text[caret])
                || (this.InputField.Text.Length > caret + 1 && !char.IsWhiteSpace(this.InputField.Text[caret + 1])))
            {
                AutoCompleteModal.Instance.ReleaseOwnership(this);
                return;
            }

            // get the current composition string (from caret back to last delimiter)
            while (startIdx > 0)
            {
                startIdx--;
                char c = this.InputField.Text[startIdx];
                if (this.delimiters.Contains(c) || char.IsWhiteSpace(c))
                {
                    startIdx++;
                    break;
                }
            }
            string input = this.InputField.Text.Substring(startIdx, caret - startIdx + 1);

            // Get MCS completions

            string[] evaluatorCompletions = ConsoleController.Evaluator.GetCompletions(input, out string prefix);

            if (evaluatorCompletions != null && evaluatorCompletions.Any())
            {
                this.suggestions.AddRange(from completion in evaluatorCompletions
                                     select new Suggestion(this.GetHighlightString(prefix, completion), completion));
            }

            // Get manual namespace completions

            foreach (string ns in ReflectionUtility.AllNamespaces)
            {
                if (ns.StartsWith(input))
                {
                    if (!this.namespaceHighlights.ContainsKey(ns)) this.namespaceHighlights.Add(ns, $"<color=#CCCCCC>{ns}</color>");

                    string completion = ns.Substring(input.Length, ns.Length - input.Length);
                    this.suggestions.Add(new Suggestion(this.namespaceHighlights[ns], completion));
                }
            }

            // Get manual keyword completions

            foreach (string kw in KeywordLexer.keywords)
            {
                if (kw.StartsWith(input))// && kw.Length > input.Length)
                {
                    if (!this.keywordHighlights.ContainsKey(kw)) this.keywordHighlights.Add(kw, $"<color=#{SignatureHighlighter.keywordBlueHex}>{kw}</color>");

                    string completion = kw.Substring(input.Length, kw.Length - input.Length);
                    this.suggestions.Add(new Suggestion(this.keywordHighlights[kw], completion));
                }
            }

            if (this.suggestions.Any())
            {
                AutoCompleteModal.TakeOwnership(this);
                AutoCompleteModal.Instance.SetSuggestions(this.suggestions);
            }
            else
            {
                AutoCompleteModal.Instance.ReleaseOwnership(this);
            }
        }


        private readonly Dictionary<string, string> namespaceHighlights = new();

        private readonly Dictionary<string, string> keywordHighlights = new();

        private readonly StringBuilder highlightBuilder = new();
        private const string OPEN_HIGHLIGHT = "<color=cyan>";

        private string GetHighlightString(string prefix, string completion)
        {
            this.highlightBuilder.Clear();
            this.highlightBuilder.Append(OPEN_HIGHLIGHT);
            this.highlightBuilder.Append(prefix);
            this.highlightBuilder.Append(SignatureHighlighter.CLOSE_COLOR);
            this.highlightBuilder.Append(completion);
            return this.highlightBuilder.ToString();
        }
    }
}
