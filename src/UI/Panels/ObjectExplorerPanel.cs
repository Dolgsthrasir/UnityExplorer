using UnityExplorer.ObjectExplorer;
using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Panels
{
    public class ObjectExplorerPanel : UEPanel
    {
        public override string Name => "Object Explorer";
        public override UIManager.Panels PanelType => UIManager.Panels.ObjectExplorer;

        public override int MinWidth => 350;
        public override int MinHeight => 200;
        public override Vector2 DefaultAnchorMin => new(0.125f, 0.175f);
        public override Vector2 DefaultAnchorMax => new(0.325f, 0.925f);

        public SceneExplorer SceneExplorer;
        public ObjectSearch ObjectSearch;

        public override bool ShowByDefault => true;
        public override bool ShouldSaveActiveState => true;

        public int SelectedTab = 0;
        private readonly List<UIModel> tabPages = new();
        private readonly List<ButtonRef> tabButtons = new();

        public ObjectExplorerPanel(UIBase owner) : base(owner)
        {
        }

        public void SetTab(int tabIndex)
        {
            if (this.SelectedTab != -1) this.DisableTab(this.SelectedTab);

            UIModel content = this.tabPages[tabIndex];
            content.SetActive(true);

            ButtonRef button = this.tabButtons[tabIndex];
            RuntimeHelper.SetColorBlock(button.Component, UniversalUI.EnabledButtonColor, UniversalUI.EnabledButtonColor * 1.2f);

            this.SelectedTab = tabIndex;
            this.SaveInternalData();
        }

        private void DisableTab(int tabIndex)
        {
            this.tabPages[tabIndex].SetActive(false);
            RuntimeHelper.SetColorBlock(this.tabButtons[tabIndex].Component, UniversalUI.DisabledButtonColor, UniversalUI.DisabledButtonColor * 1.2f);
        }

        public override void Update()
        {
            if (this.SelectedTab == 0)
                this.SceneExplorer.Update();
            else
                this.ObjectSearch.Update();
        }

        public override string ToSaveData()
        {
            return string.Join("|", new string[] { base.ToSaveData(), this.SelectedTab.ToString() });
        }

        protected override void ApplySaveData(string data)
        {
            base.ApplySaveData(data);

            try
            {
                int tab = int.Parse(data.Split('|').Last());
                this.SelectedTab = tab;
            }
            catch
            {
                this.SelectedTab = 0;
            }

            this.SelectedTab = Math.Max(0, this.SelectedTab);
            this.SelectedTab = Math.Min(1, this.SelectedTab);

            this.SetTab(this.SelectedTab);
        }

        protected override void ConstructPanelContent()
        {
            // Tab bar
            GameObject tabGroup = UIFactory.CreateHorizontalGroup(this.ContentRoot, "TabBar", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(tabGroup, minHeight: 25, flexibleHeight: 0);

            // Scene Explorer
            this.SceneExplorer = new SceneExplorer(this);
            this.SceneExplorer.ConstructUI(this.ContentRoot);
            this.tabPages.Add(this.SceneExplorer);

            // Object search
            this.ObjectSearch = new ObjectSearch(this);
            this.ObjectSearch.ConstructUI(this.ContentRoot);
            this.tabPages.Add(this.ObjectSearch);

            // set up tabs
            this.AddTabButton(tabGroup, "Scene Explorer");
            this.AddTabButton(tabGroup, "Object Search");

            // default active state: Active
            this.SetActive(true);
        }

        private void AddTabButton(GameObject tabGroup, string label)
        {
            ButtonRef button = UIFactory.CreateButton(tabGroup, $"Button_{label}", label);

            int idx = this.tabButtons.Count;
            //button.onClick.AddListener(() => { SetTab(idx); });
            button.OnClick += () => { this.SetTab(idx); };

            this.tabButtons.Add(button);

            this.DisableTab(this.tabButtons.Count - 1);
        }
    }
}
