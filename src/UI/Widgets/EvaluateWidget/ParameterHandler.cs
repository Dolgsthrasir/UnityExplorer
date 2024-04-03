using HarmonyLib;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Widgets
{
    public class ParameterHandler : BaseArgumentHandler
    {
        private ParameterInfo paramInfo;
        private Type paramType;

        internal EnumCompleter enumCompleter;
        private ButtonRef enumHelperButton;

        private bool usingBasicLabel;
        private object basicValue;
        private GameObject basicLabelHolder;
        private Text basicLabel;
        private ButtonRef pasteButton;

        public void OnBorrowed(ParameterInfo paramInfo)
        {
            this.paramInfo = paramInfo;

            this.paramType = paramInfo.ParameterType;
            if (this.paramType.IsByRef) this.paramType = this.paramType.GetElementType();

            this.argNameLabel.text =
                $"{SignatureHighlighter.Parse(this.paramType, false)} <color={SignatureHighlighter.LOCAL_ARG}>{paramInfo.Name}</color>";

            if (ParseUtility.CanParse(this.paramType) || typeof(Type).IsAssignableFrom(this.paramType))
            {
                this.usingBasicLabel = false;

                this.inputField.Component.gameObject.SetActive(true);
                this.basicLabelHolder.SetActive(false);
                this.typeCompleter.Enabled = typeof(Type).IsAssignableFrom(this.paramType);
                this.enumCompleter.Enabled = this.paramType.IsEnum;
                this.enumHelperButton.Component.gameObject.SetActive(this.paramType.IsEnum);

                if (!this.typeCompleter.Enabled)
                {
                    if (this.paramType == typeof(string))
                        this.inputField.PlaceholderText.text = "...";
                    else
                        this.inputField.PlaceholderText.text = $"eg. {ParseUtility.GetExampleInput(this.paramType)}";
                }
                else
                {
                    this.inputField.PlaceholderText.text = "Enter a Type name...";
                    this.typeCompleter.BaseType = typeof(object);
                    this.typeCompleter.CacheTypes();
                }

                if (this.enumCompleter.Enabled)
                {
                    this.enumCompleter.EnumType = this.paramType;
                    this.enumCompleter.CacheEnumValues();
                }
            }
            else
            {
                // non-parsable, and not a Type
                this.usingBasicLabel = true;

                this.inputField.Component.gameObject.SetActive(false);
                this.basicLabelHolder.SetActive(true);
                this.typeCompleter.Enabled = false;
                this.enumCompleter.Enabled = false;
                this.enumHelperButton.Component.gameObject.SetActive(false);

                this.SetDisplayedValueFromPaste();
            }
        }

        public void OnReturned()
        {
            this.paramInfo = null;

            this.enumCompleter.Enabled = false;
            this.typeCompleter.Enabled = false;

            this.inputField.Text = "";

            this.usingBasicLabel = false;
            this.basicValue = null;
        }

        public object Evaluate()
        {
            if (this.usingBasicLabel)
                return this.basicValue;

            string input = this.inputField.Text;

            if (typeof(Type).IsAssignableFrom(this.paramType))
                return ReflectionUtility.GetTypeByName(input);

            if (this.paramType == typeof(string))
                return input;

            if (string.IsNullOrEmpty(input))
            {
                if (this.paramInfo.IsOptional)
                    return this.paramInfo.DefaultValue;
                else
                    return null;
            }

            if (!ParseUtility.TryParse(input, this.paramType, out object parsed, out Exception ex))
            {
                ExplorerCore.LogWarning($"Cannot parse argument '{this.paramInfo.Name}' ({this.paramInfo.ParameterType.Name})" +
                    $"{(ex == null ? "" : $", {ex.GetType().Name}: {ex.Message}")}");
                return null;
            }
            else
                return parsed;
        }

        private void OnPasteClicked()
        {
            if (ClipboardPanel.TryPaste(this.paramType, out object paste))
            {
                this.basicValue = paste;
                this.SetDisplayedValueFromPaste();
            }
        }

        private void SetDisplayedValueFromPaste()
        {
            if (this.usingBasicLabel)
                this.basicLabel.text = ToStringUtility.ToStringWithType(this.basicValue, this.paramType, false);
            else
            {
                if (typeof(Type).IsAssignableFrom(this.paramType))
                    this.inputField.Text = (this.basicValue as Type).FullDescription();
                else
                    this.inputField.Text = ParseUtility.ToStringForInput(this.basicValue, this.paramType);
            }
        }

        public override void CreateSpecialContent()
        {
            this.enumCompleter = new(this.paramType, this.inputField)
            {
                Enabled = false
            };

            this.enumHelperButton = UIFactory.CreateButton(this.UIRoot, "EnumHelper", "▼");
            UIFactory.SetLayoutElement(this.enumHelperButton.Component.gameObject, minWidth: 25, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.enumHelperButton.OnClick += this.enumCompleter.HelperButtonClicked;

            this.basicLabelHolder = UIFactory.CreateHorizontalGroup(this.UIRoot, "BasicLabelHolder", true, true, true, true, bgColor: new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(this.basicLabelHolder, minHeight: 25, flexibleHeight: 50, minWidth: 100, flexibleWidth: 1000);
            this.basicLabel = UIFactory.CreateLabel(this.basicLabelHolder, "BasicLabel", "null", TextAnchor.MiddleLeft);
            this.basicLabel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.pasteButton = UIFactory.CreateButton(this.UIRoot, "PasteButton", "Paste", new Color(0.13f, 0.13f, 0.13f, 1f));
            UIFactory.SetLayoutElement(this.pasteButton.Component.gameObject, minHeight: 25, minWidth: 28, flexibleWidth: 0);
            this.pasteButton.ButtonText.color = Color.green;
            this.pasteButton.ButtonText.fontSize = 10;
            this.pasteButton.OnClick += this.OnPasteClicked;
        }
    }
}
