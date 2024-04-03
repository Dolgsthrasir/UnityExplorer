using UnityExplorer.Inspectors;

namespace UnityExplorer.CacheObject
{
    public class CacheMethod : CacheMember
    {
        public MethodInfo MethodInfo { get; }
        public override Type DeclaringType => this.MethodInfo.DeclaringType;
        public override bool CanWrite => false;
        public override bool IsStatic => this.MethodInfo.IsStatic;

        public override bool ShouldAutoEvaluate => false;

        public CacheMethod(MethodInfo mi)
        {
            this.MethodInfo = mi;
        }

        public override void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            base.SetInspectorOwner(inspector, member);

            this.Arguments = this.MethodInfo.GetParameters();
            if (this.MethodInfo.IsGenericMethod) this.GenericArguments = this.MethodInfo.GetGenericArguments();
        }

        protected override object TryEvaluate()
        {
            try
            {
                MethodInfo methodInfo = this.MethodInfo;
                if (methodInfo.IsGenericMethod)
                    methodInfo = this.MethodInfo.MakeGenericMethod(this.Evaluator.TryParseGenericArguments());

                object ret;
                if (this.HasArguments)
                    ret = methodInfo.Invoke(this.DeclaringInstance, this.Evaluator.TryParseArguments());
                else
                    ret = methodInfo.Invoke(this.DeclaringInstance, ArgumentUtility.EmptyArgs);
                this.LastException = null;
                return ret;
            }
            catch (Exception ex)
            {
                this.LastException = ex;
                return null;
            }
        }

        protected override void TrySetValue(object value) => throw new NotImplementedException("You can't set a method");
    }
}
