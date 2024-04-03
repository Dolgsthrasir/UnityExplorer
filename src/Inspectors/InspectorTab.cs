using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.Inspectors
{
    public class InspectorTab : IPooledObject
    {
        public GameObject UIRoot { get; set; }
        public float DefaultHeight => 25f;

        public ButtonRef TabButton;
        public Text TabText;
        public ButtonRef CloseButton;

        private static readonly Color enabledTabColor = new(0.15f, 0.22f, 0.15f);
        private static readonly Color disabledTabColor = new(0.13f, 0.13f, 0.13f);

        public void SetTabColor(bool active)
        {
            Color color = active ? enabledTabColor : disabledTabColor;
            RuntimeHelper.SetColorBlock(this.TabButton.Component, color, color * 1.2f);
        }

        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateHorizontalGroup(parent, "TabObject", false, true, true, true, 1,
                default, new Color(0.13f, 0.13f, 0.13f), childAlignment: TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 200, flexibleWidth: 0);
            this.UIRoot.AddComponent<Mask>();
            this.UIRoot.AddComponent<Outline>();

            this.TabButton = UIFactory.CreateButton(this.UIRoot, "TabButton", "");
            UIFactory.SetLayoutElement(this.TabButton.Component.gameObject, minWidth: 173, flexibleWidth: 0);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.TabButton.Component.gameObject, false, false, true, true, 0, 0, 0, 3);
            this.TabButton.GameObject.AddComponent<Mask>();

            this.TabText = this.TabButton.ButtonText;
            UIFactory.SetLayoutElement(this.TabText.gameObject, minHeight: 25, minWidth: 150, flexibleWidth: 0);
            this.TabText.alignment = TextAnchor.MiddleLeft;
            this.TabText.fontSize = 12;
            this.TabText.horizontalOverflow = HorizontalWrapMode.Overflow;

            this.CloseButton = UIFactory.CreateButton(this.UIRoot, "CloseButton", "X", new Color(0.15f, 0.15f, 0.15f, 1));
            UIFactory.SetLayoutElement(this.CloseButton.Component.gameObject, minHeight: 25, minWidth: 25, flexibleWidth: 0);
            this.CloseButton.ButtonText.color = Color.red;

            return this.UIRoot;
        }
    }
}
