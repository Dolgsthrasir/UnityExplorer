using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ButtonList;

namespace UnityExplorer.UI.Widgets
{
    public class ComponentCell : ButtonCell
    {
        public Toggle BehaviourToggle;
        public ButtonRef DestroyButton;

        public Action<bool, int> OnBehaviourToggled;
        public Action<int> OnDestroyClicked;

        private void BehaviourToggled(bool val)
        {
            this.OnBehaviourToggled?.Invoke(val, this.CurrentDataIndex);
        }

        private void DestroyClicked()
        {
            this.OnDestroyClicked?.Invoke(this.CurrentDataIndex);
        }

        public override GameObject CreateContent(GameObject parent)
        {
            GameObject root = base.CreateContent(parent);

            // Add mask to button so text doesnt overlap on Close button
            //this.Button.Component.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            this.Button.ButtonText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Behaviour toggle

            GameObject toggleObj = UIFactory.CreateToggle(this.UIRoot, "BehaviourToggle", out this.BehaviourToggle, out Text behavText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, minWidth: 25);
            this.BehaviourToggle.onValueChanged.AddListener(this.BehaviourToggled);
            // put at first object
            toggleObj.transform.SetSiblingIndex(0);

            // Destroy button

            this.DestroyButton = UIFactory.CreateButton(this.UIRoot, "DestroyButton", "X", new Color(0.3f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(this.DestroyButton.Component.gameObject, minHeight: 21, minWidth: 25);
            this.DestroyButton.OnClick += this.DestroyClicked;

            return root;
        }
    }
}
