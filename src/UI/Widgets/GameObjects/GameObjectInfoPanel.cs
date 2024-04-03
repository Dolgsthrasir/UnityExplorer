using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using Object = UnityEngine.Object;

namespace UnityExplorer.UI.Widgets
{
    public class GameObjectInfoPanel
    {
        public GameObjectControls Owner { get; }
        GameObject Target => this.Owner.Target;

        string lastGoName;
        string lastPath;
        bool lastParentState;
        int lastSceneHandle;
        string lastTag;
        int lastLayer;
        int lastFlags;

        ButtonRef ViewParentButton;
        InputFieldRef PathInput;

        InputFieldRef NameInput;
        Toggle ActiveSelfToggle;
        Text ActiveSelfText;
        Toggle IsStaticToggle;

        ButtonRef SceneButton;

        InputFieldRef InstanceIDInput;
        InputFieldRef TagInput;

        Dropdown LayerDropdown;
        Dropdown FlagsDropdown;

        public GameObjectInfoPanel(GameObjectControls owner)
        {
            this.Owner = owner;
            this.Create();
        }

        public void UpdateGameObjectInfo(bool firstUpdate, bool force)
        {
            if (firstUpdate)
            {
                this.InstanceIDInput.Text = this.Target.GetInstanceID().ToString();
            }

            if (force || (!this.NameInput.Component.isFocused && this.Target.name != this.lastGoName))
            {
                this.lastGoName = this.Target.name;
                this.Owner.Parent.Tab.TabText.text = $"[G] {this.Target.name}";
                this.NameInput.Text = this.Target.name;
            }

            if (force || !this.PathInput.Component.isFocused)
            {
                string path = this.Target.transform.GetTransformPath();
                if (path != this.lastPath)
                {
                    this.lastPath = path;
                    this.PathInput.Text = path;
                }
            }

            if (force || this.Target.transform.parent != this.lastParentState)
            {
                this.lastParentState = this.Target.transform.parent;
                this.ViewParentButton.Component.interactable = this.lastParentState;
                if (this.lastParentState)
                {
                    this.ViewParentButton.ButtonText.color = Color.white;
                    this.ViewParentButton.ButtonText.text = "◄ View Parent";
                }
                else
                {
                    this.ViewParentButton.ButtonText.color = Color.grey;
                    this.ViewParentButton.ButtonText.text = "No parent";
                }
            }

            if (force || this.Target.activeSelf != this.ActiveSelfToggle.isOn)
            {
                this.ActiveSelfToggle.Set(this.Target.activeSelf, false);
                this.ActiveSelfText.color = this.ActiveSelfToggle.isOn ? Color.green : Color.red;
            }

            if (force || this.Target.isStatic != this.IsStaticToggle.isOn)
            {
                this.IsStaticToggle.Set(this.Target.isStatic, false);
            }

            if (force || this.Target.scene.handle != this.lastSceneHandle)
            {
                this.lastSceneHandle = this.Target.scene.handle;
                this.SceneButton.ButtonText.text = this.Target.scene.IsValid() ? this.Target.scene.name : "None (Asset/Resource)";
            }

            if (force || (!this.TagInput.Component.isFocused && this.Target.tag != this.lastTag))
            {
                this.lastTag = this.Target.tag;
                this.TagInput.Text = this.lastTag;
            }

            if (force || (this.Target.layer != this.lastLayer))
            {
                this.lastLayer = this.Target.layer;
                this.LayerDropdown.value = this.Target.layer;
            }

            if (force || ((int)this.Target.hideFlags != this.lastFlags))
            {
                this.lastFlags = (int)this.Target.hideFlags;
                this.FlagsDropdown.captionText.text = this.Target.hideFlags.ToString();
            }
        }

        void DoSetParent(Transform transform)
        {
            ExplorerCore.Log($"Setting target's transform parent to: {(transform == null ? "null" : $"'{transform.name}'")}");

            if (this.Target.GetComponent<RectTransform>())
                this.Target.transform.SetParent(transform, false);
            else
                this.Target.transform.parent = transform;

            this.UpdateGameObjectInfo(false, false);

            this.Owner.TransformControl.UpdateTransformControlValues(false);
        }


        #region UI event listeners

        void OnViewParentClicked()
        {
            if (this.Target && this.Target.transform.parent)
            {
                this.Owner.Parent.OnTransformCellClicked(this.Target.transform.parent.gameObject);
            }
        }

