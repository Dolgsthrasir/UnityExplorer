using UniverseLib.UI;

namespace UnityExplorer.CacheObject.Views
{
    public class ConfigEntryCell : CacheObjectCell
    {
        public override GameObject CreateContent(GameObject parent)
        {
            // Main layout

            this.UIRoot = UIFactory.CreateUIObject(this.GetType().Name, parent, new Vector2(100, 30));
            this.Rect = this.UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.UIRoot, false, false, true, true, 4, 4, 4, 4, 4, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 600);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Left label

            this.NameLabel = UIFactory.CreateLabel(this.UIRoot, "NameLabel", "<notset>", TextAnchor.MiddleLeft);
            this.NameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetLayoutElement(this.NameLabel.gameObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 300);
            this.NameLayout = this.NameLabel.GetComponent<LayoutElement>();

            // horizontal group

            GameObject horiGroup = UIFactory.CreateUIObject("RightHoriGroup", this.UIRoot);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(horiGroup, false, false, true, true, 4, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(horiGroup, minHeight: 25, minWidth: 200, flexibleWidth: 9999, flexibleHeight: 800);

            this.SubContentButton = UIFactory.CreateButton(horiGroup, "SubContentButton", "▲", this.subInactiveColor);
            UIFactory.SetLayoutElement(this.SubContentButton.Component.gameObject, minWidth: 25, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.SubContentButton.OnClick += this.SubContentClicked;

            // Type label

            this.TypeLabel = UIFactory.CreateLabel(horiGroup, "TypeLabel", "<notset>", TextAnchor.MiddleLeft);
            this.TypeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetLayoutElement(this.TypeLabel.gameObject, minHeight: 25, flexibleHeight: 150, minWidth: 60, flexibleWidth: 0);

            // Bool and number value interaction

            GameObject toggleObj = UIFactory.CreateToggle(horiGroup, "Toggle", out this.Toggle, out this.ToggleText);
            UIFactory.SetLayoutElement(toggleObj, minWidth: 70, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.ToggleText.color = SignatureHighlighter.KeywordBlue;
            this.Toggle.onValueChanged.AddListener(this.ToggleClicked);

            this.InputField = UIFactory.CreateInputField(horiGroup, "InputField", "...");
            UIFactory.SetLayoutElement(this.InputField.UIRoot, minWidth: 150, flexibleWidth: 0, minHeight: 25, flexibleHeight: 0);

            // Apply

            this.ApplyButton = UIFactory.CreateButton(horiGroup, "ApplyButton", "Apply", new Color(0.15f, 0.19f, 0.15f));
            UIFactory.SetLayoutElement(this.ApplyButton.Component.gameObject, minWidth: 70, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);
            this.ApplyButton.OnClick += this.ApplyClicked;

            // Main value label

            this.ValueLabel = UIFactory.CreateLabel(horiGroup, "ValueLabel", "Value goes here", TextAnchor.MiddleLeft);
            this.ValueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetLayoutElement(this.ValueLabel.gameObject, minHeight: 25, flexibleHeight: 150, flexibleWidth: 9999);

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

        protected override void ConstructEvaluateHolder(GameObject parent) { }
    }
}
