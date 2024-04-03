using UnityExplorer.Inspectors;

namespace UnityExplorer.CacheObject
{
    public class CacheField : CacheMember
    {
        public FieldInfo FieldInfo { get; internal set; }
        public override Type DeclaringType => this.FieldInfo.DeclaringType;
        public override bool IsStatic => this.FieldInfo.IsStatic;
        public override bool CanWrite => this.m_canWrite ?? (bool)(this.m_canWrite = !(this.FieldInfo.IsLiteral && !this.FieldInfo.IsInitOnly));
        private bool? m_canWrite;

        public override bool ShouldAutoEvaluate => true;

        public CacheField(FieldInfo fi)
        {
            this.FieldInfo = fi;
        }

        public override void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            base.SetInspectorOwner(inspector, member);
        }

        protected override object TryEvaluate()
        {
            try
            {
                object ret = this.FieldInfo.GetValue(this.DeclaringInstance);
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
            try
            {
                this.FieldInfo.SetValue(this.DeclaringInstance, value);
            }
            catch (Exception ex)
            {
                ExplorerCore.LogWarning(ex);
            }
        }
    }
}
