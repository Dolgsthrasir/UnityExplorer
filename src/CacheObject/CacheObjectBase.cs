using System.Collections;
using UnityExplorer.CacheObject.IValues;
using UnityExplorer.CacheObject.Views;
using UniverseLib.UI;
using UniverseLib.UI.ObjectPool;
using Object = UnityEngine.Object;

namespace UnityExplorer.CacheObject
{
    public enum ValueState
    {
        NotEvaluated,
        Exception,
        Boolean,
        Number,
        String,
        Enum,
        Collection,
        Dictionary,
        ValueStruct,
        Color,
        Unsupported
    }

    public abstract class CacheObjectBase
    {
        public ICacheObjectController Owner { get; set; }
        public CacheObjectCell CellView { get; internal set; }

        public object Value { get; protected set; }
        public Type FallbackType { get; protected set; }
        public ValueState State { get; set; }
        public Exception LastException { get; protected set; }
        bool valueIsNull;
        Type currentValueType;

        // InteractiveValues
        public InteractiveValue IValue { get; private set; }
        public Type CurrentIValueType { get; private set; }
        public bool SubContentShowWanted { get; private set; }

        // UI
        public string NameLabelText { get; protected set; }
        public string NameLabelTextRaw { get; protected set; }
        public string ValueLabelText { get; protected set; }

        // Abstract
        public abstract bool ShouldAutoEvaluate { get; }
        public abstract bool HasArguments { get; }
        public abstract bool CanWrite { get; }

        protected const string NOT_YET_EVAL = "<color=grey>Not yet evaluated</color>";

        public virtual void SetFallbackType(Type fallbackType)
        {
            this.FallbackType = fallbackType;
            this.ValueLabelText = this.GetValueLabel();
        }

        public virtual void SetView(CacheObjectCell cellView)
        {
            this.CellView = cellView;
            cellView.Occupant = this;
        }

        public virtual void UnlinkFromView()
        {
            if (this.CellView == null)
                return;

            this.CellView.Occupant = null;
            this.CellView = null;

            if (this.IValue != null)
                this.IValue.UIRoot.transform.SetParent(InactiveIValueHolder.transform, false);
        }

        public virtual void ReleasePooledObjects()
        {
            if (this.IValue != null) this.ReleaseIValue();

            if (this.CellView != null) this.UnlinkFromView();
        }

        // Updating and applying values

        // The only method which sets the CacheObjectBase.Value
        public virtual void SetValueFromSource(object value)
        {
            this.Value = value;

            if (!this.Value.IsNullOrDestroyed()) this.Value = this.Value.TryCast();

            this.ProcessOnEvaluate();

            if (this.IValue != null)
            {
                if (this.SubContentShowWanted)
                    this.IValue.SetValue(this.Value);
                else
                    this.IValue.PendingValueWanted = true;
            }
        }

        public void SetUserValue(object value)
        {
            value = value.TryCast(this.FallbackType);

            this.TrySetUserValue(value);

            if (this.CellView != null) this.SetDataToCell(this.CellView);

            // If the owner's ParentCacheObject is set, we are setting the value of an inspected struct.
            // Set the inspector target as the value back to that parent.
            if (this.Owner.ParentCacheObject != null) this.Owner.ParentCacheObject.SetUserValue(this.Owner.Target);
        }

        public abstract void TrySetUserValue(object value);

        protected virtual void ProcessOnEvaluate()
        {
            ValueState prevState = this.State;

            if (this.LastException != null)
            {
                this.valueIsNull = true;
                this.currentValueType = this.FallbackType;
                this.State = ValueState.Exception;
            }
            else if (this.Value.IsNullOrDestroyed())
            {
                this.valueIsNull = true;
                this.State = this.GetStateForType(this.FallbackType);
            }
            else
            {
                this.valueIsNull = false;
                this.State = this.GetStateForType(this.Value.GetActualType());
            }

            if (this.IValue != null)
            {
                // If we changed states (always needs IValue change)
                // or if the value is null, and the fallback type isnt string (we always want to edit strings).
                if (this.State != prevState || (this.State != ValueState.String && this.State != ValueState.Exception && this.Value.IsNullOrDestroyed()))
                {
                    // need to return IValue
                    this.ReleaseIValue();
                    this.SubContentShowWanted = false;
                }
            }

            // Set label text
            this.ValueLabelText = this.GetValueLabel();
        }

