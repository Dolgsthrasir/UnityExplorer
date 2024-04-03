using System.Collections.Specialized;
using UnityExplorer.CacheObject.IValues;
using UnityExplorer.UI.Panels;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Widgets.AutoComplete
{
    public class EnumCompleter : ISuggestionProvider
    {
        public bool Enabled
        {
            get => this._enabled;
            set
            {
                this._enabled = value;
                if (!this._enabled)
                    AutoCompleteModal.Instance.ReleaseOwnership(this);
            }
        }
        private bool _enabled = true;

        public event Action<Suggestion> SuggestionClicked;

        public Type EnumType { get; set; }

        public InputFieldRef InputField { get; }
        public bool AnchorToCaretPosition => false;

        private readonly List<Suggestion> suggestions = new();
        private readonly HashSet<string> suggestedValues = new();

        private OrderedDictionary enumValues;

        internal string chosenSuggestion;

        bool ISuggestionProvider.AllowNavigation => false;

        public EnumCompleter(Type enumType, InputFieldRef inputField)
        {
            this.EnumType = enumType;
            this.InputField = inputField;

            inputField.OnValueChanged += this.OnInputFieldChanged;

            if (this.EnumType != null) this.CacheEnumValues();
        }

        public void CacheEnumValues()
        {
            this.enumValues = InteractiveEnum.GetEnumValues(this.EnumType);
        }

        private string GetLastSplitInput(string fullInput)
        {
            string ret = fullInput;

            int lastSplit = fullInput.LastIndexOf(',');
            if (lastSplit >= 0)
            {
                lastSplit++;
                if (lastSplit == fullInput.Length)
                    ret = "";
                else
                    ret = fullInput.Substring(lastSplit);
            }

            return ret;
        }

        public void OnSuggestionClicked(Suggestion suggestion)
        {
            this.chosenSuggestion = suggestion.UnderlyingValue;

            string lastInput = this.GetLastSplitInput(this.InputField.Text);

            if (lastInput != suggestion.UnderlyingValue)
            {
                string valueToSet = this.InputField.Text;

                if (valueToSet.Length > 0)
                    valueToSet = valueToSet.Substring(0, this.InputField.Text.Length - lastInput.Length);

                valueToSet += suggestion.UnderlyingValue;

                this.InputField.Text = valueToSet;

                //InputField.Text += suggestion.UnderlyingValue.Substring(lastInput.Length);
            }

            this.SuggestionClicked?.Invoke(suggestion);

            this.suggestions.Clear();
            AutoCompleteModal.Instance.SetSuggestions(this.suggestions);
        }

        public void HelperButtonClicked()
        {
            this.GetSuggestions("");
            AutoCompleteModal.TakeOwnership(this);
            AutoCompleteModal.Instance.SetSuggestions(this.suggestions);
        }

        private void OnInputFieldChanged(string value)
        {
            if (!this.Enabled)
                return;

            if (string.IsNullOrEmpty(value) || this.GetLastSplitInput(value) == this.chosenSuggestion)
            {
                this.chosenSuggestion = null;
                AutoCompleteModal.Instance.ReleaseOwnership(this);
            }
            else
            {
                this.GetSuggestions(value);

                AutoCompleteModal.TakeOwnership(this);
                AutoCompleteModal.Instance.SetSuggestions(this.suggestions);
            }
        }

        private void GetSuggestions(string value)
        {
            this.suggestions.Clear();
            this.suggestedValues.Clear();

            if (this.EnumType == null)
            {
                ExplorerCore.LogWarning("Autocompleter Base enum type is null!");
                return;
            }

            value = this.GetLastSplitInput(value);

            for (int i = 0; i < this.enumValues.Count; i++)
            {
                CachedEnumValue enumValue = (CachedEnumValue)this.enumValues[i];
                if (enumValue.Name.ContainsIgnoreCase(value)) this.AddSuggestion(enumValue.Name);
            }
        }

        internal static readonly Dictionary<string, string> sharedValueToLabel = new(4096);

        void AddSuggestion(string value)
        {
            if (this.suggestedValues.Contains(value))
                return;
            this.suggestedValues.Add(value);

            if (!sharedValueToLabel.ContainsKey(value))
                sharedValueToLabel.Add(value, $"<color={SignatureHighlighter.CONST}>{value}</color>");

            this.suggestions.Add(new Suggestion(sharedValueToLabel[value], value));
        }
    }
}
