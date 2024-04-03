using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.CacheObject.Views
{
    public abstract class CacheObjectCell : ICell
    {
        #region ICell

        public float DefaultHeight => 30f;

        public GameObject UIRoot { get; set; }

        public bool Enabled => this.m_enabled;
        private bool m_enabled;

        public RectTransform Rect { get; set; }

        public void Disable()
        {
            this.m_enabled = false;
            this.UIRoot.SetActive(false);
        }

        public void Enable()
        {
            this.m_enabled = true;
            this.UIRoot.SetActive(true);
        }

        #endregion

        public CacheObjectBase Occupant { get; set; }
        public bool SubContentActive => this.SubContentHolder.activeSelf;

        public LayoutElement NameLayout;
        public GameObject RightGroupContent;
        public LayoutElement RightGroupLayout;
        public GameObject SubContentHolder;

        public Text NameLabel;
        public InputFieldRef HiddenNameLabel; // for selecting the name label
        public Text TypeLabel;
        public Text ValueLabel;
        public Toggle Toggle;
        public Text ToggleText;
        public InputFieldRef InputField;

        public ButtonRef InspectButton;
        public ButtonRef SubContentButton;
        public ButtonRef ApplyButton;

        public ButtonRef CopyButton;
        public ButtonRef PasteButton;

        public readonly Color subInactiveColor = new(0.23f, 0.23f, 0.23f);
        public readonly Color subActiveColor = new(0.23f, 0.33f, 0.23f);

        protected virtual void ApplyClicked()
        {
            this.Occupant.OnCellApplyClicked();
        }

        protected virtual void InspectClicked()
        {
            InspectorManager.Inspect(this.Occupant.Value, this.Occupant);
        }

        protected virtual void ToggleClicked(bool value)
        {
            this.ToggleText.text = value.ToString();
        }

        protected virtual void SubContentClicked()
        {
            this.Occupant.OnCellSubContentToggle();
        }

        protected virtual void OnCopyClicked()
        {
            ClipboardPanel.Copy(this.Occupant.Value);
        }

        protected virtual void OnPasteClicked()
        {
            if (ClipboardPanel.TryPaste(this.Occupant.FallbackType, out object paste))
                this.Occupant.SetUserValue(paste);
        }

        public void RefreshSubcontentButton()
        {
            this.SubContentButton.ButtonText.text = this.SubContentHolder.activeSelf ? "▼" : "▲";
            Color color = this.SubContentHolder.activeSelf ? this.subActiveColor : this.subInactiveColor;
            RuntimeHelper.SetColorBlock(this.SubContentButton.Component, color, color * 1.3f);
        }

        protected abstract void ConstructEvaluateHolder(GameObject parent);

        public virtual GameObject CreateContent(GameObject parent)
        {
            // Main layout

            this.UIRoot = UIFactory.CreateUIObject(this.GetType().Name, parent, new Vector2(100, 30));
            this.Rect = this.UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.UIRoot, false, false, true, true, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 600);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject horiRow = UIFactory.CreateUIObject("HoriGroup", this.UIRoot);
            UIFactory.SetLayoutElement(horiRow, minHeight: 29, flexibleHeight: 150, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(horiRow, false, false, true, true, 5, 2, childAlignment: TextAnchor.UpperLeft);
            horiRow.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Left name label

            this.NameLabel = UIFactory.CreateLabel(horiRow, "NameLabel", "<notset>", TextAnchor.MiddleLeft);
            this.NameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            this.NameLayout = UIFactory.SetLayoutElement(this.NameLabel.gameObject, minHeight: 25, minWidth: 20, flexibleHeight: 300, flexibleWidth: 0);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.NameLabel.gameObject, true, true, true, true);

            this.HiddenNameLabel = UIFactory.CreateInputField(this.NameLabel.gameObject, "HiddenNameLabel", "");
            RectTransform hiddenRect = this.HiddenNameLabel.Component.GetComponent<RectTransform>();
            hiddenRect.anchorMin = Vector2.zero;
            hiddenRect.anchorMax = Vector2.one;
            this.HiddenNameLabel.Component.readOnly = true;
            this.HiddenNameLabel.Component.lineType = UnityEngine.UI.InputField.LineType.MultiLineNewline;
            this.HiddenNameLabel.Component.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            this.HiddenNameLabel.Component.gameObject.GetComponent<Image>().color = Color.clear;
            this.HiddenNameLabel.Component.textComponent.color = Color.clear;
            UIFactory.SetLayoutElement(this.HiddenNameLabel.Component.gameObject, minHeight: 25, minWidth: 20, flexibleHeight: 300, flexibleWidth: 0);

            // Right vertical group

            this.RightGroupContent = UIFactory.CreateUIObject("RightGroup", horiRow);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.RightGroupContent, false, false, true, true, 4, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.RightGroupContent, minHeight: 25, minWidth: 200, flexibleWidth: 9999, flexibleHeight: 800);
            this.RightGroupLayout = this.RightGroupContent.GetComponent<LayoutElement>();

            this.ConstructEvaluateHolder(this.RightGroupContent);

            // Right horizontal group

            GameObject rightHoriGroup = UIFactory.CreateUIObject("RightHoriGroup", this.RightGroupContent);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(rightHoriGroup, false, false, true, true, 4, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(rightHoriGroup, minHeight: 25, minWidth: 200, flexibleWidth: 9999, flexibleHeight: 800);

            this.SubContentButton = UIFactory.CreateButton(rightHoriGroup, "SubContentButton", "▲", this.subInactiveColor);
            UIFactory.SetLayoutElement(this.SubContentButton.Component.gameObject, minWidth: 25, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.SubContentButton.OnClick += this.SubContentClicked;

            // Type label

            this.TypeLabel = UIFactory.CreateLabel(rightHoriGroup, "ReturnLabel", "<notset>", TextAnchor.MiddleLeft);
            this.TypeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetLayoutElement(this.TypeLabel.gameObject, minHeight: 25, flexibleHeight: 150, minWidth: 45, flexibleWidth: 0);

            // Bool and number value interaction

            GameObject toggleObj = UIFactory.CreateToggle(rightHoriGroup, "Toggle", out this.Toggle, out this.ToggleText);
            UIFactory.SetLayoutElement(toggleObj, minWidth: 70, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.ToggleText.color = SignatureHighlighter.KeywordBlue;
            this.Toggle.onValueChanged.AddListener(this.ToggleClicked);

            this.InputField = UIFactory.CreateInputField(rightHoriGroup, "InputField", "...");
            UIFactory.SetLayoutElement(this.InputField.UIRoot, minWidth: 150, flexibleWidth: 0, minHeight: 25, flexibleHeight: 0);

            // Apply

            this.ApplyButton = UIFactory.CreateButton(rightHoriGroup, "ApplyButton", "Apply", new Color(0.15f, 0.19f, 0.15f));
            UIFactory.SetLayoutElement(this.ApplyButton.Component.gameObject, minWidth: 70, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.ApplyButton.OnClick += this.ApplyClicked;

            // Inspect 

            this.InspectButton = UIFactory.CreateButton(rightHoriGroup, "InspectButton", "Inspect", new Color(0.15f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.InspectButton.Component.gameObject, minWidth: 70, flexibleWidth: 0, minHeight: 25);
            this.InspectButton.OnClick += this.InspectClicked;

            // Main value label

            this.ValueLabel = UIFactory.CreateLabel(rightHoriGroup, "ValueLabel", "Value goes here", TextAnchor.MiddleLeft);
            this.ValueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetLayoutElement(this.ValueLabel.gameObject, minHeight: 25, flexibleHeight: 150, flexibleWidth: 9999);

            // Copy and Paste buttons

            GameObject buttonHolder = UIFactory.CreateHorizontalGroup(rightHoriGroup, "CopyPasteButtons", false, false, true, true, 4,
                bgColor: new(1, 1, 1, 0), childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(buttonHolder, minWidth: 60, flexibleWidth: 0);

            this.CopyButton = UIFactory.CreateButton(buttonHolder, "CopyButton", "Copy", new Color(0.13f, 0.13f, 0.13f, 1f));
            UIFactory.SetLayoutElement(this.CopyButton.Component.gameObject, minHeight: 25, minWidth: 28, flexibleWidth: 0);
            this.CopyButton.ButtonText.color = Color.yellow;
            this.CopyButton.ButtonText.fontSize = 10;
            this.CopyButton.OnClick += this.OnCopyClicked;

            this.PasteButton = UIFactory.CreateButton(buttonHolder, "PasteButton", "Paste", new Color(0.13f, 0.13f, 0.13f, 1f));
            UIFactory.SetLayoutElement(this.PasteButton.Component.gameObject, minHeight: 25, minWidth: 28, flexibleWidth: 0);
            this.PasteButton.ButtonText.color = Color.green;
            this.PasteButton.ButtonText.fontSize = 10;
            this.PasteButton.OnClick += this.OnPasteClicked;

            // Subcontent

            this.SubContentHolder = UIFactory.CreateUIObject("SubContent", this.UIRoot);
            UIFactory.SetLayoutElement(this.SubContentHolder.gameObject, minHeight: 30, flexibleHeight: 600, minWidth: 100, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.SubContentHolder, true, true, true, true, 2, childAlignment: TextAnchor.UpperLeft);
            //SubContentHolder.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;
            this.SubContentHolder.SetActive(false);

            // Bottom separator
            GameObject separator = UIFactory.CreateUIObject("BottomSeperator", this.UIRoot);
            UIFactory.SetLayoutElement(separator, minHeight: 1, flexibleHeight: 0, flexibleWidth: 9999);
            separator.AddComponent<Image>().color = Color.black;

            return this.UIRoot;
        }
    }
}