        public ValueState GetStateForType(Type type)
        {
            if (this.currentValueType == type && (this.State != ValueState.Exception || this.LastException != null))
                return this.State;

            this.currentValueType = type;
            if (type == typeof(bool))
                return ValueState.Boolean;
            else if (type.IsPrimitive || type == typeof(decimal))
                return ValueState.Number;
            else if (type == typeof(string))
                return ValueState.String;
            else if (type.IsEnum)
                return ValueState.Enum;
            else if (type == typeof(Color) || type == typeof(Color32))
                return ValueState.Color;
            else if (InteractiveValueStruct.SupportsType(type))
                return ValueState.ValueStruct;
            else if (ReflectionUtility.IsDictionary(type))
                return ValueState.Dictionary;
            else if (!typeof(Transform).IsAssignableFrom(type) && ReflectionUtility.IsEnumerable(type))
                return ValueState.Collection;
            else
                return ValueState.Unsupported;
        }

        protected string GetValueLabel()
        {
            string label = string.Empty;

            switch (this.State)
            {
                case ValueState.NotEvaluated:
                    return $"<i>{NOT_YET_EVAL} ({SignatureHighlighter.Parse(this.FallbackType, true)})</i>";

                case ValueState.Exception:
                    return $"<i><color=#eb4034>{this.LastException.ReflectionExToString()}</color></i>";

                // bool and number dont want the label for the value at all
                case ValueState.Boolean:
                case ValueState.Number:
                    return null;

                // and valuestruct also doesnt want it if we can parse it
                case ValueState.ValueStruct:
                    if (ParseUtility.CanParse(this.currentValueType))
                        return null;
                    break;

                // string wants it trimmed to max 200 chars
                case ValueState.String:
                    if (!this.valueIsNull)
                        return $"\"{ToStringUtility.PruneString(this.Value as string, 200, 5)}\"";
                    break;

                // try to prefix the count of the collection for lists and dicts
                case ValueState.Collection:
                    if (!this.valueIsNull)
                    {
                        if (this.Value is IList iList)
                            label = $"[{iList.Count}] ";
                        else if (this.Value is ICollection iCol)
                            label = $"[{iCol.Count}] ";
                        else
                            label = "[?] ";
                    }
                    break;

                case ValueState.Dictionary:
                    if (!this.valueIsNull)
                    {
                        if (this.Value is IDictionary iDict)
                            label = $"[{iDict.Count}] ";
                        else
                            label = "[?] ";
                    }
                    break;
            }

            // Cases which dont return will append to ToStringWithType

            return label += ToStringUtility.ToStringWithType(this.Value, this.FallbackType, true);
        }

        // Setting cell state from our model

        /// <summary>Return false if SetCell should abort, true if it should continue.</summary>
        protected abstract bool TryAutoEvaluateIfUnitialized(CacheObjectCell cell);

