using System.Collections;
using UnityEngine.SceneManagement;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets;

namespace UnityExplorer.ObjectExplorer
{
    public class SceneExplorer : UIModel
    {
        public ObjectExplorerPanel Parent { get; }

        public SceneExplorer(ObjectExplorerPanel parent)
        {
            this.Parent = parent;

            SceneHandler.OnInspectedSceneChanged += this.SceneHandler_OnInspectedSceneChanged;
            SceneHandler.OnLoadedScenesUpdated += this.SceneHandler_OnLoadedScenesUpdated;
        }

        public override GameObject UIRoot => this.uiRoot;
        private GameObject uiRoot;

        /// <summary>
        /// Whether to automatically update per auto-update interval or not.
        /// </summary>
        public bool AutoUpdate = false;

        public TransformTree Tree;
        private float timeOfLastUpdate = -1f;

        private GameObject refreshRow;
        private Dropdown sceneDropdown;
        private readonly Dictionary<Scene, Dropdown.OptionData> sceneToDropdownOption = new();

        // scene loader
        private Dropdown allSceneDropdown;
        private ButtonRef loadButton;
        private ButtonRef loadAdditiveButton;

        private IEnumerable<GameObject> GetRootEntries() => SceneHandler.CurrentRootObjects;

        public void Update()
        {
            if ((this.AutoUpdate || !SceneHandler.InspectingAssetScene) && this.timeOfLastUpdate.OccuredEarlierThan(1))
            {
                this.timeOfLastUpdate = Time.realtimeSinceStartup;
                this.UpdateTree();
            }
        }

        public void UpdateTree()
        {
            SceneHandler.Update();
            this.Tree.RefreshData(true, false, false, false);
        }

        public void JumpToTransform(Transform transform)
        {
            if (!transform)
                return;

            UIManager.SetPanelActive(this.Parent, true);
            this.Parent.SetTab(0);

            // select the transform's scene
            GameObject go = transform.gameObject;
            if (SceneHandler.SelectedScene != go.scene)
            {
                int idx;
                if (go.scene == default || go.scene.handle == -1)
                    idx = this.sceneDropdown.options.Count - 1;
                else
                    idx = this.sceneDropdown.options.IndexOf(this.sceneToDropdownOption[go.scene]);
                this.sceneDropdown.value = idx;
            }

            // Let the TransformTree handle the rest
            this.Tree.JumpAndExpandToTransform(transform);
        }

        private void OnSceneSelectionDropdownChanged(int value)
        {
            if (value < 0 || SceneHandler.LoadedScenes.Count <= value)
                return;

            SceneHandler.SelectedScene = SceneHandler.LoadedScenes[value];
            SceneHandler.Update();
            this.Tree.RefreshData(true, true, true, false);
            this.OnSelectedSceneChanged(SceneHandler.SelectedScene.Value);
        }

        private void SceneHandler_OnInspectedSceneChanged(Scene scene)
        {
            if (!this.sceneToDropdownOption.ContainsKey(scene)) this.PopulateSceneDropdown(SceneHandler.LoadedScenes);

            if (this.sceneToDropdownOption.ContainsKey(scene))
            {
                Dropdown.OptionData opt = this.sceneToDropdownOption[scene];
                int idx = this.sceneDropdown.options.IndexOf(opt);
                if (this.sceneDropdown.value != idx)
                    this.sceneDropdown.value = idx;
                else
                    this.sceneDropdown.captionText.text = opt.text;
            }

            this.OnSelectedSceneChanged(scene);
        }

        private void OnSelectedSceneChanged(Scene scene)
        {
            if (this.refreshRow) this.refreshRow.SetActive(!scene.IsValid());
        }

        private void SceneHandler_OnLoadedScenesUpdated(List<Scene> loadedScenes)
        {
            this.PopulateSceneDropdown(loadedScenes);
        }

        private void PopulateSceneDropdown(List<Scene> loadedScenes)
        {
            this.sceneToDropdownOption.Clear();
            this.sceneDropdown.options.Clear();

            foreach (Scene scene in loadedScenes)
            {
                if (this.sceneToDropdownOption.ContainsKey(scene))
                    continue;

                string name = scene.name?.Trim();

                if (!scene.IsValid())
                    name = "HideAndDontSave";
                else if (string.IsNullOrEmpty(name))
                    name = "<untitled>";

                Dropdown.OptionData option = new(name);
                this.sceneDropdown.options.Add(option);
                this.sceneToDropdownOption.Add(scene, option);
            }
        }

        private void OnFilterInput(string input)
        {
            if ((!string.IsNullOrEmpty(input) && !this.Tree.Filtering) || (string.IsNullOrEmpty(input) && this.Tree.Filtering))
            {
                this.Tree.Clear();
            }

            this.Tree.CurrentFilter = input;
            this.Tree.RefreshData(true, false, true, false);
        }

        private void TryLoadScene(LoadSceneMode mode, Dropdown allSceneDrop)
        {
            string text = allSceneDrop.captionText.text;

            if (text == DEFAULT_LOAD_TEXT)
                return;

            try
            {
                SceneManager.LoadScene(text, mode);
                allSceneDrop.value = 0;
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Unable to load the Scene! {ex.ReflectionExToString()}");
            }
        }

