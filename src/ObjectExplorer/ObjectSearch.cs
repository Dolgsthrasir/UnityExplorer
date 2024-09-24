using UnityExplorer.UI.Panels;
using UnityExplorer.UI.Widgets.AutoComplete;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets.ButtonList;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.ObjectExplorer
{
    public class ObjectSearch : UIModel
    {
        public class SearchedObject
        {
            public SearchedObject(object obj, string desc = null)
            {
                this.Object = obj;
                this.Description = string.IsNullOrEmpty(desc) ? string.Empty : desc;
            }
            public object Object;
            public string Description;

            public override string ToString()
            {
                if(string.IsNullOrEmpty(this.Description))
                    return $"{this.Object}";
                
                return $"{this.Object} => {this.Description}";
            }
        }
        
        public ObjectExplorerPanel Parent { get; }

        public ObjectSearch(ObjectExplorerPanel parent)
        {
            this.Parent = parent;
        }

        private SearchContext context = SearchContext.UnityObject;
        private SceneFilter sceneFilter = SceneFilter.Any;
        private ChildFilter childFilter = ChildFilter.Any;
        private string desiredTypeInput;
        private string lastCheckedTypeInput;
        private bool lastTypeCanHaveGameObject;

        public ButtonListHandler<SearchedObject, ButtonCell> dataHandler;
        private ScrollPool<ButtonCell> resultsScrollPool;
        private List<SearchedObject> currentResults = new();

        //public TypeCompleter typeAutocompleter;
        public TypeCompleter unityObjectTypeCompleter;
        public TypeCompleter allTypesCompleter;

        public override GameObject UIRoot => this.uiRoot;
        private GameObject uiRoot;
        private GameObject sceneFilterRow;
        private GameObject childFilterRow;
        private GameObject classInputRow;
        private GameObject nameInputRow;
        private InputFieldRef nameInputField;
        private Text resultsLabel;

        public List<SearchedObject> GetEntries() => this.currentResults;

        public void DoSearch()
        {
            cachedCellTexts.Clear();

            if (this.context == SearchContext.Singleton)
                this.currentResults = SearchProvider.InstanceSearch(this.desiredTypeInput).ToList();
            else if (this.context == SearchContext.Class)
                this.currentResults = SearchProvider.ClassSearch(this.desiredTypeInput);
            else if (this.context == SearchContext.Method)
            {
                this.currentResults = SearchProvider.MethodSearch(this.desiredTypeInput);
            }
            // else if (this.context == SearchContext.String)
            // {
                // this.currentResults = SearchProvider.StringSearch(this.desiredTypeInput);
            // }
            else
                this.currentResults = SearchProvider.UnityObjectSearch(this.nameInputField.Text, this.desiredTypeInput, this.childFilter, this.sceneFilter);

            this.dataHandler.RefreshData();
            this.resultsScrollPool.Refresh(true);

            this.resultsLabel.text = $"{this.currentResults.Count} results";
        }

        public void Update()
        {
            if (this.context == SearchContext.UnityObject && this.lastCheckedTypeInput != this.desiredTypeInput)
            {
                this.lastCheckedTypeInput = this.desiredTypeInput;

                //var type = ReflectionUtility.GetTypeByName(desiredTypeInput);
                if (ReflectionUtility.GetTypeByName(this.desiredTypeInput) is Type cachedType)
                {
                    Type type = cachedType;
                    this.lastTypeCanHaveGameObject = typeof(Component).IsAssignableFrom(type) || type == typeof(GameObject);
                    this.sceneFilterRow.SetActive(this.lastTypeCanHaveGameObject);
                    this.childFilterRow.SetActive(this.lastTypeCanHaveGameObject);
                }
                else
                {
                    this.sceneFilterRow.SetActive(false);
                    this.childFilterRow.SetActive(false);
                    this.lastTypeCanHaveGameObject = false;
                }
            }
        }

        // UI Callbacks

        private void OnContextDropdownChanged(int value)
        {
            this.context = (SearchContext)value;

            this.lastCheckedTypeInput = null;
            this.sceneFilterRow.SetActive(false);
            this.childFilterRow.SetActive(false);

            this.nameInputRow.SetActive(this.context == SearchContext.UnityObject);

            switch (this.context)
            {
                case SearchContext.UnityObject:
                    this.unityObjectTypeCompleter.Enabled = true;
                    this.allTypesCompleter.Enabled = false;
                    break;
                case SearchContext.Singleton:
                case SearchContext.Class:
                    this.allTypesCompleter.Enabled = true;
                    this.unityObjectTypeCompleter.Enabled = false;
                    break;
            }
        }

        private void OnSceneFilterDropChanged(int value) => this.sceneFilter = (SceneFilter)value;

        private void OnChildFilterDropChanged(int value) => this.childFilter = (ChildFilter)value;

        private void OnTypeInputChanged(string val)
        {
            this.desiredTypeInput = val;

            if (string.IsNullOrEmpty(val))
            {
                this.sceneFilterRow.SetActive(false);
                this.childFilterRow.SetActive(false);
                this.lastCheckedTypeInput = val;
            }
        }

        // Cache the syntax-highlighted text for each search result to reduce allocs.
        private static readonly Dictionary<int, string> cachedCellTexts = new();

        public void SetCell(ButtonCell cell, int index)
        {
            if (!cachedCellTexts.ContainsKey(index))
            {
                string text;
                if (this.context == SearchContext.Class || this.context == SearchContext.Method)
                {
                    Type type = this.currentResults[index].Object as Type;
                    if (string.IsNullOrEmpty(this.currentResults[index].Description))
                    {
                        text = $"{SignatureHighlighter.Parse(type, true)} <color=grey><i>({type.Assembly.GetName().Name})</i></color>";
                    }
                    else
                    {
                        text = $"{this.currentResults[index].Description} in {SignatureHighlighter.Parse(type, true)} <color=grey><i>({type.Assembly.GetName().Name})</i></color>";
                    }
                }
                // if (this.context == SearchContext.String)
                // {
                    // text = $"{this.currentResults[index].Description}";
                // }
                else
                    text = ToStringUtility.ToStringWithType(this.currentResults[index].Object, this.currentResults[index]?.Object.GetActualType());

                cachedCellTexts.Add(index, text);
            }

            cell.Button.ButtonText.text = cachedCellTexts[index];
        }

        private void OnCellClicked(int dataIndex)
        {
            if (this.context == SearchContext.Class || this.context == SearchContext.Method)
                InspectorManager.Inspect(this.currentResults[dataIndex].Object as Type);
            else
                InspectorManager.Inspect(this.currentResults[dataIndex].Object);
        }

        private bool ShouldDisplayCell(object arg1, string arg2) => true;

        public override void ConstructUI(GameObject parent)
        {
            this.uiRoot = UIFactory.CreateVerticalGroup(parent, "ObjectSearch", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.uiRoot, flexibleHeight: 9999);

            // Search context row

            GameObject contextGroup = UIFactory.CreateHorizontalGroup(this.uiRoot, "SearchContextRow", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(contextGroup, minHeight: 25, flexibleHeight: 0);

            Text contextLbl = UIFactory.CreateLabel(contextGroup, "SearchContextLabel", "Searching for:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(contextLbl.gameObject, minWidth: 110, flexibleWidth: 0);

            GameObject contextDropObj = UIFactory.CreateDropdown(contextGroup, "ContextDropdown", out Dropdown contextDrop, null, 14, this.OnContextDropdownChanged);
            foreach (string name in Enum.GetNames(typeof(SearchContext)))
                contextDrop.options.Add(new Dropdown.OptionData(name));
            UIFactory.SetLayoutElement(contextDropObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            // Class input

            this.classInputRow = UIFactory.CreateHorizontalGroup(this.uiRoot, "ClassRow", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.classInputRow, minHeight: 25, flexibleHeight: 0);

            Text unityClassLbl = UIFactory.CreateLabel(this.classInputRow, "ClassLabel", "Class filter:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(unityClassLbl.gameObject, minWidth: 110, flexibleWidth: 0);

            InputFieldRef classInputField = UIFactory.CreateInputField(this.classInputRow, "ClassInput", "...");
            UIFactory.SetLayoutElement(classInputField.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            this.unityObjectTypeCompleter = new(typeof(UnityEngine.Object), classInputField, true, false, true);
            this.allTypesCompleter = new(null, classInputField, true, false, true);
            this.allTypesCompleter.Enabled = false;
            classInputField.OnValueChanged += this.OnTypeInputChanged;

            //unityObjectClassRow.SetActive(false);

            // Child filter row

            this.childFilterRow = UIFactory.CreateHorizontalGroup(this.uiRoot, "ChildFilterRow", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.childFilterRow, minHeight: 25, flexibleHeight: 0);

            Text childLbl = UIFactory.CreateLabel(this.childFilterRow, "ChildLabel", "Child filter:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(childLbl.gameObject, minWidth: 110, flexibleWidth: 0);

            GameObject childDropObj = UIFactory.CreateDropdown(this.childFilterRow, "ChildFilterDropdown", out Dropdown childDrop, null, 14, this.OnChildFilterDropChanged);
            foreach (string name in Enum.GetNames(typeof(ChildFilter)))
                childDrop.options.Add(new Dropdown.OptionData(name));
            UIFactory.SetLayoutElement(childDropObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            this.childFilterRow.SetActive(false);

            // Scene filter row

            this.sceneFilterRow = UIFactory.CreateHorizontalGroup(this.uiRoot, "SceneFilterRow", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.sceneFilterRow, minHeight: 25, flexibleHeight: 0);

            Text sceneLbl = UIFactory.CreateLabel(this.sceneFilterRow, "SceneLabel", "Scene filter:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(sceneLbl.gameObject, minWidth: 110, flexibleWidth: 0);

            GameObject sceneDropObj = UIFactory.CreateDropdown(this.sceneFilterRow, "SceneFilterDropdown", out Dropdown sceneDrop, null, 14, this.OnSceneFilterDropChanged);
            foreach (string name in Enum.GetNames(typeof(SceneFilter)))
            {
                if (!SceneHandler.DontDestroyExists && name == "DontDestroyOnLoad")
                    continue;
                sceneDrop.options.Add(new Dropdown.OptionData(name));
            }
            UIFactory.SetLayoutElement(sceneDropObj, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            this.sceneFilterRow.SetActive(false);

            // Name filter input

            this.nameInputRow = UIFactory.CreateHorizontalGroup(this.uiRoot, "NameRow", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(this.nameInputRow, minHeight: 25, flexibleHeight: 0);

            Text nameLbl = UIFactory.CreateLabel(this.nameInputRow, "NameFilterLabel", "Name contains:", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(nameLbl.gameObject, minWidth: 110, flexibleWidth: 0);

            this.nameInputField = UIFactory.CreateInputField(this.nameInputRow, "NameFilterInput", "...");
            UIFactory.SetLayoutElement(this.nameInputField.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            // Search button

            ButtonRef searchButton = UIFactory.CreateButton(this.uiRoot, "SearchButton", "Search");
            UIFactory.SetLayoutElement(searchButton.Component.gameObject, minHeight: 25, flexibleHeight: 0);
            searchButton.OnClick += this.DoSearch;

            // Results count label

            GameObject resultsCountRow = UIFactory.CreateHorizontalGroup(this.uiRoot, "ResultsCountRow", true, true, true, true);
            UIFactory.SetLayoutElement(resultsCountRow, minHeight: 25, flexibleHeight: 0);

            this.resultsLabel = UIFactory.CreateLabel(resultsCountRow, "ResultsLabel", "0 results", TextAnchor.MiddleCenter);

            // RESULTS SCROLL POOL

            this.dataHandler = new ButtonListHandler<SearchedObject, ButtonCell>(this.resultsScrollPool, this.GetEntries, this.SetCell, this.ShouldDisplayCell, this.OnCellClicked);
            this.resultsScrollPool = UIFactory.CreateScrollPool<ButtonCell>(this.uiRoot, "ResultsList", out GameObject scrollObj,
                out GameObject scrollContent);

            this.resultsScrollPool.Initialize(this.dataHandler);
            UIFactory.SetLayoutElement(scrollObj, flexibleHeight: 9999);
        }
    }
}
