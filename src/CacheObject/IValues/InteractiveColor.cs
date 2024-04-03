using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveColor : InteractiveValue
    {
        public bool IsValueColor32;

        public Color EditedColor;

        private Image colorImage;
        private readonly InputFieldRef[] inputs = new InputFieldRef[4];
        private readonly Slider[] sliders = new Slider[4];

        private ButtonRef applyButton;

        private static readonly string[] fieldNames = new[] { "R", "G", "B", "A" };

        public override void OnBorrowed(CacheObjectBase owner)
        {
            base.OnBorrowed(owner);

            this.applyButton.Component.gameObject.SetActive(owner.CanWrite);

            foreach (Slider slider in this.sliders)
                slider.interactable = owner.CanWrite;
            foreach (InputFieldRef input in this.inputs)
                input.Component.readOnly = !owner.CanWrite;
        }

        // owner setting value to this
        public override void SetValue(object value)
        {
            this.OnOwnerSetValue(value);
        }

        private void OnOwnerSetValue(object value)
        {
            if (value is Color32 c32)
            {
                this.IsValueColor32 = true;
                this.EditedColor = c32;
                this.inputs[0].Text = c32.r.ToString();
                this.inputs[1].Text = c32.g.ToString();
                this.inputs[2].Text = c32.b.ToString();
                this.inputs[3].Text = c32.a.ToString();
                foreach (Slider slider in this.sliders)
                    slider.maxValue = 255;
            }
            else
            {
                this.IsValueColor32 = false;
                this.EditedColor = (Color)value;
                this.inputs[0].Text = this.EditedColor.r.ToString();
                this.inputs[1].Text = this.EditedColor.g.ToString();
                this.inputs[2].Text = this.EditedColor.b.ToString();
                this.inputs[3].Text = this.EditedColor.a.ToString();
                foreach (Slider slider in this.sliders)
                    slider.maxValue = 1;
            }

            if (this.colorImage) this.colorImage.color = this.EditedColor;
        }

        // setting value to owner

        public void SetValueToOwner()
        {
            if (this.IsValueColor32)
                this.CurrentOwner.SetUserValue((Color32)this.EditedColor);
            else
                this.CurrentOwner.SetUserValue(this.EditedColor);
        }

        private void SetColorField(float val, int fieldIndex)
        {
            switch (fieldIndex)
            {
                case 0:
                    this.EditedColor.r = val; break;
                case 1:
                    this.EditedColor.g = val; break;
                case 2:
                    this.EditedColor.b = val; break;
                case 3:
                    this.EditedColor.a = val; break;
            }

            if (this.colorImage) this.colorImage.color = this.EditedColor;
        }

        private void OnInputChanged(string val, int fieldIndex)
        {
            try
            {
                float f;
                if (this.IsValueColor32)
                {
                    byte value = byte.Parse(val);
                    this.sliders[fieldIndex].value = value;
                    f = (float)((decimal)value / 255);
                }
                else
                {
                    f = float.Parse(val);
                    this.sliders[fieldIndex].value = f;
                }

                this.SetColorField(f, fieldIndex);
            }
            catch (ArgumentException) { } // ignore bad user input
            catch (FormatException) { }
            catch (OverflowException) { }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("InteractiveColor OnInput: " + ex.ToString());
            }
        }

        private void OnSliderValueChanged(float val, int fieldIndex)
        {
            try
            {
                if (this.IsValueColor32)
                {
                    this.inputs[fieldIndex].Text = ((byte)val).ToString();
                    val /= 255f;
                }
                else
                {
                    this.inputs[fieldIndex].Text = val.ToString();
                }

                this.SetColorField(val, fieldIndex);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("InteractiveColor OnSlider: " + ex.ToString());
            }
        }

        // UI Construction

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveColor", false, false, true, true, 3, new Vector4(4, 4, 4, 4),
                new Color(0.06f, 0.06f, 0.06f));

            // hori group

            GameObject horiGroup = UIFactory.CreateHorizontalGroup(this.UIRoot, "ColorEditor", false, false, true, true, 5,
                default, new Color(1, 1, 1, 0), TextAnchor.MiddleLeft);

            // sliders / inputs

            GameObject grid = UIFactory.CreateGridGroup(horiGroup, "Grid", new Vector2(140, 25), new Vector2(2, 2), new Color(1, 1, 1, 0));
            UIFactory.SetLayoutElement(grid, minWidth: 580, minHeight: 25, flexibleWidth: 0);

            for (int i = 0; i < 4; i++) this.AddEditorRow(i, grid);

            // apply button

            this.applyButton = UIFactory.CreateButton(horiGroup, "ApplyButton", "Apply", new Color(0.2f, 0.26f, 0.2f));
            UIFactory.SetLayoutElement(this.applyButton.Component.gameObject, minHeight: 25, minWidth: 90);
            this.applyButton.OnClick += this.SetValueToOwner;

            // image of color

            GameObject imgObj = UIFactory.CreateUIObject("ColorImageHelper", horiGroup);
            UIFactory.SetLayoutElement(imgObj, minHeight: 25, minWidth: 50, flexibleWidth: 50);
            this.colorImage = imgObj.AddComponent<Image>();

            return this.UIRoot;
        }

        internal void AddEditorRow(int index, GameObject groupObj)
        {
            GameObject row = UIFactory.CreateHorizontalGroup(groupObj, "EditorRow_" + fieldNames[index],
                false, true, true, true, 5, default, new Color(1, 1, 1, 0));

            Text label = UIFactory.CreateLabel(row, "RowLabel", $"{fieldNames[index]}:", TextAnchor.MiddleRight, Color.cyan);
            UIFactory.SetLayoutElement(label.gameObject, minWidth: 17, flexibleWidth: 0, minHeight: 25);

            InputFieldRef input = UIFactory.CreateInputField(row, "Input", "...");
            UIFactory.SetLayoutElement(input.UIRoot, minWidth: 40, minHeight: 25, flexibleHeight: 0);
            this.inputs[index] = input;
            input.OnValueChanged += (string val) => { this.OnInputChanged(val, index); };

            GameObject sliderObj = UIFactory.CreateSlider(row, "Slider", out Slider slider);
            this.sliders[index] = slider;
            UIFactory.SetLayoutElement(sliderObj, minHeight: 25, minWidth: 70, flexibleWidth: 999, flexibleHeight: 0);
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.onValueChanged.AddListener((float val) => { this.OnSliderValueChanged(val, index); });
        }
    }
}