        public virtual void SetDataToCell(CacheObjectCell cell)
        {
            cell.NameLabel.text = this.NameLabelText;
            if (cell.HiddenNameLabel != null)
                cell.HiddenNameLabel.Text = this.NameLabelTextRaw ?? string.Empty;
            cell.ValueLabel.gameObject.SetActive(true);

            cell.SubContentHolder.gameObject.SetActive(this.SubContentShowWanted);
            if (this.IValue != null)
            {
                this.IValue.UIRoot.transform.SetParent(cell.SubContentHolder.transform, false);
                this.IValue.SetLayout();
            }

            bool evaluated = this.TryAutoEvaluateIfUnitialized(cell);

            if (cell.CopyButton != null)
            {
                bool canCopy = this.State != ValueState.NotEvaluated && this.State != ValueState.Exception;
                cell.CopyButton.Component.gameObject.SetActive(canCopy);
                cell.PasteButton.Component.gameObject.SetActive(canCopy && this.CanWrite);
            }

            if (!evaluated)
                return;

            // The following only executes if the object has evaluated.
            // For members and properties with args, they will return by default now.

            switch (this.State)
            {
                case ValueState.Exception:
                    this.SetValueState(cell, new(true, subContentButtonActive: true));
                    break;
                case ValueState.Boolean:
                    this.SetValueState(cell, new(false, toggleActive: true, applyActive: this.CanWrite));
                    break;
                case ValueState.Number:
                    this.SetValueState(cell, new(false, typeLabelActive: true, inputActive: true, applyActive: this.CanWrite));
                    break;
                case ValueState.String:
                    if (this.valueIsNull)
                        this.SetValueState(cell, new(true, subContentButtonActive: true));
                    else
                        this.SetValueState(cell, new(true, false, SignatureHighlighter.StringOrange, subContentButtonActive: true));
                    break;
                case ValueState.Enum:
                    this.SetValueState(cell, new(true, subContentButtonActive: this.CanWrite));
                    break;
                case ValueState.Color:
                case ValueState.ValueStruct:
                    if (ParseUtility.CanParse(this.currentValueType))
                        this.SetValueState(cell, new(false, false, null, true, false, true, this.CanWrite, true, true));
                    else
                        this.SetValueState(cell, new(true, inspectActive: true, subContentButtonActive: true));
                    break;
                case ValueState.Collection:
                case ValueState.Dictionary:
                    this.SetValueState(cell, new(true, inspectActive: !this.valueIsNull, subContentButtonActive: !this.valueIsNull));
                    break;
                case ValueState.Unsupported:
                    this.SetValueState(cell, new(true, inspectActive: !this.valueIsNull));
                    break;
            }

            cell.RefreshSubcontentButton();
        }

        protected virtual void SetValueState(CacheObjectCell cell, ValueStateArgs args)
        {
            // main value label
            if (args.valueActive)
            {
                cell.ValueLabel.text = this.ValueLabelText;
                cell.ValueLabel.supportRichText = args.valueRichText;
                cell.ValueLabel.color = args.valueColor;
            }
            else
                cell.ValueLabel.text = "";

            // Type label (for primitives)
            cell.TypeLabel.gameObject.SetActive(args.typeLabelActive);
            if (args.typeLabelActive)
                cell.TypeLabel.text = SignatureHighlighter.Parse(this.currentValueType, false);

            // toggle for bools
            cell.Toggle.gameObject.SetActive(args.toggleActive);
            if (args.toggleActive)
            {
                cell.Toggle.interactable = this.CanWrite;
                cell.Toggle.isOn = (bool)this.Value;
                cell.ToggleText.text = this.Value.ToString();
            }

            // inputfield for numbers
            cell.InputField.UIRoot.SetActive(args.inputActive);
            if (args.inputActive)
            {
                cell.InputField.Text = ParseUtility.ToStringForInput(this.Value, this.currentValueType);
                cell.InputField.Component.readOnly = !this.CanWrite;
            }

            // apply for bool and numbers
            cell.ApplyButton.Component.gameObject.SetActive(args.applyActive);

            // Inspect button only if last value not null.
            if (cell.InspectButton != null)
                cell.InspectButton.Component.gameObject.SetActive(args.inspectActive && !this.valueIsNull);

            // set subcontent button if needed, and for null strings and exceptions
            cell.SubContentButton.Component.gameObject.SetActive(
                args.subContentButtonActive
                && (!this.valueIsNull || this.State == ValueState.String || this.State == ValueState.Exception));
        }

