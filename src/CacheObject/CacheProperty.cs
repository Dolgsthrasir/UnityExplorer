using UnityExplorer.Inspectors;

namespace UnityExplorer.CacheObject
{
    public class CacheProperty : CacheMember
    {
        public PropertyInfo PropertyInfo { get; internal set; }
        public override Type DeclaringType => this.PropertyInfo.DeclaringType;
        public override bool CanWrite => this.PropertyInfo.CanWrite;
        public override bool IsStatic => this.m_isStatic ?? (bool)(this.m_isStatic = this.PropertyInfo.GetAccessors(true)[0].IsStatic);
        private bool? m_isStatic;

        public override bool ShouldAutoEvaluate => !this.HasArguments;

        public CacheProperty(PropertyInfo pi)
        {
            this.PropertyInfo = pi;
        }

        public override void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            base.SetInspectorOwner(inspector, member);

            this.Arguments = this.PropertyInfo.GetIndexParameters();
        }

        protected override object TryEvaluate()
        {
            try
            {
                object ret;
                if (this.HasArguments)
                    ret = this.PropertyInfo.GetValue(this.DeclaringInstance, this.Evaluator.TryParseArguments());
                else
                    ret = this.PropertyInfo.GetValue(this.DeclaringInstance, null);
                this.LastException = null;
                return ret;
            }
            catch (Exception ex)
            {
                this.LastException = ex;
                return null;
            }
        }

        protected override void TrySetValue(object value)
        {
            if (!this.CanWrite)
                return;

            try
            {
                bool _static = this.PropertyInfo.GetAccessors(true)[0].IsStatic;

                if (this.HasArguments)
                    this.PropertyInfo.SetValue(this.DeclaringInstance, value, this.Evaluator.TryParseArguments());
                else
                    this.PropertyInfo.SetValue(this.DeclaringInstance, value, null);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning(ex);
            }
        }
    }
}
