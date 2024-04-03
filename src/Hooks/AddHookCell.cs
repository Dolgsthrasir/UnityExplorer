using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.Hooks
{
    public class AddHookCell : ICell
    {
        public bool Enabled => this.UIRoot.activeSelf;

        public RectTransform Rect { get; set; }
        public GameObject UIRoot { get; set; }

        public float DefaultHeight => 30;

        public Text MethodNameLabel;
        public ButtonRef HookButton;

        public int CurrentDisplayedIndex;

        private void OnHookClicked()
        {
            HookCreator.AddHookClicked(this.CurrentDisplayedIndex);
        }

        public void Enable()
        {
            this.UIRoot.SetActive(true);
        }

        public void Disable()
        {
            this.UIRoot.SetActive(false);
        }

        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateUIObject(this.GetType().Name, parent, new Vector2(100, 30));
            this.Rect = this.UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.UIRoot, false, false, true, true, 5, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 600);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.HookButton = UIFactory.CreateButton(this.UIRoot, "HookButton", "Hook", new Color(0.2f, 0.25f, 0.2f));
            UIFactory.SetLayoutElement(this.HookButton.Component.gameObject, minHeight: 25, minWidth: 100);
            this.HookButton.OnClick += this.OnHookClicked;

            this.MethodNameLabel = UIFactory.CreateLabel(this.UIRoot, "MethodName", "NOT SET", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.MethodNameLabel.gameObject, minHeight: 25, flexibleWidth: 9999);

            return this.UIRoot;
        }
    }
}
