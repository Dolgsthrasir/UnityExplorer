using UnityExplorer.Inspectors;

namespace UnityExplorer.UI.Widgets
{
    // The base wrapper to hold a reference to the parent Inspector and the GameObjectInfo and TransformControls widgets.

    public class GameObjectControls
    {
        public GameObjectInspector Parent { get; }
        public GameObject Target => this.Parent.Target;

        public GameObjectInfoPanel GameObjectInfo { get; }

        public TransformControls TransformControl { get; }

        public GameObjectControls(GameObjectInspector parent)
        {
            this.Parent = parent;

            this.GameObjectInfo = new(this);
            this.TransformControl = new(this);
        }

        public void UpdateGameObjectInfo(bool firstUpdate, bool force)
        {
            this.GameObjectInfo.UpdateGameObjectInfo(firstUpdate, force);
        }

        public void UpdateVectorSlider()
        {
            this.TransformControl.UpdateVectorSlider();
        }
    }
}
