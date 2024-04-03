using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.UI.Widgets
{
    public abstract class BaseArgumentHandler : IPooledObject
    {
        internal Text argNameLabel;
        internal InputFieldRef inputField;
        internal TypeCompleter typeCompleter;

        // IPooledObject
        public float DefaultHeight => 25f;
        public GameObject UIRoot { get; set; }

        public abstract void CreateSpecialContent();

        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateUIObject("ArgRow", parent);
            UIFactory.SetLayoutElement(this.UIRoot, minHeight: 25, flexibleHeight: 50, minWidth: 50, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.UIRoot, false, false, true, true, 5);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.argNameLabel = UIFactory.CreateLabel(this.UIRoot, "ArgLabel", "not set", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.argNameLabel.gameObject, minWidth: 40, flexibleWidth: 90, minHeight: 25, flexibleHeight: 50);
            this.argNameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            this.inputField = UIFactory.CreateInputField(this.UIRoot, "InputField", "...");
            UIFactory.SetLayoutElement(this.inputField.UIRoot, minHeight: 25, flexibleHeight: 50, minWidth: 100, flexibleWidth: 1000);
            this.inputField.Component.lineType = InputField.LineType.MultiLineNewline;
            this.inputField.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.typeCompleter = new TypeCompleter(typeof(object), this.inputField)
            {
                Enabled = false
            };

            this.CreateSpecialContent();

            return this.UIRoot;
        }
    }
}
