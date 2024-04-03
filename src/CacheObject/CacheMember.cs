using UnityExplorer.CacheObject.Views;
using UnityExplorer.Inspectors;
using UnityExplorer.UI.Widgets;
using UniverseLib.UI.ObjectPool;

namespace UnityExplorer.CacheObject
{
    public abstract class CacheMember : CacheObjectBase
    {
        public abstract Type DeclaringType { get; }
        public string NameForFiltering { get; protected set; }
        public object DeclaringInstance => this.IsStatic ? null : (this.m_declaringInstance ??= this.Owner.Target.TryCast(this.DeclaringType));
        private object m_declaringInstance;

        public abstract bool IsStatic { get; }
        public override bool HasArguments => this.Arguments?.Length > 0 || this.GenericArguments.Length > 0;
        public ParameterInfo[] Arguments { get; protected set; } = new ParameterInfo[0];
        public Type[] GenericArguments { get; protected set; } = ArgumentUtility.EmptyTypes;
        public EvaluateWidget Evaluator { get; protected set; }
        public bool Evaluating => this.Evaluator != null && this.Evaluator.UIRoot.activeSelf;

        public virtual void SetInspectorOwner(ReflectionInspector inspector, MemberInfo member)
        {
            this.Owner = inspector;
            this.NameLabelText = this switch
            {
                CacheMethod => SignatureHighlighter.ParseMethod(member as MethodInfo),
                CacheConstructor => SignatureHighlighter.ParseConstructor(member as ConstructorInfo),
                _ => SignatureHighlighter.Parse(member.DeclaringType, false, member),
            };

            this.NameForFiltering = SignatureHighlighter.RemoveHighlighting(this.NameLabelText);
            this.NameLabelTextRaw = this.NameForFiltering;
        }

        public override void ReleasePooledObjects()
        {
            base.ReleasePooledObjects();

            if (this.Evaluator != null)
            {
                this.Evaluator.OnReturnToPool();
                Pool<EvaluateWidget>.Return(this.Evaluator);
                this.Evaluator = null;
            }
        }

        public override void UnlinkFromView()
        {
            if (this.Evaluator != null)
                this.Evaluator.UIRoot.transform.SetParent(Pool<EvaluateWidget>.Instance.InactiveHolder.transform, false);

            base.UnlinkFromView();
        }

        protected abstract object TryEvaluate();

        protected abstract void TrySetValue(object value);

        /// <summary>
        /// Evaluate is called when first shown (if ShouldAutoEvaluate), or else when Evaluate button is clicked, or auto-updated.
        /// </summary>
        public void Evaluate()
        {
            this.SetValueFromSource(this.TryEvaluate());
        }

        /// <summary>
        /// Called when user presses the Evaluate button.
        /// </summary>
        public void EvaluateAndSetCell()
        {
            this.Evaluate();
            if (this.CellView != null) this.SetDataToCell(this.CellView);
        }

        public override void TrySetUserValue(object value)
        {
            this.TrySetValue(value);
            this.Evaluate();
        }

        protected override void SetValueState(CacheObjectCell cell, ValueStateArgs args)
        {
            base.SetValueState(cell, args);
        }

        private static readonly Color evalEnabledColor = new(0.15f, 0.25f, 0.15f);
        private static readonly Color evalDisabledColor = new(0.15f, 0.15f, 0.15f);

        protected override bool TryAutoEvaluateIfUnitialized(CacheObjectCell objectcell)
        {
            CacheMemberCell cell = objectcell as CacheMemberCell;

            cell.EvaluateHolder.SetActive(!this.ShouldAutoEvaluate);
            if (!this.ShouldAutoEvaluate)
            {
                cell.EvaluateButton.Component.gameObject.SetActive(true);
                if (this.HasArguments)
                {
                    if (!this.Evaluating)
                        cell.EvaluateButton.ButtonText.text = $"Evaluate ({this.Arguments.Length + this.GenericArguments.Length})";
                    else
                    {
                        cell.EvaluateButton.ButtonText.text = "Hide";
                        this.Evaluator.UIRoot.transform.SetParent(cell.EvaluateHolder.transform, false);
                        RuntimeHelper.SetColorBlock(cell.EvaluateButton.Component, evalEnabledColor, evalEnabledColor * 1.3f);
                    }
                }
                else
                    cell.EvaluateButton.ButtonText.text = "Evaluate";

                if (!this.Evaluating)
                    RuntimeHelper.SetColorBlock(cell.EvaluateButton.Component, evalDisabledColor, evalDisabledColor * 1.3f);
            }

            if (this.State == ValueState.NotEvaluated && !this.ShouldAutoEvaluate)
            {
                this.SetValueState(cell, ValueStateArgs.Default);
                cell.RefreshSubcontentButton();

                return false;
            }

            if (this.State == ValueState.NotEvaluated) this.Evaluate();

            return true;
        }

        public void OnEvaluateClicked()
        {
            if (!this.HasArguments)
            {
                this.EvaluateAndSetCell();
            }
            else
            {
                if (this.Evaluator == null)
                {
                    this.Evaluator = Pool<EvaluateWidget>.Borrow();
                    this.Evaluator.OnBorrowedFromPool(this);
                    this.Evaluator.UIRoot.transform.SetParent((this.CellView as CacheMemberCell).EvaluateHolder.transform, false);
                    this.TryAutoEvaluateIfUnitialized(this.CellView);
                }
                else
                {
                    if (this.Evaluator.UIRoot.activeSelf)
                        this.Evaluator.UIRoot.SetActive(false);
                    else
                        this.Evaluator.UIRoot.SetActive(true);

                    this.TryAutoEvaluateIfUnitialized(this.CellView);
                }
            }
        }
    }
}
