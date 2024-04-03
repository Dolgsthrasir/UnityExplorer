namespace UnityExplorer.Config
{
    public class ConfigElement<T> : IConfigElement
    {
        public string Name { get; }
        public string Description { get; }

        public bool IsInternal { get; }
        public Type ElementType => typeof(T);

        public Action<T> OnValueChanged;
        public Action OnValueChangedNotify { get; set; }

        public object DefaultValue { get; }

        public ConfigHandler Handler =>
            this.IsInternal
            ? ConfigManager.InternalHandler
            : ConfigManager.Handler;

        public T Value
        {
            get => this.m_value;
            set => this.SetValue(value);
        }
        private T m_value;

        object IConfigElement.BoxedValue
        {
            get => this.m_value;
            set => this.SetValue((T)value);
        }

        public ConfigElement(string name, string description, T defaultValue, bool isInternal = false)
        {
            this.Name = name;
            this.Description = description;

            this.m_value = defaultValue;
            this.DefaultValue = defaultValue;

            this.IsInternal = isInternal;

            ConfigManager.RegisterConfigElement(this);
        }

        private void SetValue(T value)
        {
            if ((this.m_value == null && value == null) || (this.m_value != null && this.m_value.Equals(value)))
                return;

            this.m_value = value;

            this.Handler.SetConfigValue(this, value);

            this.OnValueChanged?.Invoke(value);
            this.OnValueChangedNotify?.Invoke();

            this.Handler.OnAnyConfigChanged();
        }

        object IConfigElement.GetLoaderConfigValue() => this.GetLoaderConfigValue();

        public T GetLoaderConfigValue()
        {
            return this.Handler.GetConfigValue(this);
        }

        public void RevertToDefaultValue()
        {
            this.Value = (T)this.DefaultValue;
        }
    }
}