        void OnPathEndEdit(string input)
        {
            this.lastPath = input;

            if (string.IsNullOrEmpty(input))
            {
                this.DoSetParent(null);
            }
            else
            {
                Transform parentToSet = null;

                if (input.EndsWith("/"))
                    input = input.Remove(input.Length - 1);

                // try the easy way
                if (GameObject.Find(input) is GameObject found)
                {
                    parentToSet = found.transform;
                }
                else
                {
                    // look for inactive objects
                    string name = input.Split('/').Last();
                    UnityEngine.Object[] allObjects = RuntimeHelper.FindObjectsOfTypeAll(typeof(GameObject));
                    List<GameObject> shortList = new();
                    foreach (UnityEngine.Object obj in allObjects)
                        if (obj.name == name) shortList.Add(obj.TryCast<GameObject>());
                    foreach (GameObject go in shortList)
                    {
                        string path = go.transform.GetTransformPath(true);
                        if (path.EndsWith("/"))
                            path = path.Remove(path.Length - 1);
                        if (path == input)
                        {
                            parentToSet = go.transform;
                            break;
                        }
                    }
                }

                if (parentToSet)
                    this.DoSetParent(parentToSet);
                else
                {
                    ExplorerCore.LogWarning($"Could not find any GameObject name or path '{input}'!");
                    this.UpdateGameObjectInfo(false, true);
                }
            }

        }

        void OnNameEndEdit(string value)
        {
            this.Target.name = value;
            this.UpdateGameObjectInfo(false, true);
        }

        void OnCopyClicked()
        {
            ClipboardPanel.Copy(this.Target);
        }

        void OnActiveSelfToggled(bool value)
        {
            this.Target.SetActive(value);
            this.UpdateGameObjectInfo(false, true);
        }

        void OnTagEndEdit(string value)
        {
            try
            {
                this.Target.tag = value;
                this.UpdateGameObjectInfo(false, true);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception setting tag! {ex.ReflectionExToString()}");
            }
        }
        
        void OnSceneButtonClicked()
        {
            InspectorManager.Inspect(this.Target.scene);
        }

        void OnExploreButtonClicked()
        {
            ObjectExplorerPanel panel = UIManager.GetPanel<ObjectExplorerPanel>(UIManager.Panels.ObjectExplorer);
            panel.SceneExplorer.JumpToTransform(this.Owner.Parent.Target.transform);
        }

        void OnLayerDropdownChanged(int value)
        {
            this.Target.layer = value;
            this.UpdateGameObjectInfo(false, true);
        }

        void OnFlagsDropdownChanged(int value)
        {
            try
            {
                HideFlags enumVal = hideFlagsValues[this.FlagsDropdown.options[value].text];
                this.Target.hideFlags = enumVal;

                this.UpdateGameObjectInfo(false, true);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception setting hideFlags: {ex}");
            }
        }

        void OnDestroyClicked()
        {
            Object.Destroy(this.Target);
            InspectorManager.ReleaseInspector(this.Owner.Parent);
        }

        void OnInstantiateClicked()
        {
            GameObject clone = Object.Instantiate(this.Target);
            InspectorManager.Inspect(clone);
        }

        #endregion


        #region UI Construction

