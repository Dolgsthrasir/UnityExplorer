using UnityExplorer.UI.Panels;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.Inspectors
{
    public abstract class InspectorBase : IPooledObject
    {
        public bool IsActive { get; internal set; }
        public object Target { get; set; }
        public Type TargetType { get; protected set; }

        public InspectorTab Tab { get; internal set; }

        public GameObject UIRoot { get; set; }

        public float DefaultHeight => -1f;
        public abstract GameObject CreateContent(GameObject parent);

        public abstract void Update();

        public abstract void CloseInspector();

        public virtual void OnBorrowedFromPool(object target)
        {
            this.Target = target;
            this.TargetType = target is Type type ? type : target.GetActualType();

            this.Tab = Pool<InspectorTab>.Borrow();
            this.Tab.UIRoot.transform.SetParent(InspectorPanel.Instance.NavbarHolder.transform, false);

            this.Tab.TabButton.OnClick += this.OnTabButtonClicked;
            this.Tab.CloseButton.OnClick += this.CloseInspector;
        }

        public virtual void OnReturnToPool()
        {
            Pool<InspectorTab>.Return(this.Tab);

            this.Target = null;

            this.Tab.TabButton.OnClick -= this.OnTabButtonClicked;
            this.Tab.CloseButton.OnClick -= this.CloseInspector;
        }

        public virtual void OnSetActive()
        {
            this.Tab.SetTabColor(true);
            this.UIRoot.SetActive(true);
            this.IsActive = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(this.UIRoot.GetComponent<RectTransform>());
        }

        public virtual void OnSetInactive()
        {
            this.Tab.SetTabColor(false);
            this.UIRoot.SetActive(false);
            this.IsActive = false;
        }

        private void OnTabButtonClicked()
        {
            InspectorManager.SetInspectorActive(this);
        }
    }
}
