using System.Collections;
using System.Diagnostics;
using UnityExplorer.UI.Panels;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Widgets.AutoComplete
{
    public class TypeCompleter : ISuggestionProvider
    {
        public bool Enabled
        {
            get => this.enabled;
            set
            {
                this.enabled = value;
                if (!this.enabled)
                { 
                    AutoCompleteModal.Instance.ReleaseOwnership(this);
                    if (this.getSuggestionsCoroutine != null)
                        RuntimeHelper.StopCoroutine(this.getSuggestionsCoroutine);
                }
            }
        }
        bool enabled = true;

        public event Action<Suggestion> SuggestionClicked;

        public InputFieldRef InputField { get; }
        public bool AnchorToCaretPosition => false;

        readonly bool allowAbstract;
        readonly bool allowEnum;
        readonly bool allowGeneric;

        public Type BaseType { get; set; }
        HashSet<Type> allowedTypes;
        string pendingInput;
        Coroutine getSuggestionsCoroutine;
        readonly Stopwatch cacheTypesStopwatch = new();

        readonly List<Suggestion> suggestions = new();
        readonly HashSet<string> suggestedTypes = new();
        string chosenSuggestion;

        readonly List<Suggestion> loadingSuggestions = new()
        {
            new("<color=grey>Loading...</color>", "")
        };

        bool ISuggestionProvider.AllowNavigation => false;

        static readonly Dictionary<string, Type> shorthandToType = new()
        {
            { "object", typeof(object) },
            { "string", typeof(string) },
            { "bool", typeof(bool) },
            { "byte", typeof(byte) },
            { "sbyte", typeof(sbyte) },
            { "char", typeof(char) },
            { "decimal", typeof(decimal) },
            { "double", typeof(double) },
            { "float", typeof(float) },
            { "int", typeof(int) },
            { "uint", typeof(uint) },
            { "long", typeof(long) },
            { "ulong", typeof(ulong) },
            { "short", typeof(short) },
            { "ushort", typeof(ushort) },
            { "void", typeof(void) },
        };

        public TypeCompleter(Type baseType, InputFieldRef inputField) : this(baseType, inputField, true, true, true) { }

        public TypeCompleter(Type baseType, InputFieldRef inputField, bool allowAbstract, bool allowEnum, bool allowGeneric)
        {
            this.BaseType = baseType;
            this.InputField = inputField;

            this.allowAbstract = allowAbstract;
            this.allowEnum = allowEnum;
            this.allowGeneric = allowGeneric;

            inputField.OnValueChanged += this.OnInputFieldChanged;

            this.CacheTypes();
        }

        public void OnSuggestionClicked(Suggestion suggestion)
        {
            this.chosenSuggestion = suggestion.UnderlyingValue;
            this.InputField.Text = suggestion.UnderlyingValue;
            this.SuggestionClicked?.Invoke(suggestion);

            this.suggestions.Clear();
            //AutoCompleteModal.Instance.SetSuggestions(suggestions, true);
            AutoCompleteModal.Instance.ReleaseOwnership(this);
        }

        public void CacheTypes()
        {
            this.allowedTypes = null;
            this.cacheTypesStopwatch.Reset();
            this.cacheTypesStopwatch.Start();
            ReflectionUtility.GetImplementationsOf(this.BaseType, this.OnTypesCached, this.allowAbstract, this.allowGeneric, this.allowEnum);
        }

        void OnTypesCached(HashSet<Type> set)
        {
            this.allowedTypes = set;

            // ExplorerCore.Log($"Cached {allowedTypes.Count} TypeCompleter types in {cacheTypesStopwatch.ElapsedMilliseconds * 0.001f} seconds.");

            if (this.pendingInput != null)
            {
                this.GetSuggestions(this.pendingInput);
                this.pendingInput = null;
            }
        }

        void OnInputFieldChanged(string input)
        {
            if (!this.Enabled)
                return;

            if (input != this.chosenSuggestion) this.chosenSuggestion = null;

            if (string.IsNullOrEmpty(input) || input == this.chosenSuggestion)
            {
                if (this.getSuggestionsCoroutine != null)
                    RuntimeHelper.StopCoroutine(this.getSuggestionsCoroutine);
                AutoCompleteModal.Instance.ReleaseOwnership(this);
            }
            else
            {
                this.GetSuggestions(input);
            }
        }

        void GetSuggestions(string input)
        {
            if (this.allowedTypes == null)
            {
                if (this.pendingInput != null)
                {
                    AutoCompleteModal.TakeOwnership(this);
                    AutoCompleteModal.Instance.SetSuggestions(this.loadingSuggestions, true);
                }

                this.pendingInput = input;
                return;
            }

            if (this.getSuggestionsCoroutine != null)
                RuntimeHelper.StopCoroutine(this.getSuggestionsCoroutine);

            this.getSuggestionsCoroutine = RuntimeHelper.StartCoroutine(this.GetSuggestionsAsync(input));
        }

        IEnumerator GetSuggestionsAsync(string input)
        {
            this.suggestions.Clear();
            this.suggestedTypes.Clear();

            AutoCompleteModal.TakeOwnership(this);
            AutoCompleteModal.Instance.SetSuggestions(this.suggestions, true);

            // shorthand types all inherit from System.Object
            if (shorthandToType.TryGetValue(input, out Type shorthand) && this.allowedTypes.Contains(shorthand)) this.AddSuggestion(shorthand);

            foreach (KeyValuePair<string, Type> entry in shorthandToType)
            {
                if (this.allowedTypes.Contains(entry.Value) && entry.Key.StartsWith(input, StringComparison.InvariantCultureIgnoreCase)) this.AddSuggestion(entry.Value);
            }

            // Check for exact match first
            if (ReflectionUtility.GetTypeByName(input) is Type t && this.allowedTypes.Contains(t)) this.AddSuggestion(t);

            if (!this.suggestions.Any())
                AutoCompleteModal.Instance.SetSuggestions(this.loadingSuggestions, false);
            else
                AutoCompleteModal.Instance.SetSuggestions(this.suggestions, false);

            Stopwatch sw = new();
            sw.Start();

            // ExplorerCore.Log($"Checking {allowedTypes.Count} types...");

            foreach (Type entry in this.allowedTypes)
            {
                if (AutoCompleteModal.CurrentHandler == null)
                    yield break;

                if (sw.ElapsedMilliseconds > 10)
                {
                    yield return null;
                    if (this.suggestions.Any())
                        AutoCompleteModal.Instance.SetSuggestions(this.suggestions, false);

                    sw.Reset();
                    sw.Start();
                }

                if (entry.FullName.ContainsIgnoreCase(input)) this.AddSuggestion(entry);
            }

            AutoCompleteModal.Instance.SetSuggestions(this.suggestions, false);

            // ExplorerCore.Log($"Fetched {suggestions.Count} TypeCompleter suggestions in {sw.ElapsedMilliseconds * 0.001f} seconds.");
        }

        internal static readonly Dictionary<string, string> sharedTypeToLabel = new();

        void AddSuggestion(Type type)
        {
            if (this.suggestedTypes.Contains(type.FullName))
                return;
            this.suggestedTypes.Add(type.FullName);

            if (!sharedTypeToLabel.ContainsKey(type.FullName))
                sharedTypeToLabel.Add(type.FullName, SignatureHighlighter.Parse(type, true));

            this.suggestions.Add(new Suggestion(sharedTypeToLabel[type.FullName], type.FullName));
        }
    }
}
