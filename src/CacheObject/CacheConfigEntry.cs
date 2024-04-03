using UnityExplorer.CacheObject.Views;
using UnityExplorer.Config;

namespace UnityExplorer.CacheObject
{
    public class CacheConfigEntry : CacheObjectBase
    {
        public CacheConfigEntry(IConfigElement configElement)
        {
            this.RefConfigElement = configElement;
            this.FallbackType = configElement.ElementType;

            this.NameLabelText = $"<color=cyan>{configElement.Name}</color>" +
                $"\r\n<color=grey><i>{configElement.Description}</i></color>";
            this.NameLabelTextRaw = string.Empty;

            configElement.OnValueChangedNotify += this.UpdateValueFromSource;
        }

        public IConfigElement RefConfigElement;

        public override bool ShouldAutoEvaluate => true;
        public override bool HasArguments => false;
        public override bool CanWrite => true;

        public void UpdateValueFromSource()
        {
            //if (RefConfigElement.BoxedValue.Equals(this.Value))
            //    return;

            this.SetValueFromSource(this.RefConfigElement.BoxedValue);

            if (this.CellView != null)
                this.SetDataToCell(this.CellView);
        }

        public override void TrySetUserValue(object value)
        {
            this.Value = value;
            this.RefConfigElement.BoxedValue = value;
        }

        protected override bool TryAutoEvaluateIfUnitialized(CacheObjectCell cell) => true;
    }
}
