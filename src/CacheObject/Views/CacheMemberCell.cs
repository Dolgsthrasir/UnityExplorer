using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.CacheObject.Views
{
    public class CacheMemberCell : CacheObjectCell
    {
        public CacheMember MemberOccupant => this.Occupant as CacheMember;

        public GameObject EvaluateHolder;
        public ButtonRef EvaluateButton;

        protected virtual void EvaluateClicked()
        {
            this.MemberOccupant.OnEvaluateClicked();
        }

        protected override void ConstructEvaluateHolder(GameObject parent)
        {
            // Evaluate vert group

            this.EvaluateHolder = UIFactory.CreateUIObject("EvalGroup", parent);
            UIFactory.SetLayoutGroup<VerticalLayoutGroup>(this.EvaluateHolder, false, false, true, true, 3);
            UIFactory.SetLayoutElement(this.EvaluateHolder, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 775);

            this.EvaluateButton = UIFactory.CreateButton(this.EvaluateHolder, "EvaluateButton", "Evaluate", new Color(0.15f, 0.15f, 0.15f));
            UIFactory.SetLayoutElement(this.EvaluateButton.Component.gameObject, minWidth: 100, minHeight: 25);
            this.EvaluateButton.OnClick += this.EvaluateClicked;
        }
    }
}
