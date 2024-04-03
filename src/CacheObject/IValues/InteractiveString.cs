using UnityExplorer.Config;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveString : InteractiveValue
    {
        private string RealValue;
        public string EditedValue = "";

        public InputFieldRef inputField;
        public ButtonRef ApplyButton;

        public GameObject SaveFileRow;
        public InputFieldRef SaveFilePath;

        public override void OnBorrowed(CacheObjectBase owner)
        {
            base.OnBorrowed(owner);

            bool canWrite = owner.CanWrite && owner.State != ValueState.Exception;
            this.inputField.Component.readOnly = !canWrite;
            this.ApplyButton.Component.gameObject.SetActive(canWrite);

            this.SaveFilePath.Text = Path.Combine(ConfigManager.Default_Output_Path.Value, "untitled.txt");
        }

        private bool IsStringTooLong(string s)
        {
            if (s == null)
                return false;

            return s.Length >= UniversalUI.MAX_INPUTFIELD_CHARS;
        }

        public override void SetValue(object value)
        {
            if (this.CurrentOwner.State == ValueState.Exception)
                value = this.CurrentOwner.LastException.ToString();

            this.RealValue = value as string;
            this.SaveFileRow.SetActive(this.IsStringTooLong(this.RealValue));

            if (value == null)
            {
                this.inputField.Text = "";
                this.EditedValue = "";
            }
            else
            {
                this.EditedValue = (string)value;
                this.inputField.Text = this.EditedValue;
            }
        }

        private void OnApplyClicked()
        {
            this.CurrentOwner.SetUserValue(this.EditedValue);
        }

        private void OnInputChanged(string input)
        {
            this.EditedValue = input;
            this.SaveFileRow.SetActive(this.IsStringTooLong(this.EditedValue));
        }

        private void OnSaveFileClicked()
        {
            if (this.RealValue == null)
                return;

            if (string.IsNullOrEmpty(this.SaveFilePath.Text))
            {
                ExplorerCore.LogWarning("Cannot save an empty file path!");
                return;
            }

            string path = IOUtility.EnsureValidFilePath(this.SaveFilePath.Text);

            if (File.Exists(path))
                File.Delete(path);

            File.WriteAllText(path, this.RealValue);
        }

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveString", false, false, true, true, 3, new Vector4(4, 4, 4, 4),
                new Color(0.06f, 0.06f, 0.06f));

            // Save to file helper

            this.SaveFileRow = UIFactory.CreateUIObject("SaveFileRow", this.UIRoot);
            UIFactory.SetLayoutElement(this.SaveFileRow, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.SaveFileRow, false, true, true, true, 3);

            UIFactory.CreateLabel(this.SaveFileRow, "Info", "<color=red>String is too long! Save to file if you want to see the full string.</color>",
                TextAnchor.MiddleLeft);

            GameObject horizRow = UIFactory.CreateUIObject("Horiz", this.SaveFileRow);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(horizRow, false, false, true, true, 4);

            ButtonRef saveButton = UIFactory.CreateButton(horizRow, "SaveButton", "Save file");
            UIFactory.SetLayoutElement(saveButton.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 0);
            saveButton.OnClick += this.OnSaveFileClicked;

            this.SaveFilePath = UIFactory.CreateInputField(horizRow, "SaveInput", "...");
            UIFactory.SetLayoutElement(this.SaveFilePath.UIRoot, minHeight: 25, flexibleWidth: 9999);

            // Main Input / apply

            this.ApplyButton = UIFactory.CreateButton(this.UIRoot, "ApplyButton", "Apply", new Color(0.2f, 0.27f, 0.2f));
            UIFactory.SetLayoutElement(this.ApplyButton.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 0);
            this.ApplyButton.OnClick += this.OnApplyClicked;

            this.inputField = UIFactory.CreateInputField(this.UIRoot, "InputField", "empty");
            this.inputField.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFactory.SetLayoutElement(this.inputField.UIRoot, minHeight: 25, flexibleHeight: 500, flexibleWidth: 9999);
            this.inputField.Component.lineType = InputField.LineType.MultiLineNewline;
            this.inputField.OnValueChanged += this.OnInputChanged;

            return this.UIRoot;
        }

    }
}
