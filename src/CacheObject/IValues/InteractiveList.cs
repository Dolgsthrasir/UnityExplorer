using System.Collections;
using UnityExplorer.CacheObject.Views;
using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveList : InteractiveValue, ICellPoolDataSource<CacheListEntryCell>, ICacheObjectController
    {
        CacheObjectBase ICacheObjectController.ParentCacheObject => this.CurrentOwner;
        object ICacheObjectController.Target => this.CurrentOwner.Value;
        public Type TargetType { get; private set; }

        public override bool CanWrite => base.CanWrite && ((this.RefIList != null && !this.RefIList.IsReadOnly) || this.IsWritableGenericIList);

        public Type EntryType;
        public IList RefIList;

        private bool IsWritableGenericIList;
        private PropertyInfo genericIndexer;

        public int ItemCount => this.cachedEntries.Count;
        private readonly List<CacheListEntry> cachedEntries = new();

        public ScrollPool<CacheListEntryCell> ListScrollPool { get; private set; }

        public Text TopLabel;
        private LayoutElement scrollLayout;
        private Text NotSupportedLabel;

        public override void OnBorrowed(CacheObjectBase owner)
        {
            base.OnBorrowed(owner);

            this.ListScrollPool.Refresh(true, true);
        }

        public override void ReleaseFromOwner()
        {
            base.ReleaseFromOwner();

            this.ClearAndRelease();
        }

        private void ClearAndRelease()
        {
            this.RefIList = null;

            foreach (CacheListEntry entry in this.cachedEntries)
            {
                entry.UnlinkFromView();
                entry.ReleasePooledObjects();
            }

            this.cachedEntries.Clear();
        }

        // List entry scroll pool

        public override void SetLayout()
        {
            float minHeight = 5f;

            foreach (CacheListEntryCell cell in this.ListScrollPool.CellPool)
            {
                if (cell.Enabled)
                    minHeight += cell.Rect.rect.height;
            }

            this.scrollLayout.minHeight = Math.Min(InspectorPanel.CurrentPanelHeight - 400f, minHeight);
        }

        public void OnCellBorrowed(CacheListEntryCell cell) { } // not needed

        public void SetCell(CacheListEntryCell cell, int index)
        {
            CacheObjectControllerHelper.SetCell(cell, index, this.cachedEntries, null);
        }

        // Setting the List value itself to this model
        public override void SetValue(object value)
        {
            if (value == null)
            {
                // should never be null
                if (this.cachedEntries.Any()) this.ClearAndRelease();
            }
            else
            {
                Type type = value.GetActualType();
                ReflectionUtility.TryGetEntryType(type, out this.EntryType);

                this.CacheEntries(value);

                this.TopLabel.text = $"[{this.cachedEntries.Count}] {SignatureHighlighter.Parse(type, false)}";
            }

            //this.ScrollPoolLayout.minHeight = Math.Min(400f, 35f * values.Count);
            this.ListScrollPool.Refresh(true, false);
        }

        private void CacheEntries(object value)
        {
            this.RefIList = value as IList;

            // Check if the type implements IList<T> but not IList (ie. Il2CppArrayBase)
            if (this.RefIList == null)
                this.CheckGenericIList(value);
            else
                this.IsWritableGenericIList = false;

            int idx = 0;

            if (ReflectionUtility.TryGetEnumerator(value, out IEnumerator enumerator))
            {
                this.NotSupportedLabel.gameObject.SetActive(false);

                while (enumerator.MoveNext())
                {
                    object entry = enumerator.Current;

                    // If list count increased, create new cache entries
                    CacheListEntry cache;
                    if (idx >= this.cachedEntries.Count)
                    {
                        cache = new CacheListEntry();
                        cache.SetListOwner(this, idx);
                        this.cachedEntries.Add(cache);
                    }
                    else
                        cache = this.cachedEntries[idx];

                    cache.SetFallbackType(this.EntryType);
                    cache.SetValueFromSource(entry);
                    idx++;
                }

                // Remove excess cached entries if list count decreased
                if (this.cachedEntries.Count > idx)
                {
                    for (int i = this.cachedEntries.Count - 1; i >= idx; i--)
                    {
                        CacheListEntry cache = this.cachedEntries[i];
                        if (cache.CellView != null)
                            cache.UnlinkFromView();

                        cache.ReleasePooledObjects();
                        this.cachedEntries.RemoveAt(i);
                    }
                }
            }
            else
            {
                this.NotSupportedLabel.gameObject.SetActive(true);
            }
        }

        private void CheckGenericIList(object value)
        {
            try
            {
                Type type = value.GetType();
                if (type.GetInterfaces().Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IList<>)))
                    this.IsWritableGenericIList = !(bool)type.GetProperty("IsReadOnly").GetValue(value, null);
                else
                    this.IsWritableGenericIList = false;

                if (this.IsWritableGenericIList)
                {
                    // Find the "this[int index]" property.
                    // It might be a private implementation.
                    foreach (PropertyInfo prop in type.GetProperties(ReflectionUtility.FLAGS))
                    {
                        if ((prop.Name == "Item"
                                || (prop.Name.StartsWith("System.Collections.Generic.IList<") && prop.Name.EndsWith(">.Item")))
                            && prop.GetIndexParameters() is ParameterInfo[] parameters
                            && parameters.Length == 1
                            && parameters[0].ParameterType == typeof(int))
                        {
                            this.genericIndexer = prop;
                            break;
                        }
                    }

                    if (this.genericIndexer == null)
                    {
                        ExplorerCore.LogWarning($"Failed to find indexer property for IList<T> type '{type.FullName}'!");
                        this.IsWritableGenericIList = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception processing IEnumerable for IList<T> check: {ex.ReflectionExToString()}");
                this.IsWritableGenericIList = false;
            }
        }

        // Setting the value of an index to the list

        public void TrySetValueToIndex(object value, int index)
        {
            try
            {
                if (!this.IsWritableGenericIList)
                {
                    this.RefIList[index] = value;
                }
                else
                {
                    this.genericIndexer.SetValue(this.CurrentOwner.Value, value, new object[] { index });
                }

                CacheListEntry entry = this.cachedEntries[index];
                entry.SetValueFromSource(value);

                if (entry.CellView != null)
                    entry.SetDataToCell(entry.CellView);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception setting IList value: {ex}");
            }
        }

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveList", true, true, true, true, 6, new Vector4(10, 3, 15, 4),
                new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(this.UIRoot, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 600);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Entries label

            this.TopLabel = UIFactory.CreateLabel(this.UIRoot, "EntryLabel", "not set", TextAnchor.MiddleLeft, fontSize: 16);
            this.TopLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // entry scroll pool

            this.ListScrollPool = UIFactory.CreateScrollPool<CacheListEntryCell>(this.UIRoot, "EntryList", out GameObject scrollObj,
                out GameObject _, new Color(0.09f, 0.09f, 0.09f));
            UIFactory.SetLayoutElement(scrollObj, minHeight: 400, flexibleHeight: 0);
            this.ListScrollPool.Initialize(this, this.SetLayout);
            this.scrollLayout = scrollObj.GetComponent<LayoutElement>();

            this.NotSupportedLabel = UIFactory.CreateLabel(this.ListScrollPool.Content.gameObject, "NotSupportedMessage",
                "The IEnumerable failed to enumerate. This is likely due to an issue with Unhollowed interfaces.",
                TextAnchor.MiddleLeft, Color.red);

            UIFactory.SetLayoutElement(this.NotSupportedLabel.gameObject, minHeight: 25, flexibleWidth: 9999);
            this.NotSupportedLabel.gameObject.SetActive(false);

            return this.UIRoot;
        }
    }
}