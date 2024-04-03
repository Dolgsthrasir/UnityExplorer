using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.UI.Widgets
{
    // Controls a Vector3 property of a Transform, and holds references to each AxisControl for X/Y/Z.

    public class Vector3Control
    {
        public TransformControls Owner { get; }
        public GameObject Target => this.Owner.Owner.Target;
        public Transform Transform => this.Target.transform;
        public TransformType Type { get; }

        public InputFieldRef MainInput { get; }

        public AxisControl[] AxisControls { get; } = new AxisControl[3];

        public InputFieldRef IncrementInput { get; set; }
        public float Increment { get; set; } = 0.1f;

        Vector3 lastValue;

        Vector3 CurrentValue =>
            this.Type switch
        {
            TransformType.Position => this.Transform.position,
            TransformType.LocalPosition => this.Transform.localPosition,
            TransformType.Rotation => this.Transform.localEulerAngles,
            TransformType.Scale => this.Transform.localScale,
            _ => throw new NotImplementedException()
        };

        public Vector3Control(TransformControls owner, TransformType type, InputFieldRef input)
        {
            this.Owner = owner;
            this.Type = type;
            this.MainInput = input;
        }

        public void Update(bool force)
        {
            Vector3 currValue = this.CurrentValue;
            if (force || (!this.MainInput.Component.isFocused && !this.lastValue.Equals(currValue)))
            {
                this.MainInput.Text = ParseUtility.ToStringForInput<Vector3>(currValue);
                this.lastValue = currValue;
            }
        }

        void OnTransformInputEndEdit(TransformType type, string input)
        {
            switch (type)
            {
                case TransformType.Position:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _)) this.Target.transform.position = val;
                    }
                    break;
                case TransformType.LocalPosition:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _)) this.Target.transform.localPosition = val;
                    }
                    break;
                case TransformType.Rotation:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _)) this.Target.transform.localEulerAngles = val;
                    }
                    break;
                case TransformType.Scale:
                    {
                        if (ParseUtility.TryParse(input, out Vector3 val, out _)) this.Target.transform.localScale = val;
                    }
                    break;
            }

            this.Owner.UpdateTransformControlValues(true);
        }

        void IncrementInput_OnEndEdit(string value)
        {
            if (!ParseUtility.TryParse(value, out float increment, out _))
                this.IncrementInput.Text = ParseUtility.ToStringForInput<float>(this.Increment);
            else
            {
                this.Increment = increment;
                foreach (AxisControl slider in this.AxisControls)
                {
                    slider.slider.minValue = -increment;
                    slider.slider.maxValue = increment;
                }
            }
        }

        public static Vector3Control Create(TransformControls owner, GameObject transformGroup, string title, TransformType type)
        {
            GameObject rowObj = UIFactory.CreateUIObject($"Row_{title}", transformGroup);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(rowObj, false, false, true, true, 5, 0, 0, 0, 0, default);
            UIFactory.SetLayoutElement(rowObj, minHeight: 25, flexibleWidth: 9999);

            Text titleLabel = UIFactory.CreateLabel(rowObj, "PositionLabel", title, TextAnchor.MiddleRight, Color.grey);
            UIFactory.SetLayoutElement(titleLabel.gameObject, minHeight: 25, minWidth: 110);

            InputFieldRef inputField = UIFactory.CreateInputField(rowObj, "InputField", "...");
            UIFactory.SetLayoutElement(inputField.Component.gameObject, minHeight: 25, minWidth: 100, flexibleWidth: 999);

            Vector3Control control = new(owner, type, inputField);

            inputField.Component.GetOnEndEdit().AddListener((string value) => { control.OnTransformInputEndEdit(type, value); });

            control.AxisControls[0] = AxisControl.Create(rowObj, "X", 0, control);
            control.AxisControls[1] = AxisControl.Create(rowObj, "Y", 1, control);
            control.AxisControls[2] = AxisControl.Create(rowObj, "Z", 2, control);

            control.IncrementInput = UIFactory.CreateInputField(rowObj, "IncrementInput", "...");
            control.IncrementInput.Text = "0.1";
            UIFactory.SetLayoutElement(control.IncrementInput.GameObject, minWidth: 30, flexibleWidth: 0, minHeight: 25);
            control.IncrementInput.Component.GetOnEndEdit().AddListener(control.IncrementInput_OnEndEdit);

            return control;
        }
    }
}
