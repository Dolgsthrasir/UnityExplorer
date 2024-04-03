using System.Collections;
using UnityExplorer.CacheObject.Views;
using UnityExplorer.UI.Panels;
using UniverseLib.UI;
using UniverseLib.UI.Widgets.ScrollView;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveDictionary : InteractiveValue, ICellPoolDataSource<CacheKeyValuePairCell>, ICacheObjectController
    {
        CacheObjectBase ICacheObjectController.ParentCacheObject => this.CurrentOwner;
        object ICacheObjectController.Target => this.CurrentOwner.Value;
        public Type TargetType { get; private set; }

        public override bool CanWrite => base.CanWrite && this.RefIDictionary != null && !this.RefIDictionary.IsReadOnly;

        public Type KeysType;
        public Type ValuesType;
        public IDictionary RefIDictionary;

        public int ItemCount => this.cachedEntries.Count;
        private readonly List<CacheKeyValuePair> cachedEntries = new();

        public ScrollPool<CacheKeyValuePairCell> DictScrollPool { get; private set; }

        private Text NotSupportedLabel;

        public Text TopLabel;

        public LayoutElement KeyTitleLayout;
        public LayoutElement ValueTitleLayout;

        public override void OnBorrowed(CacheObjectBase owner)
        {
            base.OnBorrowed(owner);

            this.DictScrollPool.Refresh(true, true);
        }

        public override void ReleaseFromOwner()
        {
            base.ReleaseFromOwner();

            this.ClearAndRelease();
        }

        private void ClearAndRelease()
        {
            this.RefIDictionary = null;

            foreach (CacheKeyValuePair entry in this.cachedEntries)
            {
                entry.UnlinkFromView();
                entry.ReleasePooledObjects();
            }

            this.cachedEntries.Clear();
        }

        public override void SetValue(object value)
        {
            if (value == null)
            {
                // should never be null
                this.ClearAndRelease();
                return;
            }
            else
            {
                Type type = value.GetActualType();
                ReflectionUtility.TryGetEntryTypes(type, out this.KeysType, out this.ValuesType);

                this.CacheEntries(value);

                this.TopLabel.text = $"[{this.cachedEntries.Count}] {SignatureHighlighter.Parse(type, false)}";
            }

            this.DictScrollPool.Refresh(true, false);
        }

        private void CacheEntries(object value)
        {
            this.RefIDictionary = value as IDictionary;

            if (ReflectionUtility.TryGetDictEnumerator(value, out IEnumerator<DictionaryEntry> dictEnumerator))
            {
                this.NotSupportedLabel.gameObject.SetActive(false);

                int idx = 0;
                while (dictEnumerator.MoveNext())
                {
                    CacheKeyValuePair cache;
                    if (idx >= this.cachedEntries.Count)
                    {
                        cache = new CacheKeyValuePair();
                        cache.SetDictOwner(this, idx);
                        this.cachedEntries.Add(cache);
                    }
                    else
                        cache = this.cachedEntries[idx];

                    cache.SetFallbackType(this.ValuesType);
                    cache.SetKey(dictEnumerator.Current.Key);
                    cache.SetValueFromSource(dictEnumerator.Current.Value);

                    idx++;
                }

                // Remove excess cached entries if dict count decreased
                if (this.cachedEntries.Count > idx)
                {
                    for (int i = this.cachedEntries.Count - 1; i >= idx; i--)
                    {
                        CacheKeyValuePair cache = this.cachedEntries[i];
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

        // Setting value to dictionary

        public void TrySetValueToKey(object key, object value, int keyIndex)
        {
            try
            {
                if (!this.RefIDictionary.Contains(key))
                {
                    ExplorerCore.LogWarning("Unable to set key! Key may have been boxed to/from Il2Cpp Object.");
                    return;
                }

                this.RefIDictionary[key] = value;

                CacheKeyValuePair entry = this.cachedEntries[keyIndex];
                entry.SetValueFromSource(value);
                if (entry.CellView != null)
                    entry.SetDataToCell(entry.CellView);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning($"Exception setting IDictionary key! {ex}");
            }
        }

        // KVP entry scroll pool

        public void OnCellBorrowed(CacheKeyValuePairCell cell) { }

        public void SetCell(CacheKeyValuePairCell cell, int index)
        {
            CacheObjectControllerHelper.SetCell(cell, index, this.cachedEntries, this.SetCellLayout);
        }

        public int AdjustedWidth => (int)this.UIRect.rect.width - 80;

        public override void SetLayout()
        {
            float minHeight = 5f;

            this.KeyTitleLayout.minWidth = this.AdjustedWidth * 0.44f;
            this.ValueTitleLayout.minWidth = this.AdjustedWidth * 0.55f;

            foreach (CacheKeyValuePairCell cell in this.DictScrollPool.CellPool)
            {
                this.SetCellLayout(cell);
                if (cell.Enabled)
                    minHeight += cell.Rect.rect.height;
            }

            this.scrollLayout.minHeight = Math.Min(InspectorPanel.CurrentPanelHeight - 400f, minHeight);
        }

        private void SetCellLayout(CacheObjectCell objcell)
        {
            CacheKeyValuePairCell cell = objcell as CacheKeyValuePairCell;
            cell.KeyGroupLayout.minWidth = cell.AdjustedWidth * 0.44f;
            cell.RightGroupLayout.minWidth = cell.AdjustedWidth * 0.55f;

            if (cell.Occupant?.IValue != null)
                cell.Occupant.IValue.SetLayout();
        }

        private LayoutElement scrollLayout;
        private RectTransform UIRect;

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveDict", true, true, true, true, 6, new Vector4(10, 3, 15, 4),
                new Color(0.05f, 0.05f, 0.05f));
            UIFactory.SetLayoutElement(this.UIRoot, flexibleWidth: 9999, minHeight: 25, flexibleHeight: 475);
            this.UIRoot.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.UIRect = this.UIRoot.GetComponent<RectTransform>();

            // Entries label

            this.TopLabel = UIFactory.CreateLabel(this.UIRoot, "EntryLabel", "not set", TextAnchor.MiddleLeft, fontSize: 16);
            this.TopLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // key / value titles

            GameObject titleGroup = UIFactory.CreateUIObject("TitleGroup", this.UIRoot);
            UIFactory.SetLayoutElement(titleGroup, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(titleGroup, false, true, true, true, padLeft: 65, padRight: 0, childAlignment: TextAnchor.LowerLeft);

            Text keyTitle = UIFactory.CreateLabel(titleGroup, "KeyTitle", "Keys", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(keyTitle.gameObject, minWidth: 100, flexibleWidth: 0);
            this.KeyTitleLayout = keyTitle.GetComponent<LayoutElement>();

            Text valueTitle = UIFactory.CreateLabel(titleGroup, "ValueTitle", "Values", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(valueTitle.gameObject, minWidth: 100, flexibleWidth: 0);
            this.ValueTitleLayout = valueTitle.GetComponent<LayoutElement>();

            // entry scroll pool

            this.DictScrollPool = UIFactory.CreateScrollPool<CacheKeyValuePairCell>(this.UIRoot, "EntryList", out GameObject scrollObj,
                out GameObject _, new Color(0.09f, 0.09f, 0.09f));
            UIFactory.SetLayoutElement(scrollObj, minHeight: 150, flexibleHeight: 0);
            this.DictScrollPool.Initialize(this, this.SetLayout);
            this.scrollLayout = scrollObj.GetComponent<LayoutElement>();

            this.NotSupportedLabel = UIFactory.CreateLabel(this.DictScrollPool.Content.gameObject, "NotSupportedMessage",
                "The IDictionary failed to enumerate. This is likely due to an issue with Unhollowed interfaces.",
                TextAnchor.MiddleLeft, Color.red);

            UIFactory.SetLayoutElement(this.NotSupportedLabel.gameObject, minHeight: 25, flexibleWidth: 9999);
            this.NotSupportedLabel.gameObject.SetActive(false);

            return this.UIRoot;
        }
    }
}