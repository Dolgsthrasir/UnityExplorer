using System.Collections.Specialized;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveEnum : InteractiveValue
    {
        public bool IsFlags;
        public Type EnumType;

        private Type lastType;

        public OrderedDictionary CurrentValues;

        private InputFieldRef inputField;
        private ButtonRef enumHelperButton;
        private EnumCompleter enumCompleter;

        private GameObject toggleHolder;
        private readonly List<Toggle> flagToggles = new();
        private readonly List<Text> flagTexts = new();

        public CachedEnumValue ValueAtIndex(int idx) => (CachedEnumValue)this.CurrentValues[idx];
        public CachedEnumValue ValueAtKey(object key) => (CachedEnumValue)this.CurrentValues[key];

        // Setting value from owner
        public override void SetValue(object value)
        {
            this.EnumType = value.GetType();

            if (this.lastType != this.EnumType)
            {
                this.CurrentValues = GetEnumValues(this.EnumType);

                this.IsFlags = this.EnumType.GetCustomAttributes(typeof(FlagsAttribute), true) is object[] fa && fa.Any();
                if (this.IsFlags)
                    this.SetupTogglesForEnumType();
                else
                {
                    this.inputField.Component.gameObject.SetActive(true);
                    this.enumHelperButton.Component.gameObject.SetActive(true);
                    this.toggleHolder.SetActive(false);
                }

                this.enumCompleter.EnumType = this.EnumType;
                this.enumCompleter.CacheEnumValues();

                this.lastType = this.EnumType;
            }

            if (!this.IsFlags)
                this.inputField.Text = value.ToString();
            else
                this.SetTogglesForValue(value);

            this.enumCompleter.chosenSuggestion = value.ToString();
            AutoCompleteModal.Instance.ReleaseOwnership(this.enumCompleter);
        }

        private void SetTogglesForValue(object value)
        {
            try
            {
                for (int i = 0; i < this.CurrentValues.Count; i++) this.flagToggles[i].isOn = (value as Enum).HasFlag(this.ValueAtIndex(i).ActualValue as Enum);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("Exception setting flag toggles: " + ex);
            }
        }

        // Setting value to owner

        private void OnApplyClicked()
        {
            try
            {
                if (!this.IsFlags)
                {
                    if (ParseUtility.TryParse(this.inputField.Text, this.EnumType, out object value, out Exception ex))
                        this.CurrentOwner.SetUserValue(value);
                    else
                        throw ex;
                }
                else
                {
                    this.SetValueFromFlags();
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("Exception setting from dropdown: " + ex);
            }
        }

        private void SetValueFromFlags()
        {
            try
            {
                List<string> values = new();
                for (int i = 0; i < this.CurrentValues.Count; i++)
                {
                    if (this.flagToggles[i].isOn)
                        values.Add(this.ValueAtIndex(i).Name);
                }

                this.CurrentOwner.SetUserValue(Enum.Parse(this.EnumType, string.Join(", ", values.ToArray())));
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("Exception setting from flag toggles: " + ex);
            }
        }

        // UI Construction

        private void EnumHelper_OnClick()
        {
            this.enumCompleter.HelperButtonClicked();
        }

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveEnum", false, false, true, true, 3, new Vector4(4, 4, 4, 4),
                new Color(0.06f, 0.06f, 0.06f));
            UIFactory.SetLayoutElement(this.UIRoot, minHeight: 25, flexibleHeight: 9999, flexibleWidth: 9999);

            GameObject hori = UIFactory.CreateUIObject("Hori", this.UIRoot);
            UIFactory.SetLayoutElement(hori, minHeight: 25, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(hori, false, false, true, true, 2);

            ButtonRef applyButton = UIFactory.CreateButton(hori, "ApplyButton", "Apply", new Color(0.2f, 0.27f, 0.2f));
            UIFactory.SetLayoutElement(applyButton.Component.gameObject, minHeight: 25, minWidth: 100);
            applyButton.OnClick += this.OnApplyClicked;

            this.inputField = UIFactory.CreateInputField(hori, "InputField", "Enter name or underlying value...");
            UIFactory.SetLayoutElement(this.inputField.UIRoot, minHeight: 25, flexibleHeight: 50, minWidth: 100, flexibleWidth: 1000);
            this.inputField.Component.lineType = InputField.LineType.MultiLineNewline;
            this.inputField.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.enumHelperButton = UIFactory.CreateButton(hori, "EnumHelper", "▼");
            UIFactory.SetLayoutElement(this.enumHelperButton.Component.gameObject, minWidth: 25, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.enumHelperButton.OnClick += this.EnumHelper_OnClick;

            this.enumCompleter = new EnumCompleter(this.EnumType, this.inputField);

            this.toggleHolder = UIFactory.CreateUIObject("ToggleHolder", this.UIRoot);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.toggleHolder, false, false, true, true, 4);
            UIFactory.SetLayoutElement(this.toggleHolder, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

            return this.UIRoot;
        }

        private void SetupTogglesForEnumType()
        {
            this.toggleHolder.SetActive(true);
            this.inputField.Component.gameObject.SetActive(false);
            this.enumHelperButton.Component.gameObject.SetActive(false);

            // create / set / hide toggles
            for (int i = 0; i < this.CurrentValues.Count || i < this.flagToggles.Count; i++)
            {
                if (i >= this.CurrentValues.Count)
                {
                    if (i >= this.flagToggles.Count)
                        break;

                    this.flagToggles[i].gameObject.SetActive(false);
                    continue;
                }

                if (i >= this.flagToggles.Count) this.AddToggleRow();

                this.flagToggles[i].isOn = false;
                this.flagTexts[i].text = this.ValueAtIndex(i).Name;
            }
        }

        private void AddToggleRow()
        {
            GameObject row = UIFactory.CreateUIObject("ToggleRow", this.toggleHolder);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(row, false, false, true, true, 2);
            UIFactory.SetLayoutElement(row, minHeight: 25, flexibleWidth: 9999);

            GameObject toggleObj = UIFactory.CreateToggle(row, "ToggleObj", out Toggle toggle, out Text toggleText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, flexibleWidth: 9999);

            this.flagToggles.Add(toggle);
            this.flagTexts.Add(toggleText);
        }

        #region Enum cache 

        internal static readonly Dictionary<string, OrderedDictionary> enumCache = new();

        internal static OrderedDictionary GetEnumValues(Type enumType)
        {
            //isFlags = enumType.GetCustomAttributes(typeof(FlagsAttribute), true) is object[] fa && fa.Any();

            if (!enumCache.ContainsKey(enumType.AssemblyQualifiedName))
            {
                OrderedDictionary dict = new();
                HashSet<string> addedNames = new();

                int i = 0;
                foreach (object value in Enum.GetValues(enumType))
                {
                    string name = value.ToString();
                    if (addedNames.Contains(name))
                        continue;
                    addedNames.Add(name);

                    dict.Add(value, new CachedEnumValue(value, i, name));
                    i++;
                }

                enumCache.Add(enumType.AssemblyQualifiedName, dict);
            }

            return enumCache[enumType.AssemblyQualifiedName];
        }

        #endregion
    }

    public struct CachedEnumValue
    {
        public CachedEnumValue(object value, int index, string name)
        {
            this.EnumIndex = index;
            this.Name = name;
            this.ActualValue = value;
        }

        public readonly object ActualValue;
        public int EnumIndex;
        public readonly string Name;
    }
}
