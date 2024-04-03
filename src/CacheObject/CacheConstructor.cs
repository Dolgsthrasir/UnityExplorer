using UnityExplorer.Inspectors;

namespace UnityExplorer.CacheObject
{
    public class CacheConstructor : CacheMember
    {
        public ConstructorInfo CtorInfo { get; }
        readonly Type typeForStructConstructor;

        public override Type DeclaringType => this.typeForStructConstructor ?? this.CtorInfo.DeclaringType;
        public override bool IsStatic => true;
        public override bool ShouldAutoEvaluate => false;
        public override bool CanWrite => false;

        public CacheConstructor(ConstructorInfo ci)
        {
            this.CtorInfo = ci;
        }

        public CacheConstructor(Type typeForStructConstructor)
        {
            this.typeForStructConstructor = typeForStructConstructor;
        }

        public override void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            Type ctorReturnType;
            // if is parameterless struct ctor
            if (this.typeForStructConstructor != null)
            {
                ctorReturnType = this.typeForStructConstructor;
                this.Owner = inspector;

                // eg. Vector3.Vector3()
                this.NameLabelText = SignatureHighlighter.Parse(this.typeForStructConstructor, false);
                this.NameLabelText += $".{this.NameLabelText}()";

                this.NameForFiltering = SignatureHighlighter.RemoveHighlighting(this.NameLabelText);
                this.NameLabelTextRaw = this.NameForFiltering;
                return;
            }
            else
            {
                base.SetInspectorOwner(inspector, member);

                this.Arguments = this.CtorInfo.GetParameters();
                ctorReturnType = this.CtorInfo.DeclaringType;
            }

            if (ctorReturnType.IsGenericTypeDefinition) this.GenericArguments = ctorReturnType.GetGenericArguments();
        }

        protected override object TryEvaluate()
        {
            try
            {
                Type returnType = this.DeclaringType;

                if (returnType.IsGenericTypeDefinition)
                    returnType = this.DeclaringType.MakeGenericType(this.Evaluator.TryParseGenericArguments());

                object ret;
                if (this.HasArguments)
                    ret = Activator.CreateInstance(returnType, this.Evaluator.TryParseArguments());
                else
                    ret = Activator.CreateInstance(returnType, ArgumentUtility.EmptyArgs);

                this.LastException = null;
                return ret;
            }
            catch (Exception ex)
            {
                this.LastException = ex;
                return null;
            }
        }

        protected override void TrySetValue(object value) => throw new NotImplementedException("You can't set a constructor");
    }
}