        // CacheObjectCell Apply

        public virtual void OnCellApplyClicked()
        {
            if (this.State == ValueState.Boolean)
                this.SetUserValue(this.CellView.Toggle.isOn);
            else
            {
                if (ParseUtility.TryParse(this.CellView.InputField.Text, this.currentValueType, out object value, out Exception ex))
                {
                    this.SetUserValue(value);
                }
                else
                {
                    ExplorerCore.LogWarning("Unable to parse input!");
                    if (ex != null)
                        ExplorerCore.Log(ex.ReflectionExToString());
                }
            }

            this.SetDataToCell(this.CellView);
        }

        // IValues

        public virtual void OnCellSubContentToggle()
        {
            if (this.IValue == null)
            {
                Type ivalueType = InteractiveValue.GetIValueTypeForState(this.State);

                if (ivalueType == null)
                    return;

                this.IValue = (InteractiveValue)Pool.Borrow(ivalueType);
                this.CurrentIValueType = ivalueType;

                this.IValue.OnBorrowed(this);
                this.IValue.SetValue(this.Value);
                this.IValue.UIRoot.transform.SetParent(this.CellView.SubContentHolder.transform, false);
                this.CellView.SubContentHolder.SetActive(true);
                this.SubContentShowWanted = true;

                // update our cell after creating the ivalue (the value may have updated, make sure its consistent)
                this.ProcessOnEvaluate();
                this.SetDataToCell(this.CellView);
            }
            else
            {
                this.SubContentShowWanted = !this.SubContentShowWanted;
                this.CellView.SubContentHolder.SetActive(this.SubContentShowWanted);

                if (this.SubContentShowWanted && this.IValue.PendingValueWanted)
                {
                    this.IValue.PendingValueWanted = false;
                    this.ProcessOnEvaluate();
                    this.SetDataToCell(this.CellView);
                    this.IValue.SetValue(this.Value);
                }
            }

            this.CellView.RefreshSubcontentButton();
        }

        public virtual void ReleaseIValue()
        {
            if (this.IValue == null)
                return;

            this.IValue.ReleaseFromOwner();
            Pool.Return(this.CurrentIValueType, this.IValue);

            this.IValue = null;
        }

        internal static GameObject InactiveIValueHolder
        {
            get
            {
                if (!inactiveIValueHolder)
                {
                    inactiveIValueHolder = new GameObject("Temp_IValue_Holder");
                    Object.DontDestroyOnLoad(inactiveIValueHolder);
                    inactiveIValueHolder.hideFlags = HideFlags.HideAndDontSave;
                    inactiveIValueHolder.transform.parent = UniversalUI.PoolHolder.transform;
                    inactiveIValueHolder.SetActive(false);
                }
                return inactiveIValueHolder;
            }
        }
        private static GameObject inactiveIValueHolder;

        // Value state args helper

        public struct ValueStateArgs
        {
            public static ValueStateArgs Default { get; } = new(true);

            public Color valueColor;
            public bool valueActive, valueRichText, typeLabelActive, toggleActive, inputActive, applyActive, inspectActive, subContentButtonActive;

            public ValueStateArgs(bool valueActive = true,
                bool valueRichText = true,
                Color? valueColor = null,
                bool typeLabelActive = false,
                bool toggleActive = false,
                bool inputActive = false,
                bool applyActive = false,
                bool inspectActive = false,
                bool subContentButtonActive = false)
            {
                this.valueActive = valueActive;
                this.valueRichText = valueRichText;
                this.valueColor = valueColor == null ? Color.white : (Color)valueColor;
                this.typeLabelActive = typeLabelActive;
                this.toggleActive = toggleActive;
                this.inputActive = inputActive;
                this.applyActive = applyActive;
                this.inspectActive = inspectActive;
                this.subContentButtonActive = subContentButtonActive;
            }
        }
    }
}
