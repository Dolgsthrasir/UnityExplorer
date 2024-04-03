using System.Collections;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.Inspectors
{
    public class GameObjectInspector : InspectorBase
    {
        public new GameObject Target => base.Target as GameObject;

        public GameObject Content;

        public GameObjectControls Controls;

        public TransformTree TransformTree;
        private ScrollPool<TransformCell> transformScroll;
        private readonly List<GameObject> cachedChildren = new();

        public ComponentList ComponentList;
        private ScrollPool<ComponentCell> componentScroll;

        private InputFieldRef addChildInput;
        private InputFieldRef addCompInput;

        public override void OnBorrowedFromPool(object target)
        {
            base.OnBorrowedFromPool(target);

            base.Target = target as GameObject;

            this.Controls.UpdateGameObjectInfo(true, true);
            this.Controls.TransformControl.UpdateTransformControlValues(true);

            RuntimeHelper.StartCoroutine(this.InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            yield return null;

            LayoutRebuilder.ForceRebuildLayoutImmediate(InspectorPanel.Instance.ContentRect);

            this.TransformTree.Rebuild();

            this.ComponentList.ScrollPool.Refresh(true, true);
            this.UpdateComponents();
        }

        public override void OnReturnToPool()
        {
            base.OnReturnToPool();

            this.addChildInput.Text = "";
            this.addCompInput.Text = "";

            this.TransformTree.Clear();
            this.UpdateComponents();
        }

        public override void CloseInspector()
        {
            InspectorManager.ReleaseInspector(this);
        }

        public void OnTransformCellClicked(GameObject newTarget)
        {
            base.Target = newTarget;
            this.Controls.UpdateGameObjectInfo(true, true);
            this.Controls.TransformControl.UpdateTransformControlValues(true);
            this.TransformTree.RefreshData(true, false, true, false);
            this.UpdateComponents();
        }

        private float timeOfLastUpdate;

        public override void Update()
        {
            if (!this.IsActive)
                return;

            if (base.Target.IsNullOrDestroyed(false))
            {
                InspectorManager.ReleaseInspector(this);
                return;
            }

            this.Controls.UpdateVectorSlider();
            this.Controls.TransformControl.UpdateTransformControlValues(false);

            // Slow update
            if (this.timeOfLastUpdate.OccuredEarlierThan(1))
            {
                this.timeOfLastUpdate = Time.realtimeSinceStartup;

                this.Controls.UpdateGameObjectInfo(false, false);

                this.TransformTree.RefreshData(true, false, false, false);
                this.UpdateComponents();
            }
        }

        // Child and Component Lists

        private IEnumerable<GameObject> GetTransformEntries()
        {
            if (!this.Target)
                return Enumerable.Empty<GameObject>();

            this.cachedChildren.Clear();
            for (int i = 0; i < this.Target.transform.childCount; i++) this.cachedChildren.Add(this.Target.transform.GetChild(i).gameObject);
            return this.cachedChildren;
        }

        private readonly List<Component> componentEntries = new();
        private readonly HashSet<int> compInstanceIDs = new();
        private readonly List<Behaviour> behaviourEntries = new();
        private readonly List<bool> behaviourEnabledStates = new();

        // ComponentList.GetRootEntriesMethod
        private List<Component> GetComponentEntries() => this.Target ? this.componentEntries : Enumerable.Empty<Component>().ToList();

        public void UpdateComponents()
        {
            if (!this.Target)
            {
                this.componentEntries.Clear();
                this.compInstanceIDs.Clear();
                this.behaviourEntries.Clear();
                this.behaviourEnabledStates.Clear();
                this.ComponentList.RefreshData();
                this.ComponentList.ScrollPool.Refresh(true, true);
                return;
            }

            // Check if we actually need to refresh the component cells or not.
            IEnumerable<Component> comps = this.Target.GetComponents<Component>();
            IEnumerable<Behaviour> behaviours = this.Target.GetComponents<Behaviour>();

            bool needRefresh = false;

            int count = 0;
            foreach (Component comp in comps)
            {
                if (!comp)
                    continue;
                count++;
                if (!this.compInstanceIDs.Contains(comp.GetInstanceID()))
                {
                    needRefresh = true;
                    break;
                }
            }
            if (!needRefresh)
            {
                if (count != this.componentEntries.Count)
                    needRefresh = true;
                else
                {
                    count = 0;
                    foreach (Behaviour behaviour in behaviours)
                    {
                        if (!behaviour)
                            continue;
                        if (count >= this.behaviourEnabledStates.Count || behaviour.enabled != this.behaviourEnabledStates[count])
                        {
                            needRefresh = true;
                            break;
                        }
                        count++;
                    }
                    if (!needRefresh && count != this.behaviourEntries.Count)
                        needRefresh = true;
                }
            }

            if (!needRefresh)
                return;

            this.componentEntries.Clear();
            this.compInstanceIDs.Clear();
            foreach (Component comp in comps)
            {
                if (!comp) 
                    continue;
                this.componentEntries.Add(comp);
                this.compInstanceIDs.Add(comp.GetInstanceID());
            }

            this.behaviourEntries.Clear();
            this.behaviourEnabledStates.Clear();
            foreach (Behaviour behaviour in behaviours)
            {
                if (!behaviour) 
                    continue;

                // Don't ask me how, but in some games this can be true for certain components.
                // They get picked up from GetComponents<Behaviour>, but they are not actually Behaviour...?
                if (!typeof(Behaviour).IsAssignableFrom(behaviour.GetType()))
                    continue;

                try
                {
                    this.behaviourEntries.Add(behaviour);
                }
                catch (Exception ex)
                {
                    ExplorerCore.LogWarning(ex);
                }

                this.behaviourEnabledStates.Add(behaviour.enabled);
            }

            this.ComponentList.RefreshData();
            this.ComponentList.ScrollPool.Refresh(true);
        }


        private void OnAddChildClicked(string input)
        {
            GameObject newObject = new(input);
            newObject.transform.parent = this.Target.transform;

            this.TransformTree.RefreshData(true, false, true, false);
        }

        private void OnAddComponentClicked(string input)
        {
            if (ReflectionUtility.GetTypeByName(input) is Type type)
            {
                try
                {
                    RuntimeHelper.AddComponent<Component>(this.Target, type);
                    this.UpdateComponents();
                }
                catch (Exception ex)
                {
                    ExplorerCore.LogWarning($"Exception adding component: {ex.ReflectionExToString()}");
                }
            }
            else
            {
                ExplorerCore.LogWarning($"Could not find any Type by the name '{input}'!");
            }
        }

        #region UI Construction

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "GameObjectInspector", true, false, true, true, 5,
                new Vector4(4, 4, 4, 4), new Color(0.065f, 0.065f, 0.065f));

            GameObject scrollObj = UIFactory.CreateScrollView(this.UIRoot, "GameObjectInspector", out this.Content, out AutoSliderScrollbar scrollbar,
                new Color(0.065f, 0.065f, 0.065f));
            UIFactory.SetLayoutElement(scrollObj, minHeight: 250, preferredHeight: 300, flexibleHeight: 0, flexibleWidth: 9999);

            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.Content, spacing: 3, padTop: 2, padBottom: 2, padLeft: 2, padRight: 2);

            // Construct GO Controls
            this.Controls = new GameObjectControls(this);

            this.ConstructLists();

            return this.UIRoot;
        }

        // Child and Comp Lists

        private void ConstructLists()
        {
            GameObject listHolder = UIFactory.CreateUIObject("ListHolders", this.UIRoot);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(listHolder, false, true, true, true, 8, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(listHolder, minHeight: 150, flexibleWidth: 9999, flexibleHeight: 9999);

            // Left group (Children)

            GameObject leftGroup = UIFactory.CreateUIObject("ChildrenGroup", listHolder);
            UIFactory.SetLayoutElement(leftGroup, flexibleWidth: 9999, flexibleHeight: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(leftGroup, false, false, true, true, 2);

            Text childrenLabel = UIFactory.CreateLabel(leftGroup, "ChildListTitle", "Children", TextAnchor.MiddleCenter, default, false, 16);
            UIFactory.SetLayoutElement(childrenLabel.gameObject, flexibleWidth: 9999);

            // Add Child
            GameObject addChildRow = UIFactory.CreateUIObject("AddChildRow", leftGroup);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(addChildRow, false, false, true, true, 2);

            this.addChildInput = UIFactory.CreateInputField(addChildRow, "AddChildInput", "Enter a name...");
            UIFactory.SetLayoutElement(this.addChildInput.Component.gameObject, minHeight: 25, preferredWidth: 9999);

            ButtonRef addChildButton = UIFactory.CreateButton(addChildRow, "AddChildButton", "Add Child");
            UIFactory.SetLayoutElement(addChildButton.Component.gameObject, minHeight: 25, minWidth: 80);
            addChildButton.OnClick += () => { this.OnAddChildClicked(this.addChildInput.Text); };

            // TransformTree

            this.transformScroll = UIFactory.CreateScrollPool<TransformCell>(leftGroup, "TransformTree", out GameObject transformObj,
                out GameObject transformContent, new Color(0.11f, 0.11f, 0.11f));

            this.TransformTree = new TransformTree(this.transformScroll, this.GetTransformEntries, this.OnTransformCellClicked);

            // Right group (Components)

            GameObject rightGroup = UIFactory.CreateUIObject("ComponentGroup", listHolder);
            UIFactory.SetLayoutElement(rightGroup, flexibleWidth: 9999, flexibleHeight: 9999);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(rightGroup, false, false, true, true, 2);

            Text compLabel = UIFactory.CreateLabel(rightGroup, "CompListTitle", "Components", TextAnchor.MiddleCenter, default, false, 16);
            UIFactory.SetLayoutElement(compLabel.gameObject, flexibleWidth: 9999);

            // Add Comp
            GameObject addCompRow = UIFactory.CreateUIObject("AddCompRow", rightGroup);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(addCompRow, false, false, true, true, 2);

            this.addCompInput = UIFactory.CreateInputField(addCompRow, "AddCompInput", "Enter a Component type...");
            UIFactory.SetLayoutElement(this.addCompInput.Component.gameObject, minHeight: 25, preferredWidth: 9999);

            ButtonRef addCompButton = UIFactory.CreateButton(addCompRow, "AddCompButton", "Add Comp");
            UIFactory.SetLayoutElement(addCompButton.Component.gameObject, minHeight: 25, minWidth: 80);
            addCompButton.OnClick += () => { this.OnAddComponentClicked(this.addCompInput.Text); };

            // comp autocompleter
            new TypeCompleter(typeof(Component), this.addCompInput, false, false, false);

            // Component List

            this.componentScroll = UIFactory.CreateScrollPool<ComponentCell>(rightGroup, "ComponentList", out GameObject compObj,
                out GameObject compContent, new Color(0.11f, 0.11f, 0.11f));
            UIFactory.SetLayoutElement(compObj, flexibleHeight: 9999);
            UIFactory.SetLayoutElement(compContent, flexibleHeight: 9999);

            this.ComponentList = new ComponentList(this.componentScroll, this.GetComponentEntries)
            {
                Parent = this
            };
            this.componentScroll.Initialize(this.ComponentList);
        }


        #endregion
    }
}