        public override void ConstructUI(GameObject content)
        {
            this.uiRoot = UIFactory.CreateUIObject("SceneExplorer", content);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.uiRoot, true, true, true, true, 0, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(this.uiRoot, flexibleHeight: 9999);

            // Tool bar (top area)

            GameObject toolbar = UIFactory.CreateVerticalGroup(this.uiRoot, "Toolbar", true, true, true, true, 2, new Vector4(2, 2, 2, 2),
               new Color(0.15f, 0.15f, 0.15f));

            // Scene selector dropdown

            GameObject dropRow = UIFactory.CreateHorizontalGroup(toolbar, "DropdownRow", true, true, true, true, 5, default, new Color(1, 1, 1, 0));
            UIFactory.SetLayoutElement(dropRow, minHeight: 25, flexibleWidth: 9999);

            Text dropLabel = UIFactory.CreateLabel(dropRow, "SelectorLabel", "Scene:", TextAnchor.MiddleLeft, Color.cyan, false, 15);
            UIFactory.SetLayoutElement(dropLabel.gameObject, minHeight: 25, minWidth: 60, flexibleWidth: 0);

            GameObject dropdownObj = UIFactory.CreateDropdown(dropRow, "SceneDropdown", out this.sceneDropdown, "<notset>", 13, this.OnSceneSelectionDropdownChanged);
            UIFactory.SetLayoutElement(dropdownObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            SceneHandler.Update();
            this.PopulateSceneDropdown(SceneHandler.LoadedScenes);
            this.sceneDropdown.captionText.text = this.sceneToDropdownOption.First().Value.text;

            // Filter row

            GameObject filterRow = UIFactory.CreateHorizontalGroup(toolbar, "FilterGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(filterRow, minHeight: 25, flexibleHeight: 0);

            //Filter input field
            InputFieldRef inputField = UIFactory.CreateInputField(filterRow, "FilterInput", "Search and press enter...");
            inputField.Component.targetGraphic.color = new Color(0.2f, 0.2f, 0.2f);
            RuntimeHelper.SetColorBlock(inputField.Component, new Color(0.4f, 0.4f, 0.4f), new Color(0.2f, 0.2f, 0.2f),
                new Color(0.08f, 0.08f, 0.08f));
            UIFactory.SetLayoutElement(inputField.UIRoot, minHeight: 25);
            //inputField.OnValueChanged += OnFilterInput;
            inputField.Component.GetOnEndEdit().AddListener(this.OnFilterInput);

            // refresh row

            this.refreshRow = UIFactory.CreateHorizontalGroup(toolbar, "RefreshGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.refreshRow, minHeight: 30, flexibleHeight: 0);

            ButtonRef refreshButton = UIFactory.CreateButton(this.refreshRow, "RefreshButton", "Update");
            UIFactory.SetLayoutElement(refreshButton.Component.gameObject, minWidth: 65, flexibleWidth: 0);
            refreshButton.OnClick += this.UpdateTree;

            GameObject refreshToggle = UIFactory.CreateToggle(this.refreshRow, "RefreshToggle", out Toggle toggle, out Text text);
            UIFactory.SetLayoutElement(refreshToggle, flexibleWidth: 9999);
            text.text = "Auto-update (1 second)";
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.fontSize = 12;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener((bool val) => this.AutoUpdate = val);

            this.refreshRow.SetActive(false);

            // tree labels row

            GameObject labelsRow = UIFactory.CreateHorizontalGroup(toolbar, "LabelsRow", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(labelsRow, minHeight: 30, flexibleHeight: 0);

            Text nameLabel = UIFactory.CreateLabel(labelsRow, "NameLabel", "Name", TextAnchor.MiddleLeft, color: Color.grey);
            UIFactory.SetLayoutElement(nameLabel.gameObject, flexibleWidth: 9999, minHeight: 25);

            Text indexLabel = UIFactory.CreateLabel(labelsRow, "IndexLabel", "Sibling Index", TextAnchor.MiddleLeft, fontSize: 12, color: Color.grey);
            UIFactory.SetLayoutElement(indexLabel.gameObject, minWidth: 100, flexibleWidth: 0, minHeight: 25);

            // Transform Tree

            UniverseLib.UI.Widgets.ScrollView.ScrollPool<TransformCell> scrollPool = UIFactory.CreateScrollPool<TransformCell>(this.uiRoot, "TransformTree", out GameObject scrollObj,
                out GameObject scrollContent, new Color(0.11f, 0.11f, 0.11f));
            UIFactory.SetLayoutElement(scrollObj, flexibleHeight: 9999);
            UIFactory.SetLayoutElement(scrollContent, flexibleHeight: 9999);

            this.Tree = new TransformTree(scrollPool, this.GetRootEntries, this.OnCellClicked);
            this.Tree.RefreshData(true, true, true, false);
            //scrollPool.Viewport.GetComponent<Mask>().enabled = false;
            //UIRoot.GetComponent<Mask>().enabled = false;

            // Scene Loader

            this.ConstructSceneLoader();

            RuntimeHelper.StartCoroutine(this.TempFixCoro());
        }

        void OnCellClicked(GameObject obj) => InspectorManager.Inspect(obj);

        // To "fix" a strange FPS drop issue with MelonLoader.
        private IEnumerator TempFixCoro()
        {
            float start = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - start < 2.5f)
                yield return null;

            // Select "HideAndDontSave" and then go back to first scene.
            this.sceneDropdown.value = this.sceneDropdown.options.Count - 1;
            this.sceneDropdown.value = 0;
        }

        private const string DEFAULT_LOAD_TEXT = "[Select a scene]";

        private void RefreshSceneLoaderOptions(string filter)
        {
            this.allSceneDropdown.options.Clear();
            this.allSceneDropdown.options.Add(new Dropdown.OptionData(DEFAULT_LOAD_TEXT));

            foreach (string scene in SceneHandler.AllSceneNames)
            {
                if (string.IsNullOrEmpty(filter) || scene.ContainsIgnoreCase(filter)) this.allSceneDropdown.options.Add(new Dropdown.OptionData(Path.GetFileNameWithoutExtension(scene)));
            }

            this.allSceneDropdown.RefreshShownValue();

            if (this.loadButton != null) this.RefreshSceneLoaderButtons();
        }

        private void RefreshSceneLoaderButtons()
        {
            string text = this.allSceneDropdown.captionText.text;
            if (text == DEFAULT_LOAD_TEXT)
            {
                this.loadButton.Component.interactable = false;
                this.loadAdditiveButton.Component.interactable = false;
            }
            else
            {
                this.loadButton.Component.interactable = true;
                this.loadAdditiveButton.Component.interactable = true;
            }
        }

        private void ConstructSceneLoader()
        {
            // Scene Loader
            try
            {
                if (SceneHandler.WasAbleToGetScenesInBuild)
                {
                    GameObject sceneLoaderObj = UIFactory.CreateVerticalGroup(this.uiRoot, "SceneLoader", true, true, true, true);
                    UIFactory.SetLayoutElement(sceneLoaderObj, minHeight: 25);

                    // Title

                    Text loaderTitle = UIFactory.CreateLabel(sceneLoaderObj, "SceneLoaderLabel", "Scene Loader", TextAnchor.MiddleLeft, Color.white, true, 14);
                    UIFactory.SetLayoutElement(loaderTitle.gameObject, minHeight: 25, flexibleHeight: 0);

                    // Search filter

                    InputFieldRef searchFilterObj = UIFactory.CreateInputField(sceneLoaderObj, "SearchFilterInput", "Filter scene names...");
                    UIFactory.SetLayoutElement(searchFilterObj.UIRoot, minHeight: 25, flexibleHeight: 0);
                    searchFilterObj.OnValueChanged += this.RefreshSceneLoaderOptions;

                    // Dropdown

                    GameObject allSceneDropObj = UIFactory.CreateDropdown(sceneLoaderObj, "SceneLoaderDropdown", out this.allSceneDropdown, "", 14, null);
                    UIFactory.SetLayoutElement(allSceneDropObj, minHeight: 25, minWidth: 150, flexibleWidth: 0, flexibleHeight: 0);

                    this.RefreshSceneLoaderOptions(string.Empty);

                    // Button row

                    GameObject buttonRow = UIFactory.CreateHorizontalGroup(sceneLoaderObj, "LoadButtons", true, true, true, true, 4);

                    this.loadButton = UIFactory.CreateButton(buttonRow, "LoadSceneButton", "Load (Single)", new Color(0.1f, 0.3f, 0.3f));
                    UIFactory.SetLayoutElement(this.loadButton.Component.gameObject, minHeight: 25, minWidth: 150);
                    this.loadButton.OnClick += () =>
                    {
                        this.TryLoadScene(LoadSceneMode.Single, this.allSceneDropdown);
                    };

                    this.loadAdditiveButton = UIFactory.CreateButton(buttonRow, "LoadSceneButton", "Load (Additive)", new Color(0.1f, 0.3f, 0.3f));
                    UIFactory.SetLayoutElement(this.loadAdditiveButton.Component.gameObject, minHeight: 25, minWidth: 150);
                    this.loadAdditiveButton.OnClick += () =>
                    {
                        this.TryLoadScene(LoadSceneMode.Additive, this.allSceneDropdown);
                    };

                    Color disabledColor = new(0.24f, 0.24f, 0.24f);
                    RuntimeHelper.SetColorBlock(this.loadButton.Component, disabled: disabledColor);
                    RuntimeHelper.SetColorBlock(this.loadAdditiveButton.Component, disabled: disabledColor);

                    this.loadButton.Component.interactable = false;
                    this.loadAdditiveButton.Component.interactable = false;

                    this.allSceneDropdown.onValueChanged.AddListener((int val) =>
                    {
                        this.RefreshSceneLoaderButtons();
                    });
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Could not create the Scene Loader helper! {ex.ReflectionExToString()}");
            }
        }
    }
}
