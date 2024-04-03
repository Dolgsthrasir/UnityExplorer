using UnityExplorer.Config;
using UnityExplorer.Inspectors.MouseInspectors;
using UnityExplorer.UI;
using UnityExplorer.UI.Panels;
using UniverseLib.Input;
using UniverseLib.UI;
using UniverseLib.UI.Panels;

namespace UnityExplorer.Inspectors
{
    public enum MouseInspectMode
    {
        World,
        UI
    }

    public class MouseInspector : PanelBase
    {
        public static MouseInspector Instance { get; private set; }

        private readonly WorldInspector worldInspector;
        private readonly UiInspector uiInspector;

        public static bool Inspecting { get; set; }
        public static MouseInspectMode Mode { get; set; }

        public MouseInspectorBase CurrentInspector => Mode switch
        {
            MouseInspectMode.UI => this.uiInspector,
            MouseInspectMode.World => this.worldInspector,
            _ => null,
        };

        private static Vector3 lastMousePos;

        // UIPanel
        internal static readonly string UIBaseGUID = $"{ExplorerCore.GUID}.MouseInspector";
        internal static UIBase inspectorUIBase;

        public override string Name => "Inspect Under Mouse";
        public override int MinWidth => -1;
        public override int MinHeight => -1;
        public override Vector2 DefaultAnchorMin => Vector2.zero;
        public override Vector2 DefaultAnchorMax => Vector2.zero;

        public override bool CanDragAndResize => false;

        internal Text objNameLabel;
        internal Text objPathLabel;
        internal Text mousePosLabel;

        public MouseInspector(UIBase owner) : base(owner)
        {
            Instance = this;
            this.worldInspector = new WorldInspector();
            this.uiInspector = new UiInspector();
        }

        public static void OnDropdownSelect(int index)
        {
            switch (index)
            {
                case 0: return;
                case 1: Instance.StartInspect(MouseInspectMode.World); break;
                case 2: Instance.StartInspect(MouseInspectMode.UI); break;
            }
            InspectorPanel.Instance.MouseInspectDropdown.value = 0;
        }

        public void StartInspect(MouseInspectMode mode)
        {
            Mode = mode;
            Inspecting = true;

            this.CurrentInspector.OnBeginMouseInspect();

            PanelManager.ForceEndResize();
            UIManager.NavBarRect.gameObject.SetActive(false);
            UIManager.UiBase.Panels.PanelHolder.SetActive(false);
            UIManager.UiBase.SetOnTop();

            this.SetActive(true);
        }

        internal void ClearHitData()
        {
            this.CurrentInspector.ClearHitData();

            this.objNameLabel.text = "No hits...";
            this.objPathLabel.text = "";
        }

        public void StopInspect()
        {
            this.CurrentInspector.OnEndInspect();
            this.ClearHitData();
            Inspecting = false;

            UIManager.NavBarRect.gameObject.SetActive(true);
            UIManager.UiBase.Panels.PanelHolder.SetActive(true);

            Dropdown drop = InspectorPanel.Instance.MouseInspectDropdown;
            if (drop.transform.Find("Dropdown List") is Transform list)
                drop.DestroyDropdownList(list.gameObject);

            this.UIRoot.SetActive(false);
        }

        private static float timeOfLastRaycast;

        public bool TryUpdate()
        {
            if (InputManager.GetKeyDown(ConfigManager.World_MouseInspect_Keybind.Value))
                Instance.StartInspect(MouseInspectMode.World);

            if (InputManager.GetKeyDown(ConfigManager.UI_MouseInspect_Keybind.Value))
                Instance.StartInspect(MouseInspectMode.UI);

            if (Inspecting) this.UpdateInspect();

            return Inspecting;
        }

        public void UpdateInspect()
        {
            if (InputManager.GetKeyDown(KeyCode.Escape))
            {
                this.StopInspect();
                return;
            }

            if (InputManager.GetMouseButtonDown(0))
            {
                this.CurrentInspector.OnSelectMouseInspect();
                this.StopInspect();
                return;
            }

            Vector3 mousePos = InputManager.MousePosition;
            if (mousePos != lastMousePos) this.UpdatePosition(mousePos);

            if (!timeOfLastRaycast.OccuredEarlierThan(0.1f))
                return;
            timeOfLastRaycast = Time.realtimeSinceStartup;

            this.CurrentInspector.UpdateMouseInspect(mousePos);
        }

        internal void UpdatePosition(Vector2 mousePos)
        {
            lastMousePos = mousePos;

            // use the raw mouse pos for the label
            this.mousePosLabel.text = $"<color=grey>Mouse Position:</color> {mousePos.ToString()}";

            // constrain the mouse pos we use within certain bounds
            if (mousePos.x < 350)
                mousePos.x = 350;
            if (mousePos.x > Screen.width - 350)
                mousePos.x = Screen.width - 350;
            if (mousePos.y < this.Rect.rect.height)
                mousePos.y += this.Rect.rect.height + 10;
            else
                mousePos.y -= 10;

            // calculate and set our UI position
            Vector3 inversePos = inspectorUIBase.RootObject.transform.InverseTransformPoint(mousePos);
            this.UIRoot.transform.localPosition = new Vector3(inversePos.x, inversePos.y, 0);
        }

        // UI Construction

        public override void SetDefaultSizeAndPosition()
        {
            base.SetDefaultSizeAndPosition();

            this.Rect.anchorMin = Vector2.zero;
            this.Rect.anchorMax = Vector2.zero;
            this.Rect.pivot = new Vector2(0.5f, 1);
            this.Rect.sizeDelta = new Vector2(700, 150);
        }

        protected override void ConstructPanelContent()
        {
            // hide title bar
            this.TitleBar.SetActive(false);
            this.UIRoot.transform.SetParent(UIManager.UIRoot.transform, false);

            GameObject inspectContent = UIFactory.CreateVerticalGroup(this.ContentRoot, "InspectContent", true, true, true, true, 3, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(inspectContent, flexibleWidth: 9999, flexibleHeight: 9999);

            // Title text

            Text title = UIFactory.CreateLabel(inspectContent,
                "InspectLabel",
                "<b>Mouse Inspector</b> (press <b>ESC</b> to cancel)",
                TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(title.gameObject, flexibleWidth: 9999);

            this.mousePosLabel = UIFactory.CreateLabel(inspectContent, "MousePosLabel", "Mouse Position:", TextAnchor.MiddleCenter);

            this.objNameLabel = UIFactory.CreateLabel(inspectContent, "HitLabelObj", "No hits...", TextAnchor.MiddleLeft);
            this.objNameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            this.objPathLabel = UIFactory.CreateLabel(inspectContent, "PathLabel", "", TextAnchor.MiddleLeft);
            this.objPathLabel.fontStyle = FontStyle.Italic;
            this.objPathLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            UIFactory.SetLayoutElement(this.objPathLabel.gameObject, minHeight: 75);

            this.UIRoot.SetActive(false);

            //// Create a new canvas for this panel to live on.
            //// It needs to always be shown on the main display, other panels can move displays.
            //
            //UIRoot.transform.SetParent(inspectorUIBase.RootObject.transform);
        }
    }
}
