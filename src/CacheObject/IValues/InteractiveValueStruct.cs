using UniverseLib.UI;
using UniverseLib.UI.Models;

namespace UnityExplorer.CacheObject.IValues
{
    public class InteractiveValueStruct : InteractiveValue
    {
        #region Struct cache / wrapper

        public class StructInfo
        {
            public bool IsSupported;
            public FieldInfo[] Fields;

            public StructInfo(bool isSupported, FieldInfo[] fields)
            {
                this.IsSupported = isSupported;
                this.Fields = fields;
            }

            public void SetValue(object instance, string input, int fieldIndex)
            {
                FieldInfo field = this.Fields[fieldIndex];

                object val;
                if (field.FieldType == typeof(string))
                    val = input;
                else
                {
                    if (!ParseUtility.TryParse(input, field.FieldType, out val, out Exception ex))
                    {
                        ExplorerCore.LogWarning("Unable to parse input!");
                        if (ex != null) ExplorerCore.Log(ex.ReflectionExToString());
                        return;
                    }
                }

                field.SetValue(instance, val);
            }

            public string GetValue(object instance, int fieldIndex)
            {
                FieldInfo field = this.Fields[fieldIndex];
                object value = field.GetValue(instance);
                return ParseUtility.ToStringForInput(value, field.FieldType);
            }
        }

        private static readonly Dictionary<string, StructInfo> typeSupportCache = new();

        private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string SYSTEM_VOID = "System.Void";

        public static bool SupportsType(Type type)
        {
            if (!type.IsValueType || string.IsNullOrEmpty(type.AssemblyQualifiedName) || type.FullName == SYSTEM_VOID)
                return false;

            if (typeSupportCache.TryGetValue(type.AssemblyQualifiedName, out StructInfo info))
                return info.IsSupported;

            bool supported = false;

            FieldInfo[] fields = type.GetFields(INSTANCE_FLAGS);
            if (fields.Length > 0)
            {
                if (fields.Any(it => !ParseUtility.CanParse(it.FieldType)))
                {
                    supported = false;
                    info = new StructInfo(supported, null);
                }
                else
                {
                    supported = true;
                    info = new StructInfo(supported, fields);
                }
            }

            typeSupportCache.Add(type.AssemblyQualifiedName, info);

            return supported;
        }

        #endregion

        public object RefInstance;

        public StructInfo CurrentInfo;
        private Type lastStructType;

        private ButtonRef applyButton;
        private readonly List<GameObject> fieldRows = new();
        private readonly List<InputFieldRef> inputFields = new();
        private readonly List<Text> labels = new();

        public override void OnBorrowed(CacheObjectBase owner)
        {
            base.OnBorrowed(owner);

            this.applyButton.Component.gameObject.SetActive(owner.CanWrite);
        }

        // Setting value from owner to this

        public override void SetValue(object value)
        {
            this.RefInstance = value;

            Type type = this.RefInstance.GetType();

            if (type != this.lastStructType)
            {
                this.CurrentInfo = typeSupportCache[type.AssemblyQualifiedName];
                this.SetupUIForType();
                this.lastStructType = type;
            }

            for (int i = 0; i < this.CurrentInfo.Fields.Length; i++)
            {
                this.inputFields[i].Text = this.CurrentInfo.GetValue(this.RefInstance, i);
            }
        }

        private void OnApplyClicked()
        {
            try
            {
                for (int i = 0; i < this.CurrentInfo.Fields.Length; i++)
                {
                    this.CurrentInfo.SetValue(this.RefInstance, this.inputFields[i].Text, i);
                }

                this.CurrentOwner.SetUserValue(this.RefInstance);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning("Exception setting value: " + ex);
            }
        }

        // UI Setup for type

        private void SetupUIForType()
        {
            for (int i = 0; i < this.CurrentInfo.Fields.Length || i <= this.inputFields.Count; i++)
            {
                if (i >= this.CurrentInfo.Fields.Length)
                {
                    if (i >= this.inputFields.Count)
                        break;

                    this.fieldRows[i].SetActive(false);
                    continue;
                }

                if (i >= this.inputFields.Count) this.AddEditorRow();

                this.fieldRows[i].SetActive(true);

                string label = SignatureHighlighter.Parse(this.CurrentInfo.Fields[i].FieldType, false);
                label += $" <color={SignatureHighlighter.FIELD_INSTANCE}>{this.CurrentInfo.Fields[i].Name}</color>:";
                this.labels[i].text = label;
            }
        }

        private void AddEditorRow()
        {
            GameObject row = UIFactory.CreateUIObject("HoriGroup", this.UIRoot);
            //row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIFactory.SetLayoutElement(row, minHeight: 25, flexibleWidth: 9999);
            UIFactory.SetLayoutGroup<HorizontalLayoutGroup>(row, false, false, true, true, 8, childAlignment: TextAnchor.MiddleLeft);

            this.fieldRows.Add(row);

            Text label = UIFactory.CreateLabel(row, "Label", "notset", TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(label.gameObject, minHeight: 25, minWidth: 50, flexibleWidth: 0);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            this.labels.Add(label);

            InputFieldRef input = UIFactory.CreateInputField(row, "InputField", "...");
            UIFactory.SetLayoutElement(input.UIRoot, minHeight: 25, minWidth: 200);
            ContentSizeFitter fitter = input.UIRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            input.Component.lineType = InputField.LineType.MultiLineNewline;
            this.inputFields.Add(input);
        }

        // UI Construction

        public override GameObject CreateContent(GameObject parent)
        {
            this.UIRoot = UIFactory.CreateVerticalGroup(parent, "InteractiveValueStruct", false, false, true, true, 3, new Vector4(4, 4, 4, 4),
                new Color(0.06f, 0.06f, 0.06f), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(this.UIRoot, minHeight: 25, flexibleWidth: 9999);

            this.applyButton = UIFactory.CreateButton(this.UIRoot, "ApplyButton", "Apply", new Color(0.2f, 0.27f, 0.2f));
            UIFactory.SetLayoutElement(this.applyButton.Component.gameObject, minHeight: 25, minWidth: 175);
            this.applyButton.OnClick += this.OnApplyClicked;

            return this.UIRoot;
        }
    }
}