        public void Create()
        {
            GameObject topInfoHolder = UIFactory.CreateVerticalGroup(this.Owner.Parent.Content, "TopInfoHolder", false, false, true, true, 3,
                new Vector4(3, 3, 3, 3), new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(topInfoHolder, minHeight: 100, flexibleWidth: 9999);
            topInfoHolder.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // first row (parent, path)

            GameObject firstRow = UIFactory.CreateUIObject("ParentRow", topInfoHolder);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(firstRow, false, false, true, true, 5, 0, 0, 0, 0, default);
            UIFactory.SetLayoutElement(firstRow, minHeight: 25, flexibleWidth: 9999);

            this.ViewParentButton = UIFactory.CreateButton(firstRow, "ViewParentButton", "◄ View Parent", new Color(0.2f, 0.2f, 0.2f));
            this.ViewParentButton.ButtonText.fontSize = 13;
            UIFactory.SetLayoutElement(this.ViewParentButton.Component.gameObject, minHeight: 25, minWidth: 100);
            this.ViewParentButton.OnClick += this.OnViewParentClicked;

            this.PathInput = UIFactory.CreateInputField(firstRow, "PathInput", "...");
            this.PathInput.Component.textComponent.color = Color.grey;
            this.PathInput.Component.textComponent.fontSize = 14;
            UIFactory.SetLayoutElement(this.PathInput.UIRoot, minHeight: 25, minWidth: 100, flexibleWidth: 9999);
            this.PathInput.Component.lineType = InputField.LineType.MultiLineSubmit;

            ButtonRef copyButton = UIFactory.CreateButton(firstRow, "CopyButton", "Copy to Clipboard", new Color(0.2f, 0.2f, 0.2f, 1));
            copyButton.ButtonText.color = Color.yellow;
            UIFactory.SetLayoutElement(copyButton.Component.gameObject, minHeight: 25, minWidth: 120);
            copyButton.OnClick += this.OnCopyClicked;

            this.PathInput.Component.GetOnEndEdit().AddListener((string val) => { this.OnPathEndEdit(val); });

            // Title and update row

            GameObject titleRow = UIFactory.CreateUIObject("TitleRow", topInfoHolder);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(titleRow, false, false, true, true, 5);

            Text titleLabel = UIFactory.CreateLabel(titleRow, "Title", SignatureHighlighter.Parse(typeof(GameObject), false),
                TextAnchor.MiddleLeft, fontSize: 17);
            UIFactory.SetLayoutElement(titleLabel.gameObject, minHeight: 30, minWidth: 100);

            // name

            this.NameInput = UIFactory.CreateInputField(titleRow, "NameInput", "untitled");
            UIFactory.SetLayoutElement(this.NameInput.Component.gameObject, minHeight: 30, minWidth: 100, flexibleWidth: 9999);
            this.NameInput.Component.textComponent.fontSize = 15;
            this.NameInput.Component.GetOnEndEdit().AddListener((string val) => { this.OnNameEndEdit(val); });

            // second row (toggles, instanceID, tag, buttons)

            GameObject secondRow = UIFactory.CreateUIObject("ParentRow", topInfoHolder);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(secondRow, false, false, true, true, 5, 0, 0, 0, 0, default);
            UIFactory.SetLayoutElement(secondRow, minHeight: 25, flexibleWidth: 9999);

            // activeSelf
            GameObject activeToggleObj = UIFactory.CreateToggle(secondRow, "ActiveSelf", out this.ActiveSelfToggle, out this.ActiveSelfText);
            UIFactory.SetLayoutElement(activeToggleObj, minHeight: 25, minWidth: 100);
            this.ActiveSelfText.text = "ActiveSelf";
            this.ActiveSelfToggle.onValueChanged.AddListener(this.OnActiveSelfToggled);

            // isStatic
            GameObject isStaticObj = UIFactory.CreateToggle(secondRow, "IsStatic", out this.IsStaticToggle, out Text staticText);
            UIFactory.SetLayoutElement(isStaticObj, minHeight: 25, minWidth: 80);
            staticText.text = "IsStatic";
            staticText.color = Color.grey;
            this.IsStaticToggle.interactable = false;

            // InstanceID
            Text instanceIdLabel = UIFactory.CreateLabel(secondRow, "InstanceIDLabel", "Instance ID:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(instanceIdLabel.gameObject, minHeight: 25, minWidth: 90);

            this.InstanceIDInput = UIFactory.CreateInputField(secondRow, "InstanceIDInput", "error");
            UIFactory.SetLayoutElement(this.InstanceIDInput.Component.gameObject, minHeight: 25, minWidth: 110);
            this.InstanceIDInput.Component.textComponent.color = Color.grey;
            this.InstanceIDInput.Component.readOnly = true;

            //Tag
            Text tagLabel = UIFactory.CreateLabel(secondRow, "TagLabel", "Tag:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(tagLabel.gameObject, minHeight: 25, minWidth: 40);

            this.TagInput = UIFactory.CreateInputField(secondRow, "TagInput", "none");
            UIFactory.SetLayoutElement(this.TagInput.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 999);
            this.TagInput.Component.textComponent.color = Color.white;
            this.TagInput.Component.GetOnEndEdit().AddListener((string val) => { this.OnTagEndEdit(val); });

            // Instantiate
            ButtonRef instantiateBtn = UIFactory.CreateButton(secondRow, "InstantiateBtn", "Instantiate", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(instantiateBtn.Component.gameObject, minHeight: 25, minWidth: 120);
            instantiateBtn.OnClick += this.OnInstantiateClicked;

            // Destroy
            ButtonRef destroyBtn = UIFactory.CreateButton(secondRow, "DestroyBtn", "Destroy", new Color(0.3f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(destroyBtn.Component.gameObject, minHeight: 25, minWidth: 80);
            destroyBtn.OnClick += this.OnDestroyClicked;

            // third row (scene, layer, flags)

            GameObject thirdrow = UIFactory.CreateUIObject("ParentRow", topInfoHolder);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(thirdrow, false, false, true, true, 5, 0, 0, 0, 0, default);
            UIFactory.SetLayoutElement(thirdrow, minHeight: 25, flexibleWidth: 9999);

            // Inspect in Explorer button
            ButtonRef explorerBtn = UIFactory.CreateButton(thirdrow, "ExploreBtn", "Show in Explorer", new Color(0.15f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(explorerBtn.Component.gameObject, minHeight: 25, minWidth: 100);
            explorerBtn.ButtonText.fontSize = 12;
            explorerBtn.OnClick += this.OnExploreButtonClicked;

            // Scene
            Text sceneLabel = UIFactory.CreateLabel(thirdrow, "SceneLabel", "Scene:", TextAnchor.MiddleLeft, Color.grey);
            UIFactory.SetLayoutElement(sceneLabel.gameObject, minHeight: 25, minWidth: 50);

            this.SceneButton = UIFactory.CreateButton(thirdrow, "SceneButton", "untitled");
            UIFactory.SetLayoutElement(this.SceneButton.Component.gameObject, minHeight: 25, minWidth: 120, flexibleWidth: 999);
            this.SceneButton.OnClick += this.OnSceneButtonClicked;

            // Layer
            Text layerLabel = UIFactory.CreateLabel(thirdrow, "LayerLabel", "Layer:", TextAnchor.MiddleLeft, Color.grey);
            UIFactory.SetLayoutElement(layerLabel.gameObject, minHeight: 25, minWidth: 50);

            GameObject layerDrop = UIFactory.CreateDropdown(thirdrow, "LayerDropdown", out this.LayerDropdown, "0", 14, this.OnLayerDropdownChanged);
            UIFactory.SetLayoutElement(layerDrop, minHeight: 25, minWidth: 110, flexibleWidth: 999);
            this.LayerDropdown.captionText.color = SignatureHighlighter.EnumGreen;
            if (layerToNames == null)
                GetLayerNames();
            foreach (string name in layerToNames) this.LayerDropdown.options.Add(new Dropdown.OptionData(name));
            this.LayerDropdown.value = 0;
            this.LayerDropdown.RefreshShownValue();

            // Flags
            Text flagsLabel = UIFactory.CreateLabel(thirdrow, "FlagsLabel", "Flags:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(flagsLabel.gameObject, minHeight: 25, minWidth: 50);

            GameObject flagsDrop = UIFactory.CreateDropdown(thirdrow, "FlagsDropdown", out this.FlagsDropdown, "None", 14, this.OnFlagsDropdownChanged);
            this.FlagsDropdown.captionText.color = SignatureHighlighter.EnumGreen;
            UIFactory.SetLayoutElement(flagsDrop, minHeight: 25, minWidth: 135, flexibleWidth: 999);
            if (hideFlagsValues == null)
                GetHideFlagNames();
            foreach (string name in hideFlagsValues.Keys) this.FlagsDropdown.options.Add(new Dropdown.OptionData(name));
            this.FlagsDropdown.value = 0;
            this.FlagsDropdown.RefreshShownValue();
        }

        private static List<string> layerToNames;

        private static void GetLayerNames()
        {
            layerToNames = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string name = RuntimeHelper.LayerToName(i);
                if (string.IsNullOrEmpty(name))
                    name = i.ToString();
                layerToNames.Add(name);
            }
        }

        private static Dictionary<string, HideFlags> hideFlagsValues;

        private static void GetHideFlagNames()
        {
            hideFlagsValues = new Dictionary<string, HideFlags>();

            Array names = Enum.GetValues(typeof(HideFlags));
            foreach (HideFlags value in names)
            {
                hideFlagsValues.Add(value.ToString(), value);
            }
        }

        #endregion
   
    }
}
