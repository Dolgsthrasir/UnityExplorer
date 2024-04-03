using UnityExplorer.CacheObject.IValues;
using UnityExplorer.CacheObject.Views;

namespace UnityExplorer.CacheObject
{
    public class CacheListEntry : CacheObjectBase
    {
        public int ListIndex;

        public override bool ShouldAutoEvaluate => true;
        public override bool HasArguments => false;
        public override bool CanWrite => this.Owner?.CanWrite ?? false;

        public void SetListOwner(InteractiveList list, int listIndex)
        {
            this.Owner = list;
            this.ListIndex = listIndex;
        }

        public override void SetDataToCell(CacheObjectCell cell)
        {
            base.SetDataToCell(cell);

            CacheListEntryCell listCell = cell as CacheListEntryCell;

            listCell.NameLabel.text = $"{this.ListIndex}:";
            listCell.HiddenNameLabel.Text = "";
            listCell.Image.color = this.ListIndex % 2 == 0 ? CacheListEntryCell.EvenColor : CacheListEntryCell.OddColor;
        }

        public override void TrySetUserValue(object value)
        {
            (this.Owner as InteractiveList).TrySetValueToIndex(value, this.ListIndex);
        }

        protected override bool TryAutoEvaluateIfUnitialized(CacheObjectCell cell) => true;
    }
}
