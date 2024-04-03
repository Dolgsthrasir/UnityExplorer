using UnityExplorer.Inspectors;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.UI.Widgets
{
    public class UnityObjectWidget : IPooledObject
    {
        public UnityEngine.Object unityObject;
        public Component component;
        public ReflectionInspector owner;

        protected ButtonRef gameObjectButton;
        protected InputFieldRef nameInput;
        protected InputFieldRef instanceIdInput;

        // IPooledObject
        public GameObject UIRoot { get; set; }
        public float DefaultHeight => -1;

        public static UnityObjectWidget GetUnityWidget(object target, Type targetType, ReflectionInspector inspector)
        {
            if (!typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                return null;

            UnityObjectWidget widget = target switch
            {
                Texture2D or Cubemap => Pool<Texture2DWidget>.Borrow(),
                Sprite s when s.texture => Pool<Texture2DWidget>.Borrow(),
                Image i when i.sprite?.texture => Pool<Texture2DWidget>.Borrow(),

                Material when MaterialWidget.MaterialWidgetSupported => Pool<MaterialWidget>.Borrow(),

                AudioClip => Pool<AudioClipWidget>.Borrow(),

                _ => Pool<UnityObjectWidget>.Borrow()
            };

            widget.OnBorrowed(target, targetType, inspector);

            return widget;
        }

        public virtual void OnBorrowed(object target, Type targetType, ReflectionInspector inspector)
        {
            this.owner = inspector;

            if (!this.UIRoot)
                this.CreateContent(inspector.UIRoot);
            else
                this.UIRoot.transform.SetParent(inspector.UIRoot.transform);

            this.UIRoot.transform.SetSiblingIndex(inspector.UIRoot.transform.childCount - 2);

            this.unityObject = target.TryCast<UnityEngine.Object>();
            this.UIRoot.SetActive(true);

            this.nameInput.Text = this.unityObject.name;
            this.instanceIdInput.Text = this.unityObject.GetInstanceID().ToString();

            if (typeof(Component).IsAssignableFrom(targetType))
            {
                this.component = (Component)target.TryCast(typeof(Component));
                this.gameObjectButton.Component.gameObject.SetActive(true);
            }
            else
                this.gameObjectButton.Component.gameObject.SetActive(false);
        }

        public virtual void OnReturnToPool()
        {
            this.unityObject = null;
            this.component = null;
            this.owner = null;
        }

        // Update

        public virtual void Update()
        {
            if (this.unityObject)
            {
                this.nameInput.Text = this.unityObject.name;

                this.owner.Tab.TabText.text = $"{this.owner.TabButtonText} \"{this.unityObject.name}\"";
            }
        }

        // UI Listeners

        private void OnGameObjectButtonClicked()
        {
            if (!this.component)
            {
                ExplorerCore.LogWarning("Component reference is null or destroyed!");
                return;
            }

            InspectorManager.Inspect(this.component.gameObject);
        }

        // UI construction

        public virtual GameObject CreateContent(GameObject uiRoot)
        {
            this.UIRoot = UIFactory.CreateUIObject("UnityObjectRow", uiRoot);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(this.UIRoot, false, false, true, true, 5);
            UIFactory.SetLayoutElement(this.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

            Text nameLabel = UIFactory.CreateLabel(this.UIRoot, "NameLabel", "Name:", TextAnchor.MiddleLeft, Color.grey);
            UIFactory.SetLayoutElement(nameLabel.gameObject, minHeight: 25, minWidth: 45, flexibleWidth: 0);

            this.nameInput = UIFactory.CreateInputField(this.UIRoot, "NameInput", "untitled");
            UIFactory.SetLayoutElement(this.nameInput.UIRoot, minHeight: 25, minWidth: 100, flexibleWidth: 1000);
            this.nameInput.Component.readOnly = true;

            this.gameObjectButton = UIFactory.CreateButton(this.UIRoot, "GameObjectButton", "Inspect GameObject", new Color(0.2f, 0.2f, 0.2f));
            UIFactory.SetLayoutElement(this.gameObjectButton.Component.gameObject, minHeight: 25, minWidth: 160);
            this.gameObjectButton.OnClick += this.OnGameObjectButtonClicked;

            Text instanceLabel = UIFactory.CreateLabel(this.UIRoot, "InstanceLabel", "Instance ID:", TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(instanceLabel.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 0);

            this.instanceIdInput = UIFactory.CreateInputField(this.UIRoot, "InstanceIDInput", "ERROR");
            UIFactory.SetLayoutElement(this.instanceIdInput.UIRoot, minHeight: 25, minWidth: 100, flexibleWidth: 0);
            this.instanceIdInput.Component.readOnly = true;

            this.UIRoot.SetActive(false);

            return this.UIRoot;
        }
    }
}
