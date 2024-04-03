using UnityExplorer.CacheObject.IValues;
using UnityExplorer.CacheObject.Views;

namespace UnityExplorer.CacheObject
{
    public class CacheKeyValuePair : CacheObjectBase
    {
        //public InteractiveList CurrentList { get; set; }

        public int DictIndex;
        public object DictKey;
        public object DisplayedKey;

        public bool KeyInputWanted;
        public bool InspectWanted;
        public string KeyLabelText;
        public string KeyInputText;
        public string KeyInputTypeText;

        public float DesiredKeyWidth;
        public float DesiredValueWidth;

        public override bool ShouldAutoEvaluate => true;
        public override bool HasArguments => false;
        public override bool CanWrite => this.Owner.CanWrite;

        public void SetDictOwner(InteractiveDictionary dict, int index)
        {
            this.Owner = dict;
            this.DictIndex = index;
        }

        public void SetKey(object key)
        {
            this.DictKey = key;
            this.DisplayedKey = key.TryCast();

            Type type = this.DisplayedKey.GetType();
            if (ParseUtility.CanParse(type))
            {
                this.KeyInputWanted = true;
                this.KeyInputText = ParseUtility.ToStringForInput(this.DisplayedKey, type);
                this.KeyInputTypeText = SignatureHighlighter.Parse(type, false);
            }
            else
            {
                this.KeyInputWanted = false;
                this.InspectWanted = type != typeof(bool) && !type.IsEnum;
                this.KeyLabelText = ToStringUtility.ToStringWithType(this.DisplayedKey, type, true);
            }
        }

        public override void SetDataToCell(CacheObjectCell cell)
        {
            base.SetDataToCell(cell);

            CacheKeyValuePairCell kvpCell = cell as CacheKeyValuePairCell;

            kvpCell.NameLabel.text = $"{this.DictIndex}:";
            kvpCell.HiddenNameLabel.Text = "";
            kvpCell.Image.color = this.DictIndex % 2 == 0 ? CacheListEntryCell.EvenColor : CacheListEntryCell.OddColor;

            if (this.KeyInputWanted)
            {
                kvpCell.KeyInputField.UIRoot.SetActive(true);
                kvpCell.KeyInputTypeLabel.gameObject.SetActive(true);
                kvpCell.KeyLabel.gameObject.SetActive(false);
                kvpCell.KeyInspectButton.Component.gameObject.SetActive(false);

                kvpCell.KeyInputField.Text = this.KeyInputText;
                kvpCell.KeyInputTypeLabel.text = this.KeyInputTypeText;
            }
            else
            {
                kvpCell.KeyInputField.UIRoot.SetActive(false);
                kvpCell.KeyInputTypeLabel.gameObject.SetActive(false);
                kvpCell.KeyLabel.gameObject.SetActive(true);
                kvpCell.KeyInspectButton.Component.gameObject.SetActive(this.InspectWanted);

                kvpCell.KeyLabel.text = this.KeyLabelText;
            }
        }

        public override void TrySetUserValue(object value)
        {
            (this.Owner as InteractiveDictionary).TrySetValueToKey(this.DictKey, value, this.DictIndex);
        }


        protected override bool TryAutoEvaluateIfUnitialized(CacheObjectCell cell) => true;
    }
}
