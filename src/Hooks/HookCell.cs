using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.Hooks
{
    public class HookCell : ICell
    {
        public bool Enabled => this.UIRoot.activeSelf;

        public RectTransform Rect { get; set; }
        public GameObject UIRoot { get; set; }

        public float DefaultHeight => 30;

        public Text MethodNameLabel;
        public ButtonRef EditPatchButton;
        public ButtonRef ToggleActiveButton;
        public ButtonRef ToogleActiveOnStartupButton;
        public ButtonRef DeleteButtonTmp;
        public ButtonRef DeleteButton;

        public int CurrentDisplayedIndex;

        private void OnToggleActiveClicked()
        {
            HookList.EnableOrDisableHookClicked(this.CurrentDisplayedIndex);
        }
        
        private void OnToggleActiveOnStartupClicked()
        {
            HookList.EnableOrDisableOnStartupHookClicked(this.CurrentDisplayedIndex);
        }

        private void OnDeleteTmpClicked()
        {
            HookList.DeleteHookClicked(this.CurrentDisplayedIndex, false);
            HookCreator.AddHooksScrollPool.Refresh(true, false);
        }
        
        private void OnDeleteClicked()
        {
            HookList.DeleteHookClicked(this.CurrentDisplayedIndex, true);
            HookCreator.AddHooksScrollPool.Refresh(true, false);
        }

        private void OnEditPatchClicked()
        {
            HookList.EditPatchClicked(this.CurrentDisplayedIndex);
        }

        public GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateUIObject(this.GetType().Name, parent, new Vector2(100, 30));
            this.Rect = this.UIRoot.GetComponent<RectTransform>();
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.UIRoot, false, false, true, true, 4, childAlignment: TextAnchor.UpperLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minWidth: 100, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 600);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.MethodNameLabel = UIFactory.CreateLabel(this.UIRoot, "MethodName", "NOT SET", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.MethodNameLabel.gameObject, minHeight: 25, flexibleWidth: 9999);

            this.ToggleActiveButton = UIFactory.CreateButton(this.UIRoot, "ToggleActiveBtn", "On", new Color(0.15f, 0.2f, 0.15f));
            UIFactory.SetLayoutElement(this.ToggleActiveButton.Component.gameObject, minHeight: 25, minWidth: 35);
            this.ToggleActiveButton.OnClick += this.OnToggleActiveClicked;
            
            this.ToogleActiveOnStartupButton = UIFactory.CreateButton(this.UIRoot, "ToogleActiveOnStartupButton", "Y", new Color(0.15f, 0.2f, 0.15f));
            UIFactory.SetLayoutElement(this.ToogleActiveOnStartupButton.Component.gameObject, minHeight: 25, minWidth: 35);
            this.ToogleActiveOnStartupButton.OnClick += this.OnToggleActiveOnStartupClicked;

            this.EditPatchButton = UIFactory.CreateButton(this.UIRoot, "EditButton", "Edit", new Color(0.15f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.EditPatchButton.Component.gameObject, minHeight: 25, minWidth: 35);
            this.EditPatchButton.OnClick += this.OnEditPatchClicked;

            this.DeleteButtonTmp = UIFactory.CreateButton(this.UIRoot, "DeleteButton", "X", new Color(0.2f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.DeleteButtonTmp.Component.gameObject, minHeight: 25, minWidth: 35);
            this.DeleteButtonTmp.OnClick += this.OnDeleteTmpClicked;

            this.DeleteButton = UIFactory.CreateButton(this.UIRoot, "DeleteButton", "X!", new Color(0.2f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.DeleteButton.Component.gameObject, minHeight: 25, minWidth: 35);
            this.DeleteButton.OnClick += this.OnDeleteClicked;

            return this.UIRoot;
        }

        public void Disable()
        {
            this.UIRoot.SetActive(false);
        }

        public void Enable()
        {
            this.UIRoot.SetActive(true);
        }
    }
}
