using System.Collections;
using System.Diagnostics;
using System.Reflection.Emit;
using UnityExplorer.CacheObject;
using UnityExplorer.CacheObject.Views;
using UnityExplorer.Config;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.Inspectors
{
    [Flags]
    public enum MemberFilter
    {
        None = 0,
        Property = 1,
        Field = 2,
        Constructor = 4,
        Method = 8,
        All = Property | Field | Method | Constructor,
    }

    public class ReflectionInspector : InspectorBase, ICellPoolDataSource<CacheMemberCell>, ICacheObjectController
    {
        public CacheObjectBase ParentCacheObject { get; set; }
        public bool StaticOnly { get; internal set; }
        public bool CanWrite => true;

        public bool AutoUpdateWanted => this.autoUpdateToggle.isOn;

        List<CacheMember> members = new();
        readonly List<CacheMember> filteredMembers = new();

        string nameFilter;
        BindingFlags scopeFlagsFilter;
        MemberFilter memberFilter = MemberFilter.All;

        // Updating

        bool refreshWanted;
        string lastNameFilter;
        BindingFlags lastFlagsFilter;
        MemberFilter lastMemberFilter = MemberFilter.All;
        float timeOfLastAutoUpdate;

        // UI

        static int LeftGroupWidth { get; set; }
        static int RightGroupWidth { get; set; }

        static readonly Color disabledButtonColor = new(0.24f, 0.24f, 0.24f);
        static readonly Color enabledButtonColor = new(0.2f, 0.27f, 0.2f);

        public GameObject ContentRoot { get; private set; }
        public ScrollPool<CacheMemberCell> MemberScrollPool { get; private set; }
        public int ItemCount => this.filteredMembers.Count;
        public UnityObjectWidget UnityWidget { get; private set; }
        public string TabButtonText { get; set; }

        InputFieldRef hiddenNameText;
        Text nameText;
        Text assemblyText;
        Toggle autoUpdateToggle;

        ButtonRef dnSpyButton;

        ButtonRef makeGenericButton;
        GenericConstructorWidget genericConstructor;

        InputFieldRef filterInputField;
        readonly List<Toggle> memberTypeToggles = new();
        readonly Dictionary<BindingFlags, ButtonRef> scopeFilterButtons = new();

        // Setup

        public override void OnBorrowedFromPool(object target)
        {
            base.OnBorrowedFromPool(target);
            this.CalculateLayouts();

            this.SetTarget(target);

            RuntimeHelper.StartCoroutine(this.InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(InspectorPanel.Instance.ContentRect);
        }

        public override void CloseInspector()
        {
            InspectorManager.ReleaseInspector(this);
        }

        public override void OnReturnToPool()
        {
            foreach (CacheMember member in this.members)
            {
                member.UnlinkFromView();
                member.ReleasePooledObjects();
            }

            this.members.Clear();
            this.filteredMembers.Clear();

            this.autoUpdateToggle.isOn = false;

            if (this.UnityWidget != null)
            {
                this.UnityWidget.OnReturnToPool();
                Pool.Return(this.UnityWidget.GetType(), this.UnityWidget);
                this.UnityWidget = null;
            }

            this.genericConstructor?.Cancel();

            base.OnReturnToPool();
        }

        // Setting target

        private void SetTarget(object target)
        {
            string prefix;
            if (this.StaticOnly)
            {
                this.Target = null;
                this.TargetType = target as Type;
                prefix = "[S]";

                this.makeGenericButton.GameObject.SetActive(this.TargetType.IsGenericTypeDefinition);
            }
            else
            {
                this.TargetType = target.GetActualType();
                prefix = "[R]";
            }

            // Setup main labels and tab text
            this.TabButtonText = $"{prefix} {SignatureHighlighter.Parse(this.TargetType, false)}";
            this.Tab.TabText.text = this.TabButtonText;
            this.nameText.text = SignatureHighlighter.Parse(this.TargetType, true);
            this.hiddenNameText.Text = SignatureHighlighter.RemoveHighlighting(this.nameText.text);

            string asmText;
            if (this.TargetType.Assembly is AssemblyBuilder || string.IsNullOrEmpty(this.TargetType.Assembly.Location))
            {
                asmText = $"{this.TargetType.Assembly.GetName().Name} <color=grey><i>(in memory)</i></color>";
                this.dnSpyButton.GameObject.SetActive(false);
            }
            else
            {
                asmText = Path.GetFileName(this.TargetType.Assembly.Location);
                this.dnSpyButton.GameObject.SetActive(true);
            }
            this.assemblyText.text = $"<color=grey>Assembly:</color> {asmText}";

            // Unity object helper widget

            if (!this.StaticOnly)
                this.UnityWidget = UnityObjectWidget.GetUnityWidget(target, this.TargetType, this);

            // Get cache members

            this.members = CacheMemberFactory.GetCacheMembers(this.TargetType, this);

            // reset filters

            this.filterInputField.Text = string.Empty;

            this.SetFilter(string.Empty, this.StaticOnly ? BindingFlags.Static : BindingFlags.Default);
            this.scopeFilterButtons[BindingFlags.Default].Component.gameObject.SetActive(!this.StaticOnly);
            this.scopeFilterButtons[BindingFlags.Instance].Component.gameObject.SetActive(!this.StaticOnly);

            foreach (Toggle toggle in this.memberTypeToggles)
                toggle.isOn = true;

            this.refreshWanted = true;
        }

        // Updating

        public override void Update()
        {
            if (!this.IsActive)
                return;

            if (!this.StaticOnly && this.Target.IsNullOrDestroyed(false))
            {
                InspectorManager.ReleaseInspector(this);
                return;
            }

            // check filter changes or force-refresh
            if (this.refreshWanted || this.nameFilter != this.lastNameFilter || this.scopeFlagsFilter != this.lastFlagsFilter || this.lastMemberFilter != this.memberFilter)
            {
                this.lastNameFilter = this.nameFilter;
                this.lastFlagsFilter = this.scopeFlagsFilter;
                this.lastMemberFilter = this.memberFilter;

                this.FilterMembers();
                this.MemberScrollPool.Refresh(true, true);
                this.refreshWanted = false;
            }

            // once-per-second updates
            if (this.timeOfLastAutoUpdate.OccuredEarlierThan(1))
            {
                this.timeOfLastAutoUpdate = Time.realtimeSinceStartup;

                if (this.UnityWidget != null) this.UnityWidget.Update();

                if (this.AutoUpdateWanted) this.UpdateDisplayedMembers();
            }
        }

        // Filtering

        public void SetFilter(string name, BindingFlags flags)
        {
            this.nameFilter = name;

            if (flags != this.scopeFlagsFilter)
            {
                Button btn = this.scopeFilterButtons[this.scopeFlagsFilter].Component;
                RuntimeHelper.SetColorBlock(btn, disabledButtonColor, disabledButtonColor * 1.3f);

                this.scopeFlagsFilter = flags;
                btn = this.scopeFilterButtons[this.scopeFlagsFilter].Component;
                RuntimeHelper.SetColorBlock(btn, enabledButtonColor, enabledButtonColor * 1.3f);
            }
        }

        void FilterMembers()
        {
            this.filteredMembers.Clear();

            for (int i = 0; i < this.members.Count; i++)
            {
                CacheMember member = this.members[i];

                if (this.scopeFlagsFilter != BindingFlags.Default)
                {
                    if (this.scopeFlagsFilter == BindingFlags.Instance && member.IsStatic
                        ||
                        this.scopeFlagsFilter == BindingFlags.Static && !member.IsStatic)
                        continue;
                }

                if ((member is CacheMethod && !this.memberFilter.HasFlag(MemberFilter.Method))
                    || (member is CacheField && !this.memberFilter.HasFlag(MemberFilter.Field))
                    || (member is CacheProperty && !this.memberFilter.HasFlag(MemberFilter.Property))
                    || (member is CacheConstructor && !this.memberFilter.HasFlag(MemberFilter.Constructor)))
                    continue;

                if (!string.IsNullOrEmpty(this.nameFilter) && !member.NameForFiltering.ContainsIgnoreCase(this.nameFilter))
                    continue;

                this.filteredMembers.Add(member);
            }
        }

        void UpdateDisplayedMembers()
        {
            bool shouldRefresh = false;
            foreach (CacheMemberCell cell in this.MemberScrollPool.CellPool)
            {
                if (!cell.Enabled || cell.Occupant == null)
                    continue;
                CacheMember member = cell.MemberOccupant;
                if (member.ShouldAutoEvaluate)
                {
                    shouldRefresh = true;
                    member.Evaluate();
                    member.SetDataToCell(member.CellView);
                }
            }

            if (shouldRefresh) this.MemberScrollPool.Refresh(false);
        }

        // Member cells

        public void OnCellBorrowed(CacheMemberCell cell) { } // not needed

        public void SetCell(CacheMemberCell cell, int index)
        {
            CacheObjectControllerHelper.SetCell(cell, index, this.filteredMembers, this.SetCellLayout);
        }

        // Cell layout (fake table alignment)

        internal void SetLayouts()
        {
            this.CalculateLayouts();

            foreach (CacheMemberCell cell in this.MemberScrollPool.CellPool) this.SetCellLayout(cell);
        }

        void CalculateLayouts()
        {
            LeftGroupWidth = (int)Math.Max(200, (0.4f * InspectorManager.PanelWidth) - 5);
            RightGroupWidth = (int)Math.Max(200, InspectorManager.PanelWidth - LeftGroupWidth - 65);
        }

        void SetCellLayout(CacheObjectCell cell)
        {
            cell.NameLayout.minWidth = LeftGroupWidth;
            cell.RightGroupLayout.minWidth = RightGroupWidth;

            if (cell.Occupant?.IValue != null)
                cell.Occupant.IValue.SetLayout();
        }

        // UI listeners

        void OnUpdateClicked()
        {
            this.UpdateDisplayedMembers();
        }

        public void OnSetNameFilter(string name)
        {
            this.SetFilter(name, this.scopeFlagsFilter);
        }

        public void OnSetFlags(BindingFlags flags)
        {
            this.SetFilter(this.nameFilter, flags);
        }

        void OnMemberTypeToggled(MemberFilter flag, bool val)
        {
            if (!val)
                this.memberFilter &= ~flag;
            else
                this.memberFilter |= flag;
        }

        void OnCopyClicked()
        {
            ClipboardPanel.Copy(this.Target ?? this.TargetType);
        }

        void OnDnSpyButtonClicked()
        {
            string path = ConfigManager.DnSpy_Path.Value;
            if (File.Exists(path) && path.EndsWith("dnspy.exe", StringComparison.OrdinalIgnoreCase))
            {
                Type type = this.TargetType;
                // if constructed generic type, use the generic type definition
                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                    type = type.GetGenericTypeDefinition();

                string args = $"\"{type.Assembly.Location}\" --select T:{type.FullName}";
                Process.Start(path, args);
            }
            else
            {
                Notification.ShowMessage($"Please set a valid dnSpy path in UnityExplorer Settings.");
            }
        }

        void OnMakeGenericClicked()
        {
            this.ContentRoot.SetActive(false);

            if (this.genericConstructor == null)
            {
                this.genericConstructor = new();
                this.genericConstructor.ConstructUI(this.UIRoot);
            }

            this.genericConstructor.UIRoot.SetActive(true);
            this.genericConstructor.Show(this.OnGenericSubmit, this.OnGenericCancel, this.TargetType);
        }

        void OnGenericSubmit(Type[] args)
        {
            this.ContentRoot.SetActive(true);
            this.genericConstructor.UIRoot.SetActive(false);

            Type newType = this.TargetType.MakeGenericType(args);
            InspectorManager.Inspect(newType);
            //InspectorManager.ReleaseInspector(this);
        }

        void OnGenericCancel()
        {
            this.ContentRoot.SetActive(true);
            this.genericConstructor.UIRoot.SetActive(false);
        }

        // UI Construction

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "ReflectionInspector", true, true, true, true, 5,
                new Vector4(4, 4, 4, 4), new Color(0.065f, 0.065f, 0.065f));

            // Class name, assembly

            GameObject topRow = UIFactory.CreateHorizontalGroup(this.UIRoot, "TopRow", false, false, true, true, 4, default, 
                new(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(topRow, minHeight: 25, flexibleWidth: 9999);

            GameObject titleHolder = UIFactory.CreateUIObject("TitleHolder", topRow);
            UIFactory.SetLayoutElement(titleHolder, minHeight: 35, flexibleHeight: 0, flexibleWidth: 9999);

            this.nameText = UIFactory.CreateLabel(titleHolder, "VisibleTitle", "NotSet", TextAnchor.MiddleLeft);
            RectTransform namerect = this.nameText.GetComponent<RectTransform>();
            namerect.anchorMin = new Vector2(0, 0);
            namerect.anchorMax = new Vector2(1, 1);
            this.nameText.fontSize = 17;
            UIFactory.SetLayoutElement(this.nameText.gameObject, minHeight: 35, flexibleHeight: 0, minWidth: 300, flexibleWidth: 9999);

            this.hiddenNameText = UIFactory.CreateInputField(titleHolder, "Title", "not set");
            RectTransform hiddenrect = this.hiddenNameText.Component.gameObject.GetComponent<RectTransform>();
            hiddenrect.anchorMin = new Vector2(0, 0);
            hiddenrect.anchorMax = new Vector2(1, 1);
            this.hiddenNameText.Component.readOnly = true;
            this.hiddenNameText.Component.lineType = InputField.LineType.MultiLineNewline;
            this.hiddenNameText.Component.gameObject.GetComponent<Image>().color = Color.clear;
            this.hiddenNameText.Component.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            this.hiddenNameText.Component.textComponent.fontSize = 17;
            this.hiddenNameText.Component.textComponent.color = Color.clear;
            UIFactory.SetLayoutElement(this.hiddenNameText.Component.gameObject, minHeight: 35, flexibleHeight: 0, flexibleWidth: 9999);

            this.makeGenericButton = UIFactory.CreateButton(topRow, "MakeGenericButton", "Construct Generic", new Color(0.2f, 0.3f, 0.2f));
            UIFactory.SetLayoutElement(this.makeGenericButton.GameObject, minWidth: 140, minHeight: 25);
            this.makeGenericButton.OnClick += this.OnMakeGenericClicked;
            this.makeGenericButton.GameObject.SetActive(false);

            ButtonRef copyButton = UIFactory.CreateButton(topRow, "CopyButton", "Copy to Clipboard", new Color(0.2f, 0.2f, 0.2f, 1));
            copyButton.ButtonText.color = Color.yellow;
            UIFactory.SetLayoutElement(copyButton.Component.gameObject, minHeight: 25, minWidth: 120, flexibleWidth: 0);
            copyButton.OnClick += this.OnCopyClicked;

            // Assembly row

            GameObject asmRow = UIFactory.CreateHorizontalGroup(this.UIRoot, "AssemblyRow", false, false, true, true, 5, default, new(1, 1, 1, 0));
            UIFactory.SetLayoutElement(asmRow, flexibleWidth: 9999, minHeight: 25);

            this.assemblyText = UIFactory.CreateLabel(asmRow, "AssemblyLabel", "not set", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.assemblyText.gameObject, minHeight: 25, flexibleWidth: 9999);

            this.dnSpyButton = UIFactory.CreateButton(asmRow, "DnSpyButton", "View in dnSpy");
            UIFactory.SetLayoutElement(this.dnSpyButton.GameObject, minWidth: 120, minHeight: 25);
            this.dnSpyButton.OnClick += this.OnDnSpyButtonClicked;

            // Content 

            this.ContentRoot = UIFactory.CreateVerticalGroup(this.UIRoot, "ContentRoot", false, false, true, true, 5, new Vector4(2, 2, 2, 2),
                new Color(0.12f, 0.12f, 0.12f));
            UIFactory.SetLayoutElement(this.ContentRoot, flexibleWidth: 9999, flexibleHeight: 9999);

            this.ConstructFirstRow(this.ContentRoot);

            this.ConstructSecondRow(this.ContentRoot);

            // Member scroll pool

            GameObject memberBorder = UIFactory.CreateVerticalGroup(this.ContentRoot, "ScrollPoolHolder", false, false, true, true,
                padding: new Vector4(2, 2, 2, 2), bgColor: new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(memberBorder, flexibleWidth: 9999, flexibleHeight: 9999);

            this.MemberScrollPool = UIFactory.CreateScrollPool<CacheMemberCell>(memberBorder, "MemberList", out GameObject scrollObj,
                out GameObject _, new Color(0.09f, 0.09f, 0.09f));
            UIFactory.SetLayoutElement(scrollObj, flexibleHeight: 9999);
            this.MemberScrollPool.Initialize(this);

            // For debugging scroll pool
            //InspectorPanel.Instance.UIRoot.GetComponent<Mask>().enabled = false;
            //MemberScrollPool.Viewport.GetComponent<Mask>().enabled = false;
            //MemberScrollPool.Viewport.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f);

            return this.UIRoot;
        }

        // First row

        void ConstructFirstRow(GameObject parent)
        {
            GameObject rowObj = UIFactory.CreateUIObject("FirstRow", parent);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(rowObj, true, true, true, true, 5, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(rowObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            Text nameLabel = UIFactory.CreateLabel(rowObj, "NameFilterLabel", "Filter names:", TextAnchor.MiddleLeft, Color.grey);
            UIFactory.SetLayoutElement(nameLabel.gameObject, minHeight: 25, minWidth: 90, flexibleWidth: 0);

            this.filterInputField = UIFactory.CreateInputField(rowObj, "NameFilterInput", "...");
            UIFactory.SetLayoutElement(this.filterInputField.UIRoot, minHeight: 25, flexibleWidth: 300);
            this.filterInputField.OnValueChanged += (string val) => { this.OnSetNameFilter(val); };

            GameObject spacer = UIFactory.CreateUIObject("Spacer", rowObj);
            UIFactory.SetLayoutElement(spacer, minWidth: 25);

            // Update button and toggle

            ButtonRef updateButton = UIFactory.CreateButton(rowObj, "UpdateButton", "Update displayed values", new Color(0.22f, 0.28f, 0.22f));
            UIFactory.SetLayoutElement(updateButton.Component.gameObject, minHeight: 25, minWidth: 175, flexibleWidth: 0);
            updateButton.OnClick += this.OnUpdateClicked;

            GameObject toggleObj = UIFactory.CreateToggle(rowObj, "AutoUpdateToggle", out this.autoUpdateToggle, out Text toggleText);
            UIFactory.SetLayoutElement(toggleObj, minWidth: 125, minHeight: 25);
            this.autoUpdateToggle.isOn = false;
            toggleText.text = "Auto-update";
        }

        // Second row

        void ConstructSecondRow(GameObject parent)
        {
            GameObject rowObj = UIFactory.CreateUIObject("SecondRow", parent);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(rowObj, false, false, true, true, 5, 2, 2, 2, 2);
            UIFactory.SetLayoutElement(rowObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            // Scope buttons

            Text scopeLabel = UIFactory.CreateLabel(rowObj, "ScopeLabel", "Scope:", TextAnchor.MiddleLeft, Color.grey);
            UIFactory.SetLayoutElement(scopeLabel.gameObject, minHeight: 25, minWidth: 60, flexibleWidth: 0);
            this.AddScopeFilterButton(rowObj, BindingFlags.Default, true);
            this.AddScopeFilterButton(rowObj, BindingFlags.Instance);
            this.AddScopeFilterButton(rowObj, BindingFlags.Static);

            GameObject spacer = UIFactory.CreateUIObject("Spacer", rowObj);
            UIFactory.SetLayoutElement(spacer, minWidth: 15);

            // Member type toggles

            this.AddMemberTypeToggle(rowObj, MemberTypes.Property, 90);
            this.AddMemberTypeToggle(rowObj, MemberTypes.Field, 70);
            this.AddMemberTypeToggle(rowObj, MemberTypes.Method, 90);
            this.AddMemberTypeToggle(rowObj, MemberTypes.Constructor, 110);
        }

        void AddScopeFilterButton(GameObject parent, BindingFlags flags, bool setAsActive = false)
        {
            string lbl = flags == BindingFlags.Default ? "All" : flags.ToString();
            Color color = setAsActive ? enabledButtonColor : disabledButtonColor;

            ButtonRef button = UIFactory.CreateButton(parent, "Filter_" + flags, lbl, color);
            UIFactory.SetLayoutElement(button.Component.gameObject, minHeight: 25, flexibleHeight: 0, minWidth: 70, flexibleWidth: 0);
            this.scopeFilterButtons.Add(flags, button);

            button.OnClick += () => { this.OnSetFlags(flags); };
        }

        void AddMemberTypeToggle(GameObject parent, MemberTypes type, int width)
        {
            GameObject toggleObj = UIFactory.CreateToggle(parent, "Toggle_" + type, out Toggle toggle, out Text toggleText);
            UIFactory.SetLayoutElement(toggleObj, minHeight: 25, minWidth: width);
            string color = type switch
            {
                MemberTypes.Method => SignatureHighlighter.METHOD_INSTANCE,
                MemberTypes.Field => SignatureHighlighter.FIELD_INSTANCE,
                MemberTypes.Property => SignatureHighlighter.PROP_INSTANCE,
                MemberTypes.Constructor => SignatureHighlighter.CLASS_INSTANCE,
                _ => throw new NotImplementedException()
            };
            toggleText.text = $"<color={color}>{type}</color>";

            toggle.graphic.TryCast<Image>().color = color.ToColor() * 0.65f;

            MemberFilter flag = type switch
            {
                MemberTypes.Method => MemberFilter.Method,
                MemberTypes.Property => MemberFilter.Property,
                MemberTypes.Field => MemberFilter.Field,
                MemberTypes.Constructor => MemberFilter.Constructor,
                _ => throw new NotImplementedException()
            };

            toggle.onValueChanged.AddListener((bool val) => { this.OnMemberTypeToggled(flag, val); });

            this.memberTypeToggles.Add(toggle);
        }
    }
}
